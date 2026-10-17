# CI Unity (runner laptop)

Commit `4c3fd98` · 2026-10-17 07:28 WIB · **LULUS**

| Cek | Hasil |
|---|---|
| EditMode tests | 111/111 lulus, 0 gagal |
| PlayMode tests (scene sungguhan) | 1/1 lulus, 0 gagal |
| Build APK Development | Succeeded; mode=Development; errors=0; warnings=3; bytes=1814751362; durasi=415.8s; path=C:\Users\acer\bob-ci\repo\Builds\BiteOrBetray_Quest_Development.apk; laporan=C:\Users\acer\bob-ci\repo\Builds\reports\BiteOrBetray_Quest_Development_20261017-072049_4c3fd98-dirty.txt |
| Error shader | 0 |
| Error compile | 0 |

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
Seluruh scene: 217,517 segitiga, 138 draw warna; shadow konservatif 137 draw/216,797 segitiga.
Skinned mesh: 9 | vertex: 109,165
Material scene unik: 36, instancing aktif: 36 (SkinnedMeshRenderer tidak memakai GPU instancing; SRP/static batching dapat diprioritaskan).
Tekstur scene: 19, memori Editor perkiraan 20.9 MB; material transparan: 1
Shader: Universal Render Pipeline/Lit
Volume post-process: 1
GAGAL yaw=0, pitch=-30: segitiga=216,653/250000, draw/mata=133+137=270/100, material=36/50, estimasi SetPass<=270/100
GAGAL yaw=45, pitch=-30: segitiga=201,037/250000, draw/mata=109+137=246/100, material=35/50, estimasi SetPass<=246/100
GAGAL yaw=90, pitch=-30: segitiga=170,463/250000, draw/mata=47+137=184/100, material=28/50, estimasi SetPass<=184/100
GAGAL yaw=135, pitch=-30: segitiga=161,553/250000, draw/mata=27+137=164/100, material=23/50, estimasi SetPass<=164/100
GAGAL yaw=180, pitch=-30: segitiga=161,553/250000, draw/mata=27+137=164/100, material=23/50, estimasi SetPass<=164/100
GAGAL yaw=225, pitch=-30: segitiga=161,553/250000, draw/mata=27+137=164/100, material=23/50, estimasi SetPass<=164/100
GAGAL yaw=270, pitch=-30: segitiga=170,463/250000, draw/mata=47+137=184/100, material=28/50, estimasi SetPass<=184/100
GAGAL yaw=315, pitch=-30: segitiga=205,733/250000, draw/mata=110+137=247/100, material=35/50, estimasi SetPass<=247/100
GAGAL yaw=0, pitch=0: segitiga=216,469/250000, draw/mata=134+137=271/100, material=36/50, estimasi SetPass<=271/100
GAGAL yaw=45, pitch=0: segitiga=203,497/250000, draw/mata=117+137=254/100, material=36/50, estimasi SetPass<=254/100
GAGAL yaw=90, pitch=0: segitiga=172,043/250000, draw/mata=49+137=186/100, material=26/50, estimasi SetPass<=186/100
GAGAL yaw=135, pitch=0: segitiga=163,041/250000, draw/mata=32+137=169/100, material=23/50, estimasi SetPass<=169/100
GAGAL yaw=180, pitch=0: segitiga=161,553/250000, draw/mata=27+137=164/100, material=23/50, estimasi SetPass<=164/100
GAGAL yaw=225, pitch=0: segitiga=163,041/250000, draw/mata=32+137=169/100, material=23/50, estimasi SetPass<=169/100
GAGAL yaw=270, pitch=0: segitiga=171,531/250000, draw/mata=47+137=184/100, material=27/50, estimasi SetPass<=184/100
GAGAL yaw=315, pitch=0: segitiga=208,473/250000, draw/mata=116+137=253/100, material=36/50, estimasi SetPass<=253/100
GAGAL yaw=0, pitch=30: segitiga=215,393/250000, draw/mata=131+137=268/100, material=34/50, estimasi SetPass<=268/100
GAGAL yaw=45, pitch=30: segitiga=203,573/250000, draw/mata=115+137=252/100, material=35/50, estimasi SetPass<=252/100
GAGAL yaw=90, pitch=30: segitiga=171,883/250000, draw/mata=51+137=188/100, material=25/50, estimasi SetPass<=188/100
GAGAL yaw=135, pitch=30: segitiga=164,273/250000, draw/mata=33+137=170/100, material=22/50, estimasi SetPass<=170/100
GAGAL yaw=180, pitch=30: segitiga=156,726/250000, draw/mata=27+137=164/100, material=21/50, estimasi SetPass<=164/100
GAGAL yaw=225, pitch=30: segitiga=164,273/250000, draw/mata=33+137=170/100, material=22/50, estimasi SetPass<=170/100
GAGAL yaw=270, pitch=30: segitiga=175,455/250000, draw/mata=49+137=186/100, material=25/50, estimasi SetPass<=186/100
GAGAL yaw=315, pitch=30: segitiga=208,517/250000, draw/mata=115+137=252/100, material=35/50, estimasi SetPass<=252/100
Maksimum antar-pandangan: 216,653 segitiga, 271 draw/mata, 36 material, estimasi SetPass<=271.
Renderer terbesar (seluruh scene): CherryMesh=22,273 tri/1 draw, GentleGiantMesh=20,000 tri/1 draw, BlueGentlemanMesh=19,997 tri/1 draw, LavenderWomanMesh=19,992 tri/1 draw, RedGrouchMesh=19,972 tri/1 draw, Room_Cream=17,560 tri/1 draw, Room_Gold=9,036 tri/1 draw, Dice_Teal=5,840 tri/1 draw

== QuestPerfTest ==
LULUS: Lampu realtime=1, shadow=1 (maks. satu directional)
GAGAL Assets/BiteOrBetray/Scenes/QuestPerfTest.unity: There is no 'MeshFilter' attached to the "PerfTest Board" game object, but a script is trying to access it.
You probably need to add a MeshFilter to the game object "PerfTest Board". Or your script needs to check if the component is attached before using it.

HASIL: GAGAL budget statis; lihat batas/objek yang dilaporkan.
```
