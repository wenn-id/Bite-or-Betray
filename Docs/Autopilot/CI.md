# Unity CI (laptop runner)

Commit `573fd2a8a093eb81c12f67db6472f828c7c6dcbc` · 2026-11-17 23:11 WIB · **PASSED**

| Check | Result |
|---|---|
| EditMode tests | 141/141 passed, 0 failed, 0 skipped; exit=0 |
| PlayMode tests (real scene) | 74/74 passed, 0 failed, 0 skipped; exit=0 |
| Build APK Release | Succeeded; mode=Release; errors=0; warnings=26; bytes=132215050; duration=1198.4s; path=C:\Users\acer\bob-ci\repo\Builds\BiteOrBetray_Quest_Release.apk; report=C:\Users\acer\bob-ci\repo\Builds\reports\BiteOrBetray_Quest_Release_20261117-225114_573fd2a8a093eb81c12f67db6472f828c7c6dcbc-dirty.txt |
| Shader errors | 0 |
| Compile errors | 0 |
| Budget Audit | PASSED |

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
Renderers counted=85, disabled/forceRenderingOff=303; active renderers without available geometry fail the audit.
PASS: Realtime lights=7, shadows=1 (at most one directional)
PASS: Complete geometry/materials (unknown=0)
PASS: Room BatchingStatic renderers (27)
Whole scene: 262,171 triangles, 85 color draws; conservative shadows 0 draws/0 triangles.
Local avatar head (hidden from first-person camera): 1 renderers/1 draws excluded per eye.
Skinned mesh: 18 | vertex: 90,262
Unique scene materials: 43, instancing enabled: 38 (SkinnedMeshRenderer does not use GPU instancing; SRP/static batching may be preferred).
Scene textures: 33, estimated Editor memory 100.8 MB; transparent materials: 0
Shader: Universal Render Pipeline/Lit, BiteOrBetray/ChibiFace, Universal Render Pipeline/Unlit
Volume post-process: 1
PASS yaw=0, pitch=-30: triangles=246,730/250000, draws/eye=84+0=84/100, materials=42/50, estimated SetPass<=84/100
PASS yaw=45, pitch=-30: triangles=172,049/250000, draws/eye=64+0=64/100, materials=39/50, estimated SetPass<=64/100
PASS yaw=90, pitch=-30: triangles=106,052/250000, draws/eye=23+0=23/100, materials=21/50, estimated SetPass<=23/100
PASS yaw=135, pitch=-30: triangles=43,152/250000, draws/eye=13+0=13/100, materials=13/50, estimated SetPass<=13/100
PASS yaw=180, pitch=-30: triangles=24,172/250000, draws/eye=10+0=10/100, materials=10/50, estimated SetPass<=10/100
PASS yaw=225, pitch=-30: triangles=43,384/250000, draws/eye=13+0=13/100, materials=13/50, estimated SetPass<=13/100
PASS yaw=270, pitch=-30: triangles=93,271/250000, draws/eye=21+0=21/100, materials=20/50, estimated SetPass<=21/100
PASS yaw=315, pitch=-30: triangles=163,856/250000, draws/eye=62+0=62/100, materials=39/50, estimated SetPass<=62/100
PASS yaw=0, pitch=0: triangles=246,650/250000, draws/eye=83+0=83/100, materials=42/50, estimated SetPass<=83/100
PASS yaw=45, pitch=0: triangles=175,391/250000, draws/eye=67+0=67/100, materials=40/50, estimated SetPass<=67/100
PASS yaw=90, pitch=0: triangles=112,248/250000, draws/eye=25+0=25/100, materials=21/50, estimated SetPass<=25/100
PASS yaw=135, pitch=0: triangles=43,152/250000, draws/eye=13+0=13/100, materials=13/50, estimated SetPass<=13/100
PASS yaw=180, pitch=0: triangles=18,772/250000, draws/eye=9+0=9/100, materials=9/50, estimated SetPass<=9/100
PASS yaw=225, pitch=0: triangles=43,384/250000, draws/eye=13+0=13/100, materials=13/50, estimated SetPass<=13/100
PASS yaw=270, pitch=0: triangles=99,467/250000, draws/eye=23+0=23/100, materials=20/50, estimated SetPass<=23/100
PASS yaw=315, pitch=0: triangles=168,234/250000, draws/eye=65+0=65/100, materials=39/50, estimated SetPass<=65/100
PASS yaw=0, pitch=30: triangles=237,638/250000, draws/eye=81+0=81/100, materials=41/50, estimated SetPass<=81/100
PASS yaw=45, pitch=30: triangles=181,071/250000, draws/eye=67+0=67/100, materials=39/50, estimated SetPass<=67/100
PASS yaw=90, pitch=30: triangles=96,818/250000, draws/eye=20+0=20/100, materials=18/50, estimated SetPass<=20/100
PASS yaw=135, pitch=30: triangles=25,070/250000, draws/eye=11+0=11/100, materials=11/50, estimated SetPass<=11/100
PASS yaw=180, pitch=30: triangles=21,220/250000, draws/eye=9+0=9/100, materials=9/50, estimated SetPass<=9/100
PASS yaw=225, pitch=30: triangles=25,070/250000, draws/eye=11+0=11/100, materials=11/50, estimated SetPass<=11/100
PASS yaw=270, pitch=30: triangles=90,717/250000, draws/eye=20+0=20/100, materials=18/50, estimated SetPass<=20/100
PASS yaw=315, pitch=30: triangles=168,222/250000, draws/eye=64+0=64/100, materials=38/50, estimated SetPass<=64/100
Maximum across views: 246,730 triangles, 84 draws/eye, 42 materials, estimated SetPass<=84.
Largest renderers (whole scene): Navy=15,827 tri/1 draw, Mint=15,803 tri/1 draw, Silver=15,760 tri/1 draw, Peach=15,680 tri/1 draw, Charcoal=15,528 tri/1 draw, Blond=15,441 tri/1 draw, Reaper=12,000 tri/1 draw, Furniture_0=11,964 tri/1 draw
Most draws (whole scene): CandyTray_Gold=1 color/0 shadow, TrayBottom=1 color/0 shadow, Candy_Yellow_Milk=1 color/0 shadow, Candy_Yellow_Yellow=1 color/0 shadow, Blond=1 color/0 shadow, HandLeftMesh=1 color/0 shadow, HandRightMesh=1 color/0 shadow, Silver=1 color/0 shadow, HandLeftMesh=1 color/0 shadow, HandRightMesh=1 color/0 shadow, Mint=1 color/0 shadow, HandLeftMesh=1 color/0 shadow

== QuestPerfTest ==
Renderers counted=86, disabled/forceRenderingOff=303; active renderers without available geometry fail the audit.
PASS: Realtime lights=7, shadows=1 (at most one directional)
PASS: Complete geometry/materials (unknown=0)
PASS: Room BatchingStatic renderers (27)
Whole scene: 262,227 triangles, 86 color draws; conservative shadows 0 draws/0 triangles.
Local avatar head (hidden from first-person camera): 1 renderers/1 draws excluded per eye.
Skinned mesh: 18 | vertex: 90,262
Unique scene materials: 44, instancing enabled: 38 (SkinnedMeshRenderer does not use GPU instancing; SRP/static batching may be preferred).
Scene textures: 34, estimated Editor memory 100.9 MB; transparent materials: 1
Shader: Universal Render Pipeline/Lit, BiteOrBetray/ChibiFace, Universal Render Pipeline/Unlit, GUI/Text Shader
Volume post-process: 1
PASS yaw=0, pitch=-30: triangles=246,786/250000, draws/eye=85+0=85/100, materials=43/50, estimated SetPass<=85/100
PASS yaw=45, pitch=-30: triangles=172,105/250000, draws/eye=65+0=65/100, materials=40/50, estimated SetPass<=65/100
PASS yaw=90, pitch=-30: triangles=106,052/250000, draws/eye=23+0=23/100, materials=21/50, estimated SetPass<=23/100
PASS yaw=135, pitch=-30: triangles=43,152/250000, draws/eye=13+0=13/100, materials=13/50, estimated SetPass<=13/100
PASS yaw=180, pitch=-30: triangles=24,172/250000, draws/eye=10+0=10/100, materials=10/50, estimated SetPass<=10/100
PASS yaw=225, pitch=-30: triangles=43,384/250000, draws/eye=13+0=13/100, materials=13/50, estimated SetPass<=13/100
PASS yaw=270, pitch=-30: triangles=93,271/250000, draws/eye=21+0=21/100, materials=20/50, estimated SetPass<=21/100
PASS yaw=315, pitch=-30: triangles=163,912/250000, draws/eye=63+0=63/100, materials=40/50, estimated SetPass<=63/100
PASS yaw=0, pitch=0: triangles=246,706/250000, draws/eye=84+0=84/100, materials=43/50, estimated SetPass<=84/100
PASS yaw=45, pitch=0: triangles=175,447/250000, draws/eye=68+0=68/100, materials=41/50, estimated SetPass<=68/100
PASS yaw=90, pitch=0: triangles=112,248/250000, draws/eye=25+0=25/100, materials=21/50, estimated SetPass<=25/100
PASS yaw=135, pitch=0: triangles=43,152/250000, draws/eye=13+0=13/100, materials=13/50, estimated SetPass<=13/100
PASS yaw=180, pitch=0: triangles=18,772/250000, draws/eye=9+0=9/100, materials=9/50, estimated SetPass<=9/100
PASS yaw=225, pitch=0: triangles=43,384/250000, draws/eye=13+0=13/100, materials=13/50, estimated SetPass<=13/100
PASS yaw=270, pitch=0: triangles=99,467/250000, draws/eye=23+0=23/100, materials=20/50, estimated SetPass<=23/100
PASS yaw=315, pitch=0: triangles=168,290/250000, draws/eye=66+0=66/100, materials=40/50, estimated SetPass<=66/100
PASS yaw=0, pitch=30: triangles=237,694/250000, draws/eye=82+0=82/100, materials=42/50, estimated SetPass<=82/100
PASS yaw=45, pitch=30: triangles=181,127/250000, draws/eye=68+0=68/100, materials=40/50, estimated SetPass<=68/100
PASS yaw=90, pitch=30: triangles=96,818/250000, draws/eye=20+0=20/100, materials=18/50, estimated SetPass<=20/100
PASS yaw=135, pitch=30: triangles=25,070/250000, draws/eye=11+0=11/100, materials=11/50, estimated SetPass<=11/100
PASS yaw=180, pitch=30: triangles=21,220/250000, draws/eye=9+0=9/100, materials=9/50, estimated SetPass<=9/100
PASS yaw=225, pitch=30: triangles=25,070/250000, draws/eye=11+0=11/100, materials=11/50, estimated SetPass<=11/100
PASS yaw=270, pitch=30: triangles=90,717/250000, draws/eye=20+0=20/100, materials=18/50, estimated SetPass<=20/100
PASS yaw=315, pitch=30: triangles=168,278/250000, draws/eye=65+0=65/100, materials=39/50, estimated SetPass<=65/100
Maximum across views: 246,786 triangles, 85 draws/eye, 43 materials, estimated SetPass<=85.
Largest renderers (whole scene): Navy=15,827 tri/1 draw, Mint=15,803 tri/1 draw, Silver=15,760 tri/1 draw, Peach=15,680 tri/1 draw, Charcoal=15,528 tri/1 draw, Blond=15,441 tri/1 draw, Reaper=12,000 tri/1 draw, Furniture_0=11,964 tri/1 draw
Most draws (whole scene): CandyTray_Gold=1 color/0 shadow, TrayBottom=1 color/0 shadow, Candy_Yellow_Milk=1 color/0 shadow, Candy_Yellow_Yellow=1 color/0 shadow, Blond=1 color/0 shadow, HandLeftMesh=1 color/0 shadow, HandRightMesh=1 color/0 shadow, Silver=1 color/0 shadow, HandLeftMesh=1 color/0 shadow, HandRightMesh=1 color/0 shadow, Mint=1 color/0 shadow, HandLeftMesh=1 color/0 shadow

RESULT: PASS static budget
```

## PlayMode performance (Logs/ci/perf.txt)

```
BOB-022 | Editor 6000.6.3f1 | NVIDIA GeForce RTX 3050 Laptop GPU | 2026-11-17T15:42:41.0829413Z
GC = ProfilerRecorder Memory/GC Allocated In Frame (bytes, whole frame including Editor/Test Runner).
Draw/SetPass = max(ProfilerRecorder Render Draw Calls/SetPass Calls Count, UnityStats), whole mono Editor render, NOT XR draws/eye or proof of 72 FPS on Quest.
Editor -batchmode does not draw the Game view: the main camera is rendered manually to a RenderTexture every measured frame (GC/frame includes that render).
Input timer off, tempo=4, timeScale=1, 5 bots; dispenser/inventory contents controlled by the fixture. Animation/gameplay components stay active.
BoB_Art3D before/after: 85/85 color slots, 0/0 shadow slots.
SDK visuals replaced by local avatar: 0 color slots hidden (including dynamic hand meshes); sensors/anchors preserved.
Character/decor shadows disabled. Skinned slots with matching materials combined without changing vertices/blendshapes; furniture grouped by material/lightmap/layer/probe; colliders and characters preserved.
camera=Desktop Art Preview, FOV=52.0, pixel=640x480; quality=Mobile
idle_telemetry_on: frames=354, gc_samples=354, gc_avg_B=5843.20, gc_peak_B=13926, draw_avg=84.00, draw_peak=84, setpass_avg=37.00, setpass_peak=37, frame_avg_ms=8.482, frame_peak_ms=12.851
idle_telemetry_off: frames=357, gc_samples=357, gc_avg_B=5835.39, gc_peak_B=13926, draw_avg=84.00, draw_peak=84, setpass_avg=37.00, setpass_peak=37, frame_avg_ms=8.413, frame_peak_ms=10.864
idle_gc_delta_on_minus_off_B_per_frame=7.81
idle_gc_near_zero_1KiB=False; raw numbers, Editor overhead not subtracted.
props_unchanged_instance_id=70306:1792; unchanged after PlasticFork and Eat.
BoB.RefreshProps=n/a; marker unavailable, prop identity tested separately.
automated_match: frames=1780, gc_samples=1780, gc_avg_B=6133.11, gc_peak_B=51904, draw_avg=85.31, draw_peak=90, setpass_avg=40.66, setpass_peak=46, frame_avg_ms=9.955, frame_peak_ms=31.222
match_over=true, turns=11, telemetry_rows=15; CSV=Logs/ci/perf-telemetry.csv
session,seed,players,round,turn,seat,player,is_bot,action,detail,think_seconds,turn_think_seconds,hp_after,timestamp
20261117_224241,100,6,1,1,0,You,0,ITEM,Plastic Fork,9.57,9.57,4,2026-11-17T22:42:53
20261117_224241,100,6,1,11,3,Charcoal,1,GAME_OVER,rounds=1;turns=11;winner=Charcoal;real_seconds=29.9,0.00,0.00,2,2026-11-17T22:43:11
status=complete
```

RC archive for this commit: [manifest and full evidence](../../Builds/reports/RC/573fd2a8a093eb81c12f67db6472f828c7c6dcbc/manifest.json).
