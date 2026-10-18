# CI Unity (runner laptop)

Commit `17b3465a08b6bb59788b3296b7cb5d9821154f51` · 2026-10-18 19:04 WIB · **LULUS**

| Cek | Hasil |
|---|---|
| EditMode tests | 119/119 lulus, 0 gagal, 0 dilewati; exit=0 |
| PlayMode tests (scene sungguhan) | 33/33 lulus, 0 gagal, 0 dilewati; exit=0 |
| Build APK Release | Succeeded; mode=Release; errors=0; warnings=6; bytes=1683114121; durasi=581.5s; path=C:\Users\acer\bob-ci\repo\Builds\BiteOrBetray_Quest_Release.apk; laporan=C:\Users\acer\bob-ci\repo\Builds\reports\BiteOrBetray_Quest_Release_20261018-185453_17b3465a08b6bb59788b3296b7cb5d9821154f51-dirty.txt |
| Error shader | 0 |
| Error compile | 0 |
| Budget Audit | LULUS |

## Budget Audit

```
== Budget Quest konservatif (estimasi statis, bukan bukti 72 FPS) ==
Target per pandangan/mata: <= 250,000 segitiga, <= 100 draw termasuk shadow, <= 50 material, <= 100 estimasi SetPass.
FOV 110, 8 arah yaw x 3 pitch (-30/0/30) dari Seat_0, tinggi 1,15 m; tanpa occlusion/LOD/batching discount; kepala avatar lokal tidak terlihat dari kursinya sendiri.
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
Renderer dihitung=85, nonaktif/forceRenderingOff=119; geometri dinamis yang masih aktif tetap gagal.
LULUS: Lampu realtime=1, shadow=1 (maks. satu directional)
LULUS: Geometri/material lengkap (tidak diketahui=0)
LULUS: Renderer Room BatchingStatic (14)
Seluruh scene: 217,517 segitiga, 100 draw warna; shadow konservatif 0 draw/0 segitiga.
Kepala avatar lokal (disembunyikan dari kamera first-person): 12 renderer/27 draw tidak dihitung per mata.
Skinned mesh: 9 | vertex: 109,165
Material scene unik: 36, instancing aktif: 36 (SkinnedMeshRenderer tidak memakai GPU instancing; SRP/static batching dapat diprioritaskan).
Tekstur scene: 19, memori Editor perkiraan 20.9 MB; material transparan: 1
Shader: Universal Render Pipeline/Lit
Volume post-process: 1
LULUS yaw=0, pitch=-30: segitiga=203,085/250000, draw/mata=73+0=73/100, material=26/50, estimasi SetPass<=73/100
LULUS yaw=45, pitch=-30: segitiga=191,885/250000, draw/mata=59+0=59/100, material=25/50, estimasi SetPass<=59/100
LULUS yaw=90, pitch=-30: segitiga=170,673/250000, draw/mata=24+0=24/100, material=20/50, estimasi SetPass<=24/100
LULUS yaw=135, pitch=-30: segitiga=161,201/250000, draw/mata=19+0=19/100, material=18/50, estimasi SetPass<=19/100
LULUS yaw=180, pitch=-30: segitiga=161,201/250000, draw/mata=19+0=19/100, material=18/50, estimasi SetPass<=19/100
LULUS yaw=225, pitch=-30: segitiga=161,201/250000, draw/mata=19+0=19/100, material=18/50, estimasi SetPass<=19/100
LULUS yaw=270, pitch=-30: segitiga=170,673/250000, draw/mata=24+0=24/100, material=20/50, estimasi SetPass<=24/100
LULUS yaw=315, pitch=-30: segitiga=196,581/250000, draw/mata=60+0=60/100, material=25/50, estimasi SetPass<=60/100
LULUS yaw=0, pitch=0: segitiga=202,685/250000, draw/mata=72+0=72/100, material=26/50, estimasi SetPass<=72/100
LULUS yaw=45, pitch=0: segitiga=193,781/250000, draw/mata=65+0=65/100, material=26/50, estimasi SetPass<=65/100
LULUS yaw=90, pitch=0: segitiga=171,185/250000, draw/mata=26+0=26/100, material=19/50, estimasi SetPass<=26/100
LULUS yaw=135, pitch=0: segitiga=161,201/250000, draw/mata=19+0=19/100, material=18/50, estimasi SetPass<=19/100
LULUS yaw=180, pitch=0: segitiga=161,201/250000, draw/mata=19+0=19/100, material=18/50, estimasi SetPass<=19/100
LULUS yaw=225, pitch=0: segitiga=161,201/250000, draw/mata=19+0=19/100, material=18/50, estimasi SetPass<=19/100
LULUS yaw=270, pitch=0: segitiga=170,673/250000, draw/mata=24+0=24/100, material=20/50, estimasi SetPass<=24/100
LULUS yaw=315, pitch=0: segitiga=198,757/250000, draw/mata=64+0=64/100, material=26/50, estimasi SetPass<=64/100
LULUS yaw=0, pitch=30: segitiga=201,453/250000, draw/mata=71+0=71/100, material=25/50, estimasi SetPass<=71/100
LULUS yaw=45, pitch=30: segitiga=193,077/250000, draw/mata=62+0=62/100, material=26/50, estimasi SetPass<=62/100
LULUS yaw=90, pitch=30: segitiga=170,201/250000, draw/mata=25+0=25/100, material=17/50, estimasi SetPass<=25/100
LULUS yaw=135, pitch=30: segitiga=159,969/250000, draw/mata=18+0=18/100, material=17/50, estimasi SetPass<=18/100
LULUS yaw=180, pitch=30: segitiga=155,834/250000, draw/mata=17+0=17/100, material=16/50, estimasi SetPass<=17/100
LULUS yaw=225, pitch=30: segitiga=159,969/250000, draw/mata=18+0=18/100, material=17/50, estimasi SetPass<=18/100
LULUS yaw=270, pitch=30: segitiga=173,773/250000, draw/mata=23+0=23/100, material=17/50, estimasi SetPass<=23/100
LULUS yaw=315, pitch=30: segitiga=198,021/250000, draw/mata=62+0=62/100, material=26/50, estimasi SetPass<=62/100
Maksimum antar-pandangan: 203,085 segitiga, 73 draw/mata, 26 material, estimasi SetPass<=73.
Renderer terbesar (seluruh scene): CherryMesh=22,273 tri/1 draw, GentleGiantMesh=20,000 tri/1 draw, BlueGentlemanMesh=19,997 tri/1 draw, LavenderWomanMesh=19,992 tri/1 draw, RedGrouchMesh=19,972 tri/1 draw, Furniture_3=17,560 tri/1 draw, Furniture_5=10,644 tri/1 draw, Furniture_16=6,912 tri/1 draw
Draw terbanyak (seluruh scene): Pupil_L=5 warna/0 shadow, Pupil_R=5 warna/0 shadow, Mouth=3 warna/0 shadow, EyeWhite_L=2 warna/0 shadow, Lid_L=2 warna/0 shadow, EyeWhite_R=2 warna/0 shadow, Lid_R=2 warna/0 shadow, HeadGear=2 warna/0 shadow, CandyExit=1 warna/0 shadow, CandyGlobe=1 warna/0 shadow, Dispenser_Blue=1 warna/0 shadow, Dispenser_Gold=1 warna/0 shadow

== QuestPerfTest ==
Renderer dihitung=86, nonaktif/forceRenderingOff=119; geometri dinamis yang masih aktif tetap gagal.
LULUS: Lampu realtime=1, shadow=1 (maks. satu directional)
LULUS: Geometri/material lengkap (tidak diketahui=0)
LULUS: Renderer Room BatchingStatic (14)
Seluruh scene: 217,575 segitiga, 101 draw warna; shadow konservatif 0 draw/0 segitiga.
Kepala avatar lokal (disembunyikan dari kamera first-person): 12 renderer/27 draw tidak dihitung per mata.
Skinned mesh: 9 | vertex: 109,165
Material scene unik: 37, instancing aktif: 36 (SkinnedMeshRenderer tidak memakai GPU instancing; SRP/static batching dapat diprioritaskan).
Tekstur scene: 20, memori Editor perkiraan 21.1 MB; material transparan: 2
Shader: Universal Render Pipeline/Lit, GUI/Text Shader
Volume post-process: 1
LULUS yaw=0, pitch=-30: segitiga=203,143/250000, draw/mata=74+0=74/100, material=27/50, estimasi SetPass<=74/100
LULUS yaw=45, pitch=-30: segitiga=191,943/250000, draw/mata=60+0=60/100, material=26/50, estimasi SetPass<=60/100
LULUS yaw=90, pitch=-30: segitiga=170,673/250000, draw/mata=24+0=24/100, material=20/50, estimasi SetPass<=24/100
LULUS yaw=135, pitch=-30: segitiga=161,201/250000, draw/mata=19+0=19/100, material=18/50, estimasi SetPass<=19/100
LULUS yaw=180, pitch=-30: segitiga=161,201/250000, draw/mata=19+0=19/100, material=18/50, estimasi SetPass<=19/100
LULUS yaw=225, pitch=-30: segitiga=161,201/250000, draw/mata=19+0=19/100, material=18/50, estimasi SetPass<=19/100
LULUS yaw=270, pitch=-30: segitiga=170,673/250000, draw/mata=24+0=24/100, material=20/50, estimasi SetPass<=24/100
LULUS yaw=315, pitch=-30: segitiga=196,639/250000, draw/mata=61+0=61/100, material=26/50, estimasi SetPass<=61/100
LULUS yaw=0, pitch=0: segitiga=202,743/250000, draw/mata=73+0=73/100, material=27/50, estimasi SetPass<=73/100
LULUS yaw=45, pitch=0: segitiga=193,839/250000, draw/mata=66+0=66/100, material=27/50, estimasi SetPass<=66/100
LULUS yaw=90, pitch=0: segitiga=171,185/250000, draw/mata=26+0=26/100, material=19/50, estimasi SetPass<=26/100
LULUS yaw=135, pitch=0: segitiga=161,201/250000, draw/mata=19+0=19/100, material=18/50, estimasi SetPass<=19/100
LULUS yaw=180, pitch=0: segitiga=161,201/250000, draw/mata=19+0=19/100, material=18/50, estimasi SetPass<=19/100
LULUS yaw=225, pitch=0: segitiga=161,201/250000, draw/mata=19+0=19/100, material=18/50, estimasi SetPass<=19/100
LULUS yaw=270, pitch=0: segitiga=170,673/250000, draw/mata=24+0=24/100, material=20/50, estimasi SetPass<=24/100
LULUS yaw=315, pitch=0: segitiga=198,815/250000, draw/mata=65+0=65/100, material=27/50, estimasi SetPass<=65/100
LULUS yaw=0, pitch=30: segitiga=201,511/250000, draw/mata=72+0=72/100, material=26/50, estimasi SetPass<=72/100
LULUS yaw=45, pitch=30: segitiga=193,135/250000, draw/mata=63+0=63/100, material=27/50, estimasi SetPass<=63/100
LULUS yaw=90, pitch=30: segitiga=170,201/250000, draw/mata=25+0=25/100, material=17/50, estimasi SetPass<=25/100
LULUS yaw=135, pitch=30: segitiga=159,969/250000, draw/mata=18+0=18/100, material=17/50, estimasi SetPass<=18/100
LULUS yaw=180, pitch=30: segitiga=155,834/250000, draw/mata=17+0=17/100, material=16/50, estimasi SetPass<=17/100
LULUS yaw=225, pitch=30: segitiga=159,969/250000, draw/mata=18+0=18/100, material=17/50, estimasi SetPass<=18/100
LULUS yaw=270, pitch=30: segitiga=173,773/250000, draw/mata=23+0=23/100, material=17/50, estimasi SetPass<=23/100
LULUS yaw=315, pitch=30: segitiga=198,079/250000, draw/mata=63+0=63/100, material=27/50, estimasi SetPass<=63/100
Maksimum antar-pandangan: 203,143 segitiga, 74 draw/mata, 27 material, estimasi SetPass<=74.
Renderer terbesar (seluruh scene): CherryMesh=22,273 tri/1 draw, GentleGiantMesh=20,000 tri/1 draw, BlueGentlemanMesh=19,997 tri/1 draw, LavenderWomanMesh=19,992 tri/1 draw, RedGrouchMesh=19,972 tri/1 draw, Furniture_3=17,560 tri/1 draw, Furniture_5=10,644 tri/1 draw, Furniture_16=6,912 tri/1 draw
Draw terbanyak (seluruh scene): Pupil_L=5 warna/0 shadow, Pupil_R=5 warna/0 shadow, Mouth=3 warna/0 shadow, EyeWhite_L=2 warna/0 shadow, Lid_L=2 warna/0 shadow, EyeWhite_R=2 warna/0 shadow, Lid_R=2 warna/0 shadow, HeadGear=2 warna/0 shadow, CandyExit=1 warna/0 shadow, CandyGlobe=1 warna/0 shadow, Dispenser_Blue=1 warna/0 shadow, Dispenser_Gold=1 warna/0 shadow

HASIL: LULUS budget statis; verifikasi gameplay Unity masih diperlukan.
```

## Performa PlayMode (Logs/ci/perf.txt)

```
BOB-022 | Editor 6000.6.3f1 | NVIDIA GeForce RTX 3050 Laptop GPU | 2026-10-18T11:35:40.1990308Z
GC = ProfilerRecorder Memory/GC Allocated In Frame (byte, seluruh frame termasuk Editor/Test Runner).
Draw/SetPass = maks(ProfilerRecorder Render Draw Calls/SetPass Calls Count, UnityStats), seluruh render Editor mono, BUKAN draw/mata XR atau bukti 72 FPS Quest.
Editor -batchmode tidak menggambar Game view: kamera utama dirender manual ke RenderTexture tiap frame ukur (GC/frame termasuk render itu).
Timer input mati, tempo=4, timeScale=1, 5 bot; isi dispenser/inventory dikontrol fixture. Animasi/komponen gameplay tetap aktif.
BoB_Art3D sebelum/sesudah: 138/100 slot warna, 137/0 slot shadow.
Visual SDK digantikan avatar lokal: 24 slot warna disembunyikan (termasuk mesh tangan dinamis); sensor/anchor tetap.
Shadow karakter/dekor mati. Slot skinned bermaterial sama digabung tanpa mengubah vertex/blendshape; furnitur per material/lightmap/layer/probe; collider dan karakter tetap.
camera=Desktop Art Preview, FOV=65.0, pixel=640x480; quality=Mobile
idle_telemetry_on: frames=449, gc_samples=449, gc_avg_B=6660.57, gc_peak_B=14804, draw_avg=70.00, draw_peak=70, setpass_avg=31.00, setpass_peak=31, frame_avg_ms=6.694, frame_peak_ms=13.477
idle_telemetry_off: frames=461, gc_samples=461, gc_avg_B=6636.67, gc_peak_B=15126, draw_avg=70.00, draw_peak=70, setpass_avg=31.00, setpass_peak=31, frame_avg_ms=6.515, frame_peak_ms=7.859
idle_gc_delta_on_minus_off_B_per_frame=23.90
idle_gc_near_zero_1KiB=False; angka mentah, tanpa mengurangi overhead Editor.
props_unchanged_instance_id=51733:256; tetap setelah PlasticFork dan Eat.
BoB.RefreshProps=n/a; marker tidak tersedia, identitas prop diuji terpisah.
automated_match: frames=2401, gc_samples=2401, gc_avg_B=6813.66, gc_peak_B=184360, draw_avg=71.55, draw_peak=77, setpass_avg=33.41, setpass_peak=37, frame_avg_ms=6.980, frame_peak_ms=56.121
match_over=true, turns=11, telemetry_rows=15; CSV=Logs/ci/perf-telemetry.csv
session,seed,players,round,turn,seat,player,is_bot,action,detail,think_seconds,turn_think_seconds,hp_after,timestamp
20261018_183540,100,6,1,1,0,You,0,ITEM,Plastic Fork,10.92,10.92,4,2026-10-18T18:35:53
20261018_183540,100,6,1,11,3,Peach,1,GAME_OVER,rounds=1;turns=11;winner=Peach;real_seconds=30.3,0.00,0.00,2,2026-10-18T18:36:11
status=complete
```

Arsip RC commit ini: [manifest dan bukti lengkap](../../Builds/reports/RC/17b3465a08b6bb59788b3296b7cb5d9821154f51/manifest.json).
