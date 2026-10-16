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

- 2026-10-16 · BOB-032 · claude · Menu utama diegetic di meja depan pemain: Main vs Bot (ke Atur bot BOB-028), Buat Room,
  Gabung Room, Settings. Aplikasi mulai di menu; Atur bot dan layar akhir punya Kembali ke menu (MenuRequested tetap dipanggil).
  Online lewat `IRoomLobby` (director.Lobby atau komponen scene); tanpa lobby tombol abu-abu, exception lobby jadi pesan di meja.
  Settings: `GameSettings` baru di Core (volume, tangan dominan, snap ke mulut) disimpan PlayerPrefs, diterapkan ke
  AudioListener.volume dan VRTableInput (mouthDistance, leftHanded = tangan dominan didahulukan). Tes: `~/bob/bin/check.sh` →
  CHECK LULUS (60 tes, 5 baru di GameSettingsTests). Status `needs_unity_check`. Owner: Play CandyParlour3D (mouse): mulai di menu,
  Buat/Gabung abu-abu, Main vs Bot → Atur bot → Mulai, Kembali ke menu dari Atur bot/layar akhir; ubah Settings, Play ulang →
  nilai tetap. Di Quest 3: menu terbaca dan bisa ditunjuk + trigger, snap Tinggi memudahkan makan, tangan kiri dominan menang
  saat dua trigger bersamaan, tutup/buka app → Settings tetap. Volume baru terdengar setelah audio BOB-018/033 ada.

- 2026-10-16 · BOB-016 · claude · `RuleConfig.Validate()` menolak HP, kapasitas item, batas permen, rasio bad, timer dan
  TimeoutAction yang tidak valid dengan satu pesan berisi nama field + nilai; `GameEngine.Start` memanggilnya dan melempar
  ArgumentException sebelum membagi permen. 13 tes baru di RulesTests: validasi, Pocket→Grappling, Gloves setelah Fork,
  timeout ForcedEat. Aturan Gloves tidak diubah; pertanyaan untuk owner (permen yang sudah ditusuk Fork lalu ditukar Gloves
  kembali tersembunyi, tetap begitu atau tetap terbuka?) dicatat di notes tasks.json. Tes: `~/bob/bin/check.sh` → CHECK LULUS
  (semua assembly + 73 tes). Status `needs_unity_check`. Owner: Test Runner > EditMode > Run All di Unity, semua lulus
  (termasuk 43 tes lama RulesTests), simpan file hasil XML; jawab pertanyaan desain Gloves.

- 2026-10-16 · BOB-019 · claude · Onboarding singkat sebelum game pertama: isi di Core `OnboardingGuide` (Cara main, Mengambil
  permen, Makan, Give, Item, Daftar item) memakai angka RuleConfig, efek CandyRules dan deskripsi ItemInfo; teks kontrol VR/desktop
  berbeda. `CandyTableDirector` (onboarding=true) menampilkannya setelah Mulai sebagai kartu WorldText di depan pemain dengan
  tombol TableSelectable Lanjut/Mulai main dan Lewati; B/Y, Esc, R juga melewati. Sekali per sesi, tanpa PlayerPrefs. Tes:
  `~/bob/bin/check.sh` → CHECK LULUS (79 tes, 6 baru di OnboardingGuideTests). Status `needs_unity_check`. Owner: Play
  CandyParlour3D (mouse): Main vs Bot → Mulai → 6 halaman → Mulai main memulai game; Play ulang, Lewati/Esc di tengah → game mulai;
  Main lagi tidak memunculkan onboarding lagi; kartu tidak menutupi tombol. Setujui teks. Di Quest 3 (BOB-023): terbaca dari
  kursi, trigger di Lanjut, B/Y melewati.

- 2026-10-16 · QA Unity (Codex di laptop owner) · EditMode 43/43 lulus; profil Meta Quest + Scene List, URP Quest,
  BOB-010/012/014 terbukti di Unity → `done` (juga BOB-001/002/006/027). Bug: klik "Main lagi" tertutup rect panel
  desktop yang tersembunyi → diperbaiki `1830aaa`. APK QA build Succeeded, belum terpasang (ADB tidak mendeteksi Quest).
  Temuan baru: error shader Vulkan URP Lit → BOB-042 (codex); Meta XR required fixes kamera/audio → BOB-043 (claude).
  BOB-008/013 tetap butuh uji headset.

- 2026-10-16 · BOB-004 · claude · Skrip build di `AvatarValidation.cs`: menu `Bite or Betray/Build Quest APK/Development|Release`
  dan batch `-executeMethod BiteOrBetray.EditorTools.AvatarValidation.BatchBuild -bobBuildMode Release|Development` (exit 1 jika gagal).
  Scene tetap CandyParlour3D; APK `Builds/BiteOrBetray_Quest_<mode>.apk`. Tidak lagi menulis m_BrgStripping/SaveAssets; GraphicsSettings
  dan Meta Quest.asset (plus profil aktif) disalin sebelum build dan dipulihkan kalau berubah. Laporan permanen
  `Builds/reports/BiteOrBetray_Quest_<mode>_<waktu>_<commit>.txt`: hasil, durasi, ukuran APK, langkah, error, aset terbesar, variant
  shader sebelum→sesudah stripping skrip. Tes: `~/bob/bin/check.sh` → CHECK LULUS (compile + 79 tes). Status `needs_unity_check`.
  Owner: jalankan menu Development dan batchmode Release (`-batchmode -quit -buildTarget Android`), cek APK Succeeded, isi laporan
  (bagian shader terisi), lalu `git status` tidak menunjukkan GraphicsSettings.asset/Meta Quest.asset berubah.

- 2026-10-16 · BOB-042 · codex · Dugaan bug kompilasi glslang URP 17.6: Lit/ForwardLit vertex, LOD_FADE_CROSSFADE + EVALUATE_SH_VERTEX; akar bug belum dibuktikan tanpa Unity.
  Mobile_RPAsset: m_EnableLODCrossFade=0; default dan kualitas Android Mobile/Meta Quest memakai aset ini, PC dikecualikan dari Android; Strip Unused Variants sudah aktif.
  Setelan ini membuang varian cross-fade saat build menurut [referensi URP Unity](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/universalrp-asset.html#quality).
  Tidak ada LODGroup di scene/prefab teks/kode; model LFS belum dapat diperiksa. Dampak: jika model memakai LOD, perpindahannya tanpa fade dan bisa terlihat mendadak.
  Alternatif: SH Per Pixel menghindari EVALUATE_SH_VERTEX, dapat memperhalus cahaya probe tetapi menambah biaya fragment; stripping khusus Android/Vulkan lebih sempit tetapi perlu menjaga varian pengganti.
  Dipilih satu perubahan aset; Mobile_Renderer, SH Auto, package URP, graphics API, scene/prefab/material dan pointer LFS tetap.
  Tes: ~/bob/bin/check.sh → CHECK LULUS (semua assembly + 79 tes Core); pemeriksaan statis referensi pipeline/stripping dan git diff --check lulus.
  Status needs_unity_check. Owner: profil Meta Quest/Android Vulkan, cek LOD Cross Fade mati; clean build Development + Release, simpan log tanpa 'Shader error' Vulkan serta bukti stripping LOD_FADE_CROSSFADE.
  Owner: jalankan CandyParlour3D di Quest 3, pastikan Lit tanpa pink dan periksa transisi LOD model impor saat jarak pandang berubah; lampirkan log/screenshot.

- 2026-10-16 · owner · Strategi tanpa headset: owner tidak punya dan tidak bisa pinjam Quest. Tugas perangkat
  (BOB-007/015/022/023/041) dicek di laptop lewat Unity Editor, Meta XR Simulator, dan Multiplayer Play Mode.
  Hand tracking (BOB-031) jadi prioritas terakhir; submit mengandalkan controller. Baru: BOB-044 budget performa
  konservatif (codex), BOB-045 alat rekam gameplay dari Editor untuk video Devpost (claude).

- 2026-10-16 · BOB-011 · codex · Tambah aset BoB_VRPointer/BoB_TextCard (URP Unlit) beserta meta dan referensi serialized di CandyParlour3D.
  Hapus Shader.Find/MakeCardMaterial; kartu memakai aset bersama, pointer satu salinan per tangan dengan cleanup OnDestroy.
  ArtSceneBuilder mengisi referensi saat memasang/membangun ulang director; referensi kosong menghasilkan pesan diagnosa.
  Tes: ~/bob/bin/check.sh → CHECK LULUS (semua assembly + 79 tes Core); GUID unik dan rantai scene→material→shader serta git diff --check lulus.
  Status needs_unity_check. Owner: target Android, cek field material terisi; Play di Meta XR Simulator/Quest, pointer dua tangan dan kartu tanpa pink, hover independen.
  Muat ulang scene berulang: material BoB VR Pointer tidak bertambah (dua saat XR aktif), aset tetap; build Development/Release dan simpan log serta bukti visual BOB-015.
  Tanpa headset, ikuti strategi bukti Editor/Simulator owner; tampilan build di perangkat belum dibuktikan. Tidak mengubah Core atau pointer LFS.

- 2026-10-16 · BOB-034 · codex · Arsitektur host-authoritative: TableAuthority membungkus GameEngine; kursi lokal/online/bot terikat ConfigIndex setelah d20.
  NetworkTableSession di Table3D/Net memakai RPC NGO untuk aksi dan snapshot publik reliable 5 Hz; validasi sender, pemilik, giliran, revisi dan satu antrean input.
  Director host menjalankan aturan/bot/timeout; klien hanya menampilkan snapshot, mengirim pilihan, dan menolak restart lokal; offline tetap tanpa NetworkManager aktif.
  Codec biner murni Core menyamarkan isi permen tersembunyi; snapshot memuat inventory/pocket/status/hasil, ID permen dan GameId untuk restart.
  Tes: ~/bob/bin/check.sh → CHECK LULUS (semua assembly + 94 tes Core; 15 baru: round-trip, redaksi, otorisasi/replay, timeout/restart dan paket rusak).
  Status needs_unity_check. Owner: salinan sementara CandyParlour3D + NetworkManager/UnityTransport + objek scene NetworkObject/NetworkTableSession; host+klien via Multiplayer Play Mode.
  StartHost/StartClient, lalu context menu sesi host “Mulai meja dengan klien tersambung (uji lokal)”; periksa RPC/ILPP, aksi/item/HP/timer/KO/hasil kedua mesin, late join, restart host dan R klien.
  Shutdown/despawn → Main vs Bot harus pulih; jalankan EditMode Unity dan build Android IL2CPP/Meta XR Simulator. Detail di notes tugas; pose/animasi remote, lobby/Relay dan voice tetap tugas berikutnya.
  Tidak mengubah package, scene/prefab/material/ProjectSettings atau pointer LFS; hanya menambah referensi NGO ke asmdef runtime yang ada.

- 2026-10-16 · BOB-035 · claude · Lobby Sessions (Lobby + Relay): SessionRoomLobby di Table3D/Net mengisi IRoomLobby; Buat Room membuat session + NGO host dengan kode 6 angka.
  Gabung Room: keypad angka diegetic di meja (Hapus/Gabung/Batal; desktop bisa ketik), kode dicari lewat properti session publik lalu join by id.
  Host melihat kode besar + jumlah pemain; Mulai aktif kalau minimal 2 manusia, kursi kosong diisi bot (OnlineRoom.BuildSeats) lalu StartTable.
  Error login/jaringan/kode salah/room ditutup tampil sebagai pesan di meja, kembali ke keypad/menu. Director: API panel lobby + SetLobbyButton publik.
  Editor: menu Bite or Betray > Pasang Online di Scene (NetworkManager+UnityTransport+lobby, NetworkObject+NetworkTableSession). Asmdef ditambah referensi services/NGO.
  Tes: ~/bob/bin/check.sh → CHECK LULUS (semua assembly + 103 tes Core; 9 baru OnlineRoomTests: kode, keypad, syarat mulai, isi bot).
  Status needs_unity_check. Owner: link UGS (BOB-040), jalankan Pasang Online di Scene di CandyParlour3D lalu simpan; Multiplayer Play Mode 2 pemain:
  kode terbaca di meja (desktop + Meta XR Simulator), klien gabung via keypad, Mulai → bot mengisi kursi, kode salah/offline → pesan tanpa crash.
  Cek juga host Keluar room (klien ke menu dengan pesan), Kembali ke menu lalu Main vs Bot tetap offline, dan scene klien tidak di-reload saat sinkron NGO.

- 2026-10-16 · BOB-036 · codex · Pose NGO unreliable maksimal 25 Hz: 51 byte terkuantisasi, paket 74 byte termasuk sesi/revisi/interaksi.
  Host memvalidasi sender/kursi, pose/sequence, giliran/revisi dan jarak permen; remote memakai RemoteAvatarRepresentation dengan interpolasi/timeout yang ada.
  Rig manusia online disalin runtime dari avatar lokal; tracking salinan mati, kepala/jari dipulihkan, bot asli kembali saat stop. Fokus hilang mengirim tracking=false.
  State visual host 25 Hz menyamakan permen di tangan/animasi serta item aktif (termasuk Grapple), memakai ID dan pemetaan kursi; snapshot tetap mengatur inventory/HP.
  Kode networking di Table3D/Net; tanpa asmdef/package/scene/prefab/material/ProjectSettings/LFS baru atau perubahan Core.
  Tes: ~/bob/bin/check.sh → CHECK LULUS (semua assembly + 103 tes Core); git diff --check. Menu Cek Codec Avatar Online juga dipanggil Cek Floating Avatar.
  Status needs_unity_check: pemeriksaan codec Editor belum dijalankan; owner cek kedua menu, ILPP NGO, host + 1-2 klien di Multiplayer Play Mode/Meta XR Simulator.
  Cek kepala/tangan/grip, ambil/lepas/makan/suapi, semua item/Pocket/Grapple, loss/latency/fokus/timeout/spectator/late join, restart dan kembali offline tanpa objek ganda.
  Ukur pose <=25 Hz (payload 51 byte), build Android IL2CPP; bukti visual/performa Quest masih perlu owner, atau Editor/Simulator sesuai strategi tanpa headset.

- 2026-10-16 · BOB-044 · codex · Budget Quest konservatif: Mobile/Meta Quest render scale 1,0, MSAA 4x; shadow utama 512/6 m/1 cascade + hard shadow, additional PerVertex tanpa shadow.
  Alasan: scale 1,6 membebani sekitar 2,56x piksel; atlas 512 dan satu cascade/jarak pendek mengurangi biaya shadow; PerVertex menghindari tambahan pencahayaan per piksel.
  Mobile_Renderer sudah tanpa SSAO/feature; HDR/depth/opaque/soft shadow tetap mati, SRP Batcher tetap aktif. Static batching Android global + override Build Profile Meta Quest aktif, Room sudah BatchingStatic; HeroQ + builder instancing aktif.
  VRTableInput Android menunggu OVR siap: fixed FFR High + dynamic, eye-tracked off; dynamic resolution rig (Quest 3 bisa 1,6) dimatikan, skala XR 1,0; detail tepi dapat lebih kasar.
  Audit CandyParlour3D/QuestPerfTest via preview scene, 24 pandangan: <=250k segitiga, <=100 draw/mata termasuk seluruh shadow caster, <=50 material, estimasi SetPass <=100; tidak mendiskon batching/LOD/occlusion.
  Laporan Builds/reports/QuestBudgetAudit.txt + Temp/QuestBudgetAudit.txt tetap ditulis saat GAGAL; BatchAudit/CIRunner exit 1. Draw/SetPass adalah estimasi statis, bukan angka GPU atau bukti 72 FPS.
  Tes: ~/bob/bin/check.sh → CHECK LULUS (semua assembly + 103 tes Core), git diff --check; Unity tidak tersedia, pointer LFS tidak disentuh. Status needs_unity_check.
  Owner: unduh/import LFS, target Android/Meta Quest, menu Quest Perf Test > 4. Budget Audit; simpan hasil termasuk GAGAL (baseline 132+131 draw belum terbukti turun); cek visual shadow/cahaya dan Frame Debugger/Profiler saat gameplay laptop/Simulator.
  Owner: periksa static batch Room dan setelan runtime OVR/skala XR bila Android tersedia; Simulator tidak membuktikan FFR/72 FPS. Rujukan: [OVRManager](https://developers.meta.com/vr/reference/unity/v69/class_o_v_r_manager/), [prioritas instancing/SRP](https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/reduce-draw-calls/gpuinstancing/gpuinstancing).

- 2026-10-16 · BOB-018 · codex · GameplayAudio di CandyParlour3D: ambil, makan good/bad, Give, item/Pocket/Grapple, damage aktual, KO dan menang dari director.
  Empat AudioSource 2D dipakai ulang; delapan klip mono 24 kHz dibuat sekali, reset/menu stop dan cleanup runtime; volume mengikuti Settings.
  VR ambil langsung + deduplikasi konsumsi; cue animasi host diteruskan reliable ke klien, tanpa replay dari snapshot/late join.
  Sumber SEMUA klip Pickup/EatGood/EatBad/Give/UseItem/Damage/Elimination/Win: sinus asli MakePlaceholder (GameplayAudio.cs), dibuat codex tanggal ini; tanpa rekaman/sampel pihak ketiga.
  Lisensi aset pihak ketiga: tidak diperlukan karena tidak digunakan; placeholder bukan audio final. Persetujuan penggunaan/distribusi owner BELUM ada, wajib disetujui atau diganti klip dengan sumber/lisensi yang disetujui.
  Ganti delapan field AudioClip GameplayAudio di Inspector sebelum Play; null memakai placeholder. Tidak mengubah Core/package/ProjectSettings atau pointer LFS.
  Tes: ~/bob/bin/check.sh → CHECK LULUS (semua assembly + 103 tes Core); git diff --check lulus. Unity tidak tersedia; status needs_unity_check.
  Owner: compile/Play offline + host/klien Multiplayer Play Mode/Meta XR Simulator; dengarkan grab/drop/regrab, Eat good/bad, Give, semua item/Pocket/Grapple, cupcake tanpa damage, HP/KO/menang dan timeout.
  Owner: cek volume 0/100, restart/menu/late join tanpa cue ganda, tetap 4 AudioSource/klip stabil dan cleanup keluar Play; simpan bukti serta persetujuan sumber/lisensi. Audio perangkat di BOB-023; Simulator belum membuktikan headset.
