# Agent otomatis Bite or Betray

Agent di VPS (`devops@64.235.33.194`) mengerjakan tugas di [tasks.json](tasks.json) satu per satu lewat cron
(setiap 30 menit, satu tugas per jalan). Hasilnya di-push ke branch **`develop`**. `main` tidak pernah disentuh
agent; kamu yang merge `develop` → `main` kalau sudah oke.

**Alur per jalan** ([Tools/Autopilot/agent.sh](../../Tools/Autopilot/agent.sh)):
1. Sinkronkan `develop` dengan `main`.
2. Ambil tugas `lane: agent` berikutnya yang dependency-nya sudah `done` atau `needs_unity_check`.
3. Claude Code mengerjakan (cadangan: Codex).
4. [check.sh](../../Tools/Autopilot/check.sh): compile semua assembly game tanpa Unity + tes aturan Core.
5. Kalau lulus → push. Kalau gagal → dibatalkan. Setelah 3 kali ditolak, tugas jadi `blocked`.

## Memantau dari HP

- [LOG.md](LOG.md) di branch `develop`: ringkasan tiap tugas dan apa yang perlu kamu cek.
- [tasks.json](tasks.json): status semua tugas. Kamu boleh mengubah `status` atau `notes` langsung, misalnya
  membuka lagi tugas `blocked` dengan mengubahnya ke `todo`, atau menandai tugas `human` jadi `done`.
- Status `needs_unity_check`: kodenya sudah ada, tapi perlu dibuka di Unity atau dicoba di Quest.

## Perintah di VPS

| Perintah | Fungsi |
|---|---|
| `touch ~/bob/PAUSE` / `rm ~/bob/PAUSE` | Jeda / lanjutkan agent |
| `ls -t ~/bob/logs \| head` lalu `less ~/bob/logs/<file>` | Log tiap jalan |
| `~/bob/bin/agent.sh` | Jalankan satu tugas sekarang |
| `cd ~/bob/repo && git pull && bash Tools/Autopilot/install.sh` | Pasang ulang setelah script diubah |
| `~/bob/bin/check.sh` | Cek compile + tes manual |

Paket compile tanpa Unity (`~/bob/compilekit`) dibuat dari laptop dengan
[make_compile_kit.py](../../Tools/Autopilot/make_compile_kit.py). Buat ulang setelah upgrade Unity atau package.
