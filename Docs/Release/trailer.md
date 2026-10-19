# Klip trailer otomatis (BOB-052)

`Docs/Release/record-trailer.txt` berisi `yes` = runner laptop merekam klip trailer; isi lain (mis. `no`) = tidak.
Klip direkam sekali per commit kode (commit yang hanya mengubah `Docs/`/`Builds/` tidak memicu rekaman ulang),
setelah tes PlayMode biasa, lewat tes PlayMode kategori `Trailer` (`Assets/Tests/PlayMode/TrailerClipTests.cs`).
Hasilnya tidak mengubah LULUS/GAGAL CI; daftar file dan durasinya ditulis ke bagian "Klip trailer" di `Docs/Autopilot/CI.md`.

| Klip | Kamera | Adegan |
|---|---|---|
| 01-menu | kursi pemain | menu utama |
| 02-atur-bot | kursi pemain | Atur bot: jumlah bot, tingkat per bot, Start, undian d20 |
| 03-gameplay-item | kursi pemain | Plastic Fork, makan, Hammer Crush, Grappling Hook, Little Pocket, giliran bot |
| 04-spectator | kursi pemain | pemain KO jadi spectator transparan |
| 05-hasil-akhir | kursi pemain | pemenang + urutan KO, Play again / Back to menu |
| 06-lobby-online | kursi pemain | Create Room dengan kode room 6 angka (butuh internet) |
| 07-close-up-bot | preset Close-up Bot | bot bereaksi (pertandingan bot saja) |
| 08-meja-atas | preset Meja dari Atas | meja dari atas, orbit pelan |

Semua 1920x1080, 30 fps, UI English, tanpa audio dan tanpa tangan VR. Folder hasil: `~/bob-ci/videos/<commit kode>/`
(di luar repo; tiga folder commit terbaru disimpan). Dengan ffmpeg tiap klip menjadi `<klip>.mp4` (H.264 CRF 18) dan
frame JPG dihapus; tanpa ffmpeg frame + `buat_video.cmd` tetap di folder klip.

ffmpeg di laptop (sekali): `winget install --id Gyan.FFmpeg -e`, atau unduh build dari https://www.gyan.dev/ffmpeg/builds/
dan taruh `ffmpeg.exe` di `~/bob-ci/bin`. Runner mencari di PATH, `~/bob-ci/bin` dan link winget.

Setelah `unity_runner.py` berubah, pasang ulang dengan `Tools\Autopilot\install_unity_runner.ps1`. Rekam ulang klip commit
yang sama: `python "$HOME\bob-ci\bin\unity_runner.py" --force --trailer`.
