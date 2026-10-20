# Unity CI (laptop runner)

Commit `9a3fe3afe4044b731617b37e39057f726af4965d` · 2026-10-20 11:19 WIB · **FAILED**

| Check | Result |
|---|---|
| EditMode tests | 139/139 passed, 0 failed, 0 skipped; exit=0 |
| PlayMode tests (real scene) | 34/37 passed, 3 failed, 0 skipped; exit=2 |
| Build APK Release | Failed; mode=Release; errors=1; warnings=23; bytes=134296022; duration=262.3s; path=C:\Users\acer\bob-ci\repo\Builds\BiteOrBetray_Quest_Release.apk; report=C:\Users\acer\bob-ci\repo\Builds\reports\BiteOrBetray_Quest_Release_20261020-111503_9a3fe3afe4044b731617b37e39057f726af4965d-dirty.txt |
| Shader errors | 0 |
| Compile errors | 0 |
| Budget Audit | PASSED |

- PlayMode: incomplete results / Unity exit 2

- Build Release: Failed; mode=Release; errors=1; warnings=23; bytes=134296022; duration=262.3s; path=C:\Users\acer\bob-ci\repo\Builds\BiteOrBetray_Quest_Release.apk; report=C:\Users\acer\bob-ci\repo\Builds\reports\BiteOrBetray_Quest_Release_20261020-111503_9a3fe3afe4044b731617b37e39057f726af4965d-dirty.txt; exit=1

## Failed tests

- BiteOrBetray.Tests.PlayMode.RegressionTests.Menu_bot_setup_all_onboarding_pages_and_gameplay_with_screens: 02-atur-bot: Button Yellow (x:0.87, y:0.73, width:0.05, height:0.06) covers bot head Bot_Yellow (x:0.66, y:0.53, width:0.22, height:0.23)
  Expected: False
  But was:  True
- BiteOrBetray.Tests.PlayMode.RegressionTests.Three_candy_colors_and_clover_damage_cap_follow_rules: Expected: True
  But was:  False
- BiteOrBetray.Tests.PlayMode.RegressionTests.UI_labels_do_not_overlap_in_menu_gameplay_spectator_and_results: Expected: String containing "dispenser geometry"
  But was:  "  dispenser control: Narration (x:0.03, y:0.48, width:0.31, height:0.06) covers bot head Bot_Peach (x:0.03, y:0.50, width:0.30, height:0.35)
  Expected: False
  But was:  True
"

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
BOB-022 | Editor 6000.6.3f1 | NVIDIA GeForce RTX 3050 Laptop GPU | 2026-10-20T03:36:03.8935211Z
GC = ProfilerRecorder Memory/GC Allocated In Frame (byte, seluruh frame termasuk Editor/Test Runner).
Draw/SetPass = maks(ProfilerRecorder Render Draw Calls/SetPass Calls Count, UnityStats), seluruh render Editor mono, BUKAN draw/mata XR atau bukti 72 FPS Quest.
Editor -batchmode tidak menggambar Game view: kamera utama dirender manual ke RenderTexture tiap frame ukur (GC/frame termasuk render itu).
Timer input mati, tempo=4, timeScale=1, 5 bot; isi dispenser/inventory dikontrol fixture. Animasi/komponen gameplay tetap aktif.
BoB_Art3D before/after: 100/100 color slots, 0/0 shadow slots.
SDK visuals replaced by local avatar: 0 color slots hidden (including dynamic hand meshes); sensors/anchors preserved.
Character/decor shadows disabled. Skinned slots with matching materials combined without changing vertices/blendshapes; furniture grouped by material/lightmap/layer/probe; colliders and characters preserved.
camera=Desktop Art Preview, FOV=60.0, pixel=640x480; quality=Mobile
idle_telemetry_on: frames=252, gc_samples=252, gc_avg_B=7855.54, gc_peak_B=15877, draw_avg=74.00, draw_peak=74, setpass_avg=34.00, setpass_peak=34, frame_avg_ms=11.929, frame_peak_ms=33.909
idle_telemetry_off: frames=243, gc_samples=243, gc_avg_B=7884.91, gc_peak_B=16519, draw_avg=74.00, draw_peak=74, setpass_avg=34.00, setpass_peak=34, frame_avg_ms=12.355, frame_peak_ms=14.565
idle_gc_delta_on_minus_off_B_per_frame=-29.37
idle_gc_near_zero_1KiB=False; angka mentah, tanpa mengurangi overhead Editor.
props_unchanged_instance_id=58047:12032; tetap setelah PlasticFork dan Eat.
BoB.RefreshProps=n/a; marker tidak tersedia, identitas prop diuji terpisah.
automated_match: frames=1099, gc_samples=1099, gc_avg_B=8994.47, gc_peak_B=188077, draw_avg=76.22, draw_peak=81, setpass_avg=37.90, setpass_peak=43, frame_avg_ms=15.752, frame_peak_ms=46.439
match_over=true, turns=11, telemetry_rows=15; CSV=Logs/ci/perf-telemetry.csv
session,seed,players,round,turn,seat,player,is_bot,action,detail,think_seconds,turn_think_seconds,hp_after,timestamp
20261020_103604,100,6,1,1,0,You,0,ITEM,Plastic Fork,9.83,9.83,4,2026-10-20T10:36:16
20261020_103604,100,6,1,11,3,Peach,1,GAME_OVER,rounds=1;turns=11;winner=Peach;real_seconds=29.8,0.00,0.00,2,2026-10-20T10:36:34
status=complete
```

## Trailer clips (BOB-052)

Code commit `4e2dd8b8ef8e0df5d28634da4c44ce21636bffe3` · laptop folder `C:\Users\acer\bob-ci\videos\4e2dd8b8ef8e0df5d28634da4c44ce21636bffe3` (not in the repo) · 1080p 30 fps, English UI, no audio.

- Trailer tests: 6/6 passed, 0 failed, 0 skipped; exit=0

| Clip | Scene | Duration | File | Size |
|---|---|---|---|---|
| 03-gameplay-item | Gameplay with items from the seat: Plastic Fork reveal, eat, Hammer Crush, Grappling Hook steal, Little Pocket pick, bots' turns | 62.8 s (1883 frames) | `03-gameplay-item.mp4` | 63.1 MB |
| 06-lobby-online | Online lobby: Create Room, 6-digit room code, players in room, empty seats filled by bots | 11.7 s (352 frames) | `06-lobby-online.mp4` | 12.2 MB |
| 01-menu | Main menu from the seat: Play vs Bots, Create Room, Join Room, Settings | 7.0 s (210 frames) | `01-menu.mp4` | 7.1 MB |
| 02-atur-bot | Table setup: bot count 1-5, per-bot level Easy/Normal/Hard, Start, d20 turn order | 18.3 s (550 frames) | `02-atur-bot.mp4` | 19.1 MB |
| 07-close-up-bot | Bot close-up camera: bots eating, giving and reacting | 45.0 s (1351 frames) | `07-close-up-bot.mp4` | 31.6 MB |
| 08-meja-atas | Top-down table camera, slow orbit | 30.0 s (901 frames) | `08-meja-atas.mp4` | 30.4 MB |
| 04-spectator | Spectator: knocked-out player turns transparent and keeps watching from the seat | 15.6 s (467 frames) | `04-spectator.mp4` | 15.9 MB |
| 05-hasil-akhir | Results: last bots standing, winner card with knockout order, Play again / Back to menu | 156.0 s (4681 frames) | `05-hasil-akhir.mp4` | 153.9 MB |

Total 346.5 s of video.

RC archive for this commit: [manifest and full evidence](../../Builds/reports/RC/9a3fe3afe4044b731617b37e39057f726af4965d/manifest.json).
