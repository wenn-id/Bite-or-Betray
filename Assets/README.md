# Bite or Betray: paket test Unity (Fase 1)

Paket ini berisi semua aturan GDD dalam C# murni, plus meja test di Unity Editor tanpa VR, AI bot, dan unit test.

## Cara pasang (Unity 6)

1. Buat project baru di Unity Hub. Template apa saja boleh, misalnya **Universal 3D**.
2. Salin folder `BiteOrBetray` ke dalam folder `Assets` project kamu, lalu tunggu Unity selesai compile.
3. Di menu atas, klik **Bite or Betray > Buat Scene Debug Test**.
4. Tekan **Play**. Atur kursi, lalu klik **MULAI GAME**.

Kalau menu itu tidak muncul, buat GameObject kosong di scene mana pun, tambahkan komponen `BiteOrBetrayDebugGame`, lalu tekan Play.

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

## Unit test

Buka **Window > General > Test Runner**, pilih tab **EditMode**, lalu klik **Run All**. Ada 40 test yang mencakup aturan dan edge case di GDD. Kalau package Test Framework belum terpasang, install `com.unity.test-framework` dari Package Manager.

## Struktur

| Folder | Isi |
| --- | --- |
| `Scripts/Core` | Aturan game (`GameEngine`), data (`CoreTypes`, `PlayerState`), AI bot (`BotBrain`), dan simulator (`Simulation`). Tidak bergantung pada UnityEngine, jadi bisa dipakai ulang untuk VR dan online. |
| `Scripts/DebugTools` | `BiteOrBetrayDebugGame`, UI meja test berbasis IMGUI. |
| `Scripts/Editor` | Menu pembuat scene test dan jendela Simulator. |
| `Tests/EditMode` | Unit test NUnit. |

## Catatan aturan

- Semua aksi dilakukan oleh pemain yang sedang giliran (`engine.Current`): `Eat()`, `Give(seat)`, `UseItem(index, args)`, `ChoosePocketItem(index, args)`, dan `DiscardPocket()`.
- Grappling Hook butuh `new ItemArgs(targetSeat, targetItemIndex)`.
- Pembagian good/bad per warna tidak pernah diumumkan. Yang diumumkan hanya jumlah per warna dan total good/bad.
- Damage satu permen maksimal 3, termasuk setelah Clover (`CandyRules.MaxDamagePerCandy`). Biru bad + Clover jadi 3, bukan 4, supaya combo Fork + Clover + Give tidak bisa langsung mengalahkan pemain dari HP awal 4.
- Ronde baru dimulai saat dispenser kosong dan permen baru dibutuhkan. Giliran yang sedang berlanjut tetap lanjut, dan clover ikut pindah.
- Fitur yang belum ada di paket ini: voice chat spectator, room online, dan visual VR. Semuanya dijadwalkan di fase berikutnya roadmap.
