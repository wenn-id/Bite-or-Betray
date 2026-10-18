# Roadmap: semua worker Bite Autopilot mengerjakan Bite or Betray

Versi 1 · 16 Oktober 2026 · disusun dari isi `wenn-id/bite-autopilot-engineering` (main `4800371`) dan
`wenn-id/Bite-or-Betray` (main `4a08b0d`). Status di bawah adalah rencana; belum ada yang dikerjakan.

## Ringkasan

Hari ini worker sudah terbukti bisa membuat kode, mengetes, di-review Claude, lalu auto-merge. Tapi
jalurnya masih sempit: **Python standar saja, di repo uji coba saja**. Ke-25 tugas game (BOB-001 sampai
BOB-025, plus BOB-026/027 di antrean usulan) semuanya Unity/C#. Supaya worker bisa mengerjakannya,
perlu tiga hal baru:

1. **Target kerja untuk repo game** yang aman: worker hanya menulis ke repo staging, `main` game tetap
   lewat persetujuanmu.
2. **Jalur C#/Unity**: Codex mengirim patch C#, lalu runner Unity mengecek compile, 42 EditMode test,
   dan build Android.
3. **Katalog tugas bergulir**: base mengikuti ujung branch staging, supaya tugas berikutnya tidak tertahan
   `CATALOG_BASE_ADVANCED` setiap kali ada merge.

Setelah itu, sekitar **19 dari 27 tugas bisa dikerjakan worker**. Sisanya (uji di Quest 3, keputusan
release) tetap butuh manusia.

| Tahap | Isi | Siapa | Perkiraan |
|---|---|---|---|
| 0 | Keputusan owner dan bereskan akses | Kamu | 1 hari |
| 1 | Pemanasan: hidupkan lagi worker Python | Codex (VPS) | 0,5 hari |
| 2 | Onboard repo game (staging + App + katalog bergulir) | Codex | 2–3 hari |
| 3 | Jalur C#/Unity + runner Unity | Codex + laptop | 3–5 hari |
| 4 | Eksekusi tugas BOB bergelombang | Worker, kamu di gate Quest | 5–12 hari |
| 5 | Skala dan operasi harian | Codex | berjalan |

Perkiraan waktu kasar, tergantung kuota harian dan ketersediaan runner Unity.

## Kondisi awal (fakta)

| Hal | Keadaan sekarang | Sumber |
|---|---|---|
| Jalur worker | `clamp-fix` dan `python-repo-fix`; 1–5 file `.py`/`.md`, total maks 7.200 byte | `docs/REPOSITORY-TASKS.md`, `autopilot/repository_task.py` |
| Sandbox | Landlock + seccomp: **tanpa jaringan, tanpa membuat proses atau exec** | `docs/REPOSITORY-TASKS.md` |
| Target tulis | Hanya `bite-autopilot-results`; auto-merge fast-forward aktif di sana | `AUTO-MERGE.md` |
| Kuota | 5 work item per hari (WIB), 1 berjalan bersamaan; coding dan review masing-masing 1 item | `AUTO-MERGE.md` |
| Base katalog | Dipatok ke satu SHA; setelah merge, tugas baru ditahan `CATALOG_BASE_ADVANCED` | `AUTO-MERGE.md` |
| Unity | Tidak ada runner Unity di VPS. Laptop sudah terbukti: L1 42/42 EditMode test dan L2 APK staging di `07c9f7c` | `evidence/unity-editmode.json`, `evidence/android-build.json`, `docs/SECURITY-BLOCKERS.md` |
| Quest 3 | Belum pernah diuji di headset | `evidence/android-build.json` (`quest3_hardware_tested: false`) |
| Antrean game | BOB-001..025 di `task-queue.json`, BOB-026/027 di `task-queue.proposed.json`; semua `todo` | repo engineering |
| Repo game | Sudah maju ke `4a08b0d` (karakter Meshy full-body), di atas `07c9f7c` yang dites | `git log` Bite-or-Betray |

## Aturan main

- **Worker tidak pernah menulis ke `main` repo game.** Worker menulis ke repo staging. Kamu yang
  memindahkan hasil ke `main`, atau nanti lewat auto-merge yang dibatasi ke branch staging.
- **Setiap tugas dibuktikan oleh runner Unity**, bukan cuma oleh review model. Patch tanpa bukti compile
  dan test tidak boleh lolos.
- **Isi Issue tetap dianggap data.** Repo, path, base, perintah test, dan model tetap ditentukan katalog
  milik root, persis seperti sekarang.
- **Tugas fisik Quest 3 selalu manusia.** Worker hanya menyiapkan APK dan daftar cek.
- **Semua perubahan izin, kuota, dan target butuh persetujuan owner.** Itulah isi Tahap 0.

## Tahap 0 — Keputusan owner (1 hari)

Tanpa keputusan ini, tahap berikutnya tidak boleh mulai.

| # | Keputusan | Rekomendasi | Kenapa penting |
|---|---|---|---|
| D1 | Langganan ChatGPT/Claude untuk layanan otomatis, atau API key | Cek ketentuan masing-masing. Kalau ragu, pindah ke API key lewat `bite-api-broker` yang sudah dirancang (`openai-api-key`, `anthropic-api-key`) | Dokumen sistem sendiri menandai izin langganan "unverified" |
| D2 | Deploy key `bitebot-irengcloud` (akses tulis) di repo game | Hapus, atau ganti jadi read-only | Saat ini satu-satunya jalur tulis langsung ke repo game, di luar gate |
| D3 | Izin Manus Connector (Cue) di repo engineering | **Diputuskan 16 Okt: tetap seperti sekarang** (risiko diterima owner) | Cue tetap bisa mengubah kode sistem; perubahan kode dari Cue sebaiknya tetap lewat PR yang dibaca owner |
| D4 | Kebijakan merge repo game | Worker → repo staging. PR ke `main` di-merge manual oleh kamu | GitHub Free tidak punya branch protection di repo private, jadi pengaman hanya ada di kode VPS |
| D5 | Runner Unity | Mulai dengan laptop (sudah terbukti), rencanakan mesin terpisah untuk mode laptop mati | VPS belum punya Unity berlisensi |
| D6 | Kuota harian | Naikkan dari 5 ke 10 item/hari selama Tahap 4, tetap 1 berjalan bersamaan | 19 tugas × (coding + review) + ulangan ≈ 50 item |
| D7 | Package ID dan nama perusahaan (untuk BOB-003) | Mis. `com.wennid.biteorbetray` | Worker tidak boleh menebak identitas aplikasi |
| D8 | Kanal alert | Telegram atau email untuk tugas gagal, runner mati, dan kuota habis | G7 di `SECURITY-BLOCKERS.md` belum punya kanal |

**Selesai bila:** D1–D8 tercatat di Issue engineering dengan keputusanmu, D2 sudah diterapkan, dan
`SECURITY-BLOCKERS.md` diperbarui (D3 dicatat sebagai risiko yang diterima).

## Tahap 1 — Pemanasan: worker Python hidup lagi (0,5 hari)

Tujuannya membuktikan siklus berjalan setelah `CATALOG_BASE_ADVANCED`, sebelum masuk ke repo game.

1. Tambah template katalog baru di repo results dengan base `46bd63d` (main sekarang), misalnya
   `python-repo-fix-2`: perluas `batch_utils` dengan fungsi baru dan acceptance test baru.
2. Cue membuat satu Issue; worker coding → tes sandbox → review Claude → PR → auto-merge.
3. Pastikan status Issue dan receipt merge tercatat seperti PR #3.

**Selesai bila:** satu PR hasil baru ter-merge otomatis di base baru, dan `CATALOG_BASE_ADVANCED` muncul
lagi untuk tugas lama (perilaku yang benar).

## Tahap 2 — Onboard repo game (2–3 hari)

### 2a. Repo staging

- Buat repo private `wenn-id/bite-or-betray-staging`, isi awal = mirror `Bite-or-Betray` di `4a08b0d`
  (termasuk Git LFS).
- Branch `autopilot/integration` sebagai base kerja worker. `main` staging hanya diubah oleh sinkronisasi
  dari repo game.
- Skrip sinkronisasi dua arah yang dijalankan owner:
  - **game → staging:** setelah kamu push ke game `main`, `autopilot/integration` di-rebase atau di-merge
    dari situ;
  - **staging → game:** kamu menarik hasil worker dengan `git fetch staging` lalu merge ke game `main`,
    atau lewat PR biasa.

### 2b. Izin GitHub App

- Pasang App publisher (atau App baru "Bite Publisher Game") **hanya ke repo staging**, dengan izin
  Contents + Pull requests + Checks write.
- Kode publisher mengunci repo ID staging dan hanya branch `autopilot/*`. `main` dan
  `autopilot/integration` hanya maju lewat gate fast-forward.
- Repo game asli tidak diberi App apa pun.

### 2c. Katalog bergulir

- Template katalog tidak lagi mematok satu SHA. Template mematok **ref** `autopilot/integration`.
  Coordinator mencatat SHA ujungnya saat claim.
- Gate merge tetap mewajibkan ujung branch masih SHA yang sama (sudah ada: "base berubah → tahan").
- Satu template per tugas BOB: `task_type = "bob-0NN"`. Isinya:
  - path yang boleh disentuh, diambil dari kolom `files` di `task-queue.json`;
  - check runner yang wajib lulus;
  - acceptance criteria sebagai teks review.
- Generator katalog (`tools/repository_catalog.py`) membaca `task-queue.json` → template. Hasilnya
  ditinjau owner sebelum dipasang sebagai root.

**Selesai bila:** worker bisa claim satu tugas dummy di repo staging dan membuat draft PR ke
`autopilot/integration`, tanpa satu pun izin ke repo game.

## Tahap 3 — Jalur C#/Unity (3–5 hari)

### 3a. Format patch

Batas sekarang (total 7.200 byte, file utuh) tidak cukup: `CandyTableDirector.cs` saja 1.311 baris.

- Codex mengirim **unified diff** per file, bukan file utuh. Maks 8 file dan 40 KB diff per tugas.
- Ekstensi yang boleh: `.cs`, `.asmdef`, `.md`, dan `ProjectSettings/*.asset` khusus tugas setting.
  `.unity`, `.prefab`, `.mat`, `.fbx`, dan gambar **dilarang** untuk worker. Perubahan aset seperti itu
  dibuat oleh skrip Editor yang dijalankan runner (lihat BOB-011).
- File `.meta` untuk script baru dibuat oleh runner Unity saat import, lalu ikut di-commit runner. Worker
  tidak membuat GUID sendiri.
- Diff harus diterapkan bersih (`git apply --check`) di SHA base. Kalau tidak, tugas gagal tanpa diulang
  otomatis.

### 3b. Runner Unity

| Pilihan | Kelebihan | Kekurangan |
|---|---|---|
| **A. Laptop** (`ci/UnityBuild.cs`, `tools/unity_ci.py`, sudah terbukti L1/L2) | Siap sekarang, lisensi sudah ada | Laptop harus menyala |
| B. Unity Linux di VPS | Jalan saat laptop mati | Butuh RAM ≥16 GB, disk ±30 GB, dan aktivasi lisensi headless; cek ketentuan lisensi Unity |
| C. Mesin Windows terpisah | Paling mirip laptop | Biaya mesin dan lisensi |

Rekomendasi: **mulai dengan A**. Runner menarik job dari antrean (polling keluar), jadi laptop tidak perlu
membuka port. Rencanakan B atau C sebelum Tahap 5.

**Tingkat bukti per job runner:**

| Level | Isi | Dipakai untuk |
|---|---|---|
| L0 | `git apply` bersih + pemeriksa statis scene (dari BOB-024) | Semua tugas |
| L1 | Compile semua assembly + 42 EditMode test (bertambah sesuai tugas) | Semua tugas kode |
| L2 | Build APK Android (profil Quest) + laporan build | BOB-004, 006, 015, 025 dan tugas setting |
| L3 | Uji di Quest 3 | Manusia saja |

Runner menulis bukti sebagai check di PR staging (pola yang sama dengan "Bite OS verification"):
- SHA log Editor;
- hasil XML test;
- SHA APK.

### 3c. Lane .NET murni (opsional, cepat)

`Assets/Scripts/Core` adalah C# murni tanpa UnityEngine. Tugas Core (mis. BOB-016) bisa dites dengan
`dotnet test` di VPS tanpa Unity. Masalahnya, sandbox sekarang melarang membuat proses dan exec. Butuh
profil sandbox kedua yang hanya mengizinkan toolchain `dotnet`, tetap tanpa jaringan dan dengan paket
NuGet yang sudah di-cache. Kalau terlalu rumit, lewati saja dan pakai runner Unity.

### 3d. Review

Review Claude tetap advisory, tetapi untuk repo game ditambah acceptance criteria tugas sebagai daftar cek.
Temuan "requested changes" menahan merge ke `autopilot/integration`.

**Selesai bila:** satu tugas BOB nyata (rekomendasi BOB-016 atau BOB-027) lolos L0 + L1 dari patch Codex,
ter-review, lalu masuk `autopilot/integration`.

## Tahap 4 — Eksekusi tugas BOB

### Jalur per tugas

Keterangan jalur:
- **W**: worker + runner L0/L1;
- **W+B**: worker + build APK (L2);
- **R**: runner saja, tanpa model;
- **H**: manusia di Quest 3;
- **D**: keputusan owner.

| ID | Tugas (singkat) | Jalur | Bergantung | Catatan |
|---|---|---|---|---|
| BOB-005 | Baseline compile + 42 EditMode test | R | – | Sudah lulus di `07c9f7c`; ulangi di `4a08b0d` lalu tutup |
| BOB-026 | Perbaiki referensi URP Quest secara statis | W+B | – | Versi otomatis dari BOB-001 |
| BOB-027 | Konfigurasi Build Scenes Android | W+B | – | Versi otomatis dari BOB-002 |
| BOB-001 | URP pada level kualitas Quest | R | 026 | Tutup dengan bukti log Editor dari runner |
| BOB-002 | Daftar scene + setting Android | R | 027 | Tutup dengan bukti runner |
| BOB-003 | Ganti identitas aplikasi | D → W+B | D7 | Worker menerapkan nama dari keputusanmu |
| BOB-016 | Validasi RuleConfig + tes edge case | W | 005 | Kandidat tugas pertama: C# murni, mudah dites |
| BOB-004 | Rapikan skrip build (release, laporan permanen) | W+B | 001, 002 | |
| BOB-008 | Timer berhenti saat aplikasi pause/hilang fokus | W | 005 | Tambah EditMode test untuk timer |
| BOB-009 | Hilangkan alokasi GC per frame, matikan log debug | W | 005 | |
| BOB-010 | Raycast VR menembus collider non-interaktif | W | 005 | |
| BOB-011 | Ganti `Shader.Find` dengan material aset | W | 001, 005 | Material dibuat oleh skrip Editor yang dijalankan runner, bukan YAML buatan model |
| BOB-013 | Robustness input VR | W | 005 | |
| BOB-014 | Robustness CandyTableDirector | W | 005 | File besar, wajib format diff |
| BOB-017 | Hubungkan TelemetryRecorder | W | 005, 014 | |
| BOB-018 | Audio aksi gameplay | D → W | 005, 014 | Perlu file audio dari kamu (atau sumber SFX yang disetujui) |
| BOB-020 | Optimasi RefreshProps + harness performa | W | 009 | |
| BOB-012 | Restart dari dalam VR | W | 005, 010 | |
| BOB-019 | Onboarding singkat | D → W | 012 | Perlu persetujuan isi teks onboarding |
| BOB-024 | Draft CI, pemeriksa statis scene, dokumentasi | W | 004, 005 | Pemeriksa statisnya dipakai di gate L0 |
| BOB-006 | Build APK pertama lewat profil Quest | W+B | 001–005 | L2 staging sudah ada di `07c9f7c` (development, package ID template) |
| BOB-007 | Smoke test APK di Quest 3 | H | 006 | Worker menyiapkan APK + daftar cek |
| BOB-015 | Build integrasi + validasi gameplay di Quest 3 | W+B → H | 007–014 | |
| BOB-021 | Wajah/tubuh kartun di scene gameplay | **Revisi** | 006 | Sudah digantikan cast Meshy full-body (`4a08b0d`). Ubah jadi "ekspresi wajah/kedip untuk cast Meshy" dan kerjakan di sesi art, bukan worker |
| BOB-022 | Tuning performa di Quest 3 (72 FPS) | H → W | 015, 020, 021 | Kamu merekam profiler, worker menerapkan tuning |
| BOB-023 | Regression penuh + persetujuan visual | H | 016–019, 022 | |
| BOB-025 | Freeze fitur + paket release candidate | D + W+B | 023, 024 | |

### Gelombang

Dengan 1 job bersamaan, urutan di dalam satu gelombang mengikuti prioritas. Dengan 10 item per hari,
kira-kira 4–5 tugas per hari selesai, kalau tidak ada yang gagal.

1. **Gelombang A (tanpa ketergantungan):** BOB-005 (R), 026, 027, 016, lalu 001/002 ditutup (R) dan
   003 setelah D7.
2. **Gelombang B:** 004, 008, 009, 010, 011, 013, 014.
3. **Gelombang C:** 017, 018, 020, 012, 024, lalu 006 (build APK).
4. **Gate manusia 1:** 007 smoke test di Quest 3. Hasilnya masuk Issue sebagai bukti L3.
5. **Gelombang D:** 015 build integrasi → gate manusia (validasi gameplay) → 019.
6. **Gate manusia 2:** 022 profiling di Quest → worker menerapkan tuning → 023 regression → 025 RC.

Setiap tugas selesai di `autopilot/integration`. Setiap akhir gelombang, kamu menarik
`autopilot/integration` ke game `main` setelah membaca ringkasan PR (Tahap 2a).

### Revisi antrean sebelum mulai

- Pakai `task-queue.proposed.json` (ada BOB-026/027) sebagai sumber, lalu jadikan 001/002 "tutup dengan
  bukti runner".
- Tandai BOB-005 dan sebagian BOB-006 dengan bukti yang sudah ada.
- Tulis ulang BOB-021 sesuai kondisi `4a08b0d`.
- Untuk setiap tugas, tambahkan kolom `lane` (W, W+B, R, H, D) dan `checks` (L0, L1, L2) supaya generator
  katalog bisa langsung membaca.

## Tahap 5 — Skala dan operasi

- **Laporan harian:** Cue memposting ringkasan di Issue engineering: tugas selesai, gagal, tertahan;
  kuota tersisa; status runner.
- **Alert (D8):** runner tidak membalas lebih dari 30 menit, tiga kegagalan beruntun pada satu tugas,
  kuota habis, atau gate menahan merge karena hasil yang ambigu.
- **Kegagalan:**
  - tugas gagal L1 dua kali → status `needs-human`, tidak diulang otomatis;
  - patch tidak bisa diterapkan → tugas di-rebase oleh revisi Issue baru (revision +1), bukan diulang diam-diam.
- **Rollback:**
  - stop worker: `tools/upgrade_repository.py disable` dan `tools/upgrade_auto_merge.py disable`
    (sudah ada);
  - hasil yang sudah masuk `autopilot/integration`: dibatalkan dengan revert commit oleh owner;
  - `main` game tidak pernah tersentuh worker.
- **Runner tanpa laptop:** pindah ke opsi B atau C setelah 2 minggu operasi stabil.

## Kapasitas

| Komponen | Hitungan |
|---|---|
| Tugas yang dikerjakan worker | ± 19 |
| Work item per tugas | 2 (coding + review) + ± 30% ulangan ≈ 2,6 |
| Total | ± 50 work item |
| Kuota 5 per hari | ± 10 hari kerja |
| Kuota 10 per hari | ± 5 hari kerja |
| Runner per job | L1 ± 10–15 menit, L2 ± 20–30 menit (perkiraan dari bukti laptop) |

## Risiko

| Risiko | Dampak | Penanganan |
|---|---|---|
| Model mengubah scene atau prefab YAML secara salah | Scene rusak, sulit di-review | Dilarang di format patch; aset diubah lewat skrip Editor runner |
| Runner laptop mati di tengah gelombang | Antrean berhenti | Job tertahan aman; alert; opsi B/C |
| Base sering bergeser karena kamu juga push ke game `main` | Tugas tertahan | Sinkronisasi staging terjadwal; tugas baru claim di ujung terbaru |
| Patch lolos test tapi salah perilaku di VR | Bug baru terlihat di Quest | Gate manusia 007/015/023; review dengan acceptance criteria |
| Izin langganan tidak sesuai ketentuan | Akun bermasalah | D1 sebelum skala naik |
| Auto-merge tanpa branch protection | Pengaman hanya di VPS | Repo game tidak pernah diberi akses App; `main` game manual |

## Yang tidak bisa dibuat otomatis

- **Uji di Quest 3:** BOB-007, 015, 022, 023 butuh headset dan orang.
- **Keputusan konten dan identitas:** BOB-003 (package ID), 018 (file audio), 019 (teks onboarding),
  025 (kapan freeze dan release).
- **Pekerjaan art:** model, ekspresi wajah, dan tekstur Meshy tetap di sesi art (Blender + Unity), bukan
  jalur worker.

## Langkah pertama

1. **Kamu:** putuskan D1, D2, D4–D8 (paling mendesak D2; D3 sudah diputuskan), lalu tulis di satu Issue engineering.
2. **Codex:** Tahap 1 (template Python baru) untuk membuktikan siklus jalan lagi.
3. **Codex:** buat repo staging + App terbatas + katalog bergulir (Tahap 2).
4. **Codex + laptop:** runner Unity polling L0/L1, lalu jalankan BOB-005 (R) sebagai uji pertama runner.
5. **Worker:** BOB-016 sebagai tugas C# pertama; kalau lolos, buka Gelombang A.
