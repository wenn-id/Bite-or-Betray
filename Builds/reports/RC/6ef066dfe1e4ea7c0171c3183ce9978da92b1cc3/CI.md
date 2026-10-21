# Unity CI (laptop runner)

Commit `6ef066dfe1e4ea7c0171c3183ce9978da92b1cc3` · 2026-10-20 20:43 WIB · **FAILED**

| Check | Result |
|---|---|
| EditMode tests | 139/139 passed, 0 failed, 0 skipped; exit=0 |
| PlayMode tests (real scene) | 36/37 passed, 1 failed, 0 skipped; exit=2 |
| Build APK Release | did not finish (exit -1) |
| Shader errors | 0 |
| Compile errors | 0 |
| Budget Audit | PASSED |

- PlayMode: incomplete results / Unity exit 2

- Build Release: did not finish; exit=-1

## Failed tests

- BiteOrBetray.Tests.PlayMode.RegressionTests.Ko_spectator_results_music_sfx_and_telemetry_csv_with_screens: 05-spectator: camera draw count is outside the budget of 100.
  Expected: in range (1,100)
  But was:  104

## Budget Audit

```
== Conservative Quest budget (static estimate, not proof of 72 FPS) ==
Target per view/eye: <= 250,000 triangles, <= 100 draws including shadows, <= 50 materials, <= 100 estimated SetPass calls.
FOV 110, 8 yaw directions x 3 pitches (-30/0/30) from Seat_0, height 1.15 m; no occlusion/LOD/batching discount; local avatar head hidden from its own seat.
One color draw per slot/submesh; count all scene casters once for main light shadows (also fully allocated to each eye). 
SetPass = upper bound of one change per draw, NOT a GPU measurement; SRP Batcher/instancing is not assumed to reduce draws.
Objects/animations/UI/particles appearing during Play are not covered yet: compare Frame Debugger/Profiler during laptop gameplay.

Mobile RP: scale=1, MSAA=4x, HDR=False, shadow=512/6m/1 cascade, addLights=PerVertex, additionalShadow=False, depth=False, opaque=False, SRPBatcher=True
PASS: Conservative URP
PASS: Mobile renderer without SSAO/renderer features
Quality[0] Mobile: RP=Assets/Settings/Mobile_RPAsset.asset
PASS: Quest quality pipeline
Quality[1] PC: RP=Assets/Settings/PC_RPAsset.asset
Quality[2] Meta Quest (Build Profile): RP=Assets/Settings/Mobile_RPAsset.asset
PASS: Quest quality pipeline
PASS: Static batching Android=True
Runtime VRTableInput: OVRManager FFR High + dynamic, eye-tracked off, dynamic resolution off, XR scale 1.0; check while Android XR is active.

== CandyParlour3D ==
Renderers counted=85, disabled/forceRenderingOff=119; active dynamic geometry still fails.
PASS: Realtime lights=1, shadows=1 (at most one directional)
PASS: Complete geometry/materials (unknown=0)
PASS: Room BatchingStatic renderers (14)
Whole scene: 217,517 triangles, 100 color draws; conservative shadows 0 draws/0 triangles.
Local avatar head (hidden from first-person camera): 12 renderers/27 draws excluded per eye.
Skinned mesh: 9 | vertex: 109,165
Unique scene materials: 36, instancing enabled: 36 (SkinnedMeshRenderer does not use GPU instancing; SRP/static batching may be preferred).
Scene textures: 19, estimated Editor memory 20.9 MB; transparent materials: 1
Shader: Universal Render Pipeline/Lit
Volume post-process: 1
PASS yaw=0, pitch=-30: triangles=203,085/250000, draws/eye=73+0=73/100, materials=26/50, estimated SetPass<=73/100
PASS yaw=45, pitch=-30: triangles=191,885/250000, draws/eye=59+0=59/100, materials=25/50, estimated SetPass<=59/100
PASS yaw=90, pitch=-30: triangles=170,673/250000, draws/eye=24+0=24/100, materials=20/50, estimated SetPass<=24/100
PASS yaw=135, pitch=-30: triangles=161,201/250000, draws/eye=19+0=19/100, materials=18/50, estimated SetPass<=19/100
PASS yaw=180, pitch=-30: triangles=157,066/250000, draws/eye=18+0=18/100, materials=17/50, estimated SetPass<=18/100
PASS yaw=225, pitch=-30: triangles=157,066/250000, draws/eye=18+0=18/100, materials=17/50, estimated SetPass<=18/100
PASS yaw=270, pitch=-30: triangles=170,673/250000, draws/eye=24+0=24/100, materials=20/50, estimated SetPass<=24/100
PASS yaw=315, pitch=-30: triangles=196,581/250000, draws/eye=60+0=60/100, materials=25/50, estimated SetPass<=60/100
PASS yaw=0, pitch=0: triangles=202,685/250000, draws/eye=72+0=72/100, materials=26/50, estimated SetPass<=72/100
PASS yaw=45, pitch=0: triangles=193,781/250000, draws/eye=65+0=65/100, materials=26/50, estimated SetPass<=65/100
PASS yaw=90, pitch=0: triangles=171,185/250000, draws/eye=26+0=26/100, materials=19/50, estimated SetPass<=26/100
PASS yaw=135, pitch=0: triangles=161,201/250000, draws/eye=19+0=19/100, materials=18/50, estimated SetPass<=19/100
PASS yaw=180, pitch=0: triangles=157,066/250000, draws/eye=18+0=18/100, materials=17/50, estimated SetPass<=18/100
PASS yaw=225, pitch=0: triangles=157,066/250000, draws/eye=18+0=18/100, materials=17/50, estimated SetPass<=18/100
PASS yaw=270, pitch=0: triangles=170,673/250000, draws/eye=24+0=24/100, materials=20/50, estimated SetPass<=24/100
PASS yaw=315, pitch=0: triangles=198,757/250000, draws/eye=64+0=64/100, materials=26/50, estimated SetPass<=64/100
PASS yaw=0, pitch=30: triangles=201,453/250000, draws/eye=71+0=71/100, materials=25/50, estimated SetPass<=71/100
PASS yaw=45, pitch=30: triangles=193,077/250000, draws/eye=62+0=62/100, materials=26/50, estimated SetPass<=62/100
PASS yaw=90, pitch=30: triangles=170,201/250000, draws/eye=25+0=25/100, materials=17/50, estimated SetPass<=25/100
PASS yaw=135, pitch=30: triangles=159,969/250000, draws/eye=18+0=18/100, materials=17/50, estimated SetPass<=18/100
PASS yaw=180, pitch=30: triangles=155,834/250000, draws/eye=17+0=17/100, materials=16/50, estimated SetPass<=17/100
PASS yaw=225, pitch=30: triangles=159,969/250000, draws/eye=18+0=18/100, materials=17/50, estimated SetPass<=18/100
PASS yaw=270, pitch=30: triangles=173,773/250000, draws/eye=23+0=23/100, materials=17/50, estimated SetPass<=23/100
PASS yaw=315, pitch=30: triangles=198,021/250000, draws/eye=62+0=62/100, materials=26/50, estimated SetPass<=62/100
Maximum across views: 203,085 triangles, 73 draws/eye, 26 materials, estimated SetPass<=73.
Largest renderers (whole scene): CherryMesh=22,273 tri/1 draw, GentleGiantMesh=20,000 tri/1 draw, BlueGentlemanMesh=19,997 tri/1 draw, LavenderWomanMesh=19,992 tri/1 draw, RedGrouchMesh=19,972 tri/1 draw, Furniture_3=17,560 tri/1 draw, Furniture_5=10,644 tri/1 draw, Furniture_16=6,912 tri/1 draw
Most draws (whole scene): Pupil_L=5 color/0 shadow, Pupil_R=5 color/0 shadow, Mouth=3 color/0 shadow, EyeWhite_L=2 color/0 shadow, Lid_L=2 color/0 shadow, EyeWhite_R=2 color/0 shadow, Lid_R=2 color/0 shadow, HeadGear=2 color/0 shadow, CandyExit=1 color/0 shadow, CandyGlobe=1 color/0 shadow, Dispenser_Blue=1 color/0 shadow, Dispenser_Gold=1 color/0 shadow

== QuestPerfTest ==
Renderers counted=86, disabled/forceRenderingOff=119; active dynamic geometry still fails.
PASS: Realtime lights=1, shadows=1 (at most one directional)
PASS: Complete geometry/materials (unknown=0)
PASS: Room BatchingStatic renderers (14)
Whole scene: 217,573 triangles, 101 color draws; conservative shadows 0 draws/0 triangles.
Local avatar head (hidden from first-person camera): 12 renderers/27 draws excluded per eye.
Skinned mesh: 9 | vertex: 109,165
Unique scene materials: 37, instancing enabled: 36 (SkinnedMeshRenderer does not use GPU instancing; SRP/static batching may be preferred).
Scene textures: 20, estimated Editor memory 21.1 MB; transparent materials: 2
Shader: Universal Render Pipeline/Lit, GUI/Text Shader
Volume post-process: 1
PASS yaw=0, pitch=-30: triangles=203,141/250000, draws/eye=74+0=74/100, materials=27/50, estimated SetPass<=74/100
PASS yaw=45, pitch=-30: triangles=191,941/250000, draws/eye=60+0=60/100, materials=26/50, estimated SetPass<=60/100
PASS yaw=90, pitch=-30: triangles=170,673/250000, draws/eye=24+0=24/100, materials=20/50, estimated SetPass<=24/100
PASS yaw=135, pitch=-30: triangles=161,201/250000, draws/eye=19+0=19/100, materials=18/50, estimated SetPass<=19/100
PASS yaw=180, pitch=-30: triangles=157,066/250000, draws/eye=18+0=18/100, materials=17/50, estimated SetPass<=18/100
PASS yaw=225, pitch=-30: triangles=157,066/250000, draws/eye=18+0=18/100, materials=17/50, estimated SetPass<=18/100
PASS yaw=270, pitch=-30: triangles=170,673/250000, draws/eye=24+0=24/100, materials=20/50, estimated SetPass<=24/100
PASS yaw=315, pitch=-30: triangles=196,637/250000, draws/eye=61+0=61/100, materials=26/50, estimated SetPass<=61/100
PASS yaw=0, pitch=0: triangles=202,741/250000, draws/eye=73+0=73/100, materials=27/50, estimated SetPass<=73/100
PASS yaw=45, pitch=0: triangles=193,837/250000, draws/eye=66+0=66/100, materials=27/50, estimated SetPass<=66/100
PASS yaw=90, pitch=0: triangles=171,185/250000, draws/eye=26+0=26/100, materials=19/50, estimated SetPass<=26/100
PASS yaw=135, pitch=0: triangles=161,201/250000, draws/eye=19+0=19/100, materials=18/50, estimated SetPass<=19/100
PASS yaw=180, pitch=0: triangles=157,066/250000, draws/eye=18+0=18/100, materials=17/50, estimated SetPass<=18/100
PASS yaw=225, pitch=0: triangles=157,066/250000, draws/eye=18+0=18/100, materials=17/50, estimated SetPass<=18/100
PASS yaw=270, pitch=0: triangles=170,673/250000, draws/eye=24+0=24/100, materials=20/50, estimated SetPass<=24/100
PASS yaw=315, pitch=0: triangles=198,813/250000, draws/eye=65+0=65/100, materials=27/50, estimated SetPass<=65/100
PASS yaw=0, pitch=30: triangles=201,509/250000, draws/eye=72+0=72/100, materials=26/50, estimated SetPass<=72/100
PASS yaw=45, pitch=30: triangles=193,133/250000, draws/eye=63+0=63/100, materials=27/50, estimated SetPass<=63/100
PASS yaw=90, pitch=30: triangles=170,201/250000, draws/eye=25+0=25/100, materials=17/50, estimated SetPass<=25/100
PASS yaw=135, pitch=30: triangles=159,969/250000, draws/eye=18+0=18/100, materials=17/50, estimated SetPass<=18/100
PASS yaw=180, pitch=30: triangles=155,834/250000, draws/eye=17+0=17/100, materials=16/50, estimated SetPass<=17/100
PASS yaw=225, pitch=30: triangles=159,969/250000, draws/eye=18+0=18/100, materials=17/50, estimated SetPass<=18/100
PASS yaw=270, pitch=30: triangles=173,773/250000, draws/eye=23+0=23/100, materials=17/50, estimated SetPass<=23/100
PASS yaw=315, pitch=30: triangles=198,077/250000, draws/eye=63+0=63/100, materials=27/50, estimated SetPass<=63/100
Maximum across views: 203,141 triangles, 74 draws/eye, 27 materials, estimated SetPass<=74.
Largest renderers (whole scene): CherryMesh=22,273 tri/1 draw, GentleGiantMesh=20,000 tri/1 draw, BlueGentlemanMesh=19,997 tri/1 draw, LavenderWomanMesh=19,992 tri/1 draw, RedGrouchMesh=19,972 tri/1 draw, Furniture_3=17,560 tri/1 draw, Furniture_5=10,644 tri/1 draw, Furniture_16=6,912 tri/1 draw
Most draws (whole scene): Pupil_L=5 color/0 shadow, Pupil_R=5 color/0 shadow, Mouth=3 color/0 shadow, EyeWhite_L=2 color/0 shadow, Lid_L=2 color/0 shadow, EyeWhite_R=2 color/0 shadow, Lid_R=2 color/0 shadow, HeadGear=2 color/0 shadow, CandyExit=1 color/0 shadow, CandyGlobe=1 color/0 shadow, Dispenser_Blue=1 color/0 shadow, Dispenser_Gold=1 color/0 shadow

RESULT: PASS static budget
```

## PlayMode performance (Logs/ci/perf.txt)

```
BOB-022 | Editor 6000.6.3f1 | NVIDIA GeForce RTX 3050 Laptop GPU | 2026-10-20T11:35:59.5443224Z
GC = ProfilerRecorder Memory/GC Allocated In Frame (bytes, whole frame including Editor/Test Runner).
Draw/SetPass = max(ProfilerRecorder Render Draw Calls/SetPass Calls Count, UnityStats), whole mono Editor render, NOT XR draws/eye or proof of 72 FPS on Quest.
Editor -batchmode does not draw the Game view: the main camera is rendered manually to a RenderTexture every measured frame (GC/frame includes that render).
Input timer off, tempo=4, timeScale=1, 5 bots; dispenser/inventory contents controlled by the fixture. Animation/gameplay components stay active.
BoB_Art3D before/after: 100/100 color slots, 0/0 shadow slots.
SDK visuals replaced by local avatar: 0 color slots hidden (including dynamic hand meshes); sensors/anchors preserved.
Character/decor shadows disabled. Skinned slots with matching materials combined without changing vertices/blendshapes; furniture grouped by material/lightmap/layer/probe; colliders and characters preserved.
camera=Desktop Art Preview, FOV=60.0, pixel=640x480; quality=Mobile
idle_telemetry_on: frames=243, gc_samples=243, gc_avg_B=7890.40, gc_peak_B=21453, draw_avg=74.00, draw_peak=74, setpass_avg=34.00, setpass_peak=34, frame_avg_ms=12.356, frame_peak_ms=22.695
idle_telemetry_off: frames=248, gc_samples=248, gc_avg_B=7863.01, gc_peak_B=15877, draw_avg=74.00, draw_peak=74, setpass_avg=34.00, setpass_peak=34, frame_avg_ms=12.130, frame_peak_ms=13.927
idle_gc_delta_on_minus_off_B_per_frame=27.38
idle_gc_near_zero_1KiB=False; raw numbers, Editor overhead not subtracted.
props_unchanged_instance_id=58047:11520; unchanged after PlasticFork and Eat.
BoB.RefreshProps=n/a; marker unavailable, prop identity tested separately.
automated_match: frames=1088, gc_samples=1088, gc_avg_B=8993.99, gc_peak_B=188060, draw_avg=76.26, draw_peak=81, setpass_avg=37.88, setpass_peak=43, frame_avg_ms=15.928, frame_peak_ms=98.983
match_over=true, turns=11, telemetry_rows=15; CSV=Logs/ci/perf-telemetry.csv
session,seed,players,round,turn,seat,player,is_bot,action,detail,think_seconds,turn_think_seconds,hp_after,timestamp
20261020_183600,100,6,1,1,0,You,0,ITEM,Plastic Fork,9.82,9.82,4,2026-10-20T18:36:12
20261020_183600,100,6,1,11,3,Peach,1,GAME_OVER,rounds=1;turns=11;winner=Peach;real_seconds=29.9,0.00,0.00,2,2026-10-20T18:36:30
status=complete
```

## Trailer clips (BOB-052)

Code commit `6ef066dfe1e4ea7c0171c3183ce9978da92b1cc3` · laptop folder `C:\Users\acer\bob-ci\videos\6ef066dfe1e4ea7c0171c3183ce9978da92b1cc3` (not in the repo) · 1080p 30 fps, English UI, no audio.

- Trailer tests: 6/6 passed, 0 failed, 0 skipped; exit=0

| Clip | Scene | Duration | File | Size |
|---|---|---|---|---|
| 03-gameplay-item | Gameplay with items from the seat: Plastic Fork reveal, eat, Hammer Crush, Grappling Hook steal, Little Pocket pick, bots' turns | 62.8 s (1883 frames) | `03-gameplay-item.mp4` | 63.0 MB |
| 01-menu | Main menu from the seat: Play vs Bots, Create Room, Join Room, Settings | 7.0 s (210 frames) | `01-menu.mp4` | 7.1 MB |
| 02-bot-setup | Table setup: bot count 1-5, per-bot level Easy/Normal/Hard, Start, d20 turn order | 18.3 s (550 frames) | `02-bot-setup.mp4` | 19.1 MB |
| 06-lobby-online | Online lobby: Create Room, 6-digit room code, players in room, empty seats filled by bots | 11.9 s (357 frames) | `06-lobby-online.mp4` | 12.5 MB |
| 07-close-up-bot | Bot close-up camera: bots eating, giving and reacting | 45.0 s (1351 frames) | `07-close-up-bot.mp4` | 34.0 MB |
| 08-top-down | Top-down table camera, slow orbit | 30.0 s (901 frames) | `08-top-down.mp4` | 30.6 MB |
| 04-spectator | Spectator: knocked-out player turns transparent and keeps watching from the seat | 15.6 s (467 frames) | `04-spectator.mp4` | 16.0 MB |
| 05-results | Results: last bots standing, winner card with knockout order, Play again / Back to menu | 156.0 s (4680 frames) | `05-results.mp4` | 153.9 MB |

Total 346.6 s of video.

RC archive for this commit: [manifest and full evidence](../../Builds/reports/RC/6ef066dfe1e4ea7c0171c3183ce9978da92b1cc3/manifest.json).
