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

- 2026-10-16 · BOB-020 · codex · RefreshProps merekonsiliasi slot kursi/index/tipe; objek/callback tetap, hanya slot berubah/hilang dibuang dan collider segera nonaktif.
  Little Pocket memakai sinkronisasi yang sama; marker CPU BoB.RefreshProps untuk membandingkan panggilan berulang dan instance ID peserta lain.
  Generator QuestPerfTest menyalin karakter/komponen/rig/aturan CandyParlour3D; bypass menu/setup/onboarding, tanpa bot tersembunyi atau Hero_Quest tambahan/importer/material/controller pengganti.
  Build APK selalu meregenerasi scene; monitor hanya mengubah render scale A/B/C, karakter/animasi gameplay tetap berjalan dan scale pulih saat monitor nonaktif.
  Tes: ~/bob/bin/check.sh → CHECK LULUS (semua assembly + 103 tes Core); git diff --check. Core dan pointer LFS tidak diubah.
  Status needs_unity_check: Unity tidak tersedia; QuestPerfTest.unity tersimpan BELUM diregenerasi, angka audit/profiler baru belum tersedia.
  Owner: import LFS, compile/EditMode tests; menu Quest Perf Test > 1. Build Scene, simpan, cocokkan karakter/komponen dengan gameplay tanpa PerfHero_*; Play/input manusia atau timeout, cek animasi A/B/C.
  Owner: profiler sebelum (parent commit)/sesudah dengan seed/aksi sama: Eat/Give tanpa prop baru, UseItem hanya pelaku, Grapple pelaku+target; cek ronde/KO/Pocket/restart/snapshot online dan ID prop peserta lain.
  Owner: simpan capture CPU/GC, lalu menu 4. Budget Audit dan simpan Builds/reports/QuestBudgetAudit.txt (triangle/draw/material gameplay, termasuk jika GAGAL); cocokkan Frame Debugger. Bukti Quest belum ada.

- 2026-10-16 · BOB-033 · codex · GameplayAudio memusatkan musik loop, SFX menu/pelayan dan volume utama Settings.
  Musik 2D mulai setelah Settings dimuat, memakai satu sumber khusus + pool empat SFX; restart/menu hanya stop SFX sehingga loop tetap berjalan.
  Tombol menu/setup/lobby memberi cue lokal setelah callback; pelayan berbunyi tiap pengumuman ronde, diteruskan reliable ke klien dengan validasi GameId.
  Sumber musik 8 detik + MenuSelect/Waiter: gelombang sinus asli MakeClip (codex, tanggal ini), tanpa sampel/rekaman/aset pihak ketiga; placeholder, bukan audio final.
  Ganti field backgroundMusic/menuSelect/waiter di GameplayAudio Inspector sebelum Play; disable/enable menghentikan/memulai musik, destroy membersihkan klip buatan.
  Tes: ~/bob/bin/check.sh → CHECK LULUS (semua assembly + 103 tes Core); git diff --check. Core/scene/package/ProjectSettings/pointer LFS tidak diubah.
  Status needs_unity_check: Unity tidak tersedia. Owner: offline + host/klien Multiplayer Play Mode/Meta XR Simulator, dengarkan beberapa loop dan seluruh tombol serta cue pelayan tanpa ganda.
  Owner: menu > Settings > Atur bot > game > restart > menu tanpa musik putus; volume 0/50/100 langsung dan tersimpan, termasuk saat cue berbunyi; cek penggantian klip.
  Owner: tetap lima AudioSource/klip stabil setelah restart, cleanup keluar Play; cek keseimbangan musik/SFX di Quest, karena Simulator belum membuktikan audio headset.

- 2026-10-16 · BOB-031 · codex · VRTableInput: pinch telunjuk/aim Meta XR via OVRPlugin (backend OpenXR), memilih dispenser/tombol, pegang permen, makan dan suapi lewat jalur yang ada; [rujukan Meta](https://developers.meta.com/vr/documentation/unity/unity-handtracking-interactions/).
  Satu sumber per tangan; controller terlacak diprioritaskan. Ganti sumber/tracking tangan atau confidence hilang/gesture sistem: permen kembali tanpa suapan; wajib lepas sebelum aksi baru. Haptic hanya controller.
  Aim tidak valid menyembunyikan pointer, pose tangan valid tetap bisa makan; snap ke titik mulut memakai mouthDistance dari Settings Rendah/Normal/Tinggi = 10/15/22 cm; enableHandTracking=false untuk controller saja.
  Tes: ~/bob/bin/check.sh → CHECK LULUS (semua assembly + 103 tes Core); git diff --check. Tanpa perubahan Core/package/scene/prefab/material/ProjectSettings/LFS.
  Status needs_unity_check: Unity tidak tersedia; owner tidak punya/bisa pinjam Quest. Submit tetap mengandalkan controller; Editor/Meta XR Simulator tidak membuktikan pinch/aim/pergantian perangkat.
  Bila Quest 3 tersedia: pastikan Meta XR Hands and Controllers + fitur OpenXR/manifest hand tracking aktif; build Android, nyalakan hand tracking sistem dan logInput di Development Build; cek pointer kiri/kanan mengikuti aim.
  Quest: letakkan controller, lepas pinch awal, pinch dispenser sekali → satu aksi; pinch+tahan permen → ikut tangan; dekatkan ke mulut → makan sekali; arahkan pemain lain/bot + lepas → satu suapan.
  Quest: ulangi tangan kiri/kanan dan Settings snap Rendah/Normal/Tinggi; uji dua tangan bersamaan sesuai tangan dominan, menu sistem, tracking/confidence hilang saat pegang, pulih masih pinch, serta pointer invalid dekat wajah tanpa aksi/suapan tak sengaja.
  Quest: ganti tangan ↔ controller saat idle dan pegang/pinch/trigger ditahan; permen kembali, wajib lepas/tekan ulang, tanpa aksi ganda. Ulangi controller trigger/grip/B-Y, makan/suapi, fokus/spectator dan enableHandTracking=false; simpan log/video.

- 2026-10-16 · BOB-038 · codex · Vivox AudioOnly 2D per ID session UGS; lobby mengikat join/leave, KO/spectator tetap mendengar dan bicara sesuai mute pemain.
  RoomVoiceChat mengantre operasi dengan generasi; keluar/disconnect/destroy segera mute input/output lalu logout, termasuk join lama yang selesai terlambat; kegagalan voice tampil tanpa menghentikan game.
  Settings Voice: volume dengar 0-100% dan mute mikrofon terpisah dari musik/SFX, tersimpan PlayerPrefs; panel tetap tersedia di room dan pointer spectator hanya boleh memilih kontrol voice.
  Android meminta izin mikrofon serta Bluetooth Connect API 31+ sebelum join; memakai package Vivox yang ada dan referensi asmdef Table3D, tanpa perubahan scene/prefab/material/ProjectSettings/LFS.
  Tes: ~/bob/bin/check.sh → CHECK LULUS (semua assembly + 111 tes Core; 8 kasus voice baru dan perluasan default/persistensi); git diff --check. Unity tidak tersedia; status needs_unity_check.
  Owner: aktifkan UGS/Vivox (BOB-040), dua identitas host/klien harus saling dengar di lobby/game/KO/restart, room berbeda terisolasi; uji mute/unmute, volume 0/50/100, relaunch dan panel lewat mouse/controller/pinch saat spectator tanpa aksi gameplay.
  Owner: cek posisi/keterbacaan panel di VR; keluar/disconnect/host tutup room saat izin/join menunggu lalu gabung ulang harus bersih tanpa suara room lama; kegagalan layanan/izin tidak menghentikan game offline/online.
  Owner: build IL2CPP Quest 3, cek manifest hasil merge dan dialog mikrofon/Bluetooth terima/tolak, gabung ulang setelah izin aktif, input/output headset; simpan bukti audio/log, Simulator/compilekit belum membuktikan audio perangkat.
  Rujukan: [API voice Vivox](https://docs.unity.com/en-us/vivox-unity/developer-guide/vivox-unity-sdk-basics/sign-in-with-authentication-package), [izin Android](https://docs.unity.com/en-us/vivox-unity/developer-guide/android/request-runtime-permissions).

## 2026-10-17 · QA tanpa headset · claude
- Tugas yang butuh Quest (BOB-007/015/022/023/025/041) ditulis ulang jadi tes otomatis di Editor; owner tidak punya headset.
- Baru: asmdef `BiteOrBetray.Tests.PlayMode` + `SceneSmokeTests` (lulus di Unity laptop). check.sh meng-compile tes PlayMode;
  runner laptop menjalankan PlayMode setelah EditMode dan menulis hasilnya ke CI.md, juga `Logs/ci/perf.txt` dan
  screenshot `Logs/ci/screens/*.png` (disalin ke `Docs/Autopilot/screens/`).
- Ditutup dengan bukti CI: BOB-004, 007, 011, 016, 034, 042, 044. Sisa needs_unity_check ditautkan ke tes PlayMode
  di BOB-015/022/023/041 yang akan menutupnya setelah CI LULUS.

- 2026-10-17 · BOB-015 · codex · 7 tes PlayMode integrasi scene CandyParlour3D tanpa keyboard/headset di OfflineMatchTests.
  Full_match_via_table_actions_items_grapple_results_replay_and_menu: menu → bot → Lewati → ambil/makan/Give → 8 item (Pocket/Grappling) → bot/pemenang/hasil → 5 restart → hasil/menu.
  Bot_counts_one_to_five_start_with_selected_seats_and_difficulties: semua jumlah bot dimulai, kursi kosong dan tingkat cocok dengan konfigurasi.
  Lost_application_focus_freezes_turn_timer_and_returns_held_candy / Application_pause_freezes_turn_timer_and_returns_held_candy: timer/input/fokus kembali.
  Tracking_loss_with_trigger_held_recovers_without_accidental_give / Tracking_loss_with_grip_held_recovers_without_accidental_give: kembali ke nampan lalu ambil/makan lagi.
  Eliminated_human_is_locked_transparent_spectator_until_winner_and_replay: guard input/callback, kamera, alpha/queue/shadow manusia/bot, pemulihan material.
  Hook minimal: VRTableInput memisahkan polling perangkat dari pemrosesan privat; fixture menyuplai sampel/isi dispenser/inventory, seluruh keputusan lewat jalur game.
  Tes: ~/bob/bin/check.sh → CHECK LULUS (compile semua assembly/PlayMode + 111 tes Core); git diff --check lulus. Tanpa perubahan package/asmdef/scene/prefab/material/LFS.
  Status needs_unity_check: runner laptop wajib menjalankan 7 tes dengan -testPlatform PlayMode dan menyimpan hasil di CI.md; Unity tidak tersedia di VPS, tidak meminta uji Quest.
  Tugas lain belum diubah karena CI suite baru belum ada; notes BOB-015 memetakan kandidat bukti dan celah dua tangan/anchor, robustness director, halaman onboarding, lobby/Settings.

- 2026-10-17 · BOB-022 · codex · Builder Unity menggabungkan furnitur per material/lightmap, mematikan shadow mesh kaku/dekor, mempertahankan skinned/collider/UV dan override prefab.
  IPrebuildSetup PlayMode + CIRunner menyiapkan scene gameplay/QuestPerfTest; audit TextMesh diperbaiki, draw terbanyak dilaporkan; runner menolak CI hijau bila audit gagal/exit Unity nonzero.
  Panel desktop memakai GUI Rect tetap dan label cache, menghapus GUILayout/LINQ.ToList per event saat menunggu input.
  Tes GameplayPerformanceTests: idle telemetry aktif/nonaktif >=240 frame/>=3 detik, GC ProfilerRecorder + draw/SetPass UnityStats + frame time rata-rata/puncak ke Logs/ci/perf.txt.
  Pertandingan otomatis 5 bot lewat callback meja, prop peserta lain tetap setelah PlasticFork/Eat, marker RefreshProps bila tersedia, CSV/contoh GAME_OVER disimpan.
  Tes: ~/bob/bin/check.sh CHECK LULUS (semua assembly/PlayMode + 111 tes Core), 5 skenario report runner Python, git diff --check; tanpa perubahan LFS/package/asmdef/ProjectSettings/YAML scene.
  Status needs_unity_check: runner laptop harus menghasilkan perf.txt complete dan audit kedua scene; Unity tidak tersedia di VPS, angka baru belum ada (CI lama maksimum 271/100 draw).
  GC mentah termasuk Editor/Test Runner; indikator <=1 KiB tidak menggagalkan tes. CI hijau saja belum membuktikan GC mendekati nol; tidak mengklaim performa Quest.
  BOB-009/017/020 belum ditutup: tunggu bukti GC/audit/CSV/profiler yang relevan; capture sebelum RefreshProps dan pengambilan CSV BOB-023 tetap dicatat bila belum terbukti.

- 2026-10-17 · BOB-023 · claude · Regression PlayMode Assets/Tests/PlayMode/RegressionTests.cs: 8 UnityTest, satu per skenario (checklist di notes tasks.json).
  Menu/Atur bot/6 halaman onboarding, 8 item sesuai GameEngine, 3 warna + Clover (batas damage 3, lumpuh), ronde baru/refill + item, KO/spectator/hasil.
  5 restart (4x Restart, 1x lewat menu) tanpa GameObject/Material/AudioSource/AudioClip bertambah, 2x tiap jalur fokus hilang/kembali, BOB-031 controller + pinch tanpa dobel input.
  Audio: 5 AudioSource tetap, 10 cue terpasang, musik loop dan volume Settings; CSV telemetry dicek lalu disalin ke Logs/ci/regression-telemetry.csv.
  Screenshot Camera.Render ke RenderTexture dari kursi pemain: Logs/ci/screens/01-menu..06-hasil-akhir.png (runner menyalin ke Docs/Autopilot/screens/).
  Tes: ~/bob/bin/check.sh CHECK LULUS (compile semua assembly termasuk PlayMode + 111 tes Core). Unity tidak ada di VPS: suite belum pernah dijalankan.
  Owner/CI: tunggu CI.md runner laptop (PlayMode); setujui 6 screenshot di Docs/Autopilot/screens/. isPlaying musik hanya dicek bila Editor CI punya output audio.
  Setelah CI LULUS: BOB-011/017/018/019/031/033 ke done sesuai notes BOB-023; pinch nyata di Quest tetap known gap.

- 2026-10-17 · BOB-CI · claude · Tes PlayMode gagal di SetUp karena Meta XR Simulator (runtime OpenXR laptop CI) menyalakan XR di PlayMode Editor.
  Baru: Assets/Tests/PlayMode/EditorXR.cs mematikan loader XR (StopSubsystems + DeinitializeLoader) sebelum scene dimuat di OfflineMatchTests,
  RegressionTests dan GameplayPerformanceTests; asmdef PlayMode + referensi Unity.XR.Management. Camera Rig tetap nonaktif, jadi log
  '[OVRManager] OnDestroy' hilang tanpa melonggarkan LogAssert.NoUnexpectedReceived; Assert IsXRActive tetap ada.
  Budget Audit: GameplayPerformanceBuilder.PrepareScenes tidak lagi memulihkan SceneSetup kosong (batchmode tanpa scene -> 'Invalid SceneManagerSetup').
  Tes: ~/bob/bin/check.sh CHECK LULUS (compile semua assembly + 111 tes Core). Unity tidak ada di VPS.
  Owner/CI: CI.md berikutnya harus LULUS (PlayMode 17/17, Budget Audit LULUS); gagal di badan tes = bug suite BOB-015/022/023 yang baru pertama jalan.

- 2026-10-17 · BOB-CI · claude · CI 86c2bcb: PlayMode 8/17 (OfflineMatchTests sudah lulus setelah XR Editor dimatikan).
  RegressionTests: NRE di SetUp karena Until -> PollAudio memakai field audio sebelum diisi; GameplayAudio kini diambil tepat setelah director.
  GameplayPerformanceTests: draw=0 karena Editor -batchmode tidak menggambar Game view. Saat batchmode kamera utama dirender ke RenderTexture
  tiap frame ukur; draw/SetPass = maks(ProfilerRecorder Render Draw Calls/SetPass Calls Count, UnityStats). Assert draw>0 tidak dilonggarkan.
  Tes: ~/bob/bin/check.sh CHECK LULUS (compile semua assembly + 111 tes Core). Unity tidak ada di VPS.
  Owner/CI: CI.md berikutnya PlayMode 17/17; gagal di badan RegressionTests = bug suite BOB-023 yang baru pertama lewat SetUp.
  Budget Audit masih 'GAGAL budget statis' (111-112 draw/mata, ranah BOB-022); runner laptop terpasang versi lama: jalankan install_unity_runner.ps1.

- 2026-10-17 · BOB-CI · claude · CI b46ff60 PlayMode 15/17, dua tes RegressionTests gagal di badan tes (pertama kali lewat SetUp).
  Eight_items: assert tes salah. GameEngine sengaja membiarkan permen hasil Fork tetap terbuka saat Gloves (keputusan owner,
  RulesTests.GlovesAfterFork_ForkedCandyStaysRevealed); tes kini mengecek permen lama tetap terbuka dan permen baru tertutup.
  Ko_spectator: clip musik null di menu = PlayMusic dari Start ditolak guard. GameplayAudio.PlayMusic kini memakai enabled +
  activeInHierarchy dan hanya melewati bila sumber sudah memutar klip musik itu; pesan assert musik menulis status audio.
  Tes: ~/bob/bin/check.sh CHECK LULUS (semua assembly + 111 tes Core). Unity tidak ada di VPS.
  Owner/CI: CI.md berikutnya PlayMode 17/17; kalau Ko_spectator masih gagal, pesan diagnostiknya menunjukkan penyebab.

- 2026-10-17 · BOB-043 · claude · Required fix Meta XR kamera/AudioListener di CandyParlour3D.
  Scene disimpan dalam keadaan VR: Camera Rig aktif, CenterEyeAnchor satu-satunya MainCamera + AudioListener; Desktop Art Preview
  tanpa tag, Camera/AudioListener mati. ArtPreviewControls (execution order -32000) memilih kamera di Awake: tanpa XR rig dimatikan
  sebelum OVR Awake dan preview jadi MainCamera; dengan XR preview dimatikan. VRTableInput memakai ArtPreviewControls.UseCamera.
  ArtSceneBuilder.Build menyimpan keadaan yang sama. Tes PlayMode baru: CameraSetupTests (jalur desktop dan VR).
  Tes: ~/bob/bin/check.sh CHECK LULUS (semua assembly + 111 tes Core). Unity tidak ada di VPS.
  Owner: buka CandyParlour3D, Project Setup Tool tanpa required fix kamera/audio (tanpa Fix All); Play tanpa headset tetap preview
  desktop; CI.md CameraSetupTests 2/2. Cek juga menu Render Preview Scene 3D masih menghasilkan PNG (kamera preview kini mati di scene).

- 2026-10-17 · BOB-037 · claude · Disconnect online: klien putus diganti bot, host keluar mengakhiri game klien.
  Host: NetworkTableSession mendengar OnClientDisconnectCallback; kursi Online klien itu jadi Bot (TableSeat/TableAuthority.ReplaceWithBot,
  PlayerState.IsBot, revisi naik), snapshot langsung dikirim; director berhenti menunggu aksi dan giliran dilanjutkan bot.
  Avatar remote kursi itu dimatikan, karakter bot kembali + popup "Terputus / diganti bot" di host dan klien lain. Main lagi tetap bot.
  Host keluar: klien kembali ke menu utama dengan pesan "Koneksi ke host terputus" (pesan lobby tidak ditimpa).
  Tes: ~/bob/bin/check.sh CHECK LULUS (semua assembly + 113 tes Core, 2 tes EditMode baru). Unity tidak ada di VPS.
  Owner/CI: bukti runtime host+klien menyusul di tes PlayMode BOB-041 (klien Shutdown -> game sampai pemenang; host Shutdown -> klien di menu + pesan).

- 2026-10-17 · BOB-041 · claude · Tes online otomatis tanpa headset (PlayMode, dijalankan runner laptop).
  OnlineMatchTests: host scene asli + klien NGO kedua dalam satu proses (UnityTransport 127.0.0.1), kursi kosong bot, main sampai
  pemenang; tiap revisi yang sama dibandingkan (giliran, permen, HP, status, inventory, pemenang); klien putus -> bot sampai pemenang.
  OnlineServicesTests: Buat Room asli -> login anonim, Lobby+Relay, kode 6 angka, cari dengan kode, keluar (session terhapus),
  kode tertutup -> pesan; Vivox masuk/keluar channel room. Tanpa internet = Assert.Ignore. Asmdef PlayMode + Services/Vivox.
  Bug baru: BOB-046 (scene belum berisi NetworkManager/meja/lobby, tombol online selalu abu-abu).
  Tes: ~/bob/bin/check.sh CHECK LULUS (compile semua assembly + 113 tes Core). Unity tidak ada di VPS.
  Owner/CI: CI.md harus menunjukkan 4 tes Online* lulus (bukan ignored); lalu status BOB-035/036/038 sesuai notes BOB-041.

- 2026-10-17 · BOB-046 · claude · CandyParlour3D.unity berisi BoB_Network (NetworkManager + UnityTransport + SessionRoomLobby, scene management aktif)
  dan BoB_TableSession (NetworkObject in-scene + NetworkTableSession, director = BoB_TableDirector). YAML ditulis langsung (Unity tidak ada di VPS):
  GUID script dari paket NGO 2.13.3 resmi, GlobalObjectIdHash 2879140581 dihitung dengan XXHash32 NGO dari GlobalObjectId scene, m_InScenePlaced 1.
  OnlineSceneSetup.Install(director) dipakai menu Pasang Online di Scene dan ArtSceneBuilder.Build (objek online dipakai ulang saat rebuild).
  Tes: PlayMode baru OnlineSceneTests (objek scene asli, Buat/Gabung Room aktif dari menu Start, host scene men-spawn meja in-scene,
  ClientShouldPassThrough NGO = klien tidak reload scene aktif); ~/bob/bin/check.sh CHECK LULUS (compile semua + 113 tes Core).
  Owner/CI: tunggu CI.md (OnlineSceneTests + OnlineMatchTests + OnlineServicesTests lulus); buka CandyParlour3D, pastikan tanpa Missing Script
  dan hash meja tetap 2879140581; Multiplayer Play Mode 2 pemain: klien Gabung Room tanpa scene dimuat ulang.

- 2026-10-17 · BOB-045 · claude · Alat rekam video Devpost dari Editor: GameplayRecorder, menu Bite or Betray > Rekam Video.
  1 klik "Pertandingan dari Kursi Pemain": Play desktop CandyParlour3D, semua bot main satu game, kamera di kursi pemain menoleh halus ke bot
  yang giliran, frame JPG 1920x1080 @30 fps (captureFramerate) ke Recordings/<waktu>_<shot>/ sampai pemenang, Play berhenti sendiri,
  buat_video.cmd/.sh + perintah ffmpeg (clipboard) jadi MP4. Preset: Menu Utama, Close-up Bot Bereaksi, Meja dari Atas. Editor-only, tanpa package.
  Tes: PlayMode baru GameplayRecorderTests (pertandingan sampai pemenang + 3 preset: frame/JPG 1080p/skrip ffmpeg/posisi kamera);
  ~/bob/bin/check.sh CHECK LULUS (compile semua + 113 tes Core). Unity tidak ada di VPS.
  Owner: CI.md GameplayRecorderTests 4/4; klik menu Pertandingan, jalankan buat_video.cmd (ffmpeg di PATH), tonton MP4 (gerakan halus, teks terbaca).

- 2026-10-17 · BOB-024 · claude · Draft CI GitHub, pemeriksa statis scene, README.
  .github/workflows/unity-ci.yml.draft (game-ci): pemeriksa scene -> tes EditMode+PlayMode -> Budget Audit + APK Development (CIRunner.Run).
  Tidak aktif: ekstensi .draft + job Unity butuh variable BOB_UNITY_CI; secret lisensi (UNITY_LICENSE/SERIAL/EMAIL/PASSWORD) dan runner di kepala file. Belum pernah dijalankan.
  Tools/CI/check_scene.py: baca YAML CandyParlour3D tanpa Unity, lapor GUID tanpa .meta + objek wajib (BoB_Art3D, Seats, Seat_0/HeadAnchor, Bot_*, anchor controller OVRCameraRig).
  Assets/README.md: 113 tes EditMode + 30 PlayMode, status VR, build dari menu/batch, CI laptop vs draft GitHub.
  Tes: check_scene.py pada scene asli SCENE LULUS, salinan rusak (Bot_Mint, LeftControllerAnchor diganti) SCENE GAGAL; ~/bob/bin/check.sh CHECK LULUS.
  Owner: di project laptop jalankan python Tools/CI/check_scene.py --strict (butuh Library/PackageCache) -> harus 0 GUID hilang.
  Aktifkan workflow GitHub hanya kalau setuju memakai lisensi Unity di Actions (langkah di kepala file).

- 2026-10-17 · BOB-025 · codex · Freeze fitur dan persiapan paket RC tanpa headset di Docs/Release/RC.md.
  Permintaan Release tersimpan di build-mode.txt; BatchBuild/CIRunner/runner memakai mode sama, laporan hash penuh + SHA-256 APK.
  Runner menolak tes dilewati/exit gagal/build ditunda sebagai LULUS; arsip per commit: XML, build, perf/CSV, audit, diff/status dan hash screenshot.
  RC memuat checklist BOB-023/041, bukti baseline b46ff60 yang gagal, screenshot hilang, known issues dan gaps online/voice/hand tracking.
  Tes: ~/bob/bin/check.sh CHECK LULUS (compile semua assembly + 113/113 Core), 6 tes Python runner lulus, git diff --check lulus.
  Status needs_unity_check: belum ada build Release/hasil seluruh PlayMode pada hash kandidat; tidak mengklaim CI/perangkat lulus.
  Laptop: pasang ulang install_unity_runner.ps1 lalu unity_runner.py --force; wajib Release Succeeded, seluruh tes termasuk online Passed, audit/perf/CSV/enam screenshot satu hash.
  Periksa manifest/diff/status hasil prepare, isi hash kandidat lulus di RC.md; Quest fisik, pinch sensor dan voice antarpemain tetap known gaps.

- 2026-10-17 · BOB-047 · codex · QA CI bc71d57: evaluasi 26 tugas, 13 done dan 13 tetap needs_unity_check; bukti/nama tes/known gap di notes + CI.md.
  Dua OnlineMatchTests dan tes Vivox gagal: tugas terkait tetap terbuka; GC idle 6661,97 B/frame tidak dianggap mendekati nol.
  PrepareScenes menyimpan visual SDK pengganti avatar lokal mati (sensor/anchor tetap), shadow karakter mati, submesh skinned bermaterial sama digabung tanpa kehilangan geometri/animasi.
  Langkah baru berjalan juga pada scene yang sudah punya Performance Geometry; QuestPerfTest diregenerasi; YAML scene dan pointer LFS tidak diedit.
  GameplayPerformanceTests ditambah assertion integritas avatar/mesh, rig/sensor/anchor; audit tetap memakai batas dan kegagalan geometri dinamis yang sama.
  Tes: ~/bob/bin/check.sh CHECK LULUS (semua assembly + 113/113 Core), pemeriksa scene SCENE LULUS (51 GUID package belum terverifikasi), git diff --check.
  BOB-047 needs_unity_check: CI berikutnya jalankan PlayMode + PrepareScenes/Audit/build; simpan perf-prepare/perf/audit dan screenshot regression untuk cek tampilan laptop.
  Budget terakhir gameplay 351701 tri/156 draw (+101701/+56), perf scene 351759/157 (+101759/+57); 35 shadow dan visual SDK aktif termasuk dua tangan dinamis penyebabnya.
  Hasil optimasi belum diukur Unity; wajib kedua scene <=250k tri, <=100 draw/mata, <=50 material dan geometri lengkap; Quest fisik tetap known gap.

- 2026-10-17 · BOB-CI · claude · Perbaiki OnlineMatchTests "Koordinat tidak valid" dan buka penyebab Vivox (CI bc71d57).
  Penyebab: root FBX item/permen berskala 100; skala visual online dikuantisasi milimeter ke short (maks. 32,767) sehingga host melempar IOException tiap paket.
  Skala kini dikirim half (AvatarVisualWire.WriteScale/ReadScale, tetap 6 byte, NaN/Inf ditolak); tes baru OnlineSceneTests.Scene_item_and_candy_scales_survive_visual_codec.
  RoomVoiceChat: antrian join/leave static (VivoxService singleton) dan LastError; pesan gagal tes Voice kini memuat exception Vivox asli.
  Budget Audit tidak disentuh (BOB-047 codex). Tes: ~/bob/bin/check.sh CHECK LULUS (semua assembly + 113/113 Core).
  Owner/CI: OnlineMatchTests 2/2 + tes skala lulus; kalau Voice gagal, baca exception di CI.md (cek Vivox aktif di dashboard UGS).

- 2026-10-17 · BOB-CI · claude · CI 42e7168: OnlineMatchTests + Budget Audit (QuestPerfTest 101/100 draw).
  OnlineMatchTests: stack CI menunjuk WriteVector(localScale), sudah diperbaiki e0b361b (WriteScale half) yang belum ikut CI 42e7168.
  Budget Audit: kepala avatar lokal (Pupil/Mouth/EyeWhite/Lid/HeadGear, ±23 draw) dihitung di setiap pandangan Seat_0, padahal
  LocalAvatarTracking selalu menyembunyikannya dari kamera first-person (desktop forceRenderingOff, XR layer 30 di-cull).
  QuestPerfTestBuilder.AuditScene mengecualikan headRenderers itu dari hitungan per mata saja; total scene/shadow dan batas 250k/100/50 tetap; jumlahnya ditulis di audit.
  Tes baru CameraSetupTests.Kepala_avatar_lokal_tidak_digambar_kamera_first_person (jalur desktop + XR). ~/bob/bin/check.sh CHECK LULUS (113/113 Core).
  Owner/CI: CI.md berikutnya PlayMode 32/32 dan Budget Audit LULUS dengan baris "Kepala avatar lokal ... tidak dihitung per mata"; kalau masih gagal lihat "Draw terbanyak".

- 2026-10-18 · BOB-047 · codex · QA ulang CI 610a070: cocokkan 13 tugas needs_unity_check + BOB-047 dengan XML 113/113 EditMode dan 32/32 PlayMode, tanpa gagal/skip.
  BOB-038 dan BOB-041 done; 11 tugas lain tetap terbuka dengan pemetaan AC/nama tes/bukti/celah di notes; BOB-CI tidak diubah.
  Budget Audit LULUS: CandyParlour3D maks. 203085 tri/73 draw per mata/26 material; QuestPerfTest 203143/74/27, shadow 0 (batas 250000/100/50).
  Skrip Editor PrepareScenes/Audit dan tes kepala lokal/rig/mesh sudah terbukti CI; tidak perlu perubahan kode/scene atau pointer LFS.
  GC idle 6629,95 B/frame belum mendekati nol; capture CPU, persetujuan audio, dokumen RC, join kode/visual klien/host keluar/sinkronisasi dua proses dan Meta XR Setup Tool belum seluruhnya terbukti.
  Tes: ~/bob/bin/check.sh CHECK LULUS (semua assembly + 113/113 Core), git diff --check; nama tes PlayMode di notes dicocokkan ke XML Passed.
  Owner/CI: lengkapi bukti Editor otomatis untuk tugas tersisa; pinch/voice/tampilan/performa Quest fisik tetap known gap, bukan klaim 72 FPS. BOB-047 done; tidak perlu headset untuk menutup QA ini.

- 2026-10-18 · BOB-048 · claude · English jadi bahasa default, Bahasa Indonesia lewat tombol Language di Settings (berlaku langsung, tersimpan di PlayerPrefs).
  Core/Localization.cs: Language, Phrase dan tabel Strings EN/ID (>200 kunci) dipakai menu, Atur bot, onboarding, narasi/papan/label kursi, item/permen, hasil akhir, lobby, voice, error engine/protokol, alasan bot, tooltip VR, gelembung pelayan.
  GameSettings.Language (bob.settings.language, nilai rusak = English); director.RefreshLanguage membangun ulang label tombol, menu, papan dan nama kursi manusia ("You"/"Kamu", HumanName). Log debug/telemetry tidak diubah.
  Nama objek tombol kini English ("Tombol Play vs Bots", "Tombol Start", ...); tes PlayMode disesuaikan dan memakai Strings untuk teks. Settings: Volume, Language, Dominant hand, Mouth snap, Back.
  Tes: ~/bob/bin/check.sh CHECK LULUS (semua assembly + 119/119 Core: LocalizationTests kunci EN+ID/placeholder, GameSettingsTests bahasa, onboarding EN+ID).
  Tes PlayMode baru RegressionTests.Language_english_by_default_switch_in_settings_applies_now_and_persists (ganti, muat ulang scene, kembali English); suite Regression memaksa English untuk screenshot.
  Owner/CI: PlayMode lulus semua dan screenshot 01-06 di Docs/Autopilot/screens berbahasa Inggris; cek teks Inggris yang lebih panjang tidak keluar kartu (tata letak BOB-049).

- 2026-10-18 · BOB-053 · claude · Draft halaman Devpost (English) di Docs/Devpost/submission.md, siap tempel ke Devpost.
  Isi: tagline, inspirasi, aturan 30 detik, fitur (offline 1-5 bot Easy/Normal/Hard, online 2-6 kode 6 angka + bot pengisi/disconnect, spectator, Vivox, pinch + controller, onboarding, Settings EN/ID), cara membangun, tantangan, pelajaran, rencana, tim, batasan jujur.
  Batasan: belum diuji di Quest fisik, audio masih nada sintetis placeholder, tanpa host migration, balance bot belum disimulasikan (BOB-051).
  Daftar screenshot 01-06 + caption; shot list trailer 2:30 (11 shot) hanya memakai adegan BOB-052 + preset kamera GameplayRecorder (tanpa audio/tangan VR).
  Lampiran A memetakan setiap klaim ke kode dan nama tes. Tidak ada perubahan kode/scene/LFS.
  Tes: ~/bob/bin/check.sh CHECK LULUS (semua assembly + 119/119 Core).
  Owner: isi peran/link Alwan dan Zabber, tulis ulang Inspiration; pakai screenshot CI setelah BOB-049/050; cocokkan klip setelah BOB-052 jalan.

- 2026-10-18 · BOB-049 · codex · UI meja bertema parlour: kartu krem/hijau dengan bingkai oranye dan sudut membulat, satu shader URP stereo/pass/quad tanpa tekstur baru.
  Tombol hover/disabled mengubah warna/bingkai; judul ganda dihapus, narasi dibungkus, banner diturunkan dan status kursi turun baris.
  WorldTextLayout memisahkan proyeksi label/kartu/popup dari kamera pemain, membatasi skala/lebar dan menyelaraskan collider tombol; engine/alur, scene/prefab dan pointer LFS tetap.
  Tes baru RegressionTests.UI_labels_do_not_overlap_in_menu_gameplay_spectator_and_results: 5 bot, BAD/KO bersamaan, kontrol negatif, hover/disabled dan screenshot empat keadaan.
  Screenshot Regression 01-06 juga mengaudit bounds teks+kartu, pemotongan, shader satu pass dan draw 1–100; laporan Logs/ci/ui-layout.txt.
  Tes VPS: ~/bob/bin/check.sh CHECK LULUS (semua assembly + 119/119 Core); git diff --check. PlayMode hanya dikompilasi.
  Status needs_unity_check: runner laptop perlu menghasilkan screenshot baru di Docs/Autopilot/screens serta CI.md PlayMode/build/shader/Budget Audit LULUS; tanpa langkah manual headset.

- 2026-10-18 · BOB-052 · claude · Runner laptop merekam klip trailer 1080p30 (UI English) bila Docs/Release/record-trailer.txt = yes (diset yes), sekali per commit kode.
  Tes PlayMode baru TrailerClipTests (kategori Trailer; PlayMode biasa memakai -testCategory "!Trailer", runner menjalankannya terpisah dengan -bobTrailerDir ~/bob-ci/videos/<commit>).
  Klip: 01-menu, 02-atur-bot, 03-gameplay-item, 04-spectator, 05-hasil-akhir, 06-lobby-online (kode room) dari kursi pemain (preset GameplayRecorder baru Kursi) + preset 07-close-up-bot, 08-meja-atas.
  unity_runner.py: ffmpeg (PATH, ~/bob-ci/bin, link winget) -> MP4 per klip, bagian "Klip trailer" di CI.md (adegan, durasi, file, ukuran); tidak mengubah LULUS/GAGAL. Panduan Docs/Release/trailer.md.
  Tes: ~/bob/bin/check.sh CHECK LULUS (semua assembly + 119/119 Core); python -m unittest test_unity_runner 10/10 (4 tes trailer baru).
  Owner: jalankan install_unity_runner.ps1 (salinan runner), pasang ffmpeg (winget install --id Gyan.FFmpeg -e); CI.md berikutnya harus LULUS + 8 klip, tes Trailer 6/6; tonton MP4-nya.

- 2026-10-18 · BOB-050 · codex · Bot dibawa ke busur depan 120–240°, radius 1,3 m; alas mengikuti kursi, kursi pemain tetap.
  Pelayan bergeser ke (2,7; 0; 2,2) menghadap pemain; dispenser 70% agar bot tengah terlihat, FOV desktop 60 mengeluarkan globe lampu dari tepi layar.
  ArtSceneBuilder dipanggil PrepareScenes CI; mesh furnitur gabungan dibangun ulang saat kursi/alas berubah, aman untuk scene yang sudah dioptimasi.
  Tes Regression baru memeriksa 1–5 bot, 10 reaksi pada semua slot, vertex pose dalam frame, volume alas bebas bot, ray wajah/dada pelayan bebas dispenser.
  Mask Room_Glow menguji oval sudut; kontrol negatif FOV lama dan bot di alas. Screenshot 01-menu/04-gameplay kini 5 bot, ditambah BOB-050 menu/pengumuman 1–5 bot.
  Tes VPS: ~/bob/bin/check.sh CHECK LULUS (semua assembly + 119/119 Core), git diff --check; tes PlayMode hanya dikompilasi.
  Status needs_unity_check: runner laptop wajib menghasilkan screenshot terbaru di Docs/Autopilot/screens serta CI.md PlayMode/Budget Audit/build LULUS.
  Bukti pengumuman/Wave, lima bot utuh, alas bebas tangan, dan lampu tanpa fragmen harus berasal dari Unity otomatis; tanpa langkah manual Quest. YAML/pointer LFS tidak diubah.

- 2026-10-18 · BOB-051 · codex · BotBrain Normal/Sulit mempertimbangkan HP/warna/KO, Clover ofensif, Milk curian dan menghindari Fork/Clover sia-sia.
  Simulation mencatat langkah/keputusan gagal sebelum fallback dan waktu satu kursi pengganti manusia setelah d20.
  Docs/Balance.md + dua CSV: 80 konfigurasi x 2.000 game/versi = 320.000 game; selesai semua, nol keputusan gagal, maks langkah 176 -> 160.
  Setiap campuran Mudah < Normal < Sulit; selisih N-S minimum 2,72 -> 7,82 poin; duel Sulit vs Normal 55,05% -> 65,25%.
  Tiga bot + pengganti Normal rata-rata 7,43-7,91 menit; P90 10,16-11,56 menit. Profil waktu sama dari jeda director, estimasi bukan telemetry Quest.
  20 kasus BalanceTests: duel/campuran seed tetap, durasi, progres seluruh konfigurasi, info publik, item dan pengganti manusia; cara ulang batch di laporan.
  Tes VPS: ~/bob/bin/check.sh CHECK LULUS seluruh assembly (termasuk kompilasi PlayMode), 139/139 Core; kedua batch massal lulus, git diff --check.
  Status needs_unity_check: runner laptop perlu EditMode/PlayMode/Budget Audit/shader/build dan CI.md LULUS untuk commit ini; otomatis, tanpa langkah manual headset.

- 2026-10-18 · BOB-CI · claude · CI 5fb579a GAGAL (PlayMode 39/41), dua tes diperbaiki tanpa mematikan assert.
  TrailerClipTests: field preset (Klip_preset_*) bocor ke Klip_spectator karena instance fixture NUnit dipakai ulang; SetUp kini mereset preset.
  RegressionTests.Seated_framing: kontrol negatif lampu FOV 65 tidak terdeteksi. Mask Room_Glow dirender tanpa kompilasi shader async,
  deteksi lampu terpotong mencakup tepi atas/kiri/kanan setengah atas (plus zona sudut lama), kontrol positif FOV 100, pesan gagal memuat piksel/kotak lampu.
  Tes: ~/bob/bin/check.sh CHECK LULUS (semua assembly + 139/139 Core); tes PlayMode hanya dikompilasi.
  Owner/CI: CI.md berikutnya PlayMode 41/41; bila Seated_framing masih gagal, kirim pesan diagnostik lampu dari CI.md.

- 2026-10-18 · BOB-CI · claude · CI 27d80d7 GAGAL (PlayMode 40/41): kontrol negatif lampu FOV 65 di Seated_framing tidak terdeteksi.
  Mask kini menggambar 308 px, kotak x=0-319 y=17-32 = dua globe lampu dinding di kedua tepi, tetapi terbaca di setengah bawah frame:
  readback RenderTexture terbalik Y (D3D + GetGPUProjectionMatrix renderIntoTexture), aturan tepi setengah atas tak pernah kena.
  RegressionTests.LampPixels mengkalibrasi orientasi dengan kubus penanda di atas tengah frame (jalur render sama, MaskPixels) dan membalik Y bila perlu.
  Assert framing FOV 60, kontrol negatif FOV 65 dan positif FOV 100 tidak diubah. Tes: ~/bob/bin/check.sh CHECK LULUS (139/139 Core); PlayMode hanya dikompilasi.
  Owner/CI: CI.md berikutnya PlayMode 41/41; bila gagal, kirim pesan lampu (kotak x/y, "readback Y dibalik penanda").
