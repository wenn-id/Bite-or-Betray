# BOB-025: Release candidate without a headset

Updated 2026-11-17 from the local working copy, using Unity 6000.6.3f1 on the laptop. No Quest was attached during
validation. Earlier candidate evidence retained below is historical.

## Current main candidate: finalized 2026-11-18

The tested chibi release is integrated with main's independent multiplayer fixes. **147/147 EditMode, 74/74 PlayMode and 1/1 real Meta XR Simulator startup passed**, no skips. The Release APK was rebuilt from the merged source: **136,640,644 bytes**, 0 errors, 31 warnings; signature verified.

APK: `Builds/BiteOrBetray_Quest_Release.apk`. SHA-256: `4e01a1e7532e799ee64d3c937f9a4d8b6ac3cad16eda8f445e0d5f7510bc437a`.
The old develop APK is archived locally. Use the current main APK for submission.

[Main integration evidence](../../ArtSource/MainIntegration20261117/README.md) records the test-fixture correction, render budgets, package privacy and remaining device checks. [Source/APK receipt](source-apk-20261117.json) binds the committed Unity inputs to this candidate; verify with `python Tools/CI/verify_source_apk.py`. No physical Quest was connected, so hardware performance and two-headset voice remain unverified.

## Historical develop candidate: 2026-11-17

The new chibi cast, 115% head/glove scale, Reaper/cloche and revised room are included in
`Builds/BiteOrBetray_Quest_Release.apk`: **136,622,460 bytes (130.3 MiB)**. Build succeeded with 0 errors and 9 warnings;
the APK signature was verified. SHA-256: `ccdf646673fa073078ad6181256a4c8e4c605bbac67bd0627c0e43437685a8ed`.

**141/141 EditMode and 74/74 regular PlayMode tests passed, no skips**, plus a separate successful startup with the
actual Meta XR Simulator 207.0.0. The main scene's conservative static maximum is **246,730 triangles, 84 draws/view,
42 materials**; runtime batch capture peaked at 90 draws. These are not physical Quest FPS measurements.

This pass fixed excess decorative-candy geometry, the outdated Devpost capture camera, and an SDK callback that
injected development credentials into Release packages. The final APK contains neither the previous nor regenerated
Editor server token. The owner approved regeneration, and EditorPrefs readback confirmed the token changed.
Nine current screenshots are in `Docs/Autopilot/screens/devpost*.png`.

Full results, build report, measurements, source state and remaining hardware checks:
[Submission audit](../../ArtSource/SubmissionAudit20261117/README.md). The owner already has a submission video;
it was not inspected in this pass. Inspiration/Team and submission links remain owner tasks. The tested source is
published on `develop`, with its APK binding in `source-apk-20261117.json`; verify with
`python Tools/CI/verify_source_apk.py`.

## Historical local candidate: 2026-11-12

Base commit `8bf63e548cf02f02cb5db58a7d180b7988697237`, with uncommitted models, audio, animation and release fixes.
This is a validated local working copy, not a tagged release or a new remote CI result. The
[manifest](../../Builds/reports/RC/local-20261112/manifest.json) records file hashes, workspace changes and evidence.

| Check | Current local result |
|---|---|
| EditMode | 140/140 passed, no failures or skips |
| PlayMode | 52/52 passed, no failures or skips, batch process exit 0; Trailer category excluded |
| Release APK | Succeeded, exit 0, 0 errors, 9 warnings; **149,437,767 bytes (142.5 MiB)** |
| Shader / compile errors | 0 / 0 |
| CandyParlour3D static audit | PASS: max 220,560/250,000 triangles, 73/100 draws per eye, 26/50 materials |
| QuestPerfTest static audit | PASS: max 220,616 triangles, 74 draws per eye, 27 materials |
| Runtime UI budget | 17 captures passed; peak 87/100 draws in batch mode |
| UI contrast | Rendered card/button and status-text pairs: 5.00:1 to 12.71:1 |
| Repeated matches | Ten matches alternating replay/menu; object/material/audio counts remained bounded |
| Scene references | All 137 referenced GUIDs found; required hierarchy checks passed |
| Screenshots | 25 fresh PNGs in `Docs/Autopilot/screens`, hashed in the manifest |
| Audio export | All 14 WAV files have silent endpoints; export regression passed |

Changes in this pass: correct sRGB-to-linear conversion in the two UI shaders; readable GOOD/WINNER/Clover colors;
an opaque card behind candy status; 5 ms audio boundary fades; rendered-contrast and audio-export regression checks.
Build reporting now uses the actual APK file size: Unity's total build-output size was incorrectly labelled as APK bytes.

Editor raw idle GC still includes test-runner overhead and is not near zero. No physical-device FPS, comfort, hand-tracking,
microphone/speaker quality or two-headset internet play is claimed. The APK uses the configured debug keystore in Release
mode, not production store signing. Final trailer footage has not been re-recorded for this working copy.

For local validation use the batch command in [Assets/README.md](../../Assets/README.md). Interactive Editor views can
inflate `UnityStats` beyond the game camera; the budget threshold remains 100. The historical runner instructions below
validate committed `origin/develop`, which does not include this uncommitted working copy.

## Freeze and source identity

Features are frozen at the current scope: offline match, online multiplayer (NGO + Sessions/Lobby/Relay), Vivox voice,
controller and hand tracking, onboarding, audio, spectator, results/restart and telemetry. During the freeze only
regression fixes, layout fixes from the owner-approved review (BOB-059/BOB-060), release evidence and docs are allowed.
No new packages or new features.

Owner decisions (2026-10-22, revised 2026-11-12): music/SFX are original WAVs in `Assets/Resources/Audio` (4 per-round loops + 10 SFX,
`Tools/Audio/bob_generate_audio.py`); the Meshy cast has an openable mouth and 13 facial blendshapes (`Tools/Blender/bob_meshy_faces.py`).

`Docs/Release/build-mode.txt` contains `Release`, so every laptop CI run builds a Release APK.

**Historical green candidate: `f7dacb84a49c12157d636267e502643d93e661b7`** (CI 2026-10-22 00:47 WIB, **PASSED**).
It meets every automated RC condition listed under [Running the next automated validation](#running-the-next-automated-validation).
HEAD is `7c03afb` (2026-10-28), 8 commits after `de6180be`; those commits have **no green run yet** — run the
laptop runner on HEAD before calling anything newer the release candidate. The previous candidate was `f7dacb8`
(2026-10-22).

## Evidence for de6180be

Source: [Laptop CI](../Autopilot/CI.md) and the archive
[Builds/reports/RC/de6180be…/manifest.json](../../Builds/reports/RC/de6180be020ff6689bbcbf2d1e00cb777af29234/manifest.json)
(`source_commit` matches, `missing` is empty). CI.md is overwritten by each run; the archived copy is kept in the same folder.

| Evidence | Result at de6180be |
|---|---|
| Unity EditMode | 140/140 passed, 0 failed, 0 skipped, exit 0 (`editmode.xml`) |
| Unity PlayMode | 51/51 passed, 0 failed, 0 skipped, exit 0 (`playmode.xml`), including all online tests. The `Trailer` category runs separately and is not counted |
| Build | Release Succeeded; 0 errors, 6 warnings; build total size 1,684,837,504 bytes (Unity `BuildReport` total size — see note below); 227.3 s (`build-report.txt`) |
| Shader / compile | 0 / 0 errors |
| Budget Audit | PASSED. CandyParlour3D max 203,085/250,000 triangles, 73/100 draws per eye (0 shadow draws), 26/50 materials; QuestPerfTest 203,141, 74/100, 27/50 (`audit.txt`) |
| perf.txt | status=complete (Editor mono measurement, see below) |
| Telemetry CSV | `regression-telemetry.csv`, `perf-telemetry.csv` archived |
| Screenshots | 25 files in [Docs/Autopilot/screens/](../Autopilot/screens/), SHA-256 in the manifest |
| Trailer clips | Pending: recording was paused for this run, so CI.md has no "Trailer clips" section (see [Trailer clips](#trailer-clips-bob-052)) |

**APK size note (verified 2026-11-07):** the `1,684,837,504 bytes` figure is
`BuildReportSummary.totalSize` from the build report's `Result:` line — Unity's total build size, not the APK file.
The APK file itself is **116,755,271 bytes (111.3 MB)**, SHA-256
`db69e8e89506ea6edb97f4f0c133226884a2b4c9b34fab5992d642030d70be94` (same report, `APK:` line).
Older docs called the 1.68 GB figure "the APK"; that was a mislabel. Do not quote 1.68 GB as the APK size.

The build workspace is marked `-dirty`: `GameplayPerformanceBuilder.PrepareScenes` changes both scenes, the Mobile RP asset,
XR/Vivox/Project settings and adds `Assets/BiteOrBetray/PerformanceMeshes/`. `workspace-status.txt` and `workspace.diff` in the
archive record these changes; this is not a clean-checkout claim.

Perf numbers (perf.txt, Editor on an RTX 3050 laptop, not a GPU/stereo measurement or proof of 72 FPS on Quest): idle GC with
telemetry on averages **6,101.92 B/frame** (peak 14,825), off 6,094.56 (peak 13,943), delta +7.36 B/frame; near-zero
indicator (≤1 KiB) **False**, raw numbers with Editor/Test Runner overhead. Idle 74 draws / 33 SetPass, average frame 14.370 ms.
Automated match (11 turns, finished): average frame 20.093 ms, peak 49.490 ms; peak 81 draws, 39 SetPass.

## Archived runs that are NOT RC evidence

Only a run whose manifest has `missing: []` and 0 failed tests counts as RC evidence. The archive also contains:

- **`f31ae0181b6c25b7d8ed47401de4eebf8cae5ee4` (2026-10-21): FAILED PlayMode — 37/38 passed, 1 failed**
  (`RegressionTests.Seated_framing_1_to_5_bots_clear_player_mat_and_waiter_announcement` — a bot mesh clipped
  the frame edge by ~0.0007). Its build succeeded and EditMode was 139/139, but the run is not green and must
  not be cited as a release candidate.
- The 2026-11-07 audit found 7 of the 25 `Builds/reports/RC/` directories have a non-empty manifest `missing`
  list (e.g. `0facc91…` is missing `playmode.xml`, `perf.txt`, both telemetry CSVs and all 6 screenshots).
  Nothing in the directory listing distinguishes green from failed/incomplete runs — check the manifest before
  citing any archived run.

## BOB-023 regression checklist

Methods in `Assets/Tests/PlayMode/RegressionTests.cs`. Every row **Passed** in the de6180be `playmode.xml` (51/51, 0 failed).

| Test | Coverage |
|---|---|
| Menu_bot_setup_all_onboarding_pages_and_gameplay_with_screens | Menu, bot setup, all onboarding pages, Start, back to menu |
| Eight_items_follow_game_engine_rules | All eight items, inventory and results match Core |
| Three_candy_colors_and_clover_damage_cap_follow_rules | Three good/bad colors, Clover, damage cap 3, paralysis |
| New_round_refills_dispenser_and_deals_items | Round/refill, candy and item counts, waiter |
| Ko_spectator_results_music_sfx_and_telemetry_csv_with_screens | KO, spectator, winner, music/SFX/volume, CSV |
| Five_restarts_keep_objects_materials_audio_sources_and_clips_stable | Five restarts, objects/materials/AudioSources/clips stable |
| Focus_lost_and_regained_cycles_freeze_timer_and_restore_input | Focus/pause, timer frozen, input resumes |
| Controller_path_works_and_pinch_never_doubles_input | Controller path and pinch without double actions |
| Language_english_by_default_switch_in_settings_applies_now_and_persists | English default, Bahasa Indonesia switch, persisted |
| Meshy_material_textures_survive_scene_match_red_KO_and_menu | Meshy cast textures through a match, red KO and menu |
| Seated_framing_1_to_5_bots_clear_player_mat_and_waiter_announcement | Bots framed in the seat view for 1–5 bots, through every reaction |
| UI_labels_do_not_overlap_in_menu_gameplay_spectator_and_results | Table UI labels never overlap |

Seat screenshots `01-menu.png` … `06-results.png` come from this run. They are LFS files; no visual approval is claimed by the agent.

## BOB-041 online checklist

All **Passed** at de6180be, none ignored or skipped:

- `OnlineMatchTests.Host_and_client_play_with_bots_until_winner_with_matching_snapshots`
- `OnlineMatchTests.Client_disconnect_mid_game_seat_played_by_bot_until_winner`
- `OnlineServicesTests.Create_room_with_relay_find_by_code_and_leave` (real UGS login, Sessions/Lobby/Relay)
- `OnlineServicesTests.Voice_joins_room_channel_and_leaves_with_room` (real Vivox channel)
- `OnlineSceneTests`: all four (`Main_menu_of_real_scene_enables_create_and_join_room`,
  `Scene_has_one_network_manager_lobby_and_in_scene_table_bound_to_director`,
  `Scene_host_spawns_in_scene_table_and_client_sync_keeps_loaded_scene`, `Scene_item_and_candy_scales_survive_visual_codec`)

## Trailer clips (BOB-052)

`Docs/Release/record-trailer.txt` was switched back to `yes` after f7dacb8 (commit 9c2536d). The next code commit run records
the eight clips listed in [trailer.md](trailer.md) (`01-menu`, `02-bot-setup`, `03-gameplay-item`, `04-spectator`, `05-results`,
`06-lobby-online`, `07-close-up-bot`, `08-top-down`) into `~/bob-ci/videos/<commit>/`, outside the repo. **All eight are pending**
until a CI.md shows the "Trailer clips" section with their files and durations — the de6180be CI.md has no such section.

## Known gaps

Open `needs_unity_check` tasks in `Docs/Autopilot/tasks.json`. Each is an Editor evidence gap that an automated run can close;
no related test fails at de6180be.

- **Idle GC (BOB-009, BOB-017, BOB-022):** idle allocation is 6,102 B/frame at de6180be (down from 11,145 at f7dacb8),
  not near zero, and no Profiler capture attributes it to game code vs Editor/Test Runner.
- **CPU (BOB-020):** no before/after Profiler capture for `RefreshProps` (`BoB.RefreshProps=n/a` in perf.txt).
- **Audio (BOB-018):** final WAV soundtrack/SFX replace the placeholder tones (above); needs a fresh CI run for evidence.
- **Online (BOB-035, BOB-036, BOB-037, BOB-046):** no test joins by room code from a second UGS identity; the host+client match
  tests use a local transport and a client without an active director, so remote pose interpolation, candy/item visuals on a
  full client, the host-left message on a client and two-process real-scene sync are not asserted.
- **Meta XR Setup Tool (BOB-043):** the camera/listener checks pass in simulation; the Setup Tool report is not archived by CI.
- **Trailer (BOB-052):** clips pending, see above.
- **Devpost screenshots (BOB-055):** files exist and match the manifest; the draft is refreshed by BOB-061.
- **Table UI (BOB-059) and seat labels vs wall sign (BOB-060):** BOB-059's tests pass at de6180be but the task is not yet
  re-evaluated; BOB-060 is still to do and may change the screenshots.

Device-only checks (cannot be proven without a Quest; not claimed):

- APK install/launch, Android rendering, comfort/stereo, 72 FPS, thermals, battery and stability on Quest 3.
- Hand tracking beyond the automated path (pinch mapping, input priority, duplicate prevention): aim, occlusion, tracking
  loss/recovery on device, hand/controller switching, near-face tolerance.
- Voice beyond join/leave/mute: players hearing each other, device microphone permission/speaker, echo, latency, quality.
- Online play between two headsets over a real internet connection.
- Camera/AudioListener on physical Android XR, room code readability on Quest, audio quality on the headset, CSV retrieval from the device.
- The APK is signed with the configured debug keystore (BOB-024): Release mode, not a production-signed store package.
  No claim of store readiness.

## Running the next automated validation

On the laptop licensed for Unity 6000.6.3f1 + Android Support, with internet and LFS downloaded, reinstall the runner copy
(the Task Scheduler version does not automatically follow repo changes), then force one cycle:

```powershell
powershell -ExecutionPolicy Bypass -File Tools\Autopilot\install_unity_runner.ps1
python "$HOME\bob-ci\bin\unity_runner.py" --force
```

The runner fetches `origin/develop`, runs EditMode and PlayMode, prepares scenes/audit, then calls
`BiteOrBetray.EditorTools.CIRunner.Run -bobBuildMode Release`. No headset or manual runtime steps are needed.
If the owner's Unity is open, the build is postponed and the report does not meet PASSED. Online results skipped without a network
also do not meet PASSED. Direct Release build without tests (diagnostics only):

```text
Unity -batchmode -quit -projectPath <repo> -buildTarget Android -executeMethod BiteOrBetray.EditorTools.AvatarValidation.BatchBuild -bobBuildMode Release
```

RC conditions for a hash: the manifest `source_commit` equals the hash; Release Succeeded, exit 0; all EditMode/PlayMode Passed
(including online, none skipped); Budget Audit PASSED; perf status complete; six seat screenshots and both CSVs archived;
`missing` empty; workspace diff reviewed. Record the new hash and numbers here and in
[the Devpost draft](../Devpost/submission.md). Device-only gaps stay listed until someone tests on a Quest.
