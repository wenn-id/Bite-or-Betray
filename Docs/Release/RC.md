# BOB-025 — Release candidate without a headset

Preparation date: 2026-10-17. Status **needs_unity_check — not yet fit to be declared a passed RC**.
There is no Unity or physical Quest on the agent machine. The owner does not have a Quest; there is no claim of on-device testing.

## Freeze and source identity

Features are frozen at the current scope: offline match, online multiplayer (NGO + Sessions/Lobby/Relay), Vivox voice,
controller and hand tracking, onboarding, audio, spectator, results/restart and telemetry. During the freeze only fixes for
BOB-023/BOB-041 regression blockers, together with accepted release tests/evidence, are allowed. No new packages or new features in BOB-025.

Source hash at preparation: `b0a9cac007e8d5b305cc571028fa21aca5e71b80` (before the BOB-025 commit; not a tested candidate).
The commit that requests the RC is marked by the addition of `Docs/Release/build-mode.txt` containing `Release`; after the commit it can be read with:

```bash
git log -1 --format=%H -- Docs/Release/build-mode.txt
```

**Passed candidate hash: none yet.** The full hash of the checkout actually tested by the runner is written to CI.md and to
`Builds/reports/RC/<full hash>/manifest.json` as `source_commit`. All evidence for designating the candidate must come
from that hash. The historical reports below do not validate the BOB-025 commit or the newly added online tests.

## Evidence available at preparation

Source: [Laptop CI](../Autopilot/CI.md), report for `b46ff60c2a261e645a6550f1fd4ea6cecaca5a5a`,
2026-10-17 12:39 WIB, **FAILED**. CI.md can change; the historical version can be read via
`git show b0a9cac:Docs/Autopilot/CI.md`.

| Evidence | Historical result | Release candidate evidence |
|---|---|---|
| Unity EditMode | 111/111 passed | Awaiting XML and exit 0 on the candidate hash |
| Unity PlayMode | 15/17 passed, 2 failed | Awaiting the full suite, with no ignored/skipped |
| Build | Development Succeeded; 0 errors, 6 warnings; 1,785,206,945 bytes | Release not run yet |
| Shader / compile | 0 / 0 errors | Awaiting Release build |
| Budget Audit | FAILED; gameplay 111/100, QuestPerfTest 112/100 draws/eye | Awaiting audit of both scenes |
| perf.txt | status=complete, Editor mono measurement | Awaiting a complete copy on the candidate hash |
| Screenshot | 01–04 present as LFS pointers; 05–06 not present yet | Awaiting six images from the candidate run |

The historical build report is referenced by CI.md only as
`BiteOrBetray_Quest_Development_20261017-123618_b46ff60-dirty.txt`; the raw file/APK is not yet available on the VPS.
The `Builds/reports/` folder was previously untracked. The [new archive guide](../../Builds/reports/README.md) describes the files
the runner will store; no Release result archive has been created or fabricated here.

Historical perf numbers: idle GC with telemetry enabled averages **6,608.18 B/frame**, peak 15,501; disabled 6,606.11, peak 14,799;
delta 2.07 B/frame. Near-zero indicator (<=1 KiB) **False**. Idle 78 draws / 33 SetPass; average frame 6.042 ms,
peak 12.279 ms. Automated match finished (11 turns): average 6.714 ms, peak 512.419 ms; peak 85 draws,
38 SetPass. This includes Editor/Test Runner overhead and manual rendering; it is not a GPU/stereo measurement or proof of 72 FPS on Quest.
Gameplay audit maximum 217,517 triangles, 36 materials; QuestPerfTest 217,575 triangles, 37 materials. The draw budget overshoot
is still +11/+12; repeated material/submesh slots including faces and skinned shadows are listed in the BOB-022 audit.

## BOB-023 regression checklist

The names below are methods in `Assets/Tests/PlayMode/RegressionTests.cs`. All rows are still **awaiting the candidate run**;
baseline results that do not mention a failure do not replace the test XML on the candidate.

| Test | Coverage | Baseline b46ff60 |
|---|---|---|
| Menu_bot_setup_all_onboarding_pages_and_gameplay_with_screens | Menu, Bot setup, 6 onboarding pages, Start, back to menu | Not listed as failed |
| Eight_items_follow_game_engine_rules | All eight items, inventory and results match Core | FAILED: Expected False, was True (Gloves/Fork) |
| Three_candy_colors_and_clover_damage_cap_follow_rules | Three good/bad colors, Clover, damage cap 3, paralysis | Not listed as failed |
| New_round_refills_dispenser_and_deals_items | Round/refill, candy and item counts, waiter | Not listed as failed |
| Ko_spectator_results_music_sfx_and_telemetry_csv_with_screens | KO, spectator, winner, music/SFX/volume, CSV | FAILED: menu music null |
| Five_restarts_keep_objects_materials_audio_sources_and_clips_stable | Five restarts, objects/materials/AudioSources/clips stable | Not listed as failed |
| Focus_lost_and_regained_cycles_freeze_timer_and_restore_input | Focus/pause, timer frozen, input resumes | Not listed as failed |
| Controller_path_works_and_pinch_never_doubles_input | Controller path and pinch without double actions | Not listed as failed |

The Gloves/Fork expectation fix and music playback fix are already in the source after the baseline; a rerun result is not available yet.
BOB-025 does not close the regression task or any other task without new evidence.

Required screenshots, from the player seat camera in the same run: `01-menu.png`, `02-bot-setup.png`,
`03-onboarding.png`, `04-gameplay.png`, `05-spectator.png`, `06-results.png`, in
[Docs/Autopilot/screens/](../Autopilot/screens/). The four available pointers cannot yet be visually inspected on the VPS;
no visual approval is claimed. Do not replace pointers or create synthetic screenshots as runtime evidence.

## BOB-041 online checklist and blockers

None of the following tests has evidence in the baseline; they must be **Passed** in the candidate XML:

- `OnlineMatchTests.Host_and_client_play_with_bots_until_winner_with_matching_snapshots`: local NGO, host+client+bots,
  snapshot revision/HP/inventory/winner match.
- `OnlineMatchTests.Client_disconnect_mid_game_seat_played_by_bot_until_winner`: client disconnects, a bot continues the game.
- `OnlineServicesTests.Create_room_with_relay_find_by_code_and_leave`: authentication, real Sessions/Lobby/Relay,
  room code and cleanup; requires internet. Assert.Ignore still means not validated for the RC.
- `OnlineServicesTests.Voice_joins_room_channel_and_leaves_with_room`: Vivox channel, Settings mute and cleanup.
- All three `OnlineSceneTests`: real online scene objects, Create/Join Room buttons active, in-scene table spawn and the
  scene synchronization path. BOB-046 has already added the wiring in the source; serialization/runtime validation is still awaiting Unity.

## Known issues and known gaps

- Blockers still needing evidence: the two baseline regression failures after the fixes, the draw audit exceeding the target,
  idle GC not yet near zero, and the new online/scene suites that have not been run. Do not claim the old CI is already green.
- Import and `GameplayPerformanceBuilder.PrepareScenes` can change scenes on the laptop. The build report marks `-dirty`;
  check `workspace.diff` and `workspace-status.txt` (including new assets not included in the patch) so that prepare results are not
  mistaken for a clean checkout that is only labeled with the source hash.
- Never tested on a physical Quest: APK install/launch, actual Android rendering, comfort/stereo, microphone permission,
  sensor tracking, performance/thermals/battery and device stability are unproven. Simulator/Editor is not device evidence.
- Hand tracking is still limited to the automated path (pinch mapping, input priority, duplicate prevention). Aim, occlusion,
  device tracking loss/recovery, hand/controller switching and near-face tolerance have not been proven on Quest.
- Voice is limited to joining/leaving the channel and mute; players hearing each other, device microphone/speaker, echo,
  latency and audio quality are unproven. Music `isPlaying` is only proven if the CI Editor has audio output.
- Online host+client uses a local transport with a client without the full scene. A second player over Relay with a different UGS
  identity, pose/candy/item visuals on the client, two processes with the real scene and a real internet network are not fully proven.
- The APK is built with the already configured debug keystore (BOB-024); Release mode is not a production-signed store package.
  No claim of store distribution readiness.

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

To designate the RC: record the same full manifest hash in this document; make sure Release Succeeded, exit 0, all
EditMode/PlayMode Passed (including online), audit PASSED, perf status complete and six screenshots/CSV available.
Check the `missing` list, the SHA-256 of the APK/evidence and the prepare result diff. Update the checklist with the real results, keep
known gaps without a device, and only then mark BOB-025 done. Until that evidence is available, the status remains needs_unity_check.
