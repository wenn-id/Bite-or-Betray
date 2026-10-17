# Bite or Betray: project Unity (VR Meta Quest 3)

Project Unity 6 (`6000.6.3f1`, URP) berisi aturan GDD dalam C# murni, game VR di scene `CandyParlour3D` (lawan bot dan
room online), meja test 2D tanpa VR, simulator balancing, serta tes EditMode dan PlayMode.

## Cara buka

1. Clone repo dengan Git LFS terpasang (`git lfs pull`): model `.fbx`, tekstur, dan audio disimpan di LFS.
2. Buka folder repo di Unity Hub dengan Unity `6000.6.3f1` dan modul Android Build Support.
3. Game VR: menu **Bite or Betray > Buka Scene 3D Candy Parlour**, lalu Play (tanpa headset dimainkan dengan mouse).
4. Meja test 2D: menu **Bite or Betray > Buat Scene Debug Test**, tekan **Play**, atur kursi, lalu klik **MULAI GAME**.

Kalau menu itu tidak muncul, buat GameObject kosong di scene mana pun, tambahkan komponen `BiteOrBetrayDebugGame`, lalu tekan Play.

## Status VR

Implementasi VR sudah ada di `Scripts/Table3D` dan scene `BiteOrBetray/Scenes/CandyParlour3D.unity` (Meta XR SDK,
`OVRCameraRig`): input hand tracking dengan pointer controller sebagai cadangan (`VRTableInput`), menu utama dan Settings di meja,
room offline 1-5 bot, spectator untuk pemain kalah, layar hasil dan main lagi, audio, onboarding, avatar kepala/tangan, serta
room online (Netcode for GameObjects, Lobby/Relay Multiplayer Services, voice Vivox). Semua perilaku itu dicek tes PlayMode di
Editor; belum ada pengujian di headset Quest. Fitur yang masih menunggu bukti Unity/Quest tercatat dengan status
`needs_unity_check` di `Docs/Autopilot/tasks.json`.

## Cara main di meja test

- **Panel kiri:** semua pemain, beserta HP, status, item, dan hasil d20.
- **Panel tengah:** pengumuman pelayan, permen aktif, dan tombol aksi (EAT, GIVE, item).
- **Panel kanan:** log semua kejadian.
- Kursi manusia dimainkan bergantian di layar yang sama (hotseat). Bot jalan otomatis, dan jedanya bisa diatur.
- **Mode dewa** memperlihatkan isi permen aktif. Pakai untuk debug saja.
- **Seed** yang sama menghasilkan game yang sama. Seed tampil di bawah tombol setelah game dimulai.
- Angka spawn (jumlah permen per ronde, rasio bad, item per ronde) bisa diubah di Inspector, bagian **Rules**. Default-nya adalah balancing final.
- **Timer giliran:** kursi manusia punya 30 detik berpikir per giliran (atur di Rules > Turn Time Limit Seconds). Kalau habis, Little Pocket yang terbuka dibuang, permen aktif dimakan paksa, dan giliran berakhir.
- **Telemetry:** setiap keputusan dicatat ke CSV (detik berpikir, aksi, ronde, giliran) di folder telemetry. Tombol **Folder telemetry** membuka foldernya.
- Kalau teks terlalu kecil atau besar, ubah **Ui Scale** di Inspector.

## Simulator (laporan balancing)

Klik menu **Bite or Betray > Simulator**. Atur jumlah game, jumlah pemain, dan tingkat bot per kursi, lalu klik **Jalankan simulasi**. Semua kursi dimainkan bot, dan 500 game biasanya selesai dalam beberapa detik.

Laporan berisi durasi game (giliran, ronde, dan estimasi menit di VR), win rate per tingkat bot, win rate per urutan giliran, rata-rata aksi dan pemakaian item per game, serta catatan otomatis kalau ada angka yang mencurigakan. Laporan bisa disalin, disimpan sebagai `.md`, atau disimpan datanya per game sebagai `.csv` untuk diolah di spreadsheet.

Estimasi menit memakai **Model waktu**: perkiraan detik untuk pembuka dan penutup game, pelayan, dispenser, berpikir (dibatasi timer per giliran), makan, menyuapi, dan durasi masing-masing item. Tombol **Kalibrasi dari telemetry** mengganti detik berpikir dengan rata-rata asli dari CSV telemetry meja test. Angkanya bisa diubah di jendela Simulator, dan sebaiknya dikalibrasi setelah playtest. Laporan juga membandingkan hasilnya dengan target durasi per jumlah pemain dari GDD.

Default Rules sudah memakai balancing final: HP maks 5 (mulai 4), 3 permen per pemain hidup (4-12 per ronde), rasio bad 38-52% ditambah 4% per kursi di atas 2, dan 2 item per ronde.

Tombol **Jalankan matriks semua kombinasi bot** menjalankan semua 80 kombinasi Easy/Normal/Hard untuk 2-6 pemain, lalu menyimpan dua CSV: ringkasan per kombinasi (durasi, ronde, giliran per ronde, permen dan item per ronde dan per game, win rate per tingkat) dan profil per ronde.

Rules di Simulator terpisah dari Rules di scene. Klik **Ambil Rules dari scene** untuk menyalin angka dari komponen `BiteOrBetrayDebugGame`.

## Tes

Buka **Window > General > Test Runner**, lalu klik **Run All** di tab masing-masing:

- **EditMode** (`Tests/EditMode`): 113 tes (dulu 42) untuk aturan, edge case GDD, room, settings, onboarding, protokol meja, dan
  room online. Semuanya logika Core, jadi juga bisa dijalankan tanpa Unity dengan `dotnet test` (dipakai `check.sh` agent).
- **PlayMode** (`Tests/PlayMode`): 30 tes di scene sungguhan: smoke scene, pertandingan offline penuh, kamera, regresi
  gameplay/audio/telemetry, performa, perekam video, serta online host+klien dan Unity Gaming Services (butuh internet; tanpa
  internet tes online di-skip dengan `Assert.Ignore`).

## Build APK Quest

- Dari Editor: **Bite or Betray > Build Quest APK > Development** atau **Release**. Scene yang di-build hanya `CandyParlour3D`,
  hasilnya `Builds/BiteOrBetray_Quest_<mode>.apk` dengan laporan di `Builds/reports/` (durasi, error, aset terbesar, varian shader).
  GraphicsSettings dan Build Profile dipulihkan setelah build supaya git status tetap bersih.
- Dari command line (tanpa membuka Editor):

  ```
  Unity -batchmode -quit -projectPath <repo> -buildTarget Android -executeMethod BiteOrBetray.EditorTools.AvatarValidation.BatchBuild -bobBuildMode Release
  ```

  `-bobBuildMode` boleh `Development` (default) atau `Release`. Exit code 1 kalau build gagal.
- `BiteOrBetray.EditorTools.CIRunner.Run` menjalankan Budget Audit Quest (`Logs/ci/audit.txt`) lalu build Development; ini yang
  dipakai runner CI.

## Pemeriksa statis scene

`python3 Tools/CI/check_scene.py` membaca YAML `CandyParlour3D` tanpa Unity dan melaporkan:

- GUID yang dirujuk scene tetapi tidak punya `.meta` di `Assets/`, `Packages/`, atau `Library/PackageCache/`. Tanpa
  `Library/PackageCache` (repo yang belum pernah dibuka Unity), GUID milik package hanya ditandai "belum terverifikasi";
  `--strict` membuatnya gagal.
- Objek yang dicari kode lewat nama: `BoB_Art3D`, `Seats`, `Seat_0/HeadAnchor`, `Bot_<warna>`, dan anchor `OVRCameraRig`
  (`TrackingSpace`, `Left/RightHandAnchor`, `Left/RightControllerAnchor`, `CenterEyeAnchor`). Objek yang punya cadangan di kode
  (pelayan, dispenser, cetakan permen dan item) hanya diberi peringatan.

Exit 0 = lulus, 1 = ada yang hilang. `--tree` mencetak hierarki hasil parse, dan path scene lain boleh diberikan sebagai argumen.

## CI

- **Runner laptop** (`Tools/Autopilot/unity_runner.py`) adalah satu-satunya CI yang benar-benar berjalan: Unity berlisensi di
  laptop owner menjalankan tes EditMode dan PlayMode, Budget Audit, dan build APK Development untuk branch `develop`, lalu menulis
  hasilnya ke `Docs/Autopilot/CI.md`.
- **GitHub Actions** (`.github/workflows/unity-ci.yml.draft`) masih draft dan belum pernah dijalankan. Isinya: pemeriksa statis
  scene, tes EditMode/PlayMode, dan build Android dengan game-ci. Kebutuhan secret lisensi Unity (`UNITY_LICENSE` atau
  `UNITY_SERIAL`, `UNITY_EMAIL`, `UNITY_PASSWORD`), runner, dan langkah mengaktifkannya ada di kepala file. Workflow ini baru
  boleh diaktifkan dengan persetujuan pemilik project.

## Struktur

| Folder | Isi |
| --- | --- |
| `Scripts/Core` | Aturan game (`GameEngine`), data (`CoreTypes`, `PlayerState`), AI bot (`BotBrain`), dan simulator (`Simulation`). Tidak bergantung pada UnityEngine, jadi bisa dipakai ulang untuk VR dan online. |
| `Scripts/Table3D` | Game VR: `CandyTableDirector` (alur meja), input VR, avatar, audio, dan online (`Net/`). |
| `Scripts/DebugTools` | `BiteOrBetrayDebugGame`, UI meja test berbasis IMGUI. |
| `Scripts/PerfTest` | Monitor performa untuk scene Quest Perf Test. |
| `Scripts/Editor` | Menu pembangun scene, build APK, CI (`CIRunner`), Budget Audit, perekam video, dan jendela Simulator. |
| `Tests/EditMode` | Tes NUnit untuk logika Core. |
| `Tests/PlayMode` | Tes scene sungguhan (offline, online, regresi, performa). |
| `../Tools/CI` | Pemeriksa statis scene (`check_scene.py`). |

## Catatan aturan

- Semua aksi dilakukan oleh pemain yang sedang giliran (`engine.Current`): `Eat()`, `Give(seat)`, `UseItem(index, args)`, `ChoosePocketItem(index, args)`, dan `DiscardPocket()`.
- Grappling Hook butuh `new ItemArgs(targetSeat, targetItemIndex)`.
- Pembagian good/bad per warna tidak pernah diumumkan. Yang diumumkan hanya jumlah per warna dan total good/bad.
- Damage satu permen maksimal 3, termasuk setelah Clover (`CandyRules.MaxDamagePerCandy`). Biru bad + Clover jadi 3, bukan 4, supaya combo Fork + Clover + Give tidak bisa langsung mengalahkan pemain dari HP awal 4.
- Ronde baru dimulai saat dispenser kosong dan permen baru dibutuhkan. Giliran yang sedang berlanjut tetap lanjut, dan clover ikut pindah.
- Room online, voice chat (termasuk spectator), dan visual VR sudah diimplementasikan; statusnya ada di bagian **Status VR**.
