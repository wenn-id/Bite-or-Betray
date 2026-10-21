"""Regression tests for RC reporting and trailer clips without running Unity, git publish, ffmpeg or the network."""
import copy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import unity_runner as runner


class ReleaseRunnerTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.logs = self.root / "runner-logs"
        self.logs.mkdir()
        for name, value in (("REPO", str(self.root)), ("LOGS", str(self.logs))):
            mock = patch.object(runner, name, value)
            mock.start()
            self.addCleanup(mock.stop)
        self.tests = dict(total=1, passed=1, failed=0, skipped=0, fails=[], exit=0,
                          cases=[dict(name="OnlineServicesTests.Voice", result="Passed")])
        self.build = dict(exit=0, result="Succeeded; mode=Release; errors=0", mode="Release",
                          succeeded=True, shader=[], compile=[], crash=[], skipped=False,
                          audit="RESULT: PASS static budget")

    def report(self, tests=None, build=None):
        return runner.report("a" * 40, tests or self.tests, None, self.tests, None, build or self.build)

    def test_release_request_defaults_and_rejects_invalid_mode(self):
        self.assertEqual("Development", runner.build_mode())
        request = self.root / runner.BUILD_MODE
        request.parent.mkdir(parents=True)
        request.write_text("Release\n", encoding="utf-8")
        self.assertEqual("Release", runner.build_mode())
        request.write_text("Relase", encoding="utf-8")
        with self.assertRaises(ValueError):
            runner.build_mode()

    def test_only_complete_tests_and_successful_build_are_green(self):
        ok, text, _ = self.report()
        self.assertTrue(ok)
        self.assertIn("Build APK Release", text)
        self.assertIn("a" * 40, text)
        for changes in (dict(total=0, passed=0), dict(exit=1), dict(passed=0, skipped=1),
                        dict(passed=0, failed=1)):
            with self.subTest(changes=changes):
                tests = dict(self.tests, **changes)
                self.assertFalse(self.report(tests=tests)[0])
        for changes in (dict(skipped=True), dict(exit=1), dict(succeeded=False),
                        dict(audit="RESULT: FAIL static budget"), dict(shader=["shader error"]),
                        dict(compile=["error CS0001"])):
            with self.subTest(changes=changes):
                self.assertFalse(self.report(build=dict(self.build, **changes))[0])

    def test_ignored_voice_is_visible_and_blocks_green(self):
        tests = copy.deepcopy(self.tests)
        tests.update(passed=0, skipped=1)
        tests["cases"][0]["result"] = "Skipped"
        ok, text, _ = self.report(tests=tests)
        self.assertFalse(ok)
        self.assertIn("1 skipped", text)
        self.assertIn("OnlineServicesTests.Voice", text)

    def test_xml_exit_and_skipped_results_are_preserved(self):
        (self.logs / "run-playmode.xml").write_text(
            '<test-run total="2" passed="1" failed="0"><test-case fullname="Host" result="Passed"/>'
            '<test-case fullname="Voice" result="Skipped"/></test-run>', encoding="utf-8")
        with patch.object(runner, "unity", return_value=1):
            tests, error = runner.run_tests("run", "PlayMode")
        self.assertIsNone(error)
        self.assertEqual((1, 1), (tests["exit"], tests["skipped"]))
        self.assertEqual("Voice", tests["cases"][1]["name"])

    def test_build_forwards_mode_and_rejects_development_result(self):
        log = self.logs / "run-build.log"
        log.write_text("[Bite or Betray] Succeeded; mode=Development; errors=0\n", encoding="utf-8")
        with patch.object(runner, "unity", return_value=0) as unity:
            self.assertFalse(runner.build("run", "Release")["succeeded"])
            self.assertEqual(("-bobBuildMode", "Release"), unity.call_args.args[-2:])
        for marker in ("; report=rc.txt", "; laporan=rc.txt"):    # translated and pre-translation Unity output
            log.write_text(f"[Bite or Betray] Succeeded; mode=Release; errors=0{marker}\n", encoding="utf-8")
            with patch.object(runner, "unity", return_value=0):
                build = runner.build("run", "Release")
            self.assertTrue(build["succeeded"])
            self.assertEqual("rc.txt", build["report_path"])

    def test_silent_tool_failure_and_hang_count_as_crashes(self):
        log = self.logs / "run-build.log"
        log.write_text("il2cpp.exe @rsp\nUnityEditor.Modules.BeeBuildPostprocessor:PrintStdoutOnErrorProcessor\n"
                       "[Bite or Betray] Failed; mode=Release; errors=1\n", encoding="utf-8")
        with patch.object(runner, "unity", return_value=1):
            self.assertTrue(runner.build("run", "Release")["crash"])
        log.write_text(log.read_text(encoding="utf-8") + "Error: IL2CPP error for method 'Foo'\n", encoding="utf-8")
        with patch.object(runner, "unity", return_value=1):
            self.assertFalse(runner.build("run", "Release")["crash"], "A real IL2CPP error is a code error.")
        with patch.object(runner, "unity", return_value=-1):
            self.assertIn(runner.HUNG, runner.build("run", "Release")["crash"])

    def test_lfs_file_with_right_size_but_wrong_bytes_is_checked_out_again(self):
        good = b"\x89PNG red brown albedo"
        folder = self.root / "Assets/Meshy/Red Grouch"
        folder.mkdir(parents=True)
        (folder / "Albedo.png").write_bytes(b"\x89PNG red brown albedX")     # same size, one byte differs
        (folder / "Normal.png").write_bytes(b"normal")
        listing = (f"{hashlib.sha256(good).hexdigest()} * Assets/Meshy/Red Grouch/Albedo.png\n"
                   f"{hashlib.sha256(b'normal').hexdigest()} * Assets/Meshy/Red Grouch/Normal.png")
        checkouts = []

        def git(*args, check=True):
            if args[:2] == ("lfs", "ls-files"):
                return listing
            checkouts.append(args)
            (folder / "Albedo.png").write_bytes(good)                         # smudge from the verified object
            return ""
        with patch.object(runner, "git", side_effect=git), patch.object(runner.subprocess, "run") as run:
            self.assertEqual(["Assets/Meshy/Red Grouch/Albedo.png"], runner.repair_lfs())
        self.assertEqual([("checkout", "-f", "--", "Assets/Meshy/Red Grouch/Albedo.png")], checkouts)
        self.assertEqual([["lfs", "fsck"], ["lfs", "pull"]], [c.args[0][3:] for c in run.call_args_list])
        self.assertEqual(good, (folder / "Albedo.png").read_bytes())
        self.assertIn("Albedo.png", (self.logs / "runner.log").read_text(encoding="utf-8"))
        with patch.object(runner, "git", side_effect=git), patch.object(runner.subprocess, "run"):
            self.assertEqual([], runner.repair_lfs(), "Intact LFS files are left alone.")

    def test_old_indonesian_audit_marker_still_counts_as_pass(self):
        self.assertTrue(self.report(build=dict(self.build, audit="HASIL: LULUS budget statis"))[0])

    def test_archive_keeps_full_perf_xml_and_marks_missing_evidence(self):
        perf = self.root / "Logs/ci/perf.txt"
        perf.parent.mkdir(parents=True)
        data = b"x" * 7000 + b"\nstatus=complete\n"
        perf.write_bytes(data)
        (self.logs / "run-editmode.xml").write_text("<test-run/>", encoding="utf-8")
        screens = {"01-menu.png": b"screen from this run"}
        with patch.object(runner, "git", return_value="scene diff"):
            evidence = runner.release_evidence("a" * 40, "run", self.tests, self.tests,
                                               self.build, "CI for this run", screens)
        manifest = json.loads(evidence["manifest.json"])
        self.assertEqual("a" * 40, manifest["source_commit"])
        self.assertEqual(data, evidence["perf.txt"])
        self.assertEqual(hashlib.sha256(data).hexdigest(), manifest["files"]["perf.txt"])
        self.assertIn("playmode.xml", manifest["missing"])
        self.assertIn("build-report.txt", manifest["missing"])
        self.assertIn("screens/06-results.png", manifest["missing"])
        self.assertEqual(hashlib.sha256(screens["01-menu.png"]).hexdigest(), manifest["screenshots"]["01-menu.png"])
        self.assertEqual(b"scene diff", evidence["workspace.diff"])


class TrailerRunnerTests(unittest.TestCase):
    """BOB-052: trailer clips only when record-trailer.txt = yes, once per code commit, list + durations in CI.md."""

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.videos = self.root / "videos"
        self.videos.mkdir()
        for name, value in (("REPO", str(self.root)), ("LOGS", str(self.root)), ("VIDEOS", str(self.videos))):
            mock = patch.object(runner, name, value)
            mock.start()
            self.addCleanup(mock.stop)
        self.out = self.videos / "c0de"
        (self.out / "01-menu").mkdir(parents=True)
        (self.out / "01-menu" / "frame_000000.jpg").write_bytes(b"x" * 10)
        (self.out / "03-gameplay-item").mkdir()
        (self.out / "clips.txt").write_text("01-menu\t210\tMain menu\n03-gameplay-item\t1500\tGameplay with items\n",
                                            encoding="utf-8")
        self.suite = dict(total=2, passed=2, failed=0, skipped=0, fails=[], exit=0, cases=[])

    def test_playmode_excludes_trailer_category_and_trailer_run_selects_it(self):
        with patch.object(runner, "unity", return_value=0) as unity:
            runner.run_tests("run", "PlayMode")
            self.assertEqual(("-testCategory", "!Trailer"), unity.call_args.args[-2:])
            runner.run_tests("run", "EditMode")
            self.assertNotIn("-testCategory", unity.call_args.args)
            runner.run_tests("run", "PlayMode", "trailer", ("-testCategory", "Trailer", "-bobTrailerDir", "out"))
            self.assertEqual(("-testCategory", "Trailer", "-bobTrailerDir", "out"), unity.call_args.args[-4:])
            self.assertTrue(unity.call_args.args[0].endswith("run-trailer.log"))

    def test_ffmpeg_makes_mp4_per_clip_and_ci_lists_files_and_duration(self):
        def ffmpeg(cmd, **kwargs):
            Path(cmd[-1]).write_bytes(b"m" * 2_000_000)
            return runner.subprocess.CompletedProcess(cmd, 0, "", "")
        with patch.object(runner.subprocess, "run", side_effect=ffmpeg) as run:
            clips = runner.encode_clips(str(self.out), "ffmpeg")
        self.assertEqual(["01-menu.mp4", "03-gameplay-item.mp4"], [c["file"] for c in clips])
        self.assertEqual(7.0, clips[0]["seconds"])
        cmd = run.call_args_list[0].args[0]
        self.assertTrue(cmd[cmd.index("-i") + 1].endswith("frame_%06d.jpg"))
        self.assertEqual("30", cmd[cmd.index("-framerate") + 1])
        self.assertFalse((self.out / "01-menu").exists(), "Frames are deleted once the MP4 exists.")
        text = runner.trailer_section("c0de", str(self.out), self.suite, None, clips, "ffmpeg")
        for part in ("01-menu.mp4", "7.0 s (210 frames)", "50.0 s", "Total 57.0 s", "2/2 passed"):
            self.assertIn(part, text)
        self.assertNotIn("winget", text)
        tests = dict(total=1, passed=1, failed=0, skipped=0, fails=[], exit=0, cases=[])
        build = dict(exit=0, result="Succeeded; mode=Development; errors=0", mode="Development", succeeded=True,
                     shader=[], compile=[], crash=[], skipped=False, audit="RESULT: PASS static budget")
        ok, report, _ = runner.report("a" * 40, tests, None, tests, None, build, text)
        self.assertTrue(ok, "Trailer clips do not change the CI status.")
        self.assertIn("## Trailer clips (BOB-052)", report)

    def test_without_ffmpeg_frames_stay_and_install_hint_is_reported(self):
        clips = runner.encode_clips(str(self.out), None)
        self.assertEqual("01-menu/", clips[0]["file"])
        self.assertEqual(10, clips[0]["size"])
        self.assertTrue((self.out / "01-menu" / "frame_000000.jpg").exists())
        text = runner.trailer_section("c0de", str(self.out), None, "Unity produced no test results", clips, None)
        self.assertIn("winget install --id Gyan.FFmpeg -e", text)
        self.assertIn("Unity produced no test results", text)

    def test_records_only_when_requested_and_once_per_code_commit(self):
        answers = {"show": "no"}

        def git(*args, check=True):
            return answers["show"] if args[0] == "show" else "c0de"
        with patch.object(runner, "git", side_effect=git), \
                patch.object(runner, "record_trailer", return_value="new") as record:
            self.assertIsNone(runner.trailer_report("run"))
            answers["show"] = " Yes\n"
            self.assertEqual("new", runner.trailer_report("run"))
            record.assert_called_once_with("run", "c0de")
            (self.out / "clips.md").write_text("old", encoding="utf-8")
            self.assertTrue(runner.trailer_report("run").startswith("old"))
            self.assertEqual(1, record.call_count, "A code commit that already has clips is not re-recorded.")
            self.assertEqual("new", runner.trailer_report("run", force=True))


if __name__ == "__main__":
    unittest.main()
