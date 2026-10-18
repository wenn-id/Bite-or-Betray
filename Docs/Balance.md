# Keseimbangan bot — BOB-051

Pengukuran 18 Oktober 2026. Baseline BotBrain: `eedf88cb762001ef88e85a78f2ec2d5a0d163c37`.
Sesudah: perubahan BOB-051 pada commit yang memuat laporan ini.

## Metode

- Masing-masing versi: **80 konfigurasi × 2.000 game = 160.000 game**, seed engine 1–2000;
  seed keputusan `seed * 31 + 7`. `SimulationMatrix.Combos(2..6)` mencakup seluruh komposisi
  Mudah/Normal/Sulit: 6, 10, 15, 21, dan 28 konfigurasi. Ini mewakili satu pemain + 1–5 bot.
- Semua keputusan otomatis. Satu kursi Normal menjadi pengganti manusia untuk menghitung waktu;
  bila tidak ada Normal, dipakai kursi konfigurasi pertama. `HumanProxyIndex` mengikuti `ConfigIndex`
  sesudah d20 mengacak urutan. Strategi kursi pengganti tetap BotBrain, bukan data perilaku manusia.
- RuleConfig default identik: HP 4/5, 2 item/ronde, maksimum 6 item, rasio bad dasar 0,38–0,52
  ditambah 0,04 per kursi di atas dua. Distribusi spawn item seragam pada kedua versi.
- Win rate = jumlah kemenangan tingkat itu / jumlah game / jumlah kursinya. Ini **per kursi**,
  sehingga dua Normal tidak otomatis dianggap lebih kuat dari satu Sulit. Tingkat yang tidak ada ditulis “—”.
- Batas 2.000 langkah/game; keputusan tidak valid dihitung sebelum fallback. Baseline memakai
  BotBrain lama dengan instrumentasi Simulation baru agar diagnosis dan model waktu sama.

Data lengkap: [sebelum](Balance-before.csv) dan [sesudah](Balance-after.csv), masing-masing 80 baris.

## Win rate dan durasi sebelum/sesudah

M = Mudah, N = Normal, S = Sulit. Triple win rate di bawah berurutan **M / N / S**.
Durasi adalah rata-rata estimasi menit per pertandingan, termasuk kursi pengganti manusia.

| Bot + pengganti manusia | Komposisi seluruh kursi | Win rate sebelum M / N / S | Sesudah M / N / S | Menit sebelum | Sesudah |
|---|---|---|---|---:|---:|
| 1 bot | MN | 26.65% / 73.35% / — | 20.55% / 79.45% / — | 4.76 | 4.21 |
| 1 bot | NS | — / 44.95% / 55.05% | — / 34.75% / 65.25% | 5.94 | 4.76 |
| 1 bot | MS | 21.20% / — / 78.80% | 8.75% / — / 91.25% | 4.68 | 3.76 |
| 2 bot | MNS | 10.95% / 39.95% / 49.10% | 5.70% / 32.85% / 61.45% | 7.09 | 6.36 |
| 3 bot | MNNS | 6.65% / 28.80% / 35.75% | 2.95% / 24.60% / 47.85% | 8.62 | 7.79 |
| 4 bot | MMNNS | 4.53% / 28.20% / 34.55% | 2.78% / 25.70% / 43.05% | 9.27 | 8.79 |
| 5 bot | MMNNSS | 2.73% / 21.28% / 26.00% | 1.58% / 18.23% / 30.20% | 10.21 | 9.64 |

Pada seluruh 80 konfigurasi sesudah, setiap pasangan tingkat yang hadir memiliki urutan benar.
Ada 35 perbandingan M–N, 35 N–S, dan 35 M–S. Selisih minimum N–S meningkat dari
**2,72 menjadi 7,82 poin persentase**. Selisih minimum M–N sesudah **11,40 poin**.
Pada duel N–S, Sulit menang **55,05% → 65,25%**; pada duel M–N, Normal **73,35% → 79,45%**.
Angka sampel bukan jaminan urutan setiap game; seed tetap menjaga regresi dapat direproduksi.

## Target tiga bot: rata-rata 5–10 menit

Kursi pengganti selalu Normal pada tabel ini. Setiap baris berisi 2.000 game per versi.
Win rate bot dihitung per kursi, termasuk ketika seluruh kursi sama-sama Normal.

| Tingkat ketiga bot | Win rate bot sebelum | Sesudah | Menit sebelum | Sesudah | P10–P90 sebelum | P10–P90 sesudah |
|---|---:|---:|---:|---:|---|---|
| Mudah | 14.85% | 13.08% | 7.76 | 7.43 | 5.16–10.59 | 4.99–10.16 |
| Normal | 25.00% | 25.00% | 8.90 | 7.91 | 5.66–12.85 | 4.90–11.56 |
| Sulit | 25.92% | 28.37% | 8.95 | 7.87 | 5.65–13.08 | 5.10–11.18 |

Campuran M/N/S + pengganti N: **8,62 → 7,79 menit**, P90 **12,34 → 11,26 menit**.
Seluruh 15 komposisi empat kursi sesudah mempunyai rata-rata 6,78–7,95 menit.
Target rata-rata terpenuhi; ekor pertandingan masih bisa melewati 10 menit, seperti P90 pada tabel.

### Asumsi waktu yang dipakai pada kedua versi

Profil `BalanceTests.OfflineTime` memakai `tempo = 1` dan jeda dalam
[`CandyTableDirector`](../Assets/Scripts/Table3D/CandyTableDirector.cs): bot berpikir acak 1,2–2,4 detik,
`AnnounceRound` menunggu 3 detik, `SyncCandy` bergerak 0,6 + menunggu 0,3 detik.
Estimasi makan/suap dibulatkan ke atas dari `Consume` dan jeda 0,5 detik sesudah aksi,
untuk memberi ruang pada reaksi BAD/paralyze dan interaksi. Item memakai cadangan durasi bawaan.

| Komponen TimeModel | Detik |
|---|---:|
| GameIntro / GameOutro | 30 / 15 |
| WaiterFirstRound / WaiterNextRounds | 3 / 3 |
| Dispense | 0,9 |
| ThinkPerDecision manusia / BotThinkPerDecision | 7 / 1,8 |
| TurnThinkLimit | 30 |
| Eat / Give / SkipTurn | 4 / 4,5 / 2 |
| Fork / Hammer / Milk / Gloves / Cupcake | 4 / 5 / 4 / 4 / 4 |
| LittlePocket + PocketChoice / GrapplingHook / Clover | 4 + 4 / 7 / 3 |

`AssumeAllHuman = false`. Pembuka/penutup mencakup cadangan orientasi pemain. Timer pada model
membatasi estimasi berpikir; simulasi ini tidak memicu timeout manusia atau mensimulasikan input VR.
Profil ini estimasi dari kode, **belum telemetry Unity/Quest**. Default TimeModel lama tetap untuk
pembanding prototipe/GDD; `TargetMinutes` lama 11–16 menit untuk 3–4 pemain mengacu profil itu.
Evaluasi BOB-051 memakai target 5–10 menit dan profil eksplisit di atas, identik sebelum/sesudah.

## Perubahan keputusan

- Normal lebih berhati-hati saat HP penuh (ambang peluang good 0,65), dan memakai Clover
  pada bad yang pasti atau peluang bad dominan; kuning menguntungkan karena good tidak menyembuhkan.
- Sulit membandingkan nilai heal/damage sesuai warna, batas damage Clover, risiko KO, manfaat
  menghabisi lawan, dan kesempatan melanjutkan giliran. Tetap hanya memakai informasi publik.
- Sulit mencuri Milk saat membutuhkan HP dan menyimpan Fork saat sudah kebal Cupcake.
  Sulit memilih menyerang dengan bad ketimbang menghancurkannya lewat Hammer dari inventory/hook.
- Normal/Sulit memakai kepastian hitungan publik 0%/100% tanpa Fork. Clover tidak dihabiskan
  untuk good kuning yang tidak memberi heal; skor pilihan Pocket mengikuti kegunaan tersebut.

## Progres dan regresi

| Pemeriksaan massal | Sebelum | Sesudah |
|---|---:|---:|
| Pertandingan selesai | 160.000/160.000 | 160.000/160.000 |
| Keputusan tidak valid sebelum fallback | 0 | 0 |
| Langkah maksimum/game (batas 2.000) | 176 | 160 |
| Ronde maksimum/game | 22 | 16 |
| Clover aktif/game, empat kursi Normal | 0,00 | 2,20 |

Tidak ditemukan pertandingan macet pada sampel ini. Langkah terbatas, item berkurang saat digunakan,
dan konsumsi/serangan menyelesaikan pertandingan; diagnosis mencatat fallback yang sebelumnya tersembunyi
oleh `BotBrain.Step`. Durasi turun bersamaan dengan penggunaan Clover yang lebih efektif.

`BalanceTests` menjalankan 20 kasus: duel bertingkat, urutan per kursi pada meja campuran 3/4/6,
durasi tiga bot di setiap tingkat, matriks kecil seluruh 80 konfigurasi (16 game/konfigurasi),
Fork/Cupcake, hitungan pasti, risiko KO, kesetaraan keputusan saat rasa tersembunyi ditukar,
dan kursi pengganti manusia setelah pengacakan d20. Kasus urutan memakai 128 game dengan seed tetap;
batch statistik harian memakai 2.432 game, jauh lebih kecil dari batch pengukuran.

### Menjalankan ulang tanpa Unity

Dari root repo, gunakan compilekit/CoreTests yang sudah terpasang oleh Autopilot (tidak perlu package baru):

```bash
BOB_BALANCE_GAMES=2000 BOB_BALANCE_OUTPUT=/tmp/BOB-051-after.csv \
  ~/.dotnet/dotnet test ~/bob/coretest/CoreTests.csproj \
  -p:RepoRoot="$PWD/" \
  --filter FullyQualifiedName~BalanceTests.Matrix_finishes_without_invalid_decisions
~/bob/bin/check.sh
```

Baseline dapat diulang tanpa mengganti file kerja:

```bash
baseline_dir=$(mktemp -d)
mkdir -p "$baseline_dir/Assets/Scripts/Core" "$baseline_dir/Assets/Tests/EditMode"
cp Assets/Scripts/Core/*.cs "$baseline_dir/Assets/Scripts/Core/"
cp Assets/Tests/EditMode/BalanceTests.cs "$baseline_dir/Assets/Tests/EditMode/"
git show eedf88cb762001ef88e85a78f2ec2d5a0d163c37:Assets/Scripts/Core/BotBrain.cs \
  > "$baseline_dir/Assets/Scripts/Core/BotBrain.cs"
BOB_BALANCE_GAMES=2000 BOB_BALANCE_OUTPUT=/tmp/BOB-051-before.csv \
  ~/.dotnet/dotnet test ~/bob/coretest/CoreTests.csproj \
  -p:RepoRoot="$baseline_dir/" \
  --filter FullyQualifiedName~BalanceTests.Matrix_finishes_without_invalid_decisions
```

## Bukti CI

Pengukuran sebelum/sesudah dan tes Core berjalan di .NET 8 tanpa Unity. `~/bob/bin/check.sh`
**CHECK LULUS**: seluruh assembly termasuk tes PlayMode terkompilasi, **139/139 tes Core lulus**.
Kedua batch massal (sebelum dan sesudah) juga lulus lewat filter tes yang dicantumkan di atas.
Status tugas **needs_unity_check** sampai runner laptop menghasilkan CI.md **LULUS** untuk commit
ini (EditMode, PlayMode, Budget Audit, shader, dan build). CI.md lama bukan bukti perubahan BOB-051.
Verifikasi tersebut otomatis; tidak memerlukan pemeriksaan manual dengan headset.
