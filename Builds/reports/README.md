# Bukti build Release candidate

BOB-025 meminta Release lewat `Docs/Release/build-mode.txt`. Status dan checklist ada di
[Docs/Release/RC.md](../../Docs/Release/RC.md). Belum ada hasil build Release yang bisa dilampirkan dari VPS tanpa Unity.

Runner laptop menyimpan setiap eksekusi Release ke `RC/<hash commit penuh>/` sebelum checkout untuk publikasi:

- `manifest.json`: commit sumber, mode, exit/hasil build, setiap nama/hasil tes, daftar bukti hilang, SHA-256 setiap bukti
  dan screenshot dari eksekusi yang sama.
- `CI.md`, `editmode.xml`, `playmode.xml`: laporan lengkap beserta tes gagal/dilewati.
- `build-report.txt`: laporan Unity (durasi, ukuran, SHA-256 APK, error/warning, aset terbesar, stripping shader).
- `perf.txt`, `perf-telemetry.csv`, `regression-telemetry.csv`, `audit.txt`: data utuh tanpa pemotongan cuplikan CI.md.
- `workspace.diff`, `workspace-status.txt`: perubahan tracked serta daftar berkas berubah/baru di Assets/Packages/ProjectSettings
  setelah prepare/import/tes/build; periksa bila laporan build bertanda `-dirty`. Aset baru hasil generate tidak disertakan
  dalam patch. Hash sumber saja belum membuktikan APK identik dengan checkout bersih.

Bukti yang tidak dihasilkan ditulis ke `manifest.missing`; jangan menganggapnya lulus. Pengulangan hash yang sama mengganti
arsip hash itu dengan hasil terbaru, sementara riwayat Git menyimpan publikasi sebelumnya. Screenshot PNG tidak diduplikasi di
arsip; cocokkan hash manifest dengan `Docs/Autopilot/screens/` pada commit publikasi CI tersebut (unduh LFS).
APK tidak di-commit; hasilnya tetap `Builds/BiteOrBetray_Quest_Release.apk` di laptop. Log mentah Unity tetap di `~/bob-ci/logs/`.

Folder Builds di-ignore agar APK tidak masuk Git. Runner menggunakan `git add -f` hanya untuk arsip teks `RC/<hash>/`;
README ini disertakan secara eksplisit. Jangan menambahkan APK atau mengubah pointer LFS dari VPS.
