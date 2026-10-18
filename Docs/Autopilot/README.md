# Agent otomatis Bite or Betray

Dua agent di VPS (`devops@64.235.33.194`) mengerjakan tugas di [tasks.json](tasks.json), masing-masing bagiannya
sendiri sesuai kolom `assignee`: **Claude Code** (cron menit :00/:30) dan **Codex** (menit :15/:45), satu tugas
per jalan, di salinan repo masing-masing (`~/bob/repo-claude`, `~/bob/repo-codex`). Hasilnya di-push ke branch
**`develop`**. `main` tidak pernah disentuh agent; kamu yang merge `develop` → `main` kalau sudah oke.
Mau memindah tugas ke agent lain? Ubah `assignee`-nya di tasks.json.

**Alur per jalan** ([Tools/Autopilot/agent.sh](../../Tools/Autopilot/agent.sh)):
1. Sinkronkan `develop` dengan `main`.
2. Ambil tugas `lane: agent` milik agent itu yang dependency-nya sudah `done` atau `needs_unity_check`
   (boleh milik agent lain).
3. Agent mengerjakan, lalu [check.sh](../../Tools/Autopilot/check.sh): compile semua assembly game tanpa Unity +
   tes aturan Core.
4. Digabung dengan hasil agent lain yang masuk sementara itu, dicek lagi, lalu push. Kalau gagal → dibatalkan;
   setelah 3 kali ditolak, tugas jadi `blocked`.

## Memantau dari HP

- [LOG.md](LOG.md) di branch `develop`: ringkasan tiap tugas dan apa yang perlu kamu cek.
- [tasks.json](tasks.json): status semua tugas. Kamu boleh mengubah `status` atau `notes` langsung, misalnya
  membuka lagi tugas `blocked` dengan mengubahnya ke `todo`, atau menandai tugas `human` jadi `done`.
- Status `needs_unity_check`: kodenya sudah ada, tapi perlu dibuka di Unity atau dicoba di Quest.

## Perintah di VPS

| Perintah | Fungsi |
|---|---|
| `touch ~/bob/PAUSE` / `rm ~/bob/PAUSE` | Jeda / lanjutkan kedua agent |
| `touch ~/bob/PAUSE-claude` (atau `PAUSE-codex`) | Jeda satu agent saja |
| `ls -t ~/bob/logs \| head` lalu `less ~/bob/logs/<file>` | Log tiap jalan |
| `~/bob/bin/agent.sh claude` (atau `codex`) | Jalankan satu tugas agent itu sekarang |
| `cd ~/bob/repo && git pull && bash Tools/Autopilot/install.sh` | Pasang ulang setelah script diubah |
| `cd ~/bob/repo-claude && ~/bob/bin/check.sh` | Cek compile + tes manual |

Paket compile tanpa Unity (`~/bob/compilekit`) dibuat dari laptop dengan
[make_compile_kit.py](../../Tools/Autopilot/make_compile_kit.py). Buat ulang setelah upgrade Unity atau package.

## Notifikasi ke owner (Cue)

[health.py](../../Tools/Autopilot/health.py) jalan tiap jam di laptop (Task Scheduler `BiteOrBetray Health`, dipasang oleh install_unity_runner.ps1). Masalah (agent gagal 3x beruntun, login/kuota habis, tugas `blocked`, CI merah lebih dari 24 jam, runner macet, VPS mati) ditulis ke issue berlabel `bob-alert` di repo engineering dan issue itu ditutup otomatis saat pulih. Ringkasan selalu ada di issue `bob-status`. Cue membaca keduanya lewat cron Manus dan melapor ke owner.

