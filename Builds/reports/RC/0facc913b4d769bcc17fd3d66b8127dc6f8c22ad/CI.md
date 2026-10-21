# Unity CI (laptop runner)

Commit `0facc913b4d769bcc17fd3d66b8127dc6f8c22ad` · 2026-10-21 08:51 WIB · **FAILED**

| Check | Result |
|---|---|
| EditMode tests | 139/139 passed, 0 failed, 0 skipped; exit=0 |
| PlayMode tests (real scene) | Unity produced no test results (exit 3); see 20261021-0831-0facc91-playmode.log |
| Build APK Release | Succeeded; mode=Release; errors=0; warnings=6; bytes=1682175788; duration=817.7s; path=C:\Users\acer\bob-ci\repo\Builds\BiteOrBetray_Quest_Release.apk; report=C:\Users\acer\bob-ci\repo\Builds\reports\BiteOrBetray_Quest_Release_20261021-083752_0facc913b4d769bcc17fd3d66b8127dc6f8c22ad-dirty.txt |
| Shader errors | 0 |
| Compile errors | 0 |
| Budget Audit | FAILED |

- PlayMode: Unity produced no test results (exit 3); see 20261021-0831-0facc91-playmode.log

- Build Release: Succeeded; mode=Release; errors=0; warnings=6; bytes=1682175788; duration=817.7s; path=C:\Users\acer\bob-ci\repo\Builds\BiteOrBetray_Quest_Release.apk; report=C:\Users\acer\bob-ci\repo\Builds\reports\BiteOrBetray_Quest_Release_20261021-083752_0facc913b4d769bcc17fd3d66b8127dc6f8c22ad-dirty.txt; exit=1

## Budget Audit failed

See the Budget Audit report below; a successful APK build does not yet meet the gameplay budget.

## Budget Audit

```
AUDIT FAILED
System.InvalidOperationException: Meshy base color texture could not be imported: Assets/BiteOrBetray/Art3D/Meshy/RedGrouch/RedGrouch_Full_BaseColor.png
  at BiteOrBetray.EditorTools.SeatedAvatarBuilder.PrepareMeshyMaterials () [0x00154] in C:\Users\acer\bob-ci\repo\Assets\Scripts\Editor\SeatedAvatarBuilder.cs:238 
  at BiteOrBetray.EditorTools.GameplayPerformanceBuilder.PrepareScenes () [0x0004a] in C:\Users\acer\bob-ci\repo\Assets\Scripts\Editor\GameplayPerformanceBuilder.cs:31 
  at BiteOrBetray.EditorTools.CIRunner.Run () [0x00023] in C:\Users\acer\bob-ci\repo\Assets\Scripts\Editor\CIRunner.cs:21
```

## Trailer clips (BOB-052)

Code commit `0facc913b4d769bcc17fd3d66b8127dc6f8c22ad` · laptop folder `C:\Users\acer\bob-ci\videos\0facc913b4d769bcc17fd3d66b8127dc6f8c22ad` (not in the repo) · 1080p 30 fps, English UI, no audio.

- Trailer tests: 6/6 passed, 0 failed, 0 skipped; exit=0

| Clip | Scene | Duration | File | Size |
|---|---|---|---|---|
| 03-gameplay-item | Gameplay with items from the seat: Plastic Fork reveal, eat, Hammer Crush, Grappling Hook steal, Little Pocket pick, bots' turns | 62.8 s (1883 frames) | `03-gameplay-item.mp4` | 66.6 MB |
| 01-menu | Main menu from the seat: Play vs Bots, Create Room, Join Room, Settings | 7.0 s (210 frames) | `01-menu.mp4` | 7.4 MB |
| 02-bot-setup | Table setup: bot count 1-5, per-bot level Easy/Normal/Hard, Start, d20 turn order | 18.3 s (550 frames) | `02-bot-setup.mp4` | 19.2 MB |
| 06-lobby-online | Online lobby: Create Room, 6-digit room code, players in room, empty seats filled by bots | 14.7 s (440 frames) | `06-lobby-online.mp4` | 15.6 MB |
| 07-close-up-bot | Bot close-up camera: bots eating, giving and reacting | 45.0 s (1351 frames) | `07-close-up-bot.mp4` | 30.3 MB |
| 08-top-down | Top-down table camera, slow orbit | 30.0 s (901 frames) | `08-top-down.mp4` | 37.1 MB |
| 04-spectator | Spectator: knocked-out player turns transparent and keeps watching from the seat | 15.6 s (467 frames) | `04-spectator.mp4` | 17.2 MB |
| 05-results | Results: last bots standing, winner card with knockout order, Play again / Back to menu | 156.0 s (4680 frames) | `05-results.mp4` | 167.5 MB |

Total 349.4 s of video.


Already recorded in an earlier run for this code commit (re-record: `--force --trailer`).

RC archive for this commit: [manifest and full evidence](../../Builds/reports/RC/0facc913b4d769bcc17fd3d66b8127dc6f8c22ad/manifest.json).
