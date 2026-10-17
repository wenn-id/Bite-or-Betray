# CI Unity (runner laptop)

Commit `bc71d57` · 2026-10-17 21:57 WIB · **GAGAL**

| Cek | Hasil |
|---|---|
| EditMode tests | 113/113 lulus, 0 gagal |
| PlayMode tests (scene sungguhan) | 27/30 lulus, 3 gagal |
| Build APK Development | Succeeded; mode=Release; errors=0; warnings=26; bytes=1680003565; durasi=1501.1s; path=C:\Users\acer\bob-ci\repo\Builds\BiteOrBetray_Quest_Release.apk; laporan=C:\Users\acer\bob-ci\repo\Builds\reports\BiteOrBetray_Quest_Release_20261017-213211_bc71d57c6c5c8a772b254d8146d6cf5e924bfeae-dirty.txt |
| Error shader | 0 |
| Error compile | 0 |
| Budget Audit | GAGAL |

## Budget Audit gagal

Lihat laporan Budget Audit di bawah; build APK berhasil belum memenuhi budget gameplay.

## Tes gagal

- BiteOrBetray.Tests.PlayMode.OnlineMatchTests.Client_disconnect_mid_game_seat_played_by_bot_until_winner: Unhandled log message: '[Exception] IOException: Koordinat tidak valid.'. Use UnityEngine.TestTools.LogAssert.Expect
- BiteOrBetray.Tests.PlayMode.OnlineMatchTests.Host_and_client_play_with_bots_until_winner_with_matching_snapshots: Unhandled log message: '[Exception] IOException: Koordinat tidak valid.'. Use UnityEngine.TestTools.LogAssert.Expect
- BiteOrBetray.Tests.PlayMode.OnlineServicesTests.Voice_joins_room_channel_and_leaves_with_room: Vivox gagal: <b>Voice chat</b>
<size=36>Voice belum tersedia: cek izin mikrofon, internet dan layanan Vivox.</size>
  Expected: "bob-room-wDWc68CX75uVDiZqcQomaY"
  But was:  null

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
GAGAL geometri tidak tersedia/dinamis: [BuildingBlock] Hand Tracking left (SkinnedMeshRenderer)
GAGAL geometri tidak tersedia/dinamis: [BuildingBlock] Hand Tracking right (SkinnedMeshRenderer)
GAGAL: Geometri/material lengkap (tidak diketahui=2)
LULUS: Renderer Room BatchingStatic (14)
Seluruh scene: 352,101 segitiga, 124 draw warna; shadow konservatif 35 draw/242,741 segitiga.
Skinned mesh: 31 | vertex: 198,975
Material scene unik: 45, instancing aktif: 36 (SkinnedMeshRenderer tidak memakai GPU instancing; SRP/static batching dapat diprioritaskan).
Tekstur scene: 29, memori Editor perkiraan 61.4 MB; material transparan: 2
Shader: Universal Render Pipeline/Lit, Oculus Sample/Alpha Hand Outline (SBB)
Volume post-process: 1
GAGAL yaw=0, pitch=-30: segitiga=254,181/250000, draw/mata=108+35=143/100, material=38/50, estimasi SetPass<=143/100
GAGAL yaw=45, pitch=-30: segitiga=321,247/250000, draw/mata=104+35=139/100, material=41/50, estimasi SetPass<=139/100
LULUS yaw=90, pitch=-30: segitiga=247,853/250000, draw/mata=45+35=80/100, material=31/50, estimasi SetPass<=80/100
GAGAL yaw=135, pitch=-30: segitiga=305,277/250000, draw/mata=47+35=82/100, material=31/50, estimasi SetPass<=82/100
GAGAL yaw=180, pitch=-30: segitiga=305,277/250000, draw/mata=47+35=82/100, material=31/50, estimasi SetPass<=82/100
GAGAL yaw=225, pitch=-30: segitiga=305,277/250000, draw/mata=47+35=82/100, material=31/50, estimasi SetPass<=82/100
LULUS yaw=270, pitch=-30: segitiga=247,901/250000, draw/mata=47+35=82/100, material=32/50, estimasi SetPass<=82/100
GAGAL yaw=315, pitch=-30: segitiga=325,943/250000, draw/mata=105+35=140/100, material=41/50, estimasi SetPass<=140/100
GAGAL yaw=0, pitch=0: segitiga=351,701/250000, draw/mata=121+35=156/100, material=44/50, estimasi SetPass<=156/100
GAGAL yaw=45, pitch=0: segitiga=342,713/250000, draw/mata=113+35=148/100, material=44/50, estimasi SetPass<=148/100
GAGAL yaw=90, pitch=0: segitiga=297,265/250000, draw/mata=54+35=89/100, material=32/50, estimasi SetPass<=89/100
GAGAL yaw=135, pitch=0: segitiga=305,277/250000, draw/mata=47+35=82/100, material=31/50, estimasi SetPass<=82/100
GAGAL yaw=180, pitch=0: segitiga=305,277/250000, draw/mata=47+35=82/100, material=31/50, estimasi SetPass<=82/100
GAGAL yaw=225, pitch=0: segitiga=305,277/250000, draw/mata=47+35=82/100, material=31/50, estimasi SetPass<=82/100
GAGAL yaw=270, pitch=0: segitiga=296,753/250000, draw/mata=52+35=87/100, material=33/50, estimasi SetPass<=87/100
GAGAL yaw=315, pitch=0: segitiga=347,689/250000, draw/mata=112+35=147/100, material=44/50, estimasi SetPass<=147/100
GAGAL yaw=0, pitch=30: segitiga=350,301/250000, draw/mata=118+35=153/100, material=42/50, estimasi SetPass<=153/100
GAGAL yaw=45, pitch=30: segitiga=341,925/250000, draw/mata=109+35=144/100, material=43/50, estimasi SetPass<=144/100
GAGAL yaw=90, pitch=30: segitiga=315,935/250000, draw/mata=57+35=92/100, material=33/50, estimasi SetPass<=92/100
GAGAL yaw=135, pitch=30: segitiga=304,045/250000, draw/mata=46+35=81/100, material=30/50, estimasi SetPass<=81/100
GAGAL yaw=180, pitch=30: segitiga=299,910/250000, draw/mata=45+35=80/100, material=29/50, estimasi SetPass<=80/100
GAGAL yaw=225, pitch=30: segitiga=304,045/250000, draw/mata=46+35=81/100, material=30/50, estimasi SetPass<=81/100
GAGAL yaw=270, pitch=30: segitiga=319,507/250000, draw/mata=55+35=90/100, material=33/50, estimasi SetPass<=90/100
GAGAL yaw=315, pitch=30: segitiga=346,869/250000, draw/mata=109+35=144/100, material=43/50, estimasi SetPass<=144/100
Maksimum antar-pandangan: 351,701 segitiga, 156 draw/mata, 44 material, estimasi SetPass<=156.
Renderer terbesar (seluruh scene): CherryMesh=22,273 tri/1 draw, GentleGiantMesh=20,000 tri/1 draw, BlueGentlemanMesh=19,997 tri/1 draw, LavenderWomanMesh=19,992 tri/1 draw, RedGrouchMesh=19,972 tri/1 draw, Furniture_3=17,560 tri/1 draw, left_touch_mesh=14,665 tri/2 draw, left_touch_mesh=14,665 tri/2 draw
Draw terbanyak (seluruh scene): Pupil_L=5 warna/0 shadow, Pupil_R=5 warna/0 shadow, Mouth=3 warna/3 shadow, left_touch_mesh=2 warna/2 shadow, left_touch_mesh=2 warna/2 shadow, EyeWhite_L=2 warna/0 shadow, Lid_L=2 warna/0 shadow, EyeWhite_R=2 warna/0 shadow, Lid_R=2 warna/0 shadow, HeadGear=2 warna/0 shadow, l_controller_ply=1 warna/1 shadow, r_controller_ply=1 warna/1 shadow

== QuestPerfTest ==
LULUS: Lampu realtime=1, shadow=1 (maks. satu directional)
GAGAL geometri tidak tersedia/dinamis: [BuildingBlock] Hand Tracking left (SkinnedMeshRenderer)
GAGAL geometri tidak tersedia/dinamis: [BuildingBlock] Hand Tracking right (SkinnedMeshRenderer)
GAGAL: Geometri/material lengkap (tidak diketahui=2)
LULUS: Renderer Room BatchingStatic (14)
Seluruh scene: 352,159 segitiga, 125 draw warna; shadow konservatif 35 draw/242,741 segitiga.
Skinned mesh: 31 | vertex: 198,975
Material scene unik: 46, instancing aktif: 36 (SkinnedMeshRenderer tidak memakai GPU instancing; SRP/static batching dapat diprioritaskan).
Tekstur scene: 30, memori Editor perkiraan 61.6 MB; material transparan: 3
Shader: Universal Render Pipeline/Lit, Oculus Sample/Alpha Hand Outline (SBB), GUI/Text Shader
Volume post-process: 1
GAGAL yaw=0, pitch=-30: segitiga=254,239/250000, draw/mata=109+35=144/100, material=39/50, estimasi SetPass<=144/100
GAGAL yaw=45, pitch=-30: segitiga=321,305/250000, draw/mata=105+35=140/100, material=42/50, estimasi SetPass<=140/100
LULUS yaw=90, pitch=-30: segitiga=247,853/250000, draw/mata=45+35=80/100, material=31/50, estimasi SetPass<=80/100
GAGAL yaw=135, pitch=-30: segitiga=305,277/250000, draw/mata=47+35=82/100, material=31/50, estimasi SetPass<=82/100
GAGAL yaw=180, pitch=-30: segitiga=305,277/250000, draw/mata=47+35=82/100, material=31/50, estimasi SetPass<=82/100
GAGAL yaw=225, pitch=-30: segitiga=305,277/250000, draw/mata=47+35=82/100, material=31/50, estimasi SetPass<=82/100
LULUS yaw=270, pitch=-30: segitiga=247,901/250000, draw/mata=47+35=82/100, material=32/50, estimasi SetPass<=82/100
GAGAL yaw=315, pitch=-30: segitiga=326,001/250000, draw/mata=106+35=141/100, material=42/50, estimasi SetPass<=141/100
GAGAL yaw=0, pitch=0: segitiga=351,759/250000, draw/mata=122+35=157/100, material=45/50, estimasi SetPass<=157/100
GAGAL yaw=45, pitch=0: segitiga=342,771/250000, draw/mata=114+35=149/100, material=45/50, estimasi SetPass<=149/100
GAGAL yaw=90, pitch=0: segitiga=297,265/250000, draw/mata=54+35=89/100, material=32/50, estimasi SetPass<=89/100
GAGAL yaw=135, pitch=0: segitiga=305,277/250000, draw/mata=47+35=82/100, material=31/50, estimasi SetPass<=82/100
GAGAL yaw=180, pitch=0: segitiga=305,277/250000, draw/mata=47+35=82/100, material=31/50, estimasi SetPass<=82/100
GAGAL yaw=225, pitch=0: segitiga=305,277/250000, draw/mata=47+35=82/100, material=31/50, estimasi SetPass<=82/100
GAGAL yaw=270, pitch=0: segitiga=296,753/250000, draw/mata=52+35=87/100, material=33/50, estimasi SetPass<=87/100
GAGAL yaw=315, pitch=0: segitiga=347,747/250000, draw/mata=113+35=148/100, material=45/50, estimasi SetPass<=148/100
GAGAL yaw=0, pitch=30: segitiga=350,359/250000, draw/mata=119+35=154/100, material=43/50, estimasi SetPass<=154/100
GAGAL yaw=45, pitch=30: segitiga=341,983/250000, draw/mata=110+35=145/100, material=44/50, estimasi SetPass<=145/100
GAGAL yaw=90, pitch=30: segitiga=315,935/250000, draw/mata=57+35=92/100, material=33/50, estimasi SetPass<=92/100
GAGAL yaw=135, pitch=30: segitiga=304,045/250000, draw/mata=46+35=81/100, material=30/50, estimasi SetPass<=81/100
GAGAL yaw=180, pitch=30: segitiga=299,910/250000, draw/mata=45+35=80/100, material=29/50, estimasi SetPass<=80/100
GAGAL yaw=225, pitch=30: segitiga=304,045/250000, draw/mata=46+35=81/100, material=30/50, estimasi SetPass<=81/100
GAGAL yaw=270, pitch=30: segitiga=319,507/250000, draw/mata=55+35=90/100, material=33/50, estimasi SetPass<=90/100
GAGAL yaw=315, pitch=30: segitiga=346,927/250000, draw/mata=110+35=145/100, material=44/50, estimasi SetPass<=145/100
Maksimum antar-pandangan: 351,759 segitiga, 157 draw/mata, 45 material, estimasi SetPass<=157.
Renderer terbesar (seluruh scene): CherryMesh=22,273 tri/1 draw, GentleGiantMesh=20,000 tri/1 draw, BlueGentlemanMesh=19,997 tri/1 draw, LavenderWomanMesh=19,992 tri/1 draw, RedGrouchMesh=19,972 tri/1 draw, Furniture_3=17,560 tri/1 draw, left_touch_mesh=14,665 tri/2 draw, left_touch_mesh=14,665 tri/2 draw
Draw terbanyak (seluruh scene): Pupil_L=5 warna/0 shadow, Pupil_R=5 warna/0 shadow, Mouth=3 warna/3 shadow, left_touch_mesh=2 warna/2 shadow, left_touch_mesh=2 warna/2 shadow, EyeWhite_L=2 warna/0 shadow, Lid_L=2 warna/0 shadow, EyeWhite_R=2 warna/0 shadow, Lid_R=2 warna/0 shadow, HeadGear=2 warna/0 shadow, l_controller_ply=1 warna/1 shadow, r_controller_ply=1 warna/1 shadow

HASIL: GAGAL budget statis; lihat batas/objek yang dilaporkan.
```

## Performa PlayMode (Logs/ci/perf.txt)

```
BOB-022 | Editor 6000.6.3f1 | NVIDIA GeForce RTX 3050 Laptop GPU | 2026-10-17T14:27:37.1581143Z
GC = ProfilerRecorder Memory/GC Allocated In Frame (byte, seluruh frame termasuk Editor/Test Runner).
Draw/SetPass = maks(ProfilerRecorder Render Draw Calls/SetPass Calls Count, UnityStats), seluruh render Editor mono, BUKAN draw/mata XR atau bukti 72 FPS Quest.
Editor -batchmode tidak menggambar Game view: kamera utama dirender manual ke RenderTexture tiap frame ukur (GC/frame termasuk render itu).
Timer input mati, tempo=4, timeScale=1, 5 bot; isi dispenser/inventory dikontrol fixture. Animasi/komponen gameplay tetap aktif.
BoB_Art3D sebelum/sesudah: 138/100 slot warna, 137/11 slot shadow.
Shadow mesh kaku/dekor mati; shadow skinned tetap. Furnitur digabung per material/lightmap/layer/probe; collider dan karakter tetap.
camera=Desktop Art Preview, FOV=65.0, pixel=640x480; quality=Mobile
idle_telemetry_on: frames=486, gc_samples=486, gc_avg_B=6661.97, gc_peak_B=15131, draw_avg=78.00, draw_peak=78, setpass_avg=33.00, setpass_peak=33, frame_avg_ms=6.183, frame_peak_ms=31.928
idle_telemetry_off: frames=493, gc_samples=493, gc_avg_B=6627.71, gc_peak_B=15131, draw_avg=78.00, draw_peak=78, setpass_avg=33.00, setpass_peak=33, frame_avg_ms=6.095, frame_peak_ms=18.702
idle_gc_delta_on_minus_off_B_per_frame=34.25
idle_gc_near_zero_1KiB=False; angka mentah, tanpa mengurangi overhead Editor.
props_unchanged_instance_id=51751:256; tetap setelah PlasticFork dan Eat.
BoB.RefreshProps=n/a; marker tidak tersedia, identitas prop diuji terpisah.
automated_match: frames=2537, gc_samples=2537, gc_avg_B=6832.98, gc_peak_B=184172, draw_avg=76.67, draw_peak=85, setpass_avg=34.78, setpass_peak=38, frame_avg_ms=6.610, frame_peak_ms=50.119
match_over=true, turns=11, telemetry_rows=15; CSV=Logs/ci/perf-telemetry.csv
session,seed,players,round,turn,seat,player,is_bot,action,detail,think_seconds,turn_think_seconds,hp_after,timestamp
20261017_212737,100,6,1,1,0,Kamu,0,ITEM,Plastic Fork,10.79,10.79,4,2026-10-17T21:27:50
20261017_212737,100,6,1,11,3,Peach,1,GAME_OVER,rounds=1;turns=11;winner=Peach;real_seconds=30.2,0.00,0.00,2,2026-10-17T21:28:07
status=complete
```

## QA BOB-047 (codex, 2026-10-17)

Hasil di atas tetap laporan asli runner untuk `bc71d57`, bukan hasil optimasi BOB-047. Daftar lulus berikut merupakan inferensi dari 27/30 lulus, tiga nama gagal eksplisit, serta 30 tes pada sumber `bc71d57` (tidak ada perubahan tes antara commit itu dan HEAD saat QA dimulai). XML per tes tidak tersedia di VPS; 27 + 3 = 30 sehingga tidak ada slot tes dilewati dalam laporan ini. Namespace seluruh tes: `BiteOrBetray.Tests.PlayMode`. Assertion BOB-047 yang baru ditambahkan pada tes performa masih menunggu Unity.

Tes yang dipetakan lulus:

- `CameraSetupTests.Editor_tanpa_XR_memakai_kamera_preview_desktop`
- `CameraSetupTests.VR_hanya_CenterEyeAnchor_sebagai_kamera_utama_dengan_satu_AudioListener`
- `GameplayPerformanceTests.Gameplay_idle_gc_render_stats_and_automated_match_are_recorded`
- `GameplayRecorderTests.Pertandingan_dari_kursi_pemain_sampai_pemenang_menyimpan_frame_1080p_dan_perintah_ffmpeg`
- `GameplayRecorderTests.Shot_menu_utama_dari_kursi_pemain`
- `GameplayRecorderTests.Shot_close_up_bot_bereaksi`
- `GameplayRecorderTests.Shot_meja_dari_atas`
- `OfflineMatchTests.Full_match_via_table_actions_items_grapple_results_replay_and_menu`
- `OfflineMatchTests.Bot_counts_one_to_five_start_with_selected_seats_and_difficulties`
- `OfflineMatchTests.Lost_application_focus_freezes_turn_timer_and_returns_held_candy`
- `OfflineMatchTests.Application_pause_freezes_turn_timer_and_returns_held_candy`
- `OfflineMatchTests.Tracking_loss_with_trigger_held_recovers_without_accidental_give`
- `OfflineMatchTests.Tracking_loss_with_grip_held_recovers_without_accidental_give`
- `OfflineMatchTests.Eliminated_human_is_locked_transparent_spectator_until_winner_and_replay`
- `OnlineSceneTests.Scene_has_one_network_manager_lobby_and_in_scene_table_bound_to_director`
- `OnlineSceneTests.Main_menu_of_real_scene_enables_create_and_join_room`
- `OnlineSceneTests.Scene_host_spawns_in_scene_table_and_client_sync_keeps_loaded_scene`
- `OnlineServicesTests.Create_room_with_relay_find_by_code_and_leave`
- `RegressionTests.Menu_bot_setup_all_onboarding_pages_and_gameplay_with_screens`
- `RegressionTests.Eight_items_follow_game_engine_rules`
- `RegressionTests.Three_candy_colors_and_clover_damage_cap_follow_rules`
- `RegressionTests.New_round_refills_dispenser_and_deals_items`
- `RegressionTests.Ko_spectator_results_music_sfx_and_telemetry_csv_with_screens`
- `RegressionTests.Five_restarts_keep_objects_materials_audio_sources_and_clips_stable`
- `RegressionTests.Focus_lost_and_regained_cycles_freeze_timer_and_restore_input`
- `RegressionTests.Controller_path_works_and_pinch_never_doubles_input`
- `SceneSmokeTests.Gameplay_scene_loads_into_main_menu_without_error_shaders`

Evaluasi seluruh tugas yang semula `needs_unity_check` (rincian AC, bukti dan known gap di notes masing-masing):

| Tugas | Keputusan | Bukti / sisa yang belum terbukti |
|---|---|---|
| BOB-008 | done | Timer/fokus/pause/input pulih; perangkat fisik known gap. |
| BOB-009 | needs_unity_check | GC idle 6661,97 B/frame; indikator mendekati nol=False, capture Profiler belum ada. |
| BOB-013 | done | Tracking trigger/grip dan satu aksi dua tangan; tracking fisik known gap. |
| BOB-015 | done | OfflineMatchTests 7/7; menu sampai pemenang/replay/menu. |
| BOB-017 | needs_unity_check | CSV teruji; delta GC telemetry +34,25 B/frame belum membuktikan tanpa alokasi. |
| BOB-018 | needs_unity_check | Cue/pooling lulus; persetujuan pemilik untuk placeholder audio belum tercatat. |
| BOB-019 | done | Enam halaman onboarding dan Lewati teruji; tampilan headset known gap. |
| BOB-020 | needs_unity_check | Instance prop stabil; perbandingan CPU sebelum/sesudah belum ada, marker n/a. |
| BOB-022 | needs_unity_check | Perf complete; GC belum mendekati nol, audit gagal. |
| BOB-023 | done | RegressionTests 8/8 dan enam screenshot CI; tindak lanjut yang belum memenuhi AC tetap terbuka. |
| BOB-024 | done | Draft workflow/dokumentasi dan SCENE LULUS di VPS; tidak mengklaim draft GitHub telah jalan. |
| BOB-025 | needs_unity_check | Release Succeeded; tiga PlayMode gagal dan Budget Audit gagal. |
| BOB-028 | done | Konfigurasi 1-5 bot/tingkat/kursi teruji. |
| BOB-029 | done | Input KO terkunci, avatar transparan dan kamera tetap; klien online di BOB-036. |
| BOB-030 | done | Hasil, konfigurasi replay dan hook kembali menu teruji. |
| BOB-031 | done | Controller/pinch tanpa aksi ganda; pinch/aim nyata known gap. |
| BOB-032 | done | Menu/offline/lobby terhubung; simpan/muat Settings teruji via store Core + kode PlayerPrefs. |
| BOB-033 | done | Musik/Settings/SFX/pooling teruji; dengar headset known gap. |
| BOB-035 | needs_unity_check | Host Relay/query/kode invalid lulus; pertandingan online gagal, klien identitas kedua belum teruji. |
| BOB-036 | needs_unity_check | OnlineMatchTests gagal; pose/visual klien penuh belum diuji. |
| BOB-037 | needs_unity_check | Tes disconnect gagal; host keluar pada klien penuh belum diuji. |
| BOB-038 | needs_unity_check | Tes Vivox gagal channel=null; bukti Settings saja tidak cukup. |
| BOB-041 | needs_unity_check | Dua OnlineMatchTests dan satu tes Vivox gagal. |
| BOB-046 | needs_unity_check | OnlineSceneTests 3/3; AC OnlineMatchTests/OnlineServicesTests tetap lulus belum terpenuhi. |
| BOB-043 | needs_unity_check | CameraSetupTests 2/2; hasil pemeriksa Meta XR Project Setup Tool belum ada. |
| BOB-045 | done | GameplayRecorderTests 4/4: frame 1080p, preset dan perintah ffmpeg. |

Budget historis: gameplay **351.701/250.000 segitiga (+101.701), 156/100 draw/mata (+56)**; perf scene **351.759 (+101.759), 157 (+57)**. Keduanya juga gagal karena dua mesh tangan SDK yang baru tersedia saat runtime. Terdapat 35 draw shadow; seluruh varian model controller di rig ikut dihitung.

Builder BOB-047 menyimpan visual SDK yang digantikan avatar lokal dalam keadaan tidak merender, mempertahankan sensor/anchor, mematikan caster karakter dan menggabungkan submesh skinned opaque bermaterial sama tanpa kehilangan geometri/animasi. CI sudah memanggil `GameplayPerformanceBuilder.PrepareScenes` sebelum PlayMode dan sebelum audit/build; scene sumber dan QuestPerfTest disimpan oleh Unity, tanpa edit YAML manual. Batas audit tetap. **Hasil sesudah perubahan belum tersedia**: BOB-047 tetap `needs_unity_check` sampai runner menjalankan ulang dan mencatat angka kedua scene serta hasil assertion performa yang baru.
