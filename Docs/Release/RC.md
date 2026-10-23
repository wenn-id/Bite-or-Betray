# BOB-025: Release candidate without a headset

Refreshed 2026-10-22 (BOB-061) from the latest green laptop CI. The agent machine has no Unity and no physical Quest,
and neither does the owner, so nothing here has been tested on a device.

## Freeze and source identity

Features are frozen at the current scope: offline match, online multiplayer (NGO + Sessions/Lobby/Relay), Vivox voice,
controller and hand tracking, onboarding, audio, spectator, results/restart and telemetry. During the freeze only
regression fixes, layout fixes from the owner-approved review (BOB-059/BOB-060), release evidence and docs are allowed.
No new packages or new features.

Owner decisions (2026-10-22): music and SFX stay the runtime-generated placeholder tones (`GameplayAudio.MakePlaceholder`)
for this release; the Meshy cast gets no facial expressions (BOB-021 skipped).

`Docs/Release/build-mode.txt` contains `Release`, so every laptop CI run builds a Release APK.

**Latest green candidate: `f7dacb84a49c12157d636267e502643d93e661b7`** (CI 2026-10-22 00:47 WIB, **PASSED**).
It meets every automated RC condition listed under [Running the next automated validation](#running-the-next-automated-validation).
Commits after it (BOB-060 seat label fix, trailer clips) need their own green run; the newest green hash then replaces this one.

## Evidence for f7dacb8

Source: [Laptop CI](../Autopilot/CI.md) and the archive
[Builds/reports/RC/f7dacb8…/manifest.json](../../Builds/reports/RC/f7dacb84a49c12157d636267e502643d93e661b7/manifest.json)
(`source_commit` matches, `missing` is empty). CI.md is overwritten by each run; the archived copy is kept in the same folder.

| Evidence | Result at f7dacb8 |
|---|---|
| Unity EditMode | 139/139 passed, 0 failed, 0 skipped, exit 0 (`editmode.xml`) |
| Unity PlayMode | 38/38 passed, 0 failed, 0 skipped, exit 0 (`playmode.xml`), including all online tests. The `Trailer` category runs separately and is not counted |
| Build | Release Succeeded; 0 errors, 9 warnings; APK 1,684,812,938 bytes (about 1.57 GiB); 425.2 s (`build-report.txt`) |
| Shader / compile | 0 / 0 errors |
| Budget Audit | PASSED. CandyParlour3D max 203,085/250,000 triangles, 73/100 draws per eye (0 shadow draws), 26/50 materials; QuestPerfTest 203,141, 74/100, 27/50 (`audit.txt`) |
| perf.txt | status=complete (Editor mono measurement, see below) |
| Telemetry CSV | `regression-telemetry.csv`, `perf-telemetry.csv` archived |
| Screenshots | 25 files in [Docs/Autopilot/screens/](../Autopilot/screens/), SHA-256 in the manifest; the eight `devpost-*.png` LFS oids match it |
| Trailer clips | Pending: recording was paused for this run, so CI.md has no "Trailer clips" section (see [Trailer clips](#trailer-clips-bob-052)) |

The build workspace is marked `-dirty`: `GameplayPerformanceBuilder.PrepareScenes` changes both scenes, the Mobile RP asset,
XR/Vivox/Project settings and adds `Assets/BiteOrBetray/PerformanceMeshes/`. `workspace-status.txt` and `workspace.diff` in the
archive record these changes; this is not a clean-checkout claim.

Perf numbers (perf.txt, Editor on an RTX 3050 laptop, not a GPU/stereo measurement or proof of 72 FPS on Quest): idle GC with
telemetry on averages **11,145.40 B/frame** (peak 20,215), off 11,174.15 (peak 19,341), delta −28.75 B/frame; near-zero
indicator (≤1 KiB) **False**, raw numbers with Editor/Test Runner overhead. Idle 74 draws / 33 SetPass, average frame 13.768 ms.
Automated match (11 turns, finished): average frame 17.557 ms, peak 35.316 ms; peak 81 draws, 39 SetPass.

## BOB-023 regression checklist

Methods in `Assets/Tests/PlayMode/RegressionTests.cs`. Every row **Passed** in the f7dacb8 `playmode.xml`.

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

All **Passed** at f7dacb8, none ignored or skipped:

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
until a CI.md shows the "Trailer clips" section with their files and durations.

## Known gaps

Open `needs_unity_check` tasks in `Docs/Autopilot/tasks.json`. Each is an Editor evidence gap that an automated run can close;
no related test fails at f7dacb8.

- **Idle GC (BOB-009, BOB-017, BOB-022):** idle allocation is 11,145 B/frame, not near zero, and no Profiler capture attributes
  it to game code vs Editor/Test Runner.
- **CPU (BOB-020):** no before/after Profiler capture for `RefreshProps` (`BoB.RefreshProps=n/a` in perf.txt).
- **Audio (BOB-018):** owner approval of the placeholder tones is now an owner decision (above); the task is not re-evaluated here.
- **Online (BOB-035, BOB-036, BOB-037, BOB-046):** no test joins by room code from a second UGS identity; the host+client match
  tests use a local transport and a client without an active director, so remote pose interpolation, candy/item visuals on a
  full client, the host-left message on a client and two-process real-scene sync are not asserted.
- **Meta XR Setup Tool (BOB-043):** the camera/listener checks pass in simulation; the Setup Tool report is not archived by CI.
- **Trailer (BOB-052):** clips pending, see above.
- **Devpost screenshots (BOB-055):** files exist and match the manifest; the draft is refreshed by BOB-061.
- **Table UI (BOB-059) and seat labels vs wall sign (BOB-060):** BOB-059's tests pass at f7dacb8 but the task is not yet
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
