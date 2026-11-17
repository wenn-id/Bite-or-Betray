# Main integration release evidence

Validated on 2026-11-17; finalized on 2026-11-18 with Unity 6000.6.3f1. This candidate supersedes the earlier develop APK. Main's independent multiplayer/security fixes were preserved when merging the tested chibi release: room approval/discovery, snapshot bounds, rejected-action limits, turn-order fallback and cached camera access. The current compact table labels and authored preview camera are retained.

The full PlayMode run initially exposed a test fixture race: room codes are assigned before asynchronous session creation completes. The fixture now waits for the operation to finish and asserts the session exists. The focused 3-test rerun and complete 74-test rerun passed. Initial failures remain in PlayMode-first.xml; PlayMode-final.xml is authoritative.

| Check | Result | Evidence |
| --- | --- | --- |
| EditMode | 147/147 passed, no skips | EditMode.xml |
| Regular PlayMode | 74/74 passed, no skips | PlayMode-final.xml |
| Real Meta XR Simulator | 1/1 passed; runtime 207.0.0 | MetaXRSmoke.xml |
| Release APK | 136,640,644 bytes; 0 errors, 31 warnings; signature verified | BuildReport.txt |
| Main / performance scene budget | 246,730 / 246,786 triangles; 84 / 85 draws per eye | QuestBudgetAudit.txt |
| Runtime capture | Peak 90 draws, 46 SetPass; batch measurements | RuntimePerformance.txt |
| Scene references and hierarchy | Passed | SceneCheck.txt |
| Known previous/current Editor tokens in APK | 0 matches | PackageTokenCheck.txt |

APK: `Builds/BiteOrBetray_Quest_Release.apk` from the repository root. SHA-256: `4e01a1e7532e799ee64d3c937f9a4d8b6ac3cad16eda8f445e0d5f7510bc437a`.
The previous develop APK is retained locally under ignored `Builds/archive/`.

`Docs/Release/source-apk-20261117.json` binds the committed Unity inputs to this APK. Run `python Tools/CI/verify_source_apk.py` from the repository root. The SDK restored private Editor connection settings during tests; those fields were cleared before committing, matching the existing Release prepack stripping. EditorPrefs were preserved. No player code or content changed after validation. manifest.json records the evidence hashes and cleanup.

The prior submission audit retains the audio, 115% visual and animation evidence; the full integration suite rechecks those gameplay paths. Nine Devpost screenshots were refreshed from this integration. The owner already has a demo video; it was not inspected or replaced. No physical Quest was connected: device FPS, stereo comfort, microphone/speaker balance and a two-headset internet match remain unverified. The APK uses the configured debug keystore, rather than production store signing.
