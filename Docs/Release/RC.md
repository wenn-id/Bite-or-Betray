# BOB-025 — Release candidate tanpa headset

Tanggal persiapan: 2026-10-17. Status **needs_unity_check — belum layak dinyatakan RC lulus**.
Tidak ada Unity atau Quest fisik di mesin agent. Owner tidak memiliki Quest; tidak ada klaim sudah diuji di perangkat.

## Freeze dan identitas sumber

Fitur dibekukan pada cakupan saat ini: pertandingan offline, online multiplayer (NGO + Sessions/Lobby/Relay), voice Vivox,
controller dan hand tracking, onboarding, audio, spectator, hasil/restart dan telemetry. Selama freeze hanya perbaikan blocker
regression BOB-023/BOB-041 beserta pengujian/bukti release yang diterima. Tidak ada paket baru atau fitur baru dalam BOB-025.

Hash sumber saat persiapan: `b0a9cac007e8d5b305cc571028fa21aca5e71b80` (sebelum commit BOB-025; bukan kandidat teruji).
Commit yang meminta RC ditandai oleh penambahan `Docs/Release/build-mode.txt` berisi `Release`; setelah commit dapat dibaca dengan:

```bash
git log -1 --format=%H -- Docs/Release/build-mode.txt
```

**Hash kandidat yang lulus: belum ada.** Hash penuh checkout yang benar-benar diuji runner ditulis di CI.md dan
`Builds/reports/RC/<hash penuh>/manifest.json` sebagai `source_commit`. Semua bukti untuk penetapan kandidat harus berasal
dari hash itu. Laporan historis di bawah tidak memvalidasi commit BOB-025 atau tes online yang baru ditambahkan.

## Bukti tersedia saat persiapan

Sumber: [CI laptop](../Autopilot/CI.md), laporan untuk `b46ff60c2a261e645a6550f1fd4ea6cecaca5a5a`,
2026-10-17 12:39 WIB, **GAGAL**. CI.md bisa berubah; versi historis dapat dibaca lewat
`git show b0a9cac:Docs/Autopilot/CI.md`.

| Bukti | Hasil historis | Bukti kandidat Release |
|---|---|---|
| EditMode Unity | 111/111 lulus | Menunggu XML dan exit 0 pada hash kandidat |
| PlayMode Unity | 15/17 lulus, 2 gagal | Menunggu seluruh suite, tanpa ignored/skipped |
| Build | Development Succeeded; 0 error, 6 warning; 1.785.206.945 byte | Release belum dijalankan |
| Shader / compile | 0 / 0 error | Menunggu build Release |
| Budget Audit | GAGAL; gameplay 111/100, QuestPerfTest 112/100 draw/mata | Menunggu audit kedua scene |
| perf.txt | status=complete, pengukuran Editor mono | Menunggu salinan utuh pada hash kandidat |
| Screenshot | 01–04 ada sebagai pointer LFS; 05–06 belum ada | Menunggu enam gambar dari run kandidat |

Laporan build historis hanya dirujuk CI.md sebagai
`BiteOrBetray_Quest_Development_20261017-123618_b46ff60-dirty.txt`; file mentah/APK belum tersedia di VPS.
Folder `Builds/reports/` sebelumnya tidak dilacak. [Panduan arsip baru](../../Builds/reports/README.md) menjelaskan berkas
yang akan disimpan runner; belum ada arsip hasil Release yang dibuat atau direkayasa di sini.

Angka perf historis: GC idle telemetry aktif rata-rata **6.608,18 B/frame**, puncak 15.501; nonaktif 6.606,11, puncak 14.799;
delta 2,07 B/frame. Indikator mendekati nol (<=1 KiB) **False**. Idle 78 draw / 33 SetPass; frame rata-rata 6,042 ms,
puncak 12,279 ms. Pertandingan otomatis selesai (11 giliran): rata-rata 6,714 ms, puncak 512,419 ms; puncak 85 draw,
38 SetPass. Ini mencakup overhead Editor/Test Runner dan render manual, bukan pengukuran GPU/stereo atau bukti 72 FPS Quest.
Audit maksimum gameplay 217.517 segitiga, 36 material; QuestPerfTest 217.575 segitiga, 37 material. Selisih budget draw
masih +11/+12; slot material/submesh berulang termasuk wajah dan shadow skinned tercantum di audit BOB-022.

## Checklist regression BOB-023

Nama berikut adalah metode di `Assets/Tests/PlayMode/RegressionTests.cs`. Semua baris masih **menunggu run kandidat**;
hasil baseline yang tidak menyebut gagal tidak menggantikan XML tes pada kandidat.

| Tes | Cakupan | Baseline b46ff60 |
|---|---|---|
| Menu_bot_setup_all_onboarding_pages_and_gameplay_with_screens | Menu, Atur bot, 6 halaman onboarding, Mulai, kembali menu | Tidak tercantum gagal |
| Eight_items_follow_game_engine_rules | Kedelapan item, inventory dan hasil sesuai Core | GAGAL: Expected False, was True (Gloves/Fork) |
| Three_candy_colors_and_clover_damage_cap_follow_rules | Tiga warna good/bad, Clover, batas damage 3, lumpuh | Tidak tercantum gagal |
| New_round_refills_dispenser_and_deals_items | Ronde/refill, jumlah candy dan item, waiter | Tidak tercantum gagal |
| Ko_spectator_results_music_sfx_and_telemetry_csv_with_screens | KO, spectator, pemenang, musik/SFX/volume, CSV | GAGAL: musik menu null |
| Five_restarts_keep_objects_materials_audio_sources_and_clips_stable | Lima restart, objek/material/AudioSource/clip stabil | Tidak tercantum gagal |
| Focus_lost_and_regained_cycles_freeze_timer_and_restore_input | Fokus/pause, timer beku, resume input | Tidak tercantum gagal |
| Controller_path_works_and_pinch_never_doubles_input | Jalur controller dan pinch tanpa aksi ganda | Tidak tercantum gagal |

Koreksi ekspektasi Gloves/Fork dan pemutaran musik sudah ada di sumber setelah baseline; hasil ulang belum tersedia.
BOB-025 tidak menutup tugas regression atau tugas lain tanpa bukti baru.

Screenshot yang wajib berasal dari kamera kursi pemain pada run yang sama: `01-menu.png`, `02-atur-bot.png`,
`03-onboarding.png`, `04-gameplay.png`, `05-spectator.png`, `06-hasil-akhir.png`, di
[Docs/Autopilot/screens/](../Autopilot/screens/). Empat pointer yang tersedia belum dapat diperiksa visual di VPS;
tidak ada persetujuan visual yang diklaim. Jangan mengganti pointer atau membuat screenshot sintetis sebagai bukti runtime.

## Checklist online BOB-041 dan blocker

Semua tes berikut belum memiliki bukti di baseline; wajib **Passed** pada XML kandidat:

- `OnlineMatchTests.Host_and_client_play_with_bots_until_winner_with_matching_snapshots`: NGO lokal, host+klien+bot,
  revisi snapshot/HP/inventory/pemenang cocok.
- `OnlineMatchTests.Client_disconnect_mid_game_seat_played_by_bot_until_winner`: klien putus, bot meneruskan permainan.
- `OnlineServicesTests.Create_room_with_relay_find_by_code_and_leave`: autentikasi, Sessions/Lobby/Relay nyata,
  kode room dan cleanup; wajib internet. Assert.Ignore tetap berarti belum tervalidasi untuk RC.
- `OnlineServicesTests.Voice_joins_room_channel_and_leaves_with_room`: channel Vivox, mute Settings dan cleanup.
- Ketiga `OnlineSceneTests`: objek online scene asli, tombol Buat/Gabung Room aktif, spawn meja in-scene dan jalur
  sinkronisasi scene. BOB-046 sudah menambahkan wiring di sumber; validasi serialisasi/runtime masih menunggu Unity.

## Known issues dan known gaps

- Blocker yang masih membutuhkan bukti: dua kegagalan regression baseline setelah perbaikan, audit draw melampaui target,
  GC idle belum mendekati nol, serta suite online/scene baru yang belum dijalankan. Jangan menyatakan CI lama sudah hijau.
- Import dan `GameplayPerformanceBuilder.PrepareScenes` dapat mengubah scene di laptop. Laporan build menandai `-dirty`;
  periksa `workspace.diff` dan `workspace-status.txt` (termasuk aset baru yang tidak masuk patch) agar hasil prepare tidak
  disamakan dengan checkout bersih yang hanya ditandai hash sumber.
- Belum pernah diuji pada Quest fisik: instalasi/launch APK, rendering Android aktual, kenyamanan/stereo, izin mikrofon,
  tracking sensor, performa/termal/baterai dan stabilitas perangkat belum terbukti. Simulator/Editor bukan bukti perangkat.
- Hand tracking masih terbatas pada jalur otomatis (mapping pinch, prioritas input, pencegahan duplikasi). Aim, oklusi,
  tracking loss/recovery perangkat, peralihan tangan/controller dan toleransi dekat wajah belum dibuktikan pada Quest.
- Voice terbatas pada masuk/keluar channel dan mute; saling dengar antar pemain, mikrofon/speaker perangkat, echo,
  latensi dan kualitas audio belum terbukti. `isPlaying` musik hanya dibuktikan jika Editor CI punya output audio.
- Online host+klien memakai transport lokal dengan klien tanpa scene penuh. Pemain kedua lewat Relay dengan identitas UGS
  berbeda, visual pose/candy/item di klien, dua proses dengan scene asli dan jaringan internet nyata belum terbukti penuh.
- APK dibangun dengan keystore debug yang sudah dikonfigurasi (BOB-024); mode Release bukan paket store bertanda tangan
  produksi. Tidak ada klaim kesiapan distribusi store.

## Menjalankan validasi otomatis berikutnya

Di laptop berlisensi Unity 6000.6.3f1 + Android Support, dengan internet dan LFS terunduh, pasang ulang salinan runner
(versi Task Scheduler tidak otomatis mengikuti perubahan repo), lalu paksa satu putaran:

```powershell
powershell -ExecutionPolicy Bypass -File Tools\Autopilot\install_unity_runner.ps1
python "$HOME\bob-ci\bin\unity_runner.py" --force
```

Runner mengambil `origin/develop`, menjalankan EditMode dan PlayMode, menyiapkan scene/audit lalu memanggil
`BiteOrBetray.EditorTools.CIRunner.Run -bobBuildMode Release`. Tidak perlu headset atau langkah runtime manual.
Jika Unity owner terbuka, build ditunda dan laporan belum memenuhi LULUS. Hasil online dilewati tanpa jaringan juga
belum memenuhi LULUS. Build Release langsung tanpa tes (diagnostik saja):

```text
Unity -batchmode -quit -projectPath <repo> -buildTarget Android -executeMethod BiteOrBetray.EditorTools.AvatarValidation.BatchBuild -bobBuildMode Release
```

Untuk menetapkan RC: catat hash penuh manifest yang sama di dokumen ini; pastikan Release Succeeded, exit 0, seluruh
EditMode/PlayMode Passed (termasuk online), audit LULUS, perf status complete dan enam screenshot/CSV tersedia.
Periksa daftar `missing`, SHA-256 APK/bukti dan diff hasil prepare. Perbarui checklist dengan hasil nyata, simpan
known gaps tanpa perangkat, dan baru tandai BOB-025 done. Sampai bukti itu tersedia, status tetap needs_unity_check.
