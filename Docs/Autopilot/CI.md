# CI Unity (runner laptop)

Commit `86c2bcb` · 2026-10-17 10:40 WIB · **GAGAL**

| Cek | Hasil |
|---|---|
| EditMode tests | 111/111 lulus, 0 gagal |
| PlayMode tests (scene sungguhan) | 8/17 lulus, 9 gagal |
| Build APK Development | Succeeded; mode=Development; errors=0; warnings=6; bytes=1785206947; durasi=196.9s; path=C:\Users\acer\bob-ci\repo\Builds\BiteOrBetray_Quest_Development.apk; laporan=C:\Users\acer\bob-ci\repo\Builds\reports\BiteOrBetray_Quest_Development_20261017-103629_86c2bcb-dirty.txt |
| Error shader | 0 |
| Error compile | 0 |

## Tes gagal

- BiteOrBetray.Tests.PlayMode.GameplayPerformanceTests.Gameplay_idle_gc_render_stats_and_automated_match_are_recorded: UnityStats tidak mencatat render; jalankan tanpa -nographics.
  Expected: greater than 0
  But was:  0
- BiteOrBetray.Tests.PlayMode.RegressionTests.Controller_path_works_and_pinch_never_doubles_input: SetUp : System.NullReferenceException : Object reference not set to an instance of an object
- BiteOrBetray.Tests.PlayMode.RegressionTests.Eight_items_follow_game_engine_rules: SetUp : System.NullReferenceException : Object reference not set to an instance of an object
- BiteOrBetray.Tests.PlayMode.RegressionTests.Five_restarts_keep_objects_materials_audio_sources_and_clips_stable: SetUp : System.NullReferenceException : Object reference not set to an instance of an object
- BiteOrBetray.Tests.PlayMode.RegressionTests.Focus_lost_and_regained_cycles_freeze_timer_and_restore_input: SetUp : System.NullReferenceException : Object reference not set to an instance of an object
- BiteOrBetray.Tests.PlayMode.RegressionTests.Ko_spectator_results_music_sfx_and_telemetry_csv_with_screens: SetUp : System.NullReferenceException : Object reference not set to an instance of an object
- BiteOrBetray.Tests.PlayMode.RegressionTests.Menu_bot_setup_all_onboarding_pages_and_gameplay_with_screens: SetUp : System.NullReferenceException : Object reference not set to an instance of an object
- BiteOrBetray.Tests.PlayMode.RegressionTests.New_round_refills_dispenser_and_deals_items: SetUp : System.NullReferenceException : Object reference not set to an instance of an object
- BiteOrBetray.Tests.PlayMode.RegressionTests.Three_candy_colors_and_clover_damage_cap_follow_rules: SetUp : System.NullReferenceException : Object reference not set to an instance of an object

## Budget Audit

```
== Budget Quest konservatif (estimasi statis, bukan bukti 72 FPS) ==
Target per pandangan/mata: <= 250,000 segitiga, <= 100 draw termasuk shadow, <= 50 material, <= 100 estimasi SetPass.
FOV 110, 8 arah yaw x 3 pitch (-30/0/30) dari Seat_0, tinggi 1,15 m; tanpa occlusion/LOD/batching discount.
Satu draw warna per slot/submesh; seluruh caster scene dihitung sekali untuk shadow main light (juga dialokasikan penuh ke tiap mata). 
SetPass = batas atas satu pergantian per draw, BUKAN pengukuran GPU; SRP Batcher/instancing tidak diasumsikan mengurangi draw.
Objek/animasi/UI/partikel yang muncul saat Play belum tercakup: cocokkan Frame Debugger/Profiler saat gameplay di laptop.

Mobile RP: scale=1, MSAA=4x, HDR=False, shadow=512/6m/1 cascade, addLights=PerVertex, additionalShadow=False, depth=False, opaque=False, SRPBatcher=True
LULUS: URP konservatif
LULUS: Mobile renderer tanpa SSAO/renderer feature
Quality[0] Mobile: RP=Assets/Settings/Mobile_RPAsset.asset
LULUS: Pipeline kualitas Quest
Quality[1] PC: RP=Assets/Settings/PC_RPAsset.asset
Quality[2] Meta Quest (Build Profile): RP=Assets/Settings/Mobile_RPAsset.asset
LULUS: Pipeline kualitas Quest
LULUS: Static batching Android=True
Runtime VRTableInput: OVRManager FFR High + dynamic, eye-tracked off, dynamic resolution off, skala XR 1.0; periksa saat Android XR aktif.

== CandyParlour3D ==
LULUS: Lampu realtime=1, shadow=1 (maks. satu directional)
LULUS: Geometri/material lengkap (tidak diketahui=0)
LULUS: Renderer Room BatchingStatic (14)
Seluruh scene: 217,517 segitiga, 100 draw warna; shadow konservatif 11 draw/108,157 segitiga.
Skinned mesh: 9 | vertex: 109,165
Material scene unik: 36, instancing aktif: 36 (SkinnedMeshRenderer tidak memakai GPU instancing; SRP/static batching dapat diprioritaskan).
Tekstur scene: 19, memori Editor perkiraan 20.9 MB; material transparan: 1
Shader: Universal Render Pipeline/Lit
Volume post-process: 1
GAGAL yaw=0, pitch=-30: segitiga=217,517/250000, draw/mata=100+11=111/100, material=36/50, estimasi SetPass<=111/100
LULUS yaw=45, pitch=-30: segitiga=206,317/250000, draw/mata=86+11=97/100, material=35/50, estimasi SetPass<=97/100
LULUS yaw=90, pitch=-30: segitiga=181,907/250000, draw/mata=35+11=46/100, material=28/50, estimasi SetPass<=46/100
LULUS yaw=135, pitch=-30: segitiga=170,693/250000, draw/mata=25+11=36/100, material=23/50, estimasi SetPass<=36/100
LULUS yaw=180, pitch=-30: segitiga=170,693/250000, draw/mata=25+11=36/100, material=23/50, estimasi SetPass<=36/100
LULUS yaw=225, pitch=-30: segitiga=170,693/250000, draw/mata=25+11=36/100, material=23/50, estimasi SetPass<=36/100
LULUS yaw=270, pitch=-30: segitiga=181,907/250000, draw/mata=35+11=46/100, material=28/50, estimasi SetPass<=46/100
LULUS yaw=315, pitch=-30: segitiga=211,013/250000, draw/mata=87+11=98/100, material=35/50, estimasi SetPass<=98/100
GAGAL yaw=0, pitch=0: segitiga=217,117/250000, draw/mata=99+11=110/100, material=36/50, estimasi SetPass<=110/100
GAGAL yaw=45, pitch=0: segitiga=208,129/250000, draw/mata=91+11=102/100, material=36/50, estimasi SetPass<=102/100
LULUS yaw=90, pitch=0: segitiga=182,335/250000, draw/mata=36+11=47/100, material=26/50, estimasi SetPass<=47/100
LULUS yaw=135, pitch=0: segitiga=170,693/250000, draw/mata=25+11=36/100, material=23/50, estimasi SetPass<=36/100
LULUS yaw=180, pitch=0: segitiga=170,693/250000, draw/mata=25+11=36/100, material=23/50, estimasi SetPass<=36/100
LULUS yaw=225, pitch=0: segitiga=170,693/250000, draw/mata=25+11=36/100, material=23/50, estimasi SetPass<=36/100
LULUS yaw=270, pitch=0: segitiga=181,823/250000, draw/mata=34+11=45/100, material=27/50, estimasi SetPass<=45/100
GAGAL yaw=315, pitch=0: segitiga=213,105/250000, draw/mata=90+11=101/100, material=36/50, estimasi SetPass<=101/100
GAGAL yaw=0, pitch=30: segitiga=215,717/250000, draw/mata=96+11=107/100, material=34/50, estimasi SetPass<=107/100
LULUS yaw=45, pitch=30: segitiga=207,341/250000, draw/mata=87+11=98/100, material=35/50, estimasi SetPass<=98/100
LULUS yaw=90, pitch=30: segitiga=181,351/250000, draw/mata=35+11=46/100, material=25/50, estimasi SetPass<=46/100
LULUS yaw=135, pitch=30: segitiga=169,461/250000, draw/mata=24+11=35/100, material=22/50, estimasi SetPass<=35/100
LULUS yaw=180, pitch=30: segitiga=165,326/250000, draw/mata=23+11=34/100, material=21/50, estimasi SetPass<=34/100
LULUS yaw=225, pitch=30: segitiga=169,461/250000, draw/mata=24+11=35/100, material=22/50, estimasi SetPass<=35/100
LULUS yaw=270, pitch=30: segitiga=184,923/250000, draw/mata=33+11=44/100, material=25/50, estimasi SetPass<=44/100
LULUS yaw=315, pitch=30: segitiga=212,285/250000, draw/mata=87+11=98/100, material=35/50, estimasi SetPass<=98/100
Maksimum antar-pandangan: 217,517 segitiga, 111 draw/mata, 36 material, estimasi SetPass<=111.
Renderer terbesar (seluruh scene): CherryMesh=22,273 tri/1 draw, GentleGiantMesh=20,000 tri/1 draw, BlueGentlemanMesh=19,997 tri/1 draw, LavenderWomanMesh=19,992 tri/1 draw, RedGrouchMesh=19,972 tri/1 draw, Furniture_3=17,560 tri/1 draw, Furniture_5=10,644 tri/1 draw, Furniture_16=6,912 tri/1 draw
Draw terbanyak (seluruh scene): Pupil_L=5 warna/0 shadow, Pupil_R=5 warna/0 shadow, Mouth=3 warna/3 shadow, EyeWhite_L=2 warna/0 shadow, Lid_L=2 warna/0 shadow, EyeWhite_R=2 warna/0 shadow, Lid_R=2 warna/0 shadow, HeadGear=2 warna/0 shadow, CandyExit=1 warna/0 shadow, CandyGlobe=1 warna/0 shadow, Dispenser_Blue=1 warna/0 shadow, Dispenser_Gold=1 warna/0 shadow

== QuestPerfTest ==
LULUS: Lampu realtime=1, shadow=1 (maks. satu directional)
LULUS: Geometri/material lengkap (tidak diketahui=0)
LULUS: Renderer Room BatchingStatic (14)
Seluruh scene: 217,575 segitiga, 101 draw warna; shadow konservatif 11 draw/108,157 segitiga.
Skinned mesh: 9 | vertex: 109,165
Material scene unik: 37, instancing aktif: 36 (SkinnedMeshRenderer tidak memakai GPU instancing; SRP/static batching dapat diprioritaskan).
Tekstur scene: 20, memori Editor perkiraan 21.1 MB; material transparan: 2
Shader: Universal Render Pipeline/Lit, GUI/Text Shader
Volume post-process: 1
GAGAL yaw=0, pitch=-30: segitiga=217,575/250000, draw/mata=101+11=112/100, material=37/50, estimasi SetPass<=112/100
LULUS yaw=45, pitch=-30: segitiga=206,375/250000, draw/mata=87+11=98/100, material=36/50, estimasi SetPass<=98/100
LULUS yaw=90, pitch=-30: segitiga=181,907/250000, draw/mata=35+11=46/100, material=28/50, estimasi SetPass<=46/100
LULUS yaw=135, pitch=-30: segitiga=170,693/250000, draw/mata=25+11=36/100, material=23/50, estimasi SetPass<=36/100
LULUS yaw=180, pitch=-30: segitiga=170,693/250000, draw/mata=25+11=36/100, material=23/50, estimasi SetPass<=36/100
LULUS yaw=225, pitch=-30: segitiga=170,693/250000, draw/mata=25+11=36/100, material=23/50, estimasi SetPass<=36/100
LULUS yaw=270, pitch=-30: segitiga=181,907/250000, draw/mata=35+11=46/100, material=28/50, estimasi SetPass<=46/100
LULUS yaw=315, pitch=-30: segitiga=211,071/250000, draw/mata=88+11=99/100, material=36/50, estimasi SetPass<=99/100
GAGAL yaw=0, pitch=0: segitiga=217,175/250000, draw/mata=100+11=111/100, material=37/50, estimasi SetPass<=111/100
GAGAL yaw=45, pitch=0: segitiga=208,187/250000, draw/mata=92+11=103/100, material=37/50, estimasi SetPass<=103/100
LULUS yaw=90, pitch=0: segitiga=182,335/250000, draw/mata=36+11=47/100, material=26/50, estimasi SetPass<=47/100
LULUS yaw=135, pitch=0: segitiga=170,693/250000, draw/mata=25+11=36/100, material=23/50, estimasi SetPass<=36/100
LULUS yaw=180, pitch=0: segitiga=170,693/250000, draw/mata=25+11=36/100, material=23/50, estimasi SetPass<=36/100
LULUS yaw=225, pitch=0: segitiga=170,693/250000, draw/mata=25+11=36/100, material=23/50, estimasi SetPass<=36/100
LULUS yaw=270, pitch=0: segitiga=181,823/250000, draw/mata=34+11=45/100, material=27/50, estimasi SetPass<=45/100
GAGAL yaw=315, pitch=0: segitiga=213,163/250000, draw/mata=91+11=102/100, material=37/50, estimasi SetPass<=102/100
GAGAL yaw=0, pitch=30: segitiga=215,775/250000, draw/mata=97+11=108/100, material=35/50, estimasi SetPass<=108/100
LULUS yaw=45, pitch=30: segitiga=207,399/250000, draw/mata=88+11=99/100, material=36/50, estimasi SetPass<=99/100
LULUS yaw=90, pitch=30: segitiga=181,351/250000, draw/mata=35+11=46/100, material=25/50, estimasi SetPass<=46/100
LULUS yaw=135, pitch=30: segitiga=169,461/250000, draw/mata=24+11=35/100, material=22/50, estimasi SetPass<=35/100
LULUS yaw=180, pitch=30: segitiga=165,326/250000, draw/mata=23+11=34/100, material=21/50, estimasi SetPass<=34/100
LULUS yaw=225, pitch=30: segitiga=169,461/250000, draw/mata=24+11=35/100, material=22/50, estimasi SetPass<=35/100
LULUS yaw=270, pitch=30: segitiga=184,923/250000, draw/mata=33+11=44/100, material=25/50, estimasi SetPass<=44/100
LULUS yaw=315, pitch=30: segitiga=212,343/250000, draw/mata=88+11=99/100, material=36/50, estimasi SetPass<=99/100
Maksimum antar-pandangan: 217,575 segitiga, 112 draw/mata, 37 material, estimasi SetPass<=112.
Renderer terbesar (seluruh scene): CherryMesh=22,273 tri/1 draw, GentleGiantMesh=20,000 tri/1 draw, BlueGentlemanMesh=19,997 tri/1 draw, LavenderWomanMesh=19,992 tri/1 draw, RedGrouchMesh=19,972 tri/1 draw, Furniture_3=17,560 tri/1 draw, Furniture_5=10,644 tri/1 draw, Furniture_16=6,912 tri/1 draw
Draw terbanyak (seluruh scene): Pupil_L=5 warna/0 shadow, Pupil_R=5 warna/0 shadow, Mouth=3 warna/3 shadow, EyeWhite_L=2 warna/0 shadow, Lid_L=2 warna/0 shadow, EyeWhite_R=2 warna/0 shadow, Lid_R=2 warna/0 shadow, HeadGear=2 warna/0 shadow, CandyExit=1 warna/0 shadow, CandyGlobe=1 warna/0 shadow, Dispenser_Blue=1 warna/0 shadow, Dispenser_Gold=1 warna/0 shadow

HASIL: GAGAL budget statis; lihat batas/objek yang dilaporkan.
```

## Performa PlayMode (Logs/ci/perf.txt)

```
BOB-022 | Editor 6000.6.3f1 | NVIDIA GeForce RTX 3050 Laptop GPU | 2026-10-17T03:34:54.9862682Z
GC = ProfilerRecorder Memory/GC Allocated In Frame (byte, seluruh frame termasuk Editor/Test Runner).
Draw/SetPass = UnityStats, seluruh render Editor mono, BUKAN draw/mata XR atau bukti 72 FPS Quest.
Timer input mati, tempo=4, timeScale=1, 5 bot; isi dispenser/inventory dikontrol fixture. Animasi/komponen gameplay tetap aktif.
BoB_Art3D sebelum/sesudah: 138/100 slot warna, 137/11 slot shadow.
Shadow mesh kaku/dekor mati; shadow skinned tetap. Furnitur digabung per material/lightmap/layer/probe; collider dan karakter tetap.
camera=Desktop Art Preview, FOV=65.0, pixel=640x480; quality=Mobile
idle_telemetry_on: frames=1329, gc_samples=1329, gc_avg_B=6434.54, gc_peak_B=16138, draw_avg=0.00, draw_peak=0, setpass_avg=0.00, setpass_peak=0, frame_avg_ms=2.258, frame_peak_ms=12.691
idle_telemetry_off: frames=1288, gc_samples=1288, gc_avg_B=6439.76, gc_peak_B=16138, draw_avg=0.00, draw_peak=0, setpass_avg=0.00, setpass_peak=0, frame_avg_ms=2.331, frame_peak_ms=14.466
idle_gc_delta_on_minus_off_B_per_frame=-5.22
idle_gc_near_zero_1KiB=False; angka mentah, tanpa mengurangi overhead Editor.
status=incomplete (lihat hasil tes)
```
