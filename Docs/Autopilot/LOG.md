# Log agent Bite or Betray

Setiap tugas yang dikerjakan agent di VPS dicatat di sini (terbaru di bawah). Status lengkap ada di
[tasks.json](tasks.json).

- 2026-10-16 · setup · Loop agent dipasang di VPS (`devops`, cron). Pemeriksaan otomatis: compile semua
  assembly game tanpa Unity + 42 tes aturan Core, semuanya lulus di `4a08b0d`. BOB-005 ditandai `done`.

- 2026-10-16 · BOB-026 · codex · Hubungkan kualitas Meta Quest dan default GraphicsSettings ke Mobile_RPAsset.
  Pemeriksaan statis GUID/fileID terhadap aset/meta repo lulus untuk pipeline kualitas, renderer, volume, dan global settings;
  profil Quest memakai nama kualitas yang tersedia. Parameter URP, builder, dan aset LFS tidak diubah.
  Tes: `~/bob/bin/check.sh` → CHECK LULUS; semua assembly terkompilasi dan 42 tes Core lulus.
  BOB-026 selesai untuk lingkup statis; BOB-001 tetap menunggu Unity: impor package, aktifkan profil Quest/target Android,
  jalankan Budget Audit dan simpan log pipeline; buka CandyParlour3D, lampirkan screenshot tanpa material pink.
  GUID bawaan package belum diverifikasi di VPS; render/build Unity dan uji Quest 3 belum dilakukan.
- 2026-10-16 · BOB-008 · claude · Timer giliran dan input VR dibekukan saat fokus hilang. `VRTableInput.HasFocus`
  memakai OnApplicationFocus, OnApplicationPause, dan `OVRManager.InputFocusLost/Acquired`; `CandyTableDirector`
  hanya mengurangi `turnTimer` saat fokus. Selama fokus hilang pointer/tooltip disembunyikan, permen yang dipegang
  kembali ke tempatnya, dan tombol harus ditekan ulang setelah fokus kembali. Dites: `check.sh` lulus (compile +
  42 tes Core). Owner: jalankan EditMode tests di Unity dan simpan log; di Quest buka menu sistem saat giliran,
  pastikan angka Waktu berhenti dan lanjut dari angka yang sama (BOB-015). Status `needs_unity_check`.

- 2026-10-16 · BOB-027 · codex · Jadikan CandyParlour3D scene aktif index 0 global; path/GUID cocok dengan .meta.
  Profil Meta Quest tetap mewarisi daftar global; SDK global disamakan ke minimum 32/target 34, Vulkan saja, stereo path 2.
  ARM64 dan IL2CPP sudah cocok; profil Quest tidak perlu diubah.
  Tes: pemeriksaan statis scene/GUID/pewarisan/setting Android dan `git diff --check` lulus;
  `~/bob/bin/check.sh` → CHECK LULUS (semua assembly + 42 tes Core). Status `needs_unity_check`.
  Owner/BOB-002: aktifkan profil Meta Quest/Android di Unity; periksa Scene List efektif (CandyParlour3D enabled index 0),
  SDK 32/34, Vulkan, ARM64, IL2CPP, dan stereo path pada profil serta Android global; simpan screenshot/log batchmode.
  Validasi Unity Editor tetap wajib; build dan peluncuran scene awal di Quest 3 belum diuji.

- 2026-10-16 · BOB-013 · claude · `VRTableInput`: pelepasan tombol tetap diproses saat tracking hilang, jadi permen
  kembali ke meja dan tidak tertahan 'held'; status held juga dibersihkan walau tracking hilang saat director mengambil permen.
  Satu aksi per frame (kanan diproses dulu); tangan yang kalah bergetar pendek dan tercatat di log.
  Anchor dari `OVRCameraRig` (maks 1x/detik, maks 30 kali); `FindObjectsByType<Transform>` dihapus.
  Tes: `~/bob/bin/check.sh` → CHECK LULUS (compile + 42 tes Core). Status `needs_unity_check`.
  Owner/BOB-015: compile di Unity; di Quest pegang permen, hilangkan tracking controller lalu lepas tombol → permen
  kembali; tahan tombol → permen ikut tangan lagi setelah tracking pulih; trigger dua tangan bersamaan → hanya aksi kanan.
- 2026-10-16 · BOB-009 · codex · Cache nilai papan dan label kursi; format/assign teks hanya saat berubah.
  GUIStyle dipakai ulang; teks log dan header panel desktop dicache; status controller diformat hanya saat berubah.
  showLog/logInput default mati di kode dan scene; release memaksa flag mati dan mengecualikan log diagnostik.
  Tes: `~/bob/bin/check.sh` → CHECK LULUS (semua assembly + 42 tes Core); compilekit cabang release dan diff check lulus.
  Status `needs_unity_check`: Unity tidak tersedia; owner jalankan compile/EditMode di Unity dan simpan hasil.
  Profilkan CandyParlour3D setelah warm-up saat menunggu input (flag debug mati); lampirkan screenshot GC Alloc
  mendekati 0 KB/frame dan call stack alokasi tersisa. Periksa ronde/permen, timer, HP/item/status/giliran/KO/restart.
  Uji debug di Editor/Development dan build release di Quest 3: overlay/log input tetap mati meski serialized true.

- 2026-10-16 · BOB-014 · claude · `CandyTableDirector`: `SeatOf()` memetakan lewat `PlayerState.ConfigIndex` (baru di Core:
  index SeatConfig sebelum diurutkan d20), tidak lagi lewat nama; nama kembar dilaporkan `LogWarning` saat Setup dan diberi nomor.
  Timer giliran di-reset setelah `TimeoutTurn()`, jadi ForcedEat + permen good tidak timeout beruntun.
  Aksi ditolak hanya menampilkan popup error, tanpa 'Aman! Lanjut…'.
  Tes: EditMode `Start_ConfigIndexPointsBackToSeatConfig`; `~/bob/bin/check.sh` → CHECK LULUS (compile + 43 tes Core). Status `needs_unity_check`.
  Owner: di Play Mode, (1) dua botNames sama → warning + label beda; (2) TimeoutAction=ForcedEat, timer pendek, habis saat
  permen good → timer penuh lagi; (3) aksi ditolak → tanpa 'Aman! Lanjut'. Simpan log Console sebagai bukti.

- 2026-10-16 · BOB-010 · codex · Pointer VR/mouse memakai helper RaycastNonAlloc bersama di TableSelectable.
  Target aktif/interaktif terdekat dipilih sebelum hover; collider dekorasi/bot non-interaktif dilewati saat Grappling.
  Permen di tangan diabaikan; saat menyuapi, filter bot diterapkan sebelum mencari target terdekat.
  Buffer 64 hit dipakai ulang; jika penuh kapasitas diperbesar dan query diulang agar hasil terdekat tidak terpotong.
  Tes: `~/bob/bin/check.sh` → CHECK LULUS (semua assembly + 43 tes Core); `git diff --check` lulus.
  Status `needs_unity_check`: Unity tidak tersedia; owner compile lalu Play CandyParlour3D dengan mouse/simulasi VR,
  aktifkan Grappling dan pilih item lawan di dalam bounds collider bot; simpan screenshot/log tooltip dan aksi mencuri.
  Periksa target terdekat, dekorasi di depan target, dan menyuapi melewati item; profilkan GC Alloc setelah warm-up
  serta >64 hit (buffer tumbuh sekali, tanpa alokasi berulang). Validasi controller/Quest 3 tetap di BOB-015.

- 2026-10-16 · BOB-012 · claude · `CandyTableDirector`: tombol meja "Main lagi" (TableSelectable lewat `MakeButton`) muncul di
  layar akhir di sisi kanan alas pemain dan memanggil `Restart()`; disembunyikan saat restart/game baru, mouse ikut bisa menunjuknya.
  Teks akhir: VR "tunjuk Main lagi + trigger", desktop "klik Main lagi (atau R)". `Restart()` memakai `SetMode(None)` supaya
  tombol Buang isi/Batal dan opsi Little Pocket mati kalau restart di tengah giliran. Editor/Development mencatat jumlah objek anak,
  TableSelectable, dan item di awal tiap game. Tes: `~/bob/bin/check.sh` → CHECK LULUS (compile + 43 tes Core). Status `needs_unity_check`.
  Owner: compile di Unity; Play CandyParlour3D, restart ≥5 kali lewat Main lagi/R (juga di tengah giliran/Grappling), pastikan log
  "Game #N: … objek anak …" sama tiap game dan Hierarchy tidak bertambah; simpan log Console. Controller di Quest: BOB-015.

- 2026-10-16 · BOB-017 · codex · CandyTableDirector memakai TelemetryRecorder bersama; schema CSV debug tetap 14 kolom.
  Catat aksi berhasil manusia/bot dan TIMEOUT; GAME_OVER/durasi ditulis sebelum animasi akhir, CSV di persistentDataPath/telemetry.
  Waktu keputusan/akumulasi giliran manusia hanya selama input aktif dan fokus; reset per giliran/timeout, animasi dikecualikan.
  Saat menunggu hanya penghitung numerik; format/I/O saat aksi, error I/O mematikan recorder agar game berlanjut.
  Tes: `~/bob/bin/check.sh` → CHECK LULUS (semua assembly + 43 tes Core); `git diff --check` lulus.
  Referensi asmdef Table3D → DebugTools dan compilekit VPS disinkronkan; smoke .NET lulus (14 kolom, durasi, kalibrasi, TIMEOUT).
  Status `needs_unity_check`: Unity tidak tersedia, belum ada CSV Play Mode. Owner compile/EditMode, Play CandyParlour3D
  (recordTelemetry=true, loop=false); uji makan/suapi/item/Grappling/Pocket/buang/timeout/restart, cocokkan CSV dengan debug,
  periksa waktu/reset dan satu GAME_OVER; lampirkan CSV dari path Console. Profilkan aktif/nonaktif setelah warm-up saat menunggu
  (debug mati): tanpa tambahan GC Alloc/frame dari telemetry; lampirkan capture. Pengambilan CSV Quest diverifikasi di BOB-023.

- 2026-10-16 · BOB-028 · claude · Room offline: `RoomSetup` baru di Core (1-5 bot, susunan kursi seimbang, tingkat per bot/global,
  `BuildSeats` → SeatConfig). `CandyTableDirector` membuka pemilihan di meja sebelum game pertama: Kurangi/Tambah bot, Semua bot,
  tingkat tiap bot di atas kepalanya, lalu Mulai (TableSelectable, jadi pointer VR + trigger dan mouse). Bot tidak terpilih:
  karakter, target suapi, dan label dimatikan; tidak ikut engine. Main lagi/R/loop memakai pilihan terakhir; tombol Atur bot di
  layar akhir membuka pemilihan lagi (`OpenRoomSetup`, hook untuk BOB-032). Tes: `~/bob/bin/check.sh` → CHECK LULUS (52 tes,
  9 baru di RoomSetupTests). Status `needs_unity_check`. Owner: Play CandyParlour3D (mouse) lalu Quest (controller): pilih 1..5 bot
  dan tingkat, Mulai; kursi kosong tanpa karakter/label/aksi, caption giliran menampilkan tingkat yang dipilih; Atur bot/Main lagi
  dari layar akhir; cek keterbacaan panel dari kursi di headset.

- 2026-10-16 · BOB-029 · codex · Pemain kalah tetap di kursi; guard aksi menolak input manusia kalah/bukan giliran.
  Pointer VR, pegangan permen dan shortcut keyboard spectator dikunci sampai game selesai; kamera/tracking XR tetap aktif.
  Avatar lokal dan bot transparan alpha 30% lewat salinan material URP runtime; material/bayangan pulih saat game baru,
  salinan dipakai ulang lalu dibersihkan saat objek dihancurkan. Scene/prefab/material aset dan LFS tidak diubah.
  Tes: `~/bob/bin/check.sh` → CHECK LULUS (semua assembly + 52 tes Core); `git diff --check` lulus.
  Status `needs_unity_check`. Owner: Play dengan >=2 bot, loop=false; kalahkan manusia lebih awal, coba ambil/makan/suapi,
  item/B-Y, mouse/X/Esc/R: tidak ada aksi; kamera tetap melihat meja, tracking bebas, bot lanjut sampai pemenang.
  Lihat avatar manusia dan bot kalah dari kamera lain: transparan, pemain hidup opaque; Main lagi/Atur bot memulihkan
  material/bayangan/input, ulangi beberapa game tanpa pertambahan material. Ulangi pada build Quest 3 (shader/stereo),
  simpan screenshot/log. Online belum tersedia; sinkronisasi klien milik BOB-034/036.

- 2026-10-16 · BOB-030 · claude · Layar hasil diegetic: `FinalStandings` baru di Core (pemenang lalu urutan kalah, kalah di giliran
  sama berbagi peringkat). `CandyTableDirector` menampilkan kartu "Hasil Akhir" di atas meja depan kursi pemain (bot saja: tengah
  meja) berisi peringkat + giliran kalah, pelayan mengumumkan pemenang. Tombol layar akhir: Main lagi (pilihan room terakhir) dan
  Kembali ke menu (menggantikan Atur bot). Hook BOB-032: event `MenuRequested` dipanggil `ReturnToMenu` setelah meja dibersihkan;
  tanpa pendengar membuka Atur bot. Loop otomatis tidak diubah. Tes: `~/bob/bin/check.sh` → CHECK LULUS (55 tes, 3 baru di
  FinalStandingsTests). Status `needs_unity_check`. Owner: Play CandyParlour3D (loop=false, 3+ bot) sampai selesai; cek urutan di
  kartu sesuai log, Main lagi mempertahankan bot/tingkat, Kembali ke menu membuka Atur bot, ulangi tanpa objek bertambah. Di Quest 3
  cek kartu terbaca dari kursi, tidak tertutup popup/tombol, dan tombol bisa ditunjuk + trigger.
