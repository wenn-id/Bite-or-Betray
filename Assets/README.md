# Bite or Betray: Unity project (VR Meta Quest 3)

Unity 6 project (`6000.6.3f1`, URP) containing the GDD rules in pure C#, the VR game in the `CandyParlour3D` scene (against bots and
online rooms), a 2D test table without VR, a balancing simulator, and EditMode and PlayMode tests.

## How to open

1. Clone the repo with Git LFS installed (`git lfs pull`): `.fbx` models, textures, and audio are stored in LFS.
2. Open the repo folder in Unity Hub with Unity `6000.6.3f1` and the Android Build Support module.
3. VR game: menu **Bite or Betray > Open Candy Parlour 3D Scene**, then Play (without a headset it is played with the mouse).
4. 2D test table: menu **Bite or Betray > Create Debug Test Scene**, press **Play**, set up the seats, then click **START GAME**.

If that menu does not appear, create an empty GameObject in any scene, add the `BiteOrBetrayDebugGame` component, then press Play.

## VR status

The VR implementation is in `Scripts/Table3D` and the `BiteOrBetray/Scenes/CandyParlour3D.unity` scene (Meta XR SDK,
`OVRCameraRig`): hand tracking input with a controller pointer as fallback (`VRTableInput`), main menu and Settings at the table,
offline room with 1-5 bots, spectator for players who lose, results screen and play again, audio, onboarding, head/hand avatars, and
online rooms (Netcode for GameObjects, Lobby/Relay Multiplayer Services, Vivox voice). All of that behavior is checked by PlayMode tests in the
Editor; there has been no testing on a Quest headset yet. Features still awaiting Unity/Quest evidence are recorded with status
`needs_unity_check` in `Docs/Autopilot/tasks.json`.

## How to play at the test table

- **Left panel:** all players, with HP, status, items, and d20 results.
- **Middle panel:** waiter announcements, the active candy, and action buttons (EAT, GIVE, items).
- **Right panel:** log of all events.
- Human seats are played in turns on the same screen (hotseat). Bots play automatically, and their delay can be adjusted.
- **God mode** shows the contents of the active candy. Use it for debugging only.
- The same **Seed** produces the same game. The seed is shown below the buttons after the game starts.
- Spawn numbers (candies per round, bad ratio, items per round) can be changed in the Inspector, in the **Rules** section. The defaults are the final balancing.
- **Turn timer:** human seats get 30 seconds of thinking per turn (set in Rules > Turn Time Limit Seconds). When it runs out, an open Little Pocket is discarded, the active candy is force-eaten, and the turn ends.
- **Telemetry:** every decision is logged to CSV (thinking seconds, action, round, turn) in the telemetry folder. The **Telemetry folder** button opens the folder.
- If the text is too small or too large, change **Ui Scale** in the Inspector.

## Simulator (balancing report)

Click the menu **Bite or Betray > Simulator**. Set the number of games, number of players, and bot level per seat, then click **Run simulation**. All seats are played by bots, and 500 games usually finish within a few seconds.

The report contains game duration (turns, rounds, and estimated minutes in VR), win rate per bot level, win rate per turn order, average actions and item usage per game, and automatic notes when a number looks suspicious. The report can be copied, saved as `.md`, or its per-game data saved as `.csv` for processing in a spreadsheet.

The minute estimate uses the **Timing model**: estimated seconds for game opening and closing, waiter, dispenser, thinking (capped by the per-turn timer), eating, feeding, and the duration of each item. The **Calibrate from telemetry** button replaces the thinking seconds with the real average from the test table telemetry CSVs. The numbers can be changed in the Simulator window, and should be calibrated after playtests. The report also compares the results with the GDD target duration per player count.

The default Rules already use the final balancing: max HP 5 (start 4), 3 candies per living player (4-12 per round), bad ratio 38-52% plus 4% per seat above 2, and 2 items per round.

The **Run matrix of all bot combinations** button runs all 80 Easy/Normal/Hard combinations for 2-6 players, then saves two CSVs: a summary per combination (duration, rounds, turns per round, candies and items per round and per game, win rate per level) and a per-round profile.

The Rules in the Simulator are separate from the Rules in the scene. Click **Load Rules from scene** to copy the numbers from the `BiteOrBetrayDebugGame` component.

## Tests

Open **Window > General > Test Runner**, then click **Run All** in each tab:

- **EditMode** (`Tests/EditMode`): 113 tests (previously 42) for rules, GDD edge cases, rooms, settings, onboarding, table protocol, and
  online rooms. They are all Core logic, so they can also be run without Unity with `dotnet test` (used by the agent's `check.sh`).
- **PlayMode** (`Tests/PlayMode`): 30 tests in real scenes: scene smoke, full offline match, camera, gameplay/audio/telemetry
  regression, performance, video recorder, and online host+client and Unity Gaming Services (requires internet; without
  internet the online tests are skipped with `Assert.Ignore`).

## Quest APK build

- From the Editor: **Bite or Betray > Build Quest APK > Development** or **Release**. The only scene built is `CandyParlour3D`,
  output `Builds/BiteOrBetray_Quest_<mode>.apk` with a report in `Builds/reports/` (duration, errors, largest assets, shader variants).
  GraphicsSettings and the Build Profile are restored after the build so git status stays clean.
- From the command line (without opening the Editor):

  ```
  Unity -batchmode -quit -projectPath <repo> -buildTarget Android -executeMethod BiteOrBetray.EditorTools.AvatarValidation.BatchBuild -bobBuildMode Release
  ```

  `-bobBuildMode` may be `Development` or `Release`; without the argument it uses `Docs/Release/build-mode.txt`, or Development
  if that file does not exist. Exit code 1 if the build fails. The report contains the full commit hash, a `-dirty` suffix if the working
  copy has changed, and the SHA-256 of a successful APK.
- `BiteOrBetray.EditorTools.CIRunner.Run` runs the Quest Budget Audit (`Logs/ci/audit.txt`) then builds with that mode.
  BOB-025 requests **Release**. The freeze, checklist, latest evidence and its validation limits are in [RC.md](../Docs/Release/RC.md).

## Static scene checker

`python3 Tools/CI/check_scene.py` reads the `CandyParlour3D` YAML without Unity and reports:

- GUIDs referenced by the scene that have no `.meta` in `Assets/`, `Packages/`, or `Library/PackageCache/`. Without
  `Library/PackageCache` (a repo never opened in Unity), package GUIDs are only marked "belum terverifikasi" (unverified);
  `--strict` makes them fail.
- Objects that the code looks up by name: `BoB_Art3D`, `Seats`, `Seat_0/HeadAnchor`, `Bot_<color>`, and the `OVRCameraRig` anchors
  (`TrackingSpace`, `Left/RightHandAnchor`, `Left/RightControllerAnchor`, `CenterEyeAnchor`). Objects that have a fallback in code
  (waiter, dispenser, candy and item templates) only get a warning.

Exit 0 = pass, 1 = something is missing. `--tree` prints the parsed hierarchy, and another scene path may be given as an argument.

## CI

- The **laptop runner** (`Tools/Autopilot/unity_runner.py`) is the only CI that actually runs: licensed Unity on the
  owner's laptop runs the EditMode and PlayMode tests, the Budget Audit, and the APK build according to `Docs/Release/build-mode.txt` for the
  `develop` branch, then writes the results to `Docs/Autopilot/CI.md`. Skipped tests (including online without internet), a failing exit, or a postponed
  build do not meet PASSED. For Release, the test XML, build report, perf/CSV, audit, the asset diff from prepare/import and the evidence hash
  manifest are archived in `Builds/reports/RC/<full hash>/`. The APK stays local on the laptop; screenshots are in
  `Docs/Autopilot/screens/` with hashes in the manifest.
- After the runner script changes, reinstall via `Tools/Autopilot/install_unity_runner.ps1` on the laptop; Task Scheduler uses
  the copy of the script in `~/bob-ci/bin`. Run `python ~/bob-ci/bin/unity_runner.py --force` to request validation again.
- **GitHub Actions** (`.github/workflows/unity-ci.yml.draft`) is still a draft and has never been run. It contains: the static scene
  checker, EditMode/PlayMode tests, and an Android build with game-ci. The required Unity license secrets (`UNITY_LICENSE` or
  `UNITY_SERIAL`, `UNITY_EMAIL`, `UNITY_PASSWORD`), runner, and steps to enable it are in the file header. This workflow may only
  be enabled with the project owner's approval.

## Structure

| Folder | Contents |
| --- | --- |
| `Scripts/Core` | Game rules (`GameEngine`), data (`CoreTypes`, `PlayerState`), bot AI (`BotBrain`), and simulator (`Simulation`). Does not depend on UnityEngine, so it can be reused for VR and online. |
| `Scripts/Table3D` | VR game: `CandyTableDirector` (table flow), VR input, avatars, audio, and online (`Net/`). |
| `Scripts/DebugTools` | `BiteOrBetrayDebugGame`, the IMGUI-based test table UI. |
| `Scripts/PerfTest` | Performance monitor for the Quest Perf Test scene. |
| `Scripts/Editor` | Scene builder menus, APK build, CI (`CIRunner`), Budget Audit, video recorder, and the Simulator window. |
| `Tests/EditMode` | NUnit tests for Core logic. |
| `Tests/PlayMode` | Real scene tests (offline, online, regression, performance). |
| `../Tools/CI` | Static scene checker (`check_scene.py`). |

## Rule notes

- All actions are performed by the player whose turn it is (`engine.Current`): `Eat()`, `Give(seat)`, `UseItem(index, args)`, `ChoosePocketItem(index, args)`, and `DiscardPocket()`.
- Grappling Hook needs `new ItemArgs(targetSeat, targetItemIndex)`.
- The good/bad split per color is never announced. Only the count per color and the total good/bad are announced.
- A single candy's damage is at most 3, including after Clover (`CandyRules.MaxDamagePerCandy`). Bad blue + Clover becomes 3, not 4, so the Fork + Clover + Give combo cannot knock out a player straight from the starting HP of 4.
- A new round starts when the dispenser is empty and new candy is needed. A turn in progress continues, and the clover moves along.
- Online rooms, voice chat (including spectators), and VR visuals are already implemented; their status is in the **VR status** section.
