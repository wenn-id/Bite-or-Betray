# Bite or Betray agent log

Every task the agents work on at the VPS is recorded here (newest at the bottom). Full status is in
[tasks.json](tasks.json).

- 2026-10-16 · setup · Agent loop installed on the VPS (`devops`, cron). Automatic check: compile all game
  assemblies without Unity + 42 Core rule tests, all passed at `4a08b0d`. BOB-005 marked `done`.

- 2026-10-16 · BOB-026 · codex · Connect Meta Quest quality and the GraphicsSettings default to Mobile_RPAsset.
  Static GUID/fileID check against repo assets/meta passed for the quality pipeline, renderer, volume and global settings;
  the Quest profile uses an available quality name. URP parameters, builder and LFS assets unchanged.
  Tests: `~/bob/bin/check.sh` → CHECK PASSED; all assemblies compiled and 42 Core tests passed.
  BOB-026 done for the static scope; BOB-001 still waits for Unity: import packages, activate the Quest profile/Android target,
  run Budget Audit and save the pipeline log; open CandyParlour3D, attach a screenshot without pink materials.
  Built-in package GUIDs not yet verified on the VPS; Unity render/build and Quest 3 testing not done yet.
- 2026-10-16 · BOB-008 · claude · Turn timer and VR input frozen when focus is lost. `VRTableInput.HasFocus`
  uses OnApplicationFocus, OnApplicationPause and `OVRManager.InputFocusLost/Acquired`; `CandyTableDirector`
  only decrements `turnTimer` while focused. While focus is lost the pointer/tooltip is hidden, held candy
  returns to its place, and buttons must be pressed again after focus returns. Tested: `check.sh` passed (compile +
  42 Core tests). Owner: run EditMode tests in Unity and save the log; on Quest open the system menu during a turn,
  make sure the Time number stops and resumes from the same number (BOB-015). Status `needs_unity_check`.

- 2026-10-16 · BOB-027 · codex · Make CandyParlour3D the active scene at global index 0; path/GUID match the .meta.
  The Meta Quest profile still inherits the global list; global SDK aligned to minimum 32/target 34, Vulkan only, stereo path 2.
  ARM64 and IL2CPP already matched; the Quest profile does not need changes.
  Tests: static scene/GUID/inheritance/Android settings check and `git diff --check` passed;
  `~/bob/bin/check.sh` → CHECK PASSED (all assemblies + 42 Core tests). Status `needs_unity_check`.
  Owner/BOB-002: activate the Meta Quest/Android profile in Unity; check the effective Scene List (CandyParlour3D enabled index 0),
  SDK 32/34, Vulkan, ARM64, IL2CPP and stereo path on the profile and global Android; save a batchmode screenshot/log.
  Unity Editor validation is still required; build and initial scene launch on Quest 3 not tested yet.

- 2026-10-16 · BOB-013 · claude · `VRTableInput`: button release is still processed when tracking is lost, so candy
  returns to the table and does not get stuck 'held'; held state is also cleared even if tracking is lost while the director takes the candy.
  One action per frame (right processed first); the losing hand vibrates briefly and is logged.
  Anchors from `OVRCameraRig` (max 1x/second, max 30 times); `FindObjectsByType<Transform>` removed.
  Tests: `~/bob/bin/check.sh` → CHECK PASSED (compile + 42 Core tests). Status `needs_unity_check`.
  Owner/BOB-015: compile in Unity; on Quest hold a candy, lose controller tracking then release the button → candy
  returns; hold the button → candy follows the hand again after tracking recovers; trigger both hands at once → only the right action.
- 2026-10-16 · BOB-009 · codex · Cache board values and seat labels; format/assign text only when changed.
  GUIStyle reused; log text and desktop panel header cached; controller status formatted only when changed.
  showLog/logInput default off in code and scene; release forces the flags off and excludes diagnostic logs.
  Tests: `~/bob/bin/check.sh` → CHECK PASSED (all assemblies + 42 Core tests); release branch compilekit and diff check passed.
  Status `needs_unity_check`: Unity not available; owner runs compile/EditMode in Unity and saves the results.
  Profile CandyParlour3D after warm-up while waiting for input (debug flags off); attach a screenshot of GC Alloc
  near 0 KB/frame and the remaining allocation call stacks. Check round/candy, timer, HP/item/status/turn/KO/restart.
  Test debug in Editor/Development and a release build on Quest 3: overlay/input log stays off even if serialized true.

- 2026-10-16 · BOB-014 · claude · `CandyTableDirector`: `SeatOf()` maps via `PlayerState.ConfigIndex` (new in Core:
  SeatConfig index before the d20 sort), no longer by name; duplicate names are reported with `LogWarning` during Setup and numbered.
  Turn timer is reset after `TimeoutTurn()`, so ForcedEat + good candy does not time out repeatedly.
  Rejected actions only show an error popup, without 'Aman! Lanjut…' (Safe! Keep going…).
  Tests: EditMode `Start_ConfigIndexPointsBackToSeatConfig`; `~/bob/bin/check.sh` → CHECK PASSED (compile + 43 Core tests). Status `needs_unity_check`.
  Owner: in Play Mode, (1) two identical botNames → warning + different labels; (2) TimeoutAction=ForcedEat, short timer, runs out on
  good candy → timer full again; (3) rejected action → no 'Aman! Lanjut' (Safe! Keep going). Save the Console log as evidence.

- 2026-10-16 · BOB-010 · codex · VR/mouse pointer uses a shared RaycastNonAlloc helper in TableSelectable.
  The nearest active/interactive target is chosen before hover; decoration/non-interactive bot colliders are skipped during Grappling.
  Candy in hand is ignored; when feeding, the bot filter is applied before finding the nearest target.
  The 64-hit buffer is reused; if full, capacity is increased and the query repeated so the nearest result is not cut off.
  Tests: `~/bob/bin/check.sh` → CHECK PASSED (all assemblies + 43 Core tests); `git diff --check` passed.
  Status `needs_unity_check`: Unity not available; owner compiles then Plays CandyParlour3D with mouse/VR simulation,
  activates Grappling and picks an opponent item inside the bot collider bounds; save a screenshot/log of the tooltip and steal action.
  Check nearest target, decoration in front of the target, and feeding past items; profile GC Alloc after warm-up
  plus >64 hits (buffer grows once, no repeated allocations). Controller/Quest 3 validation stays in BOB-015.

- 2026-10-16 · BOB-012 · claude · `CandyTableDirector`: a "Play again" table button (TableSelectable via `MakeButton`) appears on
  the end screen to the right of the player's mat and calls `Restart()`; hidden on restart/new game, the mouse can point at it too.
  End text: VR "point at Play again + trigger", desktop "click Play again (or R)". `Restart()` uses `SetMode(None)` so
  the Discard/Cancel buttons and Little Pocket options turn off on a mid-turn restart. Editor/Development logs the child object count,
  TableSelectables and items at the start of each game. Tests: `~/bob/bin/check.sh` → CHECK PASSED (compile + 43 Core tests). Status `needs_unity_check`.
  Owner: compile in Unity; Play CandyParlour3D, restart ≥5 times via Play again/R (also mid-turn/Grappling), make sure the log
  "Game #N: … objek anak …" (child objects) is the same each game and the Hierarchy does not grow; save the Console log. Controller on Quest: BOB-015.

- 2026-10-16 · BOB-017 · codex · CandyTableDirector uses the shared TelemetryRecorder; the debug CSV schema stays at 14 columns.
  Logs successful human/bot actions and TIMEOUT; GAME_OVER/duration written before the end animation, CSV in persistentDataPath/telemetry.
  Human decision/accumulated turn time only while input is active and focused; reset per turn/timeout, animations excluded.
  While waiting only numeric counters; format/I/O on actions, an I/O error disables the recorder so the game continues.
  Tests: `~/bob/bin/check.sh` → CHECK PASSED (all assemblies + 43 Core tests); `git diff --check` passed.
  Table3D → DebugTools asmdef reference and VPS compilekit synced; .NET smoke passed (14 columns, duration, calibration, TIMEOUT).
  Status `needs_unity_check`: Unity not available, no Play Mode CSV yet. Owner compile/EditMode, Play CandyParlour3D
  (recordTelemetry=true, loop=false); test eat/feed/item/Grappling/Pocket/discard/timeout/restart, match the CSV with debug,
  check time/reset and a single GAME_OVER; attach the CSV from the Console path. Profile enabled/disabled after warm-up while waiting
  (debug off): no extra GC Alloc/frame from telemetry; attach the capture. Quest CSV retrieval verified in BOB-023.

- 2026-10-16 · BOB-028 · claude · Offline room: new `RoomSetup` in Core (1-5 bots, balanced seat layout, per-bot/global level,
  `BuildSeats` → SeatConfig). `CandyTableDirector` opens the selection on the table before the first game: Fewer/More bots, All bots,
  each bot's level above its head, then Start (TableSelectable, so VR pointer + trigger and mouse). Unselected bots:
  character, feed target and label disabled; not part of the engine. Play again/R/loop use the last selection; the Bot setup button on
  the end screen opens the selection again (`OpenRoomSetup`, hook for BOB-032). Tests: `~/bob/bin/check.sh` → CHECK PASSED (52 tests,
  9 new in RoomSetupTests). Status `needs_unity_check`. Owner: Play CandyParlour3D (mouse) then Quest (controller): pick 1..5 bots
  and level, Start; empty seats without character/label/actions, turn caption shows the chosen level; Bot setup/Play again
  from the end screen; check panel readability from the seat in the headset.

- 2026-10-16 · BOB-029 · codex · A losing player stays in the seat; the action guard rejects input from a lost human/not their turn.
  VR pointer, candy grip and spectator keyboard shortcuts locked until the game ends; XR camera/tracking stays active.
  Local avatar and bots transparent at alpha 30% via runtime URP material copies; materials/shadows restored on a new game,
  copies reused then cleaned up when the object is destroyed. Scene/prefab/material assets and LFS unchanged.
  Tests: `~/bob/bin/check.sh` → CHECK PASSED (all assemblies + 52 Core tests); `git diff --check` passed.
  Status `needs_unity_check`. Owner: Play with >=2 bots, loop=false; make the human lose early, try take/eat/feed,
  item/B-Y, mouse/X/Esc/R: no actions; camera still sees the table, tracking free, bots continue until a winner.
  View the human avatar and lost bots from another camera: transparent, living players opaque; Play again/Bot setup restore
  materials/shadows/input, repeat several games without material growth. Repeat on a Quest 3 build (shader/stereo),
  save screenshot/log. Online not available yet; client sync belongs to BOB-034/036.

- 2026-10-16 · BOB-030 · claude · Diegetic results screen: new `FinalStandings` in Core (winner then elimination order, eliminations in the same
  turn share a rank). `CandyTableDirector` shows a "Results" card on the table in front of the player seat (bots only: center
  of the table) with rank + elimination turn, the waiter announces the winner. End screen buttons: Play again (last room selection) and
  Back to menu (replaces Bot setup). BOB-032 hook: event `MenuRequested` is invoked by `ReturnToMenu` after the table is cleared;
  without listeners it opens Bot setup. Auto loop unchanged. Tests: `~/bob/bin/check.sh` → CHECK PASSED (55 tests, 3 new in
  FinalStandingsTests). Status `needs_unity_check`. Owner: Play CandyParlour3D (loop=false, 3+ bots) to the end; check the order on
  the card matches the log, Play again keeps bots/levels, Back to menu opens Bot setup, repeat without objects growing. On Quest 3
  check the card is readable from the seat, not covered by popups/buttons, and the buttons can be pointed at + triggered.

- 2026-10-16 · BOB-032 · claude · Diegetic main menu on the table in front of the player: Play vs Bots (to Bot setup BOB-028), Create Room,
  Join Room, Settings. The app starts at the menu; Bot setup and the end screen have Back to menu (MenuRequested still invoked).
  Online via `IRoomLobby` (director.Lobby or a scene component); without a lobby the buttons are greyed out, lobby exceptions become a message on the table.
  Settings: new `GameSettings` in Core (volume, dominant hand, mouth snap) saved to PlayerPrefs, applied to
  AudioListener.volume and VRTableInput (mouthDistance, leftHanded = dominant hand takes priority). Tests: `~/bob/bin/check.sh` →
  CHECK PASSED (60 tests, 5 new in GameSettingsTests). Status `needs_unity_check`. Owner: Play CandyParlour3D (mouse): starts at the menu,
  Create/Join greyed out, Play vs Bots → Bot setup → Start, Back to menu from Bot setup/end screen; change Settings, Play again →
  values kept. On Quest 3: menu readable and can be pointed at + triggered, High snap makes eating easier, dominant left hand wins
  when both triggers are pressed together, close/reopen the app → Settings kept. Volume only audible once BOB-018/033 audio exists.

- 2026-10-16 · BOB-016 · claude · `RuleConfig.Validate()` rejects invalid HP, item capacity, candy limits, bad ratio, timer and
  TimeoutAction with a single message containing the field name + value; `GameEngine.Start` calls it and throws
  ArgumentException before dealing candy. 13 new tests in RulesTests: validation, Pocket→Grappling, Gloves after Fork,
  ForcedEat timeout. Gloves rules unchanged; a question for the owner (a candy already pierced by Fork then swapped by Gloves
  becomes hidden again, keep it that way or keep it revealed?) recorded in the tasks.json notes. Tests: `~/bob/bin/check.sh` → CHECK PASSED
  (all assemblies + 73 tests). Status `needs_unity_check`. Owner: Test Runner > EditMode > Run All in Unity, all pass
  (including the 43 old RulesTests), save the XML results file; answer the Gloves design question.

- 2026-10-16 · BOB-019 · claude · Short onboarding before the first game: content in Core `OnboardingGuide` (How to play, Grabbing
  candy, Eating, Give, Item, Item list) uses RuleConfig numbers, CandyRules effects and ItemInfo descriptions; VR/desktop control text
  differs. `CandyTableDirector` (onboarding=true) shows it after Start as a WorldText card in front of the player with
  TableSelectable buttons Next/Start playing and Skip; B/Y, Esc, R also skip. Once per session, no PlayerPrefs. Tests:
  `~/bob/bin/check.sh` → CHECK PASSED (79 tests, 6 new in OnboardingGuideTests). Status `needs_unity_check`. Owner: Play
  CandyParlour3D (mouse): Play vs Bots → Start → 6 pages → Start playing starts the game; Play again, Skip/Esc midway → game starts;
  Play again does not show onboarding again; the card does not cover buttons. Approve the text. On Quest 3 (BOB-023): readable from
  the seat, trigger on Next, B/Y skips.

- 2026-10-16 · Unity QA (Codex on the owner's laptop) · EditMode 43/43 passed; Meta Quest profile + Scene List, Quest URP,
  BOB-010/012/014 proven in Unity → `done` (also BOB-001/002/006/027). Bug: clicking "Play again" was blocked by the rect of the hidden
  desktop panel → fixed in `1830aaa`. QA APK build Succeeded, not installed yet (ADB did not detect the Quest).
  New findings: Vulkan URP Lit shader error → BOB-042 (codex); Meta XR required fixes for camera/audio → BOB-043 (claude).
  BOB-008/013 still need headset testing.

- 2026-10-16 · BOB-004 · claude · Build script in `AvatarValidation.cs`: menu `Bite or Betray/Build Quest APK/Development|Release`
  and batch `-executeMethod BiteOrBetray.EditorTools.AvatarValidation.BatchBuild -bobBuildMode Release|Development` (exit 1 on failure).
  Scene stays CandyParlour3D; APK `Builds/BiteOrBetray_Quest_<mode>.apk`. No longer writes m_BrgStripping/SaveAssets; GraphicsSettings
  and Meta Quest.asset (plus the active profile) are copied before the build and restored if changed. Permanent report
  `Builds/reports/BiteOrBetray_Quest_<mode>_<time>_<commit>.txt`: result, duration, APK size, steps, errors, largest assets, shader
  variants before→after script stripping. Tests: `~/bob/bin/check.sh` → CHECK PASSED (compile + 79 tests). Status `needs_unity_check`.
  Owner: run the Development menu and batchmode Release (`-batchmode -quit -buildTarget Android`), check APK Succeeded, the report contents
  (shader section filled), then `git status` shows no changes to GraphicsSettings.asset/Meta Quest.asset.

- 2026-10-16 · BOB-042 · codex · Suspected URP 17.6 glslang compile bug: Lit/ForwardLit vertex, LOD_FADE_CROSSFADE + EVALUATE_SH_VERTEX; root cause not proven without Unity.
  Mobile_RPAsset: m_EnableLODCrossFade=0; the default and the Android Mobile/Meta Quest qualities use this asset, PC is excluded from Android; Strip Unused Variants already on.
  This setting strips cross-fade variants at build time according to the [Unity URP reference](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/universalrp-asset.html#quality).
  No LODGroup in text scenes/prefabs/code; LFS models could not be checked yet. Impact: if models use LOD, transitions have no fade and may look abrupt.
  Alternatives: SH Per Pixel avoids EVALUATE_SH_VERTEX, can smooth probe lighting but adds fragment cost; Android/Vulkan-specific stripping is narrower but requires keeping replacement variants.
  Chose a single asset change; Mobile_Renderer, SH Auto, URP package, graphics API, scenes/prefabs/materials and LFS pointers unchanged.
  Tests: ~/bob/bin/check.sh → CHECK PASSED (all assemblies + 79 Core tests); static pipeline/stripping reference check and git diff --check passed.
  Status needs_unity_check. Owner: Meta Quest/Android Vulkan profile, check LOD Cross Fade is off; clean Development + Release build, save logs without Vulkan 'Shader error' plus evidence of LOD_FADE_CROSSFADE stripping.
  Owner: run CandyParlour3D on Quest 3, make sure Lit has no pink and check LOD transitions of imported models as view distance changes; attach log/screenshot.

- 2026-10-16 · owner · Headset-free strategy: the owner does not have and cannot borrow a Quest. Device tasks
  (BOB-007/015/022/023/041) are checked on the laptop via Unity Editor, Meta XR Simulator and Multiplayer Play Mode.
  Hand tracking (BOB-031) becomes the last priority; the submission relies on controllers. New: BOB-044 conservative performance
  budget (codex), BOB-045 Editor gameplay recording tool for the Devpost video (claude).

- 2026-10-16 · BOB-011 · codex · Add BoB_VRPointer/BoB_TextCard assets (URP Unlit) with meta and serialized references in CandyParlour3D.
  Remove Shader.Find/MakeCardMaterial; cards use the shared asset, pointer one copy per hand with OnDestroy cleanup.
  ArtSceneBuilder fills the references when installing/rebuilding the director; empty references produce a diagnostic message.
  Tests: ~/bob/bin/check.sh → CHECK PASSED (all assemblies + 79 Core tests); unique GUIDs and the scene→material→shader chain plus git diff --check passed.
  Status needs_unity_check. Owner: Android target, check the material fields are filled; Play in Meta XR Simulator/Quest, two-hand pointers and cards without pink, independent hover.
  Reload the scene repeatedly: BoB VR Pointer materials do not grow (two when XR is active), assets unchanged; Development/Release build and save logs plus BOB-015 visual evidence.
  Without a headset, follow the owner's Editor/Simulator evidence strategy; on-device build appearance not yet proven. Does not change Core or LFS pointers.

- 2026-10-16 · BOB-034 · codex · Host-authoritative architecture: TableAuthority wraps GameEngine; local/online/bot seats bound to ConfigIndex after d20.
  NetworkTableSession in Table3D/Net uses NGO RPCs for actions and reliable 5 Hz public snapshots; validates sender, owner, turn, revision and a single input queue.
  The host director runs rules/bots/timeouts; clients only display snapshots, send choices and reject local restart; offline stays without an active NetworkManager.
  Pure Core binary codec masks hidden candy contents; snapshots include inventory/pocket/status/results, candy IDs and GameId for restart.
  Tests: ~/bob/bin/check.sh → CHECK PASSED (all assemblies + 94 Core tests; 15 new: round-trip, redaction, authorization/replay, timeout/restart and corrupt packets).
  Status needs_unity_check. Owner: temporary copy of CandyParlour3D + NetworkManager/UnityTransport + scene object NetworkObject/NetworkTableSession; host+client via Multiplayer Play Mode.
  StartHost/StartClient, then the host session context menu “Start table with connected clients (local test)”; check RPC/ILPP, actions/items/HP/timer/KO/results on both machines, late join, host restart and client R.
  Shutdown/despawn → Play vs Bots must recover; run Unity EditMode and an Android IL2CPP/Meta XR Simulator build. Details in the task notes; remote pose/animation, lobby/Relay and voice remain later tasks.
  Does not change packages, scenes/prefabs/materials/ProjectSettings or LFS pointers; only adds NGO references to the existing runtime asmdef.

- 2026-10-16 · BOB-035 · claude · Lobby Sessions (Lobby + Relay): SessionRoomLobby in Table3D/Net implements IRoomLobby; Create Room creates a session + NGO host with a 6-digit code.
  Join Room: diegetic number keypad on the table (Delete/Join/Cancel; desktop can type), the code is looked up via a public session property then join by id.
  The host sees a large code + player count; Start is enabled with at least 2 humans, empty seats are filled with bots (OnlineRoom.BuildSeats) then StartTable.
  Login/network/wrong code/room closed errors are shown as a message on the table, back to keypad/menu. Director: lobby panel API + public SetLobbyButton.
  Editor: menu Bite or Betray > Install Online in Scene (NetworkManager+UnityTransport+lobby, NetworkObject+NetworkTableSession). Asmdef gets services/NGO references.
  Tests: ~/bob/bin/check.sh → CHECK PASSED (all assemblies + 103 Core tests; 9 new OnlineRoomTests: code, keypad, start conditions, bot fill).
  Status needs_unity_check. Owner: link UGS (BOB-040), run Install Online in Scene in CandyParlour3D then save; Multiplayer Play Mode 2 players:
  code readable on the table (desktop + Meta XR Simulator), client joins via keypad, Start → bots fill seats, wrong code/offline → message without a crash.
  Also check host Leave room (client to menu with a message), Back to menu then Play vs Bots stays offline, and the client scene is not reloaded during NGO sync.

- 2026-10-16 · BOB-036 · codex · Unreliable NGO pose at most 25 Hz: 51 quantized bytes, 74-byte packet including session/revision/interaction.
  The host validates sender/seat, pose/sequence, turn/revision and candy distance; remotes use RemoteAvatarRepresentation with the existing interpolation/timeout.
  Online human rigs are copied at runtime from the local avatar; tracking on the copy disabled, head/fingers restored, original bots return on stop. Lost focus sends tracking=false.
  Host visual state at 25 Hz syncs candy in hand/animations and active items (including Grapple), using IDs and seat mapping; snapshots still control inventory/HP.
  Networking code in Table3D/Net; no new asmdef/package/scene/prefab/material/ProjectSettings/LFS or Core changes.
  Tests: ~/bob/bin/check.sh → CHECK PASSED (all assemblies + 103 Core tests); git diff --check. The Check Online Avatar Codec menu is also called by Check Floating Avatar.
  Status needs_unity_check: Editor codec check not run yet; owner checks both menus, NGO ILPP, host + 1-2 clients in Multiplayer Play Mode/Meta XR Simulator.
  Check head/hands/grip, take/release/eat/feed, all items/Pocket/Grapple, loss/latency/focus/timeout/spectator/late join, restart and return to offline without duplicate objects.
  Measure pose <=25 Hz (51-byte payload), Android IL2CPP build; Quest visual/performance evidence still needs the owner, or Editor/Simulator per the headset-free strategy.

- 2026-10-16 · BOB-044 · codex · Conservative Quest budget: Mobile/Meta Quest render scale 1.0, MSAA 4x; main shadow 512/6 m/1 cascade + hard shadow, additional PerVertex without shadows.
  Rationale: scale 1.6 costs about 2.56x pixels; a 512 atlas and one cascade/short distance reduce shadow cost; PerVertex avoids extra per-pixel lighting.
  Mobile_Renderer already has no SSAO/features; HDR/depth/opaque/soft shadow stay off, SRP Batcher stays on. Global Android static batching + Meta Quest Build Profile override on, Room already BatchingStatic; HeroQ + builder instancing on.
  VRTableInput on Android waits for OVR to be ready: fixed FFR High + dynamic, eye-tracked off; rig dynamic resolution (Quest 3 can do 1.6) disabled, XR scale 1.0; edge detail may be coarser.
  Audit of CandyParlour3D/QuestPerfTest via preview scene, 24 views: <=250k triangles, <=100 draws/eye including all shadow casters, <=50 materials, estimated SetPass <=100; does not discount batching/LOD/occlusion.
  Report Builds/reports/QuestBudgetAudit.txt + Temp/QuestBudgetAudit.txt still written on FAIL; BatchAudit/CIRunner exit 1. Draw/SetPass are static estimates, not GPU numbers or proof of 72 FPS.
  Tests: ~/bob/bin/check.sh → CHECK PASSED (all assemblies + 103 Core tests), git diff --check; Unity not available, LFS pointers untouched. Status needs_unity_check.
  Owner: download/import LFS, Android/Meta Quest target, menu Quest Perf Test > 4. Budget Audit; save the result including FAIL (baseline 132+131 draws not yet proven to drop); check shadow/lighting visuals and Frame Debugger/Profiler during laptop/Simulator gameplay.
  Owner: check Room static batching and OVR runtime/XR scale settings when Android is available; the Simulator does not prove FFR/72 FPS. References: [OVRManager](https://developers.meta.com/vr/reference/unity/v69/class_o_v_r_manager/), [instancing/SRP priority](https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/reduce-draw-calls/gpuinstancing/gpuinstancing).

- 2026-10-16 · BOB-018 · codex · GameplayAudio in CandyParlour3D: take, eat good/bad, Give, item/Pocket/Grapple, actual damage, KO and win from the director.
  Four 2D AudioSources reused; eight mono 24 kHz clips created once, reset/menu stop and runtime cleanup; volume follows Settings.
  VR direct take + consumption deduplication; host animation cues forwarded reliably to clients, no replay from snapshots/late join.
  Source of ALL Pickup/EatGood/EatBad/Give/UseItem/Damage/Elimination/Win clips: original MakePlaceholder sine waves (GameplayAudio.cs), created by codex on this date; no third-party recordings/samples.
  Third-party asset licence: not needed since none are used; placeholders are not final audio. Owner approval for use/distribution NOT given yet, must be approved or the clips replaced with an approved source/licence.
  Replace the eight GameplayAudio AudioClip fields in the Inspector before Play; null uses the placeholder. Does not change Core/packages/ProjectSettings or LFS pointers.
  Tests: ~/bob/bin/check.sh → CHECK PASSED (all assemblies + 103 Core tests); git diff --check passed. Unity not available; status needs_unity_check.
  Owner: compile/Play offline + host/client Multiplayer Play Mode/Meta XR Simulator; listen to grab/drop/regrab, Eat good/bad, Give, all items/Pocket/Grapple, cupcake without damage, HP/KO/win and timeout.
  Owner: check volume 0/100, restart/menu/late join without duplicate cues, still 4 AudioSources/stable clips and cleanup on exiting Play; save evidence plus source/licence approval. Device audio in BOB-023; the Simulator does not prove the headset yet.

- 2026-10-16 · BOB-020 · codex · RefreshProps reconciles seat/index/type slots; objects/callbacks kept, only changed/missing slots are discarded and colliders disabled immediately.
  Little Pocket uses the same sync; CPU marker BoB.RefreshProps to compare repeated calls and other participants' instance IDs.
  The QuestPerfTest generator copies CandyParlour3D characters/components/rigs/rules; bypasses menu/setup/onboarding, no hidden bots or extra Hero_Quest/importer/material/replacement controller.
  The APK build always regenerates the scene; the monitor only changes render scale A/B/C, gameplay characters/animations keep running and scale is restored when the monitor is disabled.
  Tests: ~/bob/bin/check.sh → CHECK PASSED (all assemblies + 103 Core tests); git diff --check. Core and LFS pointers unchanged.
  Status needs_unity_check: Unity not available; the saved QuestPerfTest.unity NOT yet regenerated, new audit/profiler numbers not available yet.
  Owner: import LFS, compile/EditMode tests; menu Quest Perf Test > 1. Build Scene, save, match characters/components with gameplay without PerfHero_*; Play/human input or timeout, check A/B/C animations.
  Owner: profiler before (parent commit)/after with the same seed/actions: Eat/Give without new props, UseItem only the actor, Grapple actor+target; check round/KO/Pocket/restart/online snapshot and other participants' prop IDs.
  Owner: save CPU/GC captures, then menu 4. Budget Audit and save Builds/reports/QuestBudgetAudit.txt (gameplay triangles/draws/materials, including on FAIL); match with Frame Debugger. No Quest evidence yet.

- 2026-10-16 · BOB-033 · codex · GameplayAudio centralizes loop music, menu/waiter SFX and the Settings master volume.
  2D music starts after Settings load, uses one dedicated source + a pool of four SFX; restart/menu only stop SFX so the loop keeps playing.
  Menu/setup/lobby buttons give a local cue after the callback; the waiter sounds on every round announcement, forwarded reliably to clients with GameId validation.
  Source of the 8-second music + MenuSelect/Waiter: original MakeClip sine waves (codex, this date), no third-party samples/recordings/assets; placeholders, not final audio.
  Replace the backgroundMusic/menuSelect/waiter fields in the GameplayAudio Inspector before Play; disable/enable stops/starts the music, destroy cleans up generated clips.
  Tests: ~/bob/bin/check.sh → CHECK PASSED (all assemblies + 103 Core tests); git diff --check. Core/scene/packages/ProjectSettings/LFS pointers unchanged.
  Status needs_unity_check: Unity not available. Owner: offline + host/client Multiplayer Play Mode/Meta XR Simulator, listen through several loops and all buttons plus waiter cues without duplicates.
  Owner: menu > Settings > Bot setup > game > restart > menu without the music cutting out; volume 0/50/100 applied immediately and saved, including while a cue plays; check clip replacement.
  Owner: still five AudioSources/stable clips after restart, cleanup on exiting Play; check music/SFX balance on Quest, since the Simulator does not prove headset audio yet.

- 2026-10-16 · BOB-031 · codex · VRTableInput: Meta XR index pinch/aim via OVRPlugin (OpenXR backend), selects dispenser/buttons, holds candy, eats and feeds through the existing paths; [Meta reference](https://developers.meta.com/vr/documentation/unity/unity-handtracking-interactions/).
  One source per hand; tracked controllers take priority. Switching hand source/tracking or losing confidence/system gesture: candy returns without feeding; must release before a new action. Haptics only on controllers.
  An invalid aim hides the pointer, a valid hand pose can still eat; mouth snap uses mouthDistance from Settings Low/Normal/High = 10/15/22 cm; enableHandTracking=false for controllers only.
  Tests: ~/bob/bin/check.sh → CHECK PASSED (all assemblies + 103 Core tests); git diff --check. No Core/package/scene/prefab/material/ProjectSettings/LFS changes.
  Status needs_unity_check: Unity not available; the owner does not have and cannot borrow a Quest. The submission still relies on controllers; Editor/Meta XR Simulator do not prove pinch/aim/device switching.
  If a Quest 3 is available: make sure Meta XR Hands and Controllers + OpenXR features/hand tracking manifest are on; build Android, enable system hand tracking and logInput in a Development Build; check left/right pointers follow aim.
  Quest: put down the controllers, release the initial pinch, pinch the dispenser once → one action; pinch+hold candy → follows the hand; bring it to the mouth → eats once; point at another player/bot + release → one feed.
  Quest: repeat with left/right hand and Settings snap Low/Normal/High; test both hands at once per dominant hand, system menu, tracking/confidence lost while holding, recovering while still pinching, and an invalid pointer near the face without accidental actions/feeds.
  Quest: switch hand ↔ controller while idle and while grip/pinch/trigger is held; candy returns, must release/press again, no duplicate actions. Repeat controller trigger/grip/B-Y, eat/feed, focus/spectator and enableHandTracking=false; save log/video.

- 2026-10-16 · BOB-038 · codex · Vivox AudioOnly 2D per UGS session ID; the lobby binds join/leave, KO/spectators still hear and speak according to the player's mute.
  RoomVoiceChat queues operations with generations; leave/disconnect/destroy immediately mutes input/output then logs out, including old joins that finish late; voice failures are shown without stopping the game.
  Voice Settings: listen volume 0-100% and microphone mute separate from music/SFX, saved to PlayerPrefs; the panel stays available in the room and the spectator pointer may only select voice controls.
  Android requests microphone permission plus Bluetooth Connect on API 31+ before joining; uses the existing Vivox package and the Table3D asmdef reference, no scene/prefab/material/ProjectSettings/LFS changes.
  Tests: ~/bob/bin/check.sh → CHECK PASSED (all assemblies + 111 Core tests; 8 new voice cases and extended defaults/persistence); git diff --check. Unity not available; status needs_unity_check.
  Owner: enable UGS/Vivox (BOB-040), two host/client identities must hear each other in lobby/game/KO/restart, different rooms isolated; test mute/unmute, volume 0/50/100, relaunch and the panel via mouse/controller/pinch as spectator without gameplay actions.
  Owner: check panel position/readability in VR; leave/disconnect/host closes the room while permission/join is pending then rejoin must be clean without old room audio; service/permission failures do not stop offline/online games.
  Owner: Quest 3 IL2CPP build, check the merged manifest and microphone/Bluetooth dialogs accept/deny, rejoin after permission is granted, headset input/output; save audio/log evidence, Simulator/compilekit do not prove device audio yet.
  References: [Vivox voice API](https://docs.unity.com/en-us/vivox-unity/developer-guide/vivox-unity-sdk-basics/sign-in-with-authentication-package), [Android permissions](https://docs.unity.com/en-us/vivox-unity/developer-guide/android/request-runtime-permissions).

## 2026-10-17 · Headset-free QA · claude
- Tasks that need a Quest (BOB-007/015/022/023/025/041) rewritten as automated tests in the Editor; the owner does not have a headset.
- New: asmdef `BiteOrBetray.Tests.PlayMode` + `SceneSmokeTests` (pass in Unity on the laptop). check.sh compiles the PlayMode tests;
  the laptop runner runs PlayMode after EditMode and writes the results to CI.md, as well as `Logs/ci/perf.txt` and
  screenshots `Logs/ci/screens/*.png` (copied to `Docs/Autopilot/screens/`).
- Closed with CI evidence: BOB-004, 007, 011, 016, 034, 042, 044. Remaining needs_unity_check items are linked to PlayMode tests
  in BOB-015/022/023/041, which will close them once CI PASSES.

- 2026-10-17 · BOB-015 · codex · 7 PlayMode integration tests of the CandyParlour3D scene without keyboard/headset in OfflineMatchTests.
  Full_match_via_table_actions_items_grapple_results_replay_and_menu: menu → bots → Skip → take/eat/Give → 8 items (Pocket/Grappling) → bot/winner/results → 5 restarts → results/menu.
  Bot_counts_one_to_five_start_with_selected_seats_and_difficulties: every bot count starts, empty seats and levels match the configuration.
  Lost_application_focus_freezes_turn_timer_and_returns_held_candy / Application_pause_freezes_turn_timer_and_returns_held_candy: timer/input/focus recover.
  Tracking_loss_with_trigger_held_recovers_without_accidental_give / Tracking_loss_with_grip_held_recovers_without_accidental_give: back to the tray, then take/eat again.
  Eliminated_human_is_locked_transparent_spectator_until_winner_and_replay: input/callback guard, camera, alpha/queue/shadow of human/bots, material restore.
  Minimal hook: VRTableInput separates device polling from private processing; the fixture supplies samples/dispenser contents/inventory, every decision goes through the game path.
  Tests: ~/bob/bin/check.sh → CHECK PASSED (compile all assemblies/PlayMode + 111 Core tests); git diff --check passed. No package/asmdef/scene/prefab/material/LFS changes.
  Status needs_unity_check: the laptop runner must run the 7 tests with -testPlatform PlayMode and store the results in CI.md; Unity is not available on the VPS, no Quest test requested.
  Other tasks not changed yet because the new CI suite does not exist yet; BOB-015 notes map candidate evidence and the two-hand/anchor gaps, director robustness, onboarding pages, lobby/Settings.

- 2026-10-17 · BOB-022 · codex · Unity builder merges furniture per material/lightmap, turns off shadows for rigid/decor meshes, keeps skinned/collider/UV and prefab overrides.
  PlayMode IPrebuildSetup + CIRunner prepare the gameplay/QuestPerfTest scenes; TextMesh audit fixed, top draws reported; the runner rejects green CI if the audit fails/Unity exits nonzero.
  The desktop panel uses a fixed GUI Rect and cached labels, removing GUILayout/LINQ.ToList per event while waiting for input.
  GameplayPerformanceTests: idle with telemetry on/off >=240 frames/>=3 seconds, GC via ProfilerRecorder + draw/SetPass via UnityStats + average/peak frame time to Logs/ci/perf.txt.
  Automated 5-bot match via table callbacks, other participants' props kept after PlasticFork/Eat, RefreshProps marker when available, CSV/GAME_OVER sample saved.
  Tests: ~/bob/bin/check.sh CHECK PASSED (all assemblies/PlayMode + 111 Core tests), 5 Python runner report scenarios, git diff --check; no LFS/package/asmdef/ProjectSettings/scene YAML changes.
  Status needs_unity_check: the laptop runner must produce a complete perf.txt and an audit of both scenes; Unity is not available on the VPS, no new numbers yet (old CI max 271/100 draws).
  Raw GC includes Editor/Test Runner; the <=1 KiB indicator does not fail the test. Green CI alone does not prove GC is near zero; no Quest performance claim.
  BOB-009/017/020 not closed yet: waiting for relevant GC/audit/CSV/profiler evidence; capture before RefreshProps and BOB-023 CSV collection are still recorded if not yet proven.

- 2026-10-17 · BOB-023 · claude · PlayMode regression Assets/Tests/PlayMode/RegressionTests.cs: 8 UnityTests, one per scenario (checklist in tasks.json notes).
  Menu/Bot setup/6 onboarding pages, 8 items matching GameEngine, 3 colors + Clover (damage cap 3, paralysis), new round/refill + items, KO/spectator/results.
  5 restarts (4x Restart, 1x via the menu) without GameObject/Material/AudioSource/AudioClip growth, 2x each focus lost/regained path, BOB-031 controller + pinch without double input.
  Audio: 5 AudioSources stay, 10 cues assigned, music loops and Settings volume; telemetry CSV checked then copied to Logs/ci/regression-telemetry.csv.
  Camera.Render screenshots to a RenderTexture from the player seat: Logs/ci/screens/01-menu..06-results.png (the runner copies them to Docs/Autopilot/screens/).
  Tests: ~/bob/bin/check.sh CHECK PASSED (compile all assemblies including PlayMode + 111 Core tests). Unity is not on the VPS: the suite has never run.
  Owner/CI: wait for the laptop runner's CI.md (PlayMode); approve the 6 screenshots in Docs/Autopilot/screens/. Music isPlaying is only checked if the CI Editor has audio output.
  After CI PASSES: BOB-011/017/018/019/031/033 to done per the BOB-023 notes; real pinch on Quest remains a known gap.

- 2026-10-17 · BOB-CI · claude · PlayMode tests failed in SetUp because Meta XR Simulator (the CI laptop's OpenXR runtime) turns on XR in Editor PlayMode.
  New: Assets/Tests/PlayMode/EditorXR.cs turns off the XR loader (StopSubsystems + DeinitializeLoader) before the scene loads in OfflineMatchTests,
  RegressionTests and GameplayPerformanceTests; PlayMode asmdef + Unity.XR.Management reference. Camera Rig stays inactive, so the
  '[OVRManager] OnDestroy' log is gone without relaxing LogAssert.NoUnexpectedReceived; Assert IsXRActive stays.
  Budget Audit: GameplayPerformanceBuilder.PrepareScenes no longer restores an empty SceneSetup (batchmode without a scene -> 'Invalid SceneManagerSetup').
  Tests: ~/bob/bin/check.sh CHECK PASSED (compile all assemblies + 111 Core tests). Unity is not on the VPS.
  Owner/CI: the next CI.md must PASS (PlayMode 17/17, Budget Audit PASSED); a failure in a test body = a bug in the BOB-015/022/023 suites, which are running for the first time.

- 2026-10-17 · BOB-CI · claude · CI 86c2bcb: PlayMode 8/17 (OfflineMatchTests now pass after turning off Editor XR).
  RegressionTests: NRE in SetUp because Until -> PollAudio used the audio field before it was set; GameplayAudio is now fetched right after the director.
  GameplayPerformanceTests: draw=0 because Editor -batchmode does not draw the Game view. In batchmode the main camera is rendered to a RenderTexture
  every measured frame; draw/SetPass = max(ProfilerRecorder Render Draw Calls/SetPass Calls Count, UnityStats). Assert draw>0 is not relaxed.
  Tests: ~/bob/bin/check.sh CHECK PASSED (compile all assemblies + 111 Core tests). Unity is not on the VPS.
  Owner/CI: next CI.md PlayMode 17/17; a failure in a RegressionTests body = a bug in the BOB-023 suite, which is getting past SetUp for the first time.
  Budget Audit still 'RESULT: FAIL static budget' (111-112 draws/eye, BOB-022's scope); the laptop runner has an old version installed: run install_unity_runner.ps1.

- 2026-10-17 · BOB-CI · claude · CI b46ff60 PlayMode 15/17, two RegressionTests fail in the test body (first time past SetUp).
  Eight_items: wrong test assert. GameEngine deliberately leaves the candy revealed by Fork open on Gloves (owner decision,
  RulesTests.GlovesAfterFork_ForkedCandyStaysRevealed); the test now checks that the old candy stays revealed and the new candy is hidden.
  Ko_spectator: null music clip in the menu = PlayMusic from Start rejected by a guard. GameplayAudio.PlayMusic now uses enabled +
  activeInHierarchy and only skips if the source is already playing that music clip; the music assert message writes the audio state.
  Tests: ~/bob/bin/check.sh CHECK PASSED (all assemblies + 111 Core tests). Unity is not on the VPS.
  Owner/CI: next CI.md PlayMode 17/17; if Ko_spectator still fails, its diagnostic message shows the cause.

- 2026-10-17 · BOB-043 · claude · Meta XR required fix for camera/AudioListener in CandyParlour3D.
  The scene is saved in VR state: Camera Rig active, CenterEyeAnchor the only MainCamera + AudioListener; Desktop Art Preview
  untagged, Camera/AudioListener off. ArtPreviewControls (execution order -32000) picks the camera in Awake: without XR the rig is turned off
  before OVR Awake and the preview becomes MainCamera; with XR the preview is turned off. VRTableInput uses ArtPreviewControls.UseCamera.
  ArtSceneBuilder.Build saves the same state. New PlayMode test: CameraSetupTests (desktop and VR paths).
  Tests: ~/bob/bin/check.sh CHECK PASSED (all assemblies + 111 Core tests). Unity is not on the VPS.
  Owner: open CandyParlour3D, Project Setup Tool without a camera/audio required fix (without Fix All); Play without a headset still gives the desktop
  preview; CI.md CameraSetupTests 2/2. Also check that the Render 3D Scene Preview menu still produces a PNG (the preview camera is now off in the scene).

- 2026-10-17 · BOB-037 · claude · Online disconnect: a disconnected client is replaced by a bot, the host leaving ends the clients' game.
  Host: NetworkTableSession listens to OnClientDisconnectCallback; that client's Online seat becomes a Bot (TableSeat/TableAuthority.ReplaceWithBot,
  PlayerState.IsBot, revision bumped), snapshot sent immediately; the director stops waiting for the action and the turn continues with the bot.
  That seat's remote avatar is turned off, the bot character returns + "Disconnected / replaced by a bot" popup on the host and other clients. Play again keeps the bot.
  Host leaves: clients return to the main menu with the message "Lost connection to the host" (the lobby message is not overwritten).
  Tests: ~/bob/bin/check.sh CHECK PASSED (all assemblies + 113 Core tests, 2 new EditMode tests). Unity is not on the VPS.
  Owner/CI: host+client runtime evidence follows in the BOB-041 PlayMode tests (client Shutdown -> game until a winner; host Shutdown -> client in menu + message).

- 2026-10-17 · BOB-041 · claude · Automated online tests without a headset (PlayMode, run by the laptop runner).
  OnlineMatchTests: host on the real scene + a second NGO client in the same process (UnityTransport 127.0.0.1), empty seats bots, play until
  a winner; every matching revision is compared (turn, candy, HP, status, inventory, winner); client disconnects -> bot until a winner.
  OnlineServicesTests: real Create Room -> anonymous sign-in, Lobby+Relay, 6-digit code, search by code, leave (session deleted),
  closed code -> message; Vivox join/leave of the room channel. No internet = Assert.Ignore. PlayMode asmdef + Services/Vivox.
  New bug: BOB-046 (the scene does not contain NetworkManager/table/lobby yet, online buttons always grayed out).
  Tests: ~/bob/bin/check.sh CHECK PASSED (compile all assemblies + 113 Core tests). Unity is not on the VPS.
  Owner/CI: CI.md must show the 4 Online* tests passed (not ignored); then set the status of BOB-035/036/038 per the BOB-041 notes.

- 2026-10-17 · BOB-046 · claude · CandyParlour3D.unity contains BoB_Network (NetworkManager + UnityTransport + SessionRoomLobby, scene management on)
  and BoB_TableSession (in-scene NetworkObject + NetworkTableSession, director = BoB_TableDirector). YAML written directly (Unity is not on the VPS):
  script GUIDs from the official NGO 2.13.3 package, GlobalObjectIdHash 2879140581 computed with NGO's XXHash32 from the scene GlobalObjectId, m_InScenePlaced 1.
  OnlineSceneSetup.Install(director) is used by the Install Online in Scene menu and ArtSceneBuilder.Build (online objects are reused on rebuild).
  Tests: new PlayMode OnlineSceneTests (real scene objects, Create/Join Room active from the Start menu, scene host spawns the in-scene table,
  NGO ClientShouldPassThrough = client does not reload the active scene); ~/bob/bin/check.sh CHECK PASSED (compile all + 113 Core tests).
  Owner/CI: wait for CI.md (OnlineSceneTests + OnlineMatchTests + OnlineServicesTests passed); open CandyParlour3D, make sure there is no Missing Script
  and the table hash stays 2879140581; Multiplayer Play Mode with 2 players: client Join Room without the scene reloading.

- 2026-10-17 · BOB-045 · claude · Devpost video recording tool from the Editor: GameplayRecorder, menu Bite or Betray > Record Video.
  1 click "Match from Player Seat": desktop Play of CandyParlour3D, all bots play one game, the camera at the player seat turns smoothly toward the bot
  whose turn it is, 1920x1080 JPG frames @30 fps (captureFramerate) to Recordings/<time>_<shot>/ until a winner, Play stops by itself,
  make_video.cmd/.sh + ffmpeg command (clipboard) turn it into an MP4. Presets: Main Menu, Reacting Bot Close-up, Top-down Table. Editor-only, no package.
  Tests: new PlayMode GameplayRecorderTests (match until a winner + 3 presets: frames/1080p JPG/ffmpeg script/camera position);
  ~/bob/bin/check.sh CHECK PASSED (compile all + 113 Core tests). Unity is not on the VPS.
  Owner: CI.md GameplayRecorderTests 4/4; click the Match menu, run make_video.cmd (ffmpeg on PATH), watch the MP4 (smooth movement, readable text).

- 2026-10-17 · BOB-024 · claude · GitHub CI draft, static scene checker, README.
  .github/workflows/unity-ci.yml.draft (game-ci): scene checker -> EditMode+PlayMode tests -> Budget Audit + Development APK (CIRunner.Run).
  Inactive: .draft extension + the Unity job needs variable BOB_UNITY_CI; license secrets (UNITY_LICENSE/SERIAL/EMAIL/PASSWORD) and runner in the file header. Never run yet.
  Tools/CI/check_scene.py: reads the CandyParlour3D YAML without Unity, reports GUIDs without .meta + required objects (BoB_Art3D, Seats, Seat_0/HeadAnchor, Bot_*, OVRCameraRig controller anchors).
  Assets/README.md: 113 EditMode + 30 PlayMode tests, VR status, build from menu/batch, laptop CI vs GitHub draft.
  Tests: check_scene.py on the real scene SCENE PASSED, a broken copy (Bot_Mint, LeftControllerAnchor replaced) SCENE FAILED; ~/bob/bin/check.sh CHECK PASSED.
  Owner: in the laptop project run python Tools/CI/check_scene.py --strict (needs Library/PackageCache) -> must be 0 missing GUIDs.
  Enable the GitHub workflow only if you agree to use the Unity license in Actions (steps in the file header).

- 2026-10-17 · BOB-025 · codex · Feature freeze and RC package preparation without a headset in Docs/Release/RC.md.
  Release request stored in build-mode.txt; BatchBuild/CIRunner/runner use the same mode, report full hash + APK SHA-256.
  The runner rejects skipped tests/failed exit/deferred build as PASSED; per-commit archive: XML, build, perf/CSV, audit, diff/status and screenshot hashes.
  RC contains the BOB-023/041 checklist, failing baseline evidence b46ff60, missing screenshots, known issues and online/voice/hand tracking gaps.
  Tests: ~/bob/bin/check.sh CHECK PASSED (compile all assemblies + 113/113 Core), 6 Python runner tests passed, git diff --check passed.
  Status needs_unity_check: no Release build/full PlayMode results on the candidate hash yet; no claim that CI/device passed.
  Laptop: reinstall install_unity_runner.ps1 then unity_runner.py --force; Release Succeeded required, all tests including online Passed, audit/perf/CSV/six screenshots on one hash.
  Check the prepare manifest/diff/status output, fill in the passing candidate hash in RC.md; physical Quest, pinch sensor and voice between players remain known gaps.

- 2026-10-17 · BOB-047 · codex · QA of CI bc71d57: evaluated 26 tasks, 13 done and 13 stay needs_unity_check; evidence/test names/known gaps in notes + CI.md.
  Two OnlineMatchTests and the Vivox test fail: related tasks stay open; idle GC 6661.97 B/frame is not considered near zero.
  PrepareScenes keeps the SDK visuals replacing the local avatar off (sensors/anchors kept), character shadows off, same-material skinned submeshes merged without losing geometry/animation.
  The new step also runs on scenes that already have Performance Geometry; QuestPerfTest regenerated; scene YAML and LFS pointers not edited.
  GameplayPerformanceTests gains avatar/mesh, rig/sensor/anchor integrity assertions; the audit keeps the same limits and dynamic-geometry failures.
  Tests: ~/bob/bin/check.sh CHECK PASSED (all assemblies + 113/113 Core), scene checker SCENE PASSED (51 package GUIDs not verified yet), git diff --check.
  BOB-047 needs_unity_check: the next CI runs PlayMode + PrepareScenes/Audit/build; keep perf-prepare/perf/audit and regression screenshots to check the look on the laptop.
  Latest gameplay budget 351701 tris/156 draws (+101701/+56), perf scene 351759/157 (+101759/+57); 35 shadows and active SDK visuals including two dynamic hands are the cause.
  Optimization results not yet measured in Unity; both scenes must be <=250k tris, <=100 draws/eye, <=50 materials with complete geometry; physical Quest remains a known gap.

- 2026-10-17 · BOB-CI · claude · Fix OnlineMatchTests "Invalid coordinates" and expose the Vivox cause (CI bc71d57).
  Cause: item/candy FBX roots have scale 100; online visual scale was quantized in millimeters to short (max. 32.767), so the host threw IOException on every packet.
  Scale is now sent as half (AvatarVisualWire.WriteScale/ReadScale, still 6 bytes, NaN/Inf rejected); new test OnlineSceneTests.Scene_item_and_candy_scales_survive_visual_codec.
  RoomVoiceChat: static join/leave queue (VivoxService singleton) and LastError; the Voice test failure message now includes the original Vivox exception.
  Budget Audit not touched (BOB-047 codex). Tests: ~/bob/bin/check.sh CHECK PASSED (all assemblies + 113/113 Core).
  Owner/CI: OnlineMatchTests 2/2 + scale test pass; if Voice fails, read the exception in CI.md (check Vivox is enabled in the UGS dashboard).

- 2026-10-17 · BOB-CI · claude · CI 42e7168: OnlineMatchTests + Budget Audit (QuestPerfTest 101/100 draws).
  OnlineMatchTests: the CI stack points at WriteVector(localScale), already fixed by e0b361b (WriteScale half), which was not in CI 42e7168.
  Budget Audit: the local avatar head (Pupil/Mouth/EyeWhite/Lid/HeadGear, ±23 draws) was counted in every Seat_0 view, even though
  LocalAvatarTracking always hides it from the first-person camera (desktop forceRenderingOff, XR layer 30 culled).
  QuestPerfTestBuilder.AuditScene excludes those headRenderers from the per-eye count only; scene/shadow totals and the 250k/100/50 limits stay; the count is written to the audit.
  New test CameraSetupTests.Local_avatar_head_is_not_drawn_by_first_person_camera (desktop + XR paths). ~/bob/bin/check.sh CHECK PASSED (113/113 Core).
  Owner/CI: next CI.md PlayMode 32/32 and Budget Audit PASSED with the line "Local avatar head ... excluded per eye"; if it still fails see "Most draws".

- 2026-10-18 · BOB-047 · codex · Re-QA of CI 610a070: matched 13 needs_unity_check tasks + BOB-047 against XML 113/113 EditMode and 32/32 PlayMode, no failures/skips.
  BOB-038 and BOB-041 done; 11 other tasks stay open with AC/test name/evidence/gap mapping in notes; BOB-CI not changed.
  Budget Audit PASSED: CandyParlour3D max. 203085 tris/73 draws per eye/26 materials; QuestPerfTest 203143/74/27, shadows 0 (limits 250000/100/50).
  The PrepareScenes/Audit Editor scripts and the local head/rig/mesh tests are proven by CI; no code/scene or LFS pointer changes needed.
  Idle GC 6629.95 B/frame not yet near zero; CPU capture, audio approval, RC document, code join/client visuals/host leaving/two-process sync and Meta XR Setup Tool not all proven yet.
  Tests: ~/bob/bin/check.sh CHECK PASSED (all assemblies + 113/113 Core), git diff --check; PlayMode test names in notes matched to Passed in the XML.
  Owner/CI: complete automated Editor evidence for the remaining tasks; pinch/voice/look/performance on a physical Quest remain known gaps, no 72 FPS claim. BOB-047 done; no headset needed to close this QA.

- 2026-10-18 · BOB-048 · claude · English becomes the default language, Bahasa Indonesia via the Language button in Settings (applies immediately, saved in PlayerPrefs).
  Core/Localization.cs: Language, Phrase and an EN/ID Strings table (>200 keys) used by the menu, Bot setup, onboarding, narration/board/seat labels, items/candy, results, lobby, voice, engine/protocol errors, bot reasons, VR tooltips, waiter bubbles.
  GameSettings.Language (bob.settings.language, corrupt value = English); director.RefreshLanguage rebuilds button labels, menu, board and the human seat name ("You"/"Kamu", HumanName). Debug/telemetry logs unchanged.
  Button object names are now English ("Button Play vs Bots", "Button Start", ...); PlayMode tests adjusted and use Strings for text. Settings: Volume, Language, Dominant hand, Mouth snap, Back.
  Tests: ~/bob/bin/check.sh CHECK PASSED (all assemblies + 119/119 Core: LocalizationTests EN+ID keys/placeholders, GameSettingsTests language, onboarding EN+ID).
  New PlayMode test RegressionTests.Language_english_by_default_switch_in_settings_applies_now_and_persists (switch, reload the scene, back to English); the Regression suite forces English for screenshots.
  Owner/CI: PlayMode all passed and screenshots 01-06 in Docs/Autopilot/screens in English; check that the longer English text does not overflow the cards (BOB-049 layout).

- 2026-10-18 · BOB-053 · claude · Devpost page draft (English) in Docs/Devpost/submission.md, ready to paste into Devpost.
  Contents: tagline, inspiration, 30-second rules, features (offline 1-5 bots Easy/Normal/Hard, online 2-6 with 6-digit code + filler bots/disconnect, spectator, Vivox, pinch + controller, onboarding, Settings EN/ID), how it was built, challenges, lessons, plans, team, honest limitations.
  Limitations: not yet tested on a physical Quest, audio is still synthetic placeholder tones, no host migration, bot balance not yet simulated (BOB-051).
  List of screenshots 01-06 + captions; 2:30 trailer shot list (11 shots) uses only BOB-052 scenes + GameplayRecorder camera presets (no audio/VR hands).
  Appendix A maps every claim to code and test names. No code/scene/LFS changes.
  Tests: ~/bob/bin/check.sh CHECK PASSED (all assemblies + 119/119 Core).
  Owner: fill in Alwan's and Zabber's roles/links, rewrite Inspiration; use CI screenshots after BOB-049/050; match the clips after BOB-052 runs.

- 2026-10-18 · BOB-049 · codex · Parlour-themed table UI: cream/green cards with orange frames and rounded corners, one stereo/single-pass/quad URP shader with no new textures.
  Hover/disabled buttons change color/frame; duplicate title removed, narration wrapped, banner lowered and seat status moved to a new line.
  WorldTextLayout separates label/card/popup projection from the player camera, limits scale/width and aligns button colliders; engine/flow, scenes/prefabs and LFS pointers unchanged.
  New test RegressionTests.UI_labels_do_not_overlap_in_menu_gameplay_spectator_and_results: 5 bots, simultaneous BAD/KO, negative controls, hover/disabled and screenshots of four states.
  Regression screenshots 01-06 also audit text+card bounds, clipping, single-pass shader and draws 1–100; report in Logs/ci/ui-layout.txt.
  VPS tests: ~/bob/bin/check.sh CHECK PASSED (all assemblies + 119/119 Core); git diff --check. PlayMode only compiled.
  Status needs_unity_check: the laptop runner needs to produce new screenshots in Docs/Autopilot/screens and CI.md PlayMode/build/shader/Budget Audit PASSED; no manual headset steps.

- 2026-10-18 · BOB-052 · claude · The laptop runner records 1080p30 trailer clips (English UI) when Docs/Release/record-trailer.txt = yes (set to yes), once per code commit.
  New PlayMode test TrailerClipTests (category Trailer; regular PlayMode uses -testCategory "!Trailer", the runner runs it separately with -bobTrailerDir ~/bob-ci/videos/<commit>).
  Clips: 01-menu, 02-bot-setup, 03-gameplay-item, 04-spectator, 05-results, 06-lobby-online (room code) from the player seat (new GameplayRecorder preset Seat) + presets 07-close-up-bot, 08-top-down.
  unity_runner.py: ffmpeg (PATH, ~/bob-ci/bin, winget link) -> MP4 per clip, a "Trailer clips" section in CI.md (scene, duration, file, size); does not change PASSED/FAILED. Guide: Docs/Release/trailer.md.
  Tests: ~/bob/bin/check.sh CHECK PASSED (all assemblies + 119/119 Core); python -m unittest test_unity_runner 10/10 (4 new trailer tests).
  Owner: run install_unity_runner.ps1 (runner copy), install ffmpeg (winget install --id Gyan.FFmpeg -e); the next CI.md must PASS + 8 clips, Trailer tests 6/6; watch the MP4s.

- 2026-10-18 · BOB-050 · codex · Bots moved to the front arc 120–240°, radius 1.3 m; platforms follow the seats, the player seat stays.
  The waiter moves to (2.7; 0; 2.2) facing the player; dispenser at 70% so the middle bot is visible, desktop FOV 60 pushes the lamp globes out of the screen edges.
  ArtSceneBuilder is called by CI PrepareScenes; merged furniture meshes are rebuilt when seats/platforms change, safe for already optimized scenes.
  New Regression test checks 1–5 bots, 10 reactions in all slots, pose vertices within the frame, platform volume free of bots, waiter face/chest rays free of the dispenser.
  Room_Glow mask tests an oval corner; negative controls for the old FOV and a bot on the platform. Screenshots 01-menu/04-gameplay now have 5 bots, plus BOB-050 menu/announcement for 1–5 bots.
  VPS tests: ~/bob/bin/check.sh CHECK PASSED (all assemblies + 119/119 Core), git diff --check; PlayMode tests only compiled.
  Status needs_unity_check: the laptop runner must produce the latest screenshots in Docs/Autopilot/screens and CI.md PlayMode/Budget Audit/build PASSED.
  Evidence for the announcement/Wave, five whole bots, platforms free of hands, and lamps without fragments must come from automated Unity; no manual Quest steps. YAML/LFS pointers unchanged.

- 2026-10-18 · BOB-051 · codex · BotBrain Normal/Hard weighs HP/color/KO, offensive Clover, stolen Milk and avoids wasted Fork/Clover.
  Simulation records steps/failed decisions before fallback and the time of one human-substitute seat after the d20.
  Docs/Balance.md + two CSVs: 80 configurations x 2,000 games/version = 320,000 games; all finished, zero failed decisions, max steps 176 -> 160.
  Every mix Easy < Normal < Hard; minimum N-H gap 2.72 -> 7.82 points; Hard vs Normal duel 55.05% -> 65.25%.
  Three bots + Normal substitute average 7.43-7.91 minutes; P90 10.16-11.56 minutes. Same time profile from director pauses, an estimate, not Quest telemetry.
  20 BalanceTests cases: fixed-seed duels/mixes, duration, progress of all configurations, public info, items and human substitute; how to rerun the batch is in the report.
  VPS tests: ~/bob/bin/check.sh CHECK PASSED all assemblies (including PlayMode compile), 139/139 Core; both mass batches passed, git diff --check.
  Status needs_unity_check: the laptop runner needs EditMode/PlayMode/Budget Audit/shader/build and CI.md PASSED for this commit; automated, no manual headset steps.

- 2026-10-18 · BOB-CI · claude · CI 5fb579a FAILED (PlayMode 39/41), two tests fixed without turning off asserts.
  TrailerClipTests: the preset field (Clip_preset_*) leaked into Clip_spectator_and_results_from_player_seat because the NUnit fixture instance is reused; SetUp now resets the preset.
  RegressionTests.Seated_framing: the FOV 65 lamp negative control was not detected. The Room_Glow mask is rendered without async shader compilation,
  clipped-lamp detection covers the top/left/right edges of the upper half (plus the old corner zone), positive control FOV 100, the failure message includes lamp pixels/boxes.
  Tests: ~/bob/bin/check.sh CHECK PASSED (all assemblies + 139/139 Core); PlayMode tests only compiled.
  Owner/CI: next CI.md PlayMode 41/41; if Seated_framing still fails, send the lamp diagnostic message from CI.md.

- 2026-10-18 · BOB-CI · claude · CI 27d80d7 FAILED (PlayMode 40/41): the FOV 65 lamp negative control in Seated_framing was not detected.
  The mask now draws 308 px, box x=0-319 y=17-32 = two wall lamp globes at both edges, but read in the lower half of the frame:
  RenderTexture readback is Y-flipped (D3D + GetGPUProjectionMatrix renderIntoTexture), so the upper-half edge rule never triggered.
  RegressionTests.LampPixels calibrates orientation with a marker cube above the frame center (same render path, MaskPixels) and flips Y if needed.
  FOV 60 framing assert, FOV 65 negative control and FOV 100 positive control unchanged. Tests: ~/bob/bin/check.sh CHECK PASSED (139/139 Core); PlayMode only compiled.
  Owner/CI: next CI.md PlayMode 41/41; if it fails, send the lamp message (x/y box, "readback Y flipped by marker").

- 2026-10-19 · BOB-CI · claude · CI 874adfa FAILED (PlayMode 40/41): lamp control passed, Seated_framing failed "Bot_Red covered by dispenser (offset -0.06)".
  playmode.xml stack: RegressionTests.cs:178 = 5-bot reaction loop (idle/announcement passed). Bot_Red (Seat_3) is right behind the dispenser;
  reactions lower the face (Hurt squashes the body 9%, Eat/Give lean) so the ray to the chin enters the bounds of the 70% dispenser.
  ArtSceneBuilder.ConfigureSeatedFraming: dispenser at 60% of source scale (used by PrepareScenes before tests/build). Assert not relaxed;
  the message now names the phase/reaction/sample, dispenser part, top y vs ray height. MaskPixels calls RestoreAsyncCompilation.
  Tests: ~/bob/bin/check.sh CHECK PASSED (all assemblies + 139/139 Core); PlayMode only compiled.
  Owner/CI: next CI.md PlayMode 41/41; if still "covered by dispenser", send the message (reaction, top y, ray entry y).

- 2026-10-19 · BOB-CI · claude · CI 64a4ea2 FAILED (PlayMode 40/41): dispenser passed, Seated_framing failed "Bot_Yellow/LavenderWomanMesh clipped at right of frame" x 1.014 (y 0.78 = arm raised during a reaction).
  Seat_1 at 120°: outer hand ~46.5° from the desktop camera axis, limit 0.99 = 45.2° (FOV 60, 16:9). ArtSceneBuilder.SeatDirection is now the arc 126–234°
  (27° spacing, radius 1.3 m unchanged; applied by PrepareScenes before tests/build), outermost edge estimate ~0.96; Bot_Peach symmetric. Lamps/dispenser/waiter unchanged.
  Assert 0.01/0.99 not relaxed; the "clipped" message now names the phase (menu/announcement/reaction+sample).
  Tests: ~/bob/bin/check.sh CHECK PASSED (all assemblies + 139/139 Core); PlayMode only compiled.
  Owner/CI: next CI.md PlayMode 41/41; if still clipped, send the message (bot, phase/reaction, min/max viewport). Visually check that neighboring bot platforms do not overlap.

- 2026-10-19 · BOB-047 · codex · Re-evaluated all 16 needs_unity_check tasks against CI 93802872a1e2239d9c511c31c6833992bc3e2970.
  XML: EditMode 139/139, PlayMode 41/41, zero failed/skipped; Release Succeeded. Each task's notes map AC, tests and remaining gaps.
  BOB-048/049/050/051 become done via the language test, UI overlap, 1–5 bot framing and BalanceTests; 12 tasks stay open, BOB-047 done.
  Budget Audit PASSED: gameplay 203085 tris/73 draws per eye/26 materials; perf scene 203143/74/27; shadows 0, complete geometry.
  No extra optimization needed; PrepareScenes -> Audit/build already proven by CI. Status/notes and log only; code, raw CI, scenes and LFS pointers unchanged.
  Idle GC 7681.86 B/frame not yet near zero; CPU capture, audio approval, RC, full client join/visuals/disconnect, two processes and Setup Tool not proven yet.
  TrailerClipTests 6/6 record 8 scenes into a temporary folder that gets deleted; no CI trailer table/stored clip evidence yet. BOB-052 stays open.
  VPS tests: ~/bob/bin/check.sh CHECK PASSED (all assemblies + 139/139 Core), git diff --check; 16 screenshot oids match the manifest, no claim of visual inspection.
  Next CI: automated Editor evidence for the remaining gaps and persistent trailer recordings; sensors/pinch/voice/stereo/Quest performance remain known gaps, no manual headset steps.

- 2026-10-19 · BOB-055 · claude · New PlayMode fixture `DevpostScreenshotTests` (own file, no overlap with BOB-054) plays one normal match:
  default rules (2 items/round, 30 s timer, random draw), scene seed 0, default bot levels, 4 bots, English; the test never fills the dispenser.
  Captures from the seat camera: devpost-01-menu, -02-bot-setup, -03-onboarding, -04-your-turn, -05-item (Fork/Hammer), -06-spectator,
  -07-results (1280x720) and devpost-cover (1920x1280, 3:2). Nudges: a Plastic Fork only when none of Fork/Hammer was dealt, HP 1 after the item shot.
  Docs/Devpost/submission.md: CI 9380287 counts (139 EditMode, 41 PlayMode), done work dropped from What's next/Honest limitations,
  BOB-051 balance result added, screenshot table points at devpost-*.png. Inspiration/Team left for the owner.
  Tests: ~/bob/bin/check.sh CHECK PASSED (all assemblies + 139/139 Core); PlayMode only compiled.
  CI should prove: PlayMode 42/42 incl. Normal_match_devpost_screens_*, and devpost-*.png in Docs/Autopilot/screens.

- 2026-10-19 · BOB-054 · codex · World labels reserve animated bot head bounds and dispenser geometry in seat-camera space.
  Crowded cards fit into clear slots; existing text/card depth and shared materials are preserved.
  Results show the winner headline once, hide the round board and waiter announcement, and suppress existing/late action popups.
  Reset restores the round board; match rules, input and winner reactions remain unchanged.
  RegressionTests.UI_labels_do_not_overlap_in_menu_gameplay_spectator_and_results adds head/dispenser negative controls and result/reset checks.
  The shared screenshot audit checks all visible labels against projected head and dispenser bounds.
  Tested: ~/bob/bin/check.sh CHECK PASSED (all assemblies, 139/139 Core); git diff --check. Unity is unavailable here.
  Status: needs_unity_check; laptop CI must pass PlayMode and Budget Audit and refresh menu/gameplay/results screenshots.

- 2026-10-19 · BOB-057 · codex · Translated scoped C#/shader comments, developer strings, runtime object names, recorder presets, and Blender prose to English.
  Budget Audit emits exact RESULT: PASS static budget / RESULT: FAIL static budget lines; build logs use ; report=; video scripts are make_video.cmd/.sh.
  Updated dependent test references and log assertions; added a PlayMode Seat recording test for English script files and the player camera.
  Localization values, PlayerPrefs keys, LFS assets, scenes, prefabs, materials, ProjectSettings, and gameplay logic are unchanged.
  Tested: ~/bob/bin/check.sh CHECK PASSED (all assemblies, 139/139 Core); 11 runner tests; Blender syntax/AST; localization and source marker checks; git diff --check.
  Status needs_unity_check: laptop CI must pass PlayMode (including GameplayRecorderTests and TrailerClipTests), build successfully, and report Budget Audit PASSED with the English marker and parsed build report path in CI.md.

- 2026-10-20 · BOB-CI · claude · CI 9a3fe3a: 3 RegressionTests failed and the Release build failed (il2cpp.exe, no output).
  Head/label overlaps: CharacterAnimator now runs LateUpdate before the director ([DefaultExecutionOrder(-10)]), so labels are
  arranged around this frame's head pose (at timeScale 20 a one-frame lag is a large move). RegressionTests.HeadBounds now
  samples the rest pose (animator briefly disabled), matching the director's Start sample; asserts and controls unchanged.
  Three_candy_colors: expects the translated engine log "Damage capped at 3 per candy." (BOB-057).
  Build report: adds "== Editor errors during build ==" with Editor error logs when a build fails (il2cpp gave only its command line).
  Tested: ~/bob/bin/check.sh CHECK PASSED (all assemblies, 139/139 Core). Unity is unavailable here.
  CI should prove: PlayMode 37/37 (Menu_bot_setup, UI_labels, Three_candy_colors) and build Succeeded.

- 2026-10-20 · BOB-056 · claude · Translated to English: Docs/ (Autopilot README/Roadmap, Release, Balance, VisualProduction), Assets/README.md,
  Builds/reports/README.md, every tasks.json text field and all earlier LOG.md entries (dates, IDs and meaning kept).
  Assets/Tests: comments, assertion messages and the perf report in English; 13 Indonesian test methods renamed (CameraSetupTests,
  GameplayRecorderTests, TrailerClipTests); screenshots 02-bot-setup.png / 06-results.png, trailer clips 02-bot-setup / 05-results / 08-top-down.
  Unchanged: asserted Bahasa Indonesia localization values, PlayerPrefs keys, task IDs, archived RC evidence, runner-written CI.md.
  Tested: ~/bob/bin/check.sh CHECK PASSED (all assemblies, 139/139 Core); grep for common Indonesian words finds only localization data
  and quoted historical tool output. CI should prove: PlayMode all green and Docs/Autopilot/screens holding the English screenshot names.

- 2026-10-20 · BOB-058 · codex · Audited material writers and archived CI diffs: only RedGrouch _MainTex is cleared; no direct gameplay/test Meshy texture write was found.
  URP copies _BaseMap into _MainTex during validation; unresolved import state is suspected and still needs Unity confirmation.
  PrepareScenes resolves/reimports affected albedos and restores both authored slots in Edit Mode; valid Meshy materials are not reimported.
  Palette polishing skips Meshy materials; WorldText clones its fallback font material; failed texture installation throws before assigning null.
  RegressionTests.Meshy_material_textures_survive_scene_match_red_KO_and_menu checks all six albedos, rendered/spectator materials,
  unchanged serialized state/file bytes for all Art3D materials, a full match and menu restoration; captures BOB-058-textured-bots.png.
  Tested: ~/bob/bin/check.sh CHECK PASSED (all assemblies, 139/139 Core); git diff --check. PlayMode compiled; Unity unavailable.
  Status needs_unity_check: CI must pass the regression/full suite/build, show no Art3D/Materials/*.mat workspace changes, and capture textured red/brown Bot_Red.

- 2026-10-20 · BOB-CI · claude · CI 6ef066d: Ko_spectator 05-spectator drew 104 > 100 (the Release build hang was a laptop clang crash, so no code change for it).
  Only runtime change since 9a3fe3a (passing) is CharacterAnimator's execution order, which toggles no renderers, so the KO frame sits near the limit.
  Without Unity the 4 extra draws can't be attributed: CI.md only had the total. AssertLabelLayout now puts a breakdown in the failure message and
  Logs/ci/ui-layout.txt: UnityStats setpass/srpBatcher/standard/staticBatched/instanced/shadowCasters plus a frustum estimate per material
  slot (transparent/spectator counts, top 16 groups: labels by name, scene objects by path). Budget 1-100 and all asserts unchanged.
  Tested: ~/bob/bin/check.sh CHECK PASSED (all assemblies, 139/139 Core). Unity unavailable here.
  CI should prove: PlayMode all green; if Ko_spectator fails again, its message names the draw contributors to cut.

- 2026-10-21 · BOB-CI · claude · CI 0facc91: PlayMode gave no results (exit 3) and the Budget Audit failed, both because PrepareMeshyMaterials threw
  (called by CIRunner and by the PlayMode IPrebuildSetup): RedGrouch_Full_BaseColor.png could not be imported on the laptop.
  The LFS object is a valid PNG, but RC 874adfa's workspace.diff shows the CI copy hashing to a different oid at the same size: a corrupt
  local file that checkout -f / lfs pull never replace. This also explains the white Bot_Red (BOB-058).
  unity_runner.repair_lfs: git lfs fsck + pull, hash every LFS file against its oid, check mismatches out again (logged). New runner test.
  PrepareMeshyMaterials still throws, but now names the reason (LFS pointer, or which PNG chunk fails its CRC).
  Tested: ~/bob/bin/check.sh CHECK PASSED (all assemblies, 139/139 Core); runner tests 13/13; PNG check run against the real and a corrupted file.
  Owner: re-run install_unity_runner.ps1 so the laptop uses the new runner. CI should prove: PlayMode results all green, Budget Audit PASSED.

- 2026-10-21 · BOB-CI · claude · CI 65f0372: still the corrupt RedGrouch_Full_BaseColor.png (IDAT at byte 4274498 fails its CRC; LFS object b3aee419 is valid).
  git now reports the file clean, so checkout -f rewrites it from a corrupt local LFS object; the laptop runner copy was also not reinstalled.
  PrepareMeshyMaterials (CIRunner + PlayMode IPrebuildSetup) now restores a Meshy texture that fails to import: hash vs index pointer oid,
  delete a corrupt local LFS object and the file, git checkout -f (smudge downloads and verifies), lfs pull fallback, warning, reimport.
  unity_runner.repair_lfs also deletes a corrupt local object itself (runner test extended).
  Tested: ~/bob/bin/check.sh CHECK PASSED (all assemblies, 139/139 Core); runner tests 13/13. Unity unavailable here.
  CI should prove: PlayMode results all passing, Budget Audit PASSED; Editor log warns '... did not match its Git LFS object'.

- 2026-10-21 · BOB-CI · claude · CI 585a9cc: the in-Unity restore found RedGrouch_Full_BaseColor.png corrupt and deleted it (the local LFS object was fine),
  but git checkout -f / lfs pull run from Unity wrote nothing (stderr not captured): audit 'file missing', and Unity also dropped the .meta.
  RestoreFromGitLfs now copies the LFS object from .git/lfs/objects over the file only once the object hashes to the pointer oid
  (git lfs fetch --include only if the object is missing or corrupt), never deletes the file first, runs git without prompts
  and puts git's exit code and stderr in the exception.
  Tested: ~/bob/bin/check.sh CHECK PASSED (all assemblies, 139/139 Core). Unity unavailable here.
  CI should prove: PlayMode results all passing, Budget Audit PASSED; if not, the audit message names the git error or missing object.

- 2026-10-21 · BOB-047 · claude · QA against CI b1d74e3 (EditMode 139/139, PlayMode 38/38, Release, Budget Audit PASSED): evaluated all 18 tasks.
  Done: BOB-054, BOB-056, BOB-057, BOB-058 (+ BOB-CI confirmed, BOB-047). Still needs_unity_check, gap named in each task's notes:
  BOB-009/017/022 idle GC 7954 B/frame, BOB-020 CPU capture, BOB-018 audio approval, BOB-025 RC.md, BOB-035/036/037/046 online
  Editor tests, BOB-043 Setup Tool report, BOB-052 trailer recording paused, BOB-055 submission.md cites CI 9380287.
  Fixed the misleading audit line 'active dynamic geometry still fails' in QuestPerfTestBuilder (log text only).
  Tested: ~/bob/bin/check.sh CHECK PASSED; manifest screenshot hashes match all 25 LFS pointers. Unity unavailable here.
  CI should prove: Budget Audit still PASSED with the reworded renderer line; rerun this QA for the remaining tasks.

- 2026-10-21 · BOB-059 · codex · Anchored bot seat/difficulty labels above their own projected head bounds before packing other cards.
  Main menu title, four buttons and hint form one centered column with equal card widths and matching colliders.
  Card/text shaders render after spectator materials and ignore character depth, keeping standings and summary readable; gameplay unchanged.
  Extended PlayMode layout audit: owner/head alignment, all reaction poses, menu columns at 16:9 and square aspects, character queue/depth/sorting, results separation; negative controls included.
  Tested: ~/bob/bin/check.sh CHECK PASSED (all assemblies, 139/139 Core); PlayMode compiled only. Status: needs_unity_check.
  Laptop CI must pass the extended RegressionTests/full suite and Budget Audit, and refresh menu/your-turn/results/BOB-058 screenshots to prove the layout visually.

- 2026-10-21 · BOB-CI · claude · CI 7af3aee (PlayMode 36/38): both failures were in the RegressionTests audit, not the game.
  Ko_spectator 05-spectator draw=103: UnityStats latch at frame end, so reading right after camera.Render() returned an earlier render
  (no-render controls repeat the last screenshot; the next test's 01-menu read 68 = the spectator render). Screenshot now takes the
  frustum estimate at render time, yields a frame, then asserts the same 1-100 budget (AssertDrawBudget); controls no longer read stale stats.
  03-onboarding 'Label Yellow above its own head': seat labels are empty before the first game state (card hidden, nothing drawn);
  AssertSeatLabels now skips empty labels like AssertLabelLayout. No budget, assert or negative control loosened.
  Tested: ~/bob/bin/check.sh CHECK PASSED (all assemblies, 139/139 Core). Unity unavailable here. Status: needs_unity_check.
  CI should prove: PlayMode 38/38, Budget Audit PASSED; playmode.xml shows '05-spectator: draw=' around 68.

- 2026-10-21 · BOB-CI · claude · CI f31ae01 (PlayMode 37/38): Seated_framing clipped Bot_Yellow's outer hand at viewport x 0.9907 (limit 0.99), 5 bots, Cheer sample 7.
  The test had passed 7 runs since the 126-234 seat arc, so the Cheer peak sat right at the frame edge. Cheer is the widest pose (arm spread 55).
  CharacterAnimator Cheer arm spread is now 40 (raise/bend unchanged). Spread sets the hand's sideways reach, giving an estimated 0.02-0.03 viewport margin.
  Seat arc unchanged (a tighter arc risks the label overlap audits); the 0.01/0.99 frame assert is not loosened.
  Tested: ~/bob/bin/check.sh CHECK PASSED (all assemblies, 139/139 Core). Unity unavailable here. Status: needs_unity_check.
  CI should prove: PlayMode 38/38 (Seated_framing passes every reaction sample), Budget Audit PASSED.

- 2026-10-21 · BOB-CI · claude · CI 86ae2a3 (PlayMode 37/38): Seated_framing clipped Bot_Mint (GentleGiant) at frame top, viewport y 0.9916 (limit 0.99), Cheer sample 1.
  Spread 40 (previous fix) leaves the raised arms closer to vertical, so the hands sit higher; sample 1 is near the hop + squash-stretch peak.
  CharacterAnimator Cheer hop and squash amplitude .06 -> .04: ~2-3 cm lower at that sample (~0.01 viewport). The squashed (widest) phase is narrower too, so no extra side reach.
  Spread/raise/bend and the 0.01/0.99 frame assert unchanged.
  Tested: ~/bob/bin/check.sh CHECK PASSED (all assemblies, 139/139 Core). Unity unavailable here. Status: needs_unity_check.
  CI should prove: PlayMode 38/38 (Seated_framing passes every reaction sample), Budget Audit PASSED.

- 2026-10-22 · BOB-061 · claude · RC.md and Devpost draft refreshed to the latest green CI f7dacb8 (EditMode 139/139, PlayMode 38/38, Release APK 1,684,812,938 bytes, Budget Audit 73 draws/eye).
  RC.md: f7dacb8 named as latest green candidate; regression/online checklists marked Passed from the archived playmode.xml; open needs_unity_check tasks listed as Editor gaps, device-only checks separately; owner decisions recorded.
  submission.md: 41 -> 38 PlayMode, CI numbers updated, Inspiration/Team marked as owner sections, audio/reaction wording matched to code, limitations corrected (local-transport match tests, no facial expressions).
  Screenshots: all eight devpost-*.png exist and match the manifest hashes. Trailer: the eight v2 clips are listed by name as pending (CI.md for f7dacb8 has no Trailer clips section).
  Tested: ~/bob/bin/check.sh CHECK PASSED (docs-only change). Status: done.
  CI should prove: the next run stays green and its "Trailer clips" section lists the eight clips; then update both docs with the new hash.

- 2026-10-22 · BOB-060 · codex · Reserved the authored wall sign panel and shelf tags, including after furniture batching.
  Seat/difficulty cards stay centered above their own heads and fit into the clear vertical band around room text; gameplay unchanged.
  Extended RegressionTests label audits independently measure source sign/tag mesh vertices and check full card bounds, with sign/shelf negative controls.
  Shared screenshot and seated-framing audits cover bot counts, reactions, gameplay, spectators and results.
  Tested: ~/bob/bin/check.sh CHECK PASSED (all assemblies, 139/139 Core tests); PlayMode compiled only. Status: needs_unity_check.
  Laptop CI must pass UI_labels_do_not_overlap_in_menu_gameplay_spectator_and_results, Seated_framing_1_to_5_bots_clear_player_mat_and_waiter_announcement and the full suite.
  Refreshed devpost-04-your-turn.png and devpost-07-results.png must show labels clear of the wall sign and shelf text.
