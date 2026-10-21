# Automated trailer clips (BOB-052)

`Docs/Release/record-trailer.txt` containing `yes` = the laptop runner records trailer clips; any other content (e.g. `no`) = it does not.
Clips are recorded once per code commit (commits that only change `Docs/`/`Builds/` do not trigger a re-recording),
after the regular PlayMode tests, via PlayMode tests in the `Trailer` category (`Assets/Tests/PlayMode/TrailerClipTests.cs`).
The result does not change CI PASSED/FAILED; the list of files and their durations is written to the "Trailer clips" section in `Docs/Autopilot/CI.md`.

| Clip | Camera | Scene |
|---|---|---|
| 01-menu | player seat | main menu |
| 02-bot-setup | player seat | Bot setup: bot count, level per bot, Start, d20 roll |
| 03-gameplay-item | player seat | Plastic Fork, eating, Hammer Crush, Grappling Hook, Little Pocket, bot turns |
| 04-spectator | player seat | KO'd player becomes a transparent spectator |
| 05-results | player seat | winner + KO order, Play again / Back to menu |
| 06-lobby-online | player seat | Create Room with a 6-digit room code (requires internet) |
| 07-close-up-bot | Close-up Bot preset | bots reacting (bot-only match) |
| 08-top-down | Top-down Table preset | table from above, slow orbit |

All 1920x1080, 30 fps, English UI, no audio and no VR hands. Output folder: `~/bob-ci/videos/<code commit>/`
(outside the repo; the three most recent commit folders are kept). With ffmpeg each clip becomes `<clip>.mp4` (H.264 CRF 18) and
the JPG frames are deleted; without ffmpeg the frames + `make_video.cmd` stay in the clip folder.

ffmpeg on the laptop (once): `winget install --id Gyan.FFmpeg -e`, or download a build from https://www.gyan.dev/ffmpeg/builds/
and put `ffmpeg.exe` in `~/bob-ci/bin`. The runner looks in PATH, `~/bob-ci/bin` and the winget link.

After `unity_runner.py` changes, reinstall with `Tools\Autopilot\install_unity_runner.ps1`. Re-record the clips for the same
commit: `python "$HOME\bob-ci\bin\unity_runner.py" --force --trailer`.
