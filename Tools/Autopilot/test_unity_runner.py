"""Regresi pelaporan RC tanpa menjalankan Unity, git publish, atau jaringan."""
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
                          audit="HASIL: LULUS budget statis")

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
                        dict(audit="HASIL: GAGAL budget statis"), dict(shader=["shader error"]),
                        dict(compile=["error CS0001"])):
            with self.subTest(changes=changes):
                self.assertFalse(self.report(build=dict(self.build, **changes))[0])

    def test_ignored_voice_is_visible_and_blocks_green(self):
        tests = copy.deepcopy(self.tests)
        tests.update(passed=0, skipped=1)
        tests["cases"][0]["result"] = "Skipped"
        ok, text, _ = self.report(tests=tests)
        self.assertFalse(ok)
        self.assertIn("1 dilewati", text)
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
        log.write_text("[Bite or Betray] Succeeded; mode=Release; errors=0; laporan=rc.txt\n", encoding="utf-8")
        with patch.object(runner, "unity", return_value=0):
            build = runner.build("run", "Release")
        self.assertTrue(build["succeeded"])
        self.assertEqual("rc.txt", build["report_path"])

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
        self.assertIn("screens/06-hasil-akhir.png", manifest["missing"])
        self.assertEqual(hashlib.sha256(screens["01-menu.png"]).hexdigest(), manifest["screenshots"]["01-menu.png"])
        self.assertEqual(b"scene diff", evidence["workspace.diff"])


if __name__ == "__main__":
    unittest.main()
