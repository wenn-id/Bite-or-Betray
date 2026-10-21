# Release candidate build evidence

BOB-025 requests Release via `Docs/Release/build-mode.txt`. Status and checklist are in
[Docs/Release/RC.md](../../Docs/Release/RC.md). There is no Release build result yet that can be attached from the VPS without Unity.

The laptop runner stores every Release run in `RC/<full commit hash>/` before the checkout for publishing:

- `manifest.json`: source commit, mode, build exit/result, every test name/result, list of missing evidence, SHA-256 of every piece of evidence
  and screenshot from the same run.
- `CI.md`, `editmode.xml`, `playmode.xml`: full reports including failed/skipped tests.
- `build-report.txt`: Unity report (duration, size, APK SHA-256, errors/warnings, largest assets, shader stripping).
- `perf.txt`, `perf-telemetry.csv`, `regression-telemetry.csv`, `audit.txt`: complete data without the CI.md excerpt truncation.
- `workspace.diff`, `workspace-status.txt`: tracked changes plus the list of changed/new files in Assets/Packages/ProjectSettings
  after prepare/import/tests/build; check them when the build report is marked `-dirty`. New generated assets are not included
  in the patch. The source hash alone does not prove the APK is identical to a clean checkout.

Evidence that was not produced is written to `manifest.missing`; do not treat it as passed. Repeating the same hash replaces
that hash's archive with the latest result, while Git history keeps the previous publications. PNG screenshots are not duplicated in
the archive; match the manifest hashes against `Docs/Autopilot/screens/` at that CI publishing commit (download LFS).
The APK is not committed; it stays at `Builds/BiteOrBetray_Quest_Release.apk` on the laptop. Raw Unity logs stay in `~/bob-ci/logs/`.

The Builds folder is ignored so the APK does not get into Git. The runner uses `git add -f` only for the `RC/<hash>/` text archive;
this README is included explicitly. Do not add APKs or change LFS pointers from the VPS.
