# Devpost readiness audit — 2026-11-17

Validated the working copy in Unity 6000.6.3f1. The candidate uses the six enlarged chibi heads, twelve gloves, Reaper waiter with serving cloche, and the revised interior. The source snapshot and this evidence are published together on `develop`. `Docs/Release/source-apk-20261117.json` binds all committed Unity inputs to the tested APK hash; run `python Tools/CI/verify_source_apk.py` to verify the checkout.

## Candidate to share

`../../Builds/BiteOrBetray_Quest_Release.apk`

- Release, ARM64, `com.biteorbetray.game`, version 0.1.0 / code 1.
- 136,622,460 bytes (130.3 MiB), build succeeded with 0 errors and 9 warnings.
- APK signature verified with the installed Android apksigner. It uses the project's configured debug keystore, rather than production store signing.
- SHA-256: `ccdf646673fa073078ad6181256a4c8e4c605bbac67bd0627c0e43437685a8ed`.
- The configured Unity cloud project ID is present in `globalgamemanagers`. A Services Core build warning also checks whether the batch Editor is signed in; the actual Relay/session and voice-channel tests passed.

## Findings corrected

1. **Development credential in the release package.** Meta SDK's order-1 DevAgent build callback injected the Editor server token into a Resources asset, even for Release. The first APK reproduced the leak. The runtime client asset now contains no connection credentials; an order-last Release callback disables DevAgent and strips its tokens/address after SDK processing. A regression test verifies stripping. The final APK scan finds neither the previous repository token nor the newly generated server token. The owner approved token regeneration; EditorPrefs readback confirmed that the old token was replaced. No private token values are recorded in this report.
2. **Quest static triangle budget exceeded.** The main/performance scenes reached 273,130 / 273,186 triangles per view, above their 250,000 target. Forty-four tiny decorative candies used 768-triangle built-in spheres. They now share a smooth 168-triangle sphere, preserving diameter, positions and colors. This removes 26,400 triangles; the actual furniture batches are rebuilt. Faces, glove skinning and gameplay candy meshes are unchanged.
3. **Devpost screenshot capture failed.** The capture fixture assumed a camera at the player's seat while the authored desktop preview now uses an overview. It now explicitly selects a seated capture camera, with a suitable field of view. All eight normal-match screenshots were refreshed, plus a separate six-character overview from the existing layout test.

The first failing PlayMode run and budget reports remain in `Before/`. The unsafe intermediate APK was withdrawn from the Release path and removed after the clean candidate was verified. The original November 12 APK backup remains in ignored `Builds/archive/`; use the candidate above for submission.

## Final verification

| Check | Result | Evidence |
| --- | --- | --- |
| EditMode | **141/141 passed**, no skips | `EditMode.xml`; 140 existing rules/protocol/settings tests plus Release settings regression |
| Regular PlayMode | **74/74 passed**, no skips | `PlayMode.xml`; Trailer category excluded |
| Actual Meta XR Simulator startup | **1/1 passed**, runtime 207.0.0, original OVRPlugin enabled | `MetaXRSmoke.xml`, `../../Logs/SubmissionAudit20261117-meta-xr-smoke.log` |
| Main scene static budget | **246,730 triangles, 84 draws/view, 42 materials** | `QuestBudgetAudit.txt` |
| Performance scene static budget | **246,786 triangles, 85 draws/view, 43 materials** | `QuestBudgetAudit.txt` |
| Runtime render assertions | Peak **90 draws**, 46 SetPass in batch capture | `RuntimePerformance.txt`; not a Quest FPS measurement |
| Scene references | **175/175 found**, required hierarchy passed | `SceneCheck.txt` |
| Enlarged visual preservation | 18 transforms at 1.15 scale, six mouth anchors, 44 optimized candy sources in each scene | `SceneState.json` |
| Audio assets/export | All **14 WAVs** non-silent, unclipped individually, silent endpoints; export regression passed | `AudioFiles.json`, `Tools/Audio/test_audio.py` |
| Release package privacy | Known previous token absent; regenerated Editor token absent | `PackageTokenCheck.txt`, `TokenRotation.json` |

PlayMode includes full matches and item interactions, focus/input regressions, both grips and eating, all six blink/chew rigs, reaction framing, restart/cancellation and repeated-match object counts, local host/client matches, host leave/rejoin, and real Unity Gaming Services room/Relay creation/lookup/leave plus voice channel join/leave. Music playback was tested with an available audio device: all four tracks wrap their endpoints, repeated play does not restart the same track, volume and component toggles preserve playback, and live rounds 1–5 select the expected tracks before returning to the menu track.

Nine current screenshots are in `../../Docs/Autopilot/screens/devpost*.png`; `devpost-overview.png` shows all six characters, the Reaper/cloche, and the revised room. The Devpost draft's cast/reaction descriptions and validation figures have been updated. The owner confirmed that a submission video already exists; this audit did not inspect or replace that video.

## Remaining checks before submission

- Install/launch this APK on a physical Quest and check controller alignment, grabbing/eating, audio balance and stereo comfort. No headset was connected; device FPS, thermals, microphone/speaker quality and a two-headset internet/voice session remain unverified.
- Fill the owner-authored Inspiration and Team sections in the Devpost draft and provide the actual APK/video links. Use the source commit on `develop` that contains this audit and its source/APK receipt.
- Existing tools that used the old Meta AgentBridge token must reconnect with the regenerated token. It is stored in user-specific EditorPrefs, not the shared runtime asset.

Passing these checks establishes the tested paths and a successful release build; it does not establish that every possible game state is bug-free.
