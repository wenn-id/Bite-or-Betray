# Bite or Betray: Devpost submission draft

> Draft for the Devpost project page (BOB-053). Everything below the "Project page" heading can be pasted into Devpost.
> Every feature named here exists in the code or tests; the [evidence map](#appendix-a-evidence-map-not-for-devpost)
> at the end links each one. Alwan and Zabber: rewrite **Inspiration** and **Team** in your own voice before
> submitting. Test counts are from the latest CI run in `Docs/Autopilot/CI.md` (commit 9380287).

---

# Project page

## Name

**Bite or Betray**

## Tagline

A VR candy party for Meta Quest 3: every candy is either a treat or poison. Eat it yourself, or feed it to the
player across the table.

## Inspiration

We wanted a push-your-luck party game that feels physical in VR. Bluffing games on a flat screen reduce betrayal to
a menu click. In VR you pick up a candy, look a friend in the eye and hand it to them. That one gesture became the
core of the game: you get information from the table, take a risk, and decide who has to swallow the result.

## What it does

You sit at a round table in a cosy candy parlour with up to five other players. The waiter brings a tray of candies
each round and announces how many there are of each color and how many are good or bad in total. It never says
which ones.

**Rules in 30 seconds**

- Everyone starts with **4 HP (max 5)**. A d20 roll decides the turn order. Run out of HP and you are out. The last
  player standing wins.
- Each turn there is one **active candy**. Its color is visible; whether it is good or bad is hidden.
- On your turn you can:
  - **Eat it.** Bring the candy to your mouth. Good: you keep going with the next candy. Bad: you take its effect and
    your turn ends.
  - **Give it.** Aim at another player and let go. They eat it and take the effect, and your turn always ends.
  - **Use items** first. Using an item never ends your turn.
- Candy effects:
  - **Yellow:** good has no effect, bad is HP −1.
  - **Blue:** good is HP +1, bad is HP −2.
  - **Red:** good is HP +1, bad is HP −1 and you are paralyzed (your next turn is skipped).
- You get **2 random items every round** and can hold up to 6:
  - **Plastic Fork:** reveal the active candy to everyone.
  - **Hammer Crush:** smash it (revealing it) and release a new one.
  - **Hot Milk:** HP +1.
  - **Magic Gloves:** swap the candy blind.
  - **Cupcake:** immune to the next candy you eat.
  - **Little Pocket:** open 3 random items, keep 1 and use it right away.
  - **Grappling Hook:** steal and use an opponent's item.
  - **Clover Coin:** doubles the candy's effect, capped at 3 damage, so a one-turn kill isn't possible.
- **30 seconds** to think. When time runs out, the candy is force-eaten.

**Features**

- **Offline vs bots:** choose **1 to 5 bots** and set each bot's level (**Easy / Normal / Hard**) on buttons above its
  head before the match. Easy plays half at random. Normal estimates the odds from public information and uses basic
  items. Hard also sets up Clover Coin combos. Bots only use information a human could see, and each bot's reasoning
  ("good chance 67%", "high bad chance, smash it") appears under its action.
- **Online, 2 to 6 players:** **Create Room** shows a **6-digit room code** on the table. Friends type it on a keypad
  on the table under **Join Room**. Once at least two humans have joined, the host presses Start and **bots fill the
  empty seats**. The host is authoritative. If a client disconnects mid-game, a bot takes over their seat and the match
  continues. If the host leaves, everyone returns to the menu with a clear message.
- **Spectator mode:** when you are knocked out, you stay in your seat as a 30%-transparent spectator. Your game input
  is locked, but you keep watching (and talking) until the winner is decided.
- **Voice chat:** room voice chat via Vivox, with separate voice volume and a mic mute in Settings. Spectators can
  still hear and talk.
- **Hand tracking and controllers:** pinch with your index finger to point, grab, eat and feed, or use Touch
  controllers. Each hand automatically uses whichever is available, and a tracked controller takes priority. When
  tracking is lost while you hold a candy, it goes back to the tray instead of being fed to someone by accident.
- **Onboarding:** an optional step-by-step "How to play" on the table before your first match (grabbing, eating,
  Give, items, item list). The numbers come from the same rule config as the game engine, so they stay in sync. You
  can skip it at any time.
- **Diegetic UI everywhere:** the main menu (Play vs Bots, Create Room, Join Room, Settings), table setup, lobby,
  results and Play again / Back to menu are all buttons on the table. There are no floating screens.
- **Settings:** master volume, dominant hand, mouth-snap distance (how close the candy must be to be eaten) and
  **English / Bahasa Indonesia**. All settings are saved between sessions.
- **Reactions:** characters wave, react to good and bad candies, look sick at low HP and slump when they are
  knocked out. Every key action has background music and sound effects.

## How we built it

- **Unity 6 (6000.6) with URP**, targeting **Meta Quest 3** with a conservative mobile renderer: render scale 1.0,
  MSAA 4x, a single 512 px shadow cascade, per-vertex additional lights and fixed foveated rendering.
- **Meta XR SDK** (Camera Rig, OVRInput controllers, OVRPlugin hand pinch) for input and haptics.
- **Pure C# rules engine** (`GameEngine`, `BotBrain`) with no Unity dependency. It is covered by 139 EditMode tests and
  runs the same way offline, on the online host and in our balance simulator.
- **Netcode for GameObjects**, host-authoritative: clients send intents, and the host validates the sender, seat,
  turn and revision before applying them. Public snapshots go out at 5 Hz, and hidden candy contents are removed
  before sending. Head and hand poses are quantized to 51 bytes and sent at up to 25 Hz.
- **Unity Multiplayer Services (Sessions: Lobby + Relay)** for rooms. Lobby codes contain letters, so our 6-digit
  numeric code is stored as an indexed session property and looked up by query.
- **Vivox** for room voice chat, one channel per session.
- **Characters made with Meshy** (multi-view image references → Meshy → auto-rig). A Blender script reduces each
  character to about 20k triangles and seats it at the table. The cast includes Cherry, Blue Gentleman, Red Grouch,
  Lavender Woman, Gentle Giant and the Striped Chef waiter.
- **AI-assisted development with automated verification:** two AI coding agents (Claude Code and Codex) worked
  through a shared task queue. Every change had to pass a headless compile and rules-test gate. A Windows laptop
  runner then ran the full Unity pipeline on each commit: EditMode and PlayMode tests in the real scene, a Release APK
  build and a static Quest performance budget audit.

## Challenges we ran into

- **No headset.** Nobody on the team owns a Quest, so we had to prove runtime behavior with automated tests. 41
  PlayMode tests run in the real scene: full offline matches, all eight items, knockout and spectator mode, focus
  loss, tracking loss while holding a candy, and controller vs pinch input never doubling up. Online tests include a
  host, a client and bots playing to a winner, and a client disconnect where a bot takes over.
- **Performance without a profiler on device.** We wrote a conservative static budget audit: 24 view directions from
  the player's seat, every draw counted with no batching discount. It brought the scene from 111 to 73 draw calls per
  eye, under 250k triangles and under 50 materials.
- **Networking surprises.** Imported FBX roots with a scale of 100 overflowed our 16-bit scale quantization and broke
  every visual packet, so we switched scale to half-floats. Vivox is a singleton, so joining and leaving voice
  between scene reloads had to go through one shared queue.
- **Hidden information online.** Snapshots must show every player the table without leaking which candies are bad.
  The binary codec removes hidden contents before they leave the host.

## Accomplishments that we're proud of

- A complete game loop in VR, from menu and table setup through onboarding, matches, knockout and spectator mode,
  results and replay, with no flat UI.
- Offline and online share one rules engine. Online play falls back to bots gracefully instead of breaking.
- Bot levels tuned with 160,000 simulated games: in all 80 table compositions Hard beats Normal and Normal beats
  Easy, and a match with three bots takes an estimated 8 minutes on average.
- A test and continuous integration (CI) pipeline that builds a Release APK and checks a Quest budget on every
  change, which let us ship a VR game we have never held in our hands.

## What we learned

- Write VR game rules as plain C# from day one. Tests, bots, the balance simulator and the online host all reused
  the same engine for free.
- Without hardware, a measurable proxy is better than a guess. A strict, conservative budget audit and PlayMode
  tests in the real scene caught problems we would otherwise only have found on a headset.
- In VR, physical interaction design is game design. Bring the candy to your mouth to eat it, aim at someone and
  release to betray them.

## What's next for Bite or Betray

- **Test on a physical Quest 3**: frame rate, hand-tracking feel and voice quality.
- Replace the procedurally generated placeholder sounds with final music and SFX.
- Add host migration, so a room survives when the host leaves.

## Honest limitations

- **Not tested on a physical Quest 3 yet.** All evidence comes from the Unity Editor, automated PlayMode tests, the
  Release APK build and a static performance estimate. We make no 72 FPS claim.
- Hand-tracking pinch, haptics and voice over a real internet connection between headsets have not been tried on
  device. The online tests run a host and a client in one Editor with real Relay/Lobby services.
- Music and sound effects are runtime-generated placeholder tones, not final audio.
- No host migration: if the host leaves, the online match ends.

## Built with

`unity` · `unity-6` · `urp` · `c#` · `meta-quest-3` · `meta-xr-sdk` · `openxr` · `netcode-for-gameobjects` ·
`unity-multiplayer-services` · `unity-relay` · `unity-lobby` · `vivox` · `meshy` · `blender`

## Team

- **Alwan**: _role, links_
- **Zabber**: _role, links_

---

# Screenshots

All captures come from the camera at the player's seat in the real `CandyParlour3D` scene, recorded by
`DevpostScreenshotTests` on the laptop runner in every CI run, in English. It plays a normal match: default rules
(2 items per round, 30-second timer, random draw), the scene's random seed, default bot levels and 4 bots, with a
dispenser the test never fills. The only nudges are a Plastic Fork when the human was not dealt a Fork or Hammer, and
HP 1 after the item shot so the knockout comes quickly. The files are in
[Docs/Autopilot/screens/](../Autopilot/screens/) (Git LFS) and change with each run, because the seed is random.

| File | Suggested Devpost caption |
|---|---|
| [devpost-cover.png](../Autopilot/screens/devpost-cover.png) | Cover image (3:2, 1920×1280): your turn at the candy table. |
| [devpost-01-menu.png](../Autopilot/screens/devpost-01-menu.png) | The main menu lives on the table: Play vs Bots, Create Room, Join Room, Settings. |
| [devpost-02-bot-setup.png](../Autopilot/screens/devpost-02-bot-setup.png) | Table setup: choose 1–5 bots and set each bot's level above its head. |
| [devpost-03-onboarding.png](../Autopilot/screens/devpost-03-onboarding.png) | A short "How to play" on the table before your first match. You can skip it at any time. |
| [devpost-04-your-turn.png](../Autopilot/screens/devpost-04-your-turn.png) | Your turn: eat the candy, feed it to a rival, or use an item from your mat. |
| [devpost-05-item.png](../Autopilot/screens/devpost-05-item.png) | Items never end your turn: a Plastic Fork reveals the candy, a Hammer Crush smashes it. |
| [devpost-06-spectator.png](../Autopilot/screens/devpost-06-spectator.png) | Knocked out? Stay at the table as a transparent spectator until the winner is decided. |
| [devpost-07-results.png](../Autopilot/screens/devpost-07-results.png) | Results with the winner and knockout order, then Play again or Back to menu. |

The other 1280×720 captures are 16:9. Devpost recommends 3:2, so crop them or check the edges before uploading. The
item shot shows a Fork or a Hammer depending on the deal; the CI log line `devpost-05-item: <item>` names it.

---

# Trailer (max. 3:00, target 2:30)

**Source footage:** clips recorded automatically by the laptop runner (BOB-052, `Docs/Release/record-trailer.txt` =
`yes`) into `~/bob-ci/videos/<commit>/`, at 1080p30 with English UI. Each shot below names its clip. "Seat" is the
player's seat camera. The camera presets come from the Editor recorder (BOB-045 `GameplayRecorder`): main menu,
bot close-up, top-down table.

**Editing rules**

- The recorder captures **no audio**. Add a licensed or royalty-free music track and optional voice-over (VO) in the
  edit.
- The footage comes from the desktop preview camera in the Editor, so **no VR hands or pointer** appear. Don't
  imply headset capture. Put a small caption on the first gameplay shot: *"Captured in Unity Editor from the
  player's seat."*
- Voice chat and hand tracking can't be shown in this footage. Mention them only in text cards, as listed below.

| # | Time | Clip / camera | What's on screen | Voice-over / text card |
|---|---|---|---|---|
| 1 | 0:00–0:08 | Top-down table | The table from above, the waiter beside the dispenser, candies on the tray | Card: **Bite or Betray**, a VR candy party for Meta Quest 3 |
| 2 | 0:08–0:18 | Main menu (seat, slow push in) | Play vs Bots / Create Room / Join Room / Settings on the table | VO: "Pull up a chair. Every candy is either a treat… or poison." |
| 3 | 0:18–0:32 | Table setup (seat) | Changing the bot count, cycling a bot's level Easy → Normal → Hard above its head, pressing Start | Card: **1–5 bots · Easy / Normal / Hard** |
| 4 | 0:32–0:45 | Gameplay (seat) | d20 turn-order popups, then the waiter's round announcement bubble with counts per color and good/bad totals | VO: "The waiter tells you how many are bad. Never which ones." |
| 5 | 0:45–1:05 | Gameplay with items (seat) | A bot eats a good candy and keeps going, then a bad one: HP drops and the bot reacts | Card: **Eat it… or keep going?** |
| 6 | 1:05–1:20 | Bot close-up | A bot feeding a candy to a rival (Give), and the rival's bad-candy reaction | VO: "Or hand it to a friend." · Card: **Betray** |
| 7 | 1:20–1:45 | Gameplay with items (seat) | Item moments: Plastic Fork reveal, Hammer Crush, Grappling Hook steal, Little Pocket pick | Card: **8 items** · captions with each item name |
| 8 | 1:45–1:58 | Spectator (seat) | A knocked-out player turns transparent and keeps watching | Card: **Knocked out? Stay at the table as a spectator** |
| 9 | 1:58–2:13 | Online lobby with room code (seat) | Host panel showing a 6-digit room code, player count and "empty seats filled by bots" | Card: **Online 2–6 players · room codes · bots fill empty seats · Vivox voice chat** |
| 10 | 2:13–2:23 | Results (seat) | Winner card with knockout order, Play again / Back to menu | VO: "Last one standing wins." |
| 11 | 2:23–2:30 | Top-down table (slow pull out) | The full table | Card: **Hand tracking or controllers · English / Bahasa Indonesia** · **Bite or Betray**, Unity 6 · Meta XR · Netcode · Vivox |

Scenes 3, 8, 9 and 10 depend on the clip set from BOB-052 (table setup, spectator, online lobby, results). If a
clip is missing, replace it with the matching screenshot above as a still, and keep the timing.

---

# Appendix A: evidence map (not for Devpost)

| Claim | Code | Test / evidence |
|---|---|---|
| Rules: HP 4/5, d20 order, eat/Give, 3 colors, 8 items, Clover cap 3, 30 s timer | `Assets/Scripts/Core/GameEngine.cs`, `CoreTypes.cs` (`RuleConfig`, `CandyRules`, `ItemInfo`) | `RulesTests`, `RegressionTests.Eight_items_follow_game_engine_rules`, `Three_candy_colors_and_clover_damage_cap_follow_rules` |
| 1–5 bots, Easy/Normal/Hard per bot | `Core/RoomSetup.cs`, `Core/BotBrain.cs` | `RoomSetupTests`, `OfflineMatchTests.Bot_counts_one_to_five_start_with_selected_seats_and_difficulties` |
| Bot reasoning shown under its action | `BotDecision.Reason`, `Strings.Why*` in `Core/Localization.cs` | `LocalizationTests` |
| Online 2–6, 6-digit code, bots fill empty seats | `Core/OnlineRoom.cs`, `Table3D/Net/SessionRoomLobby.cs` | `OnlineRoomTests`, `OnlineServicesTests.Create_room_with_relay_find_by_code_and_leave`, `OnlineMatchTests.Host_and_client_play_with_bots_until_winner_with_matching_snapshots` |
| Disconnect → bot, host leaves → menu | `Table3D/Net/NetworkTableSession.cs`, `CandyTableDirector.cs` (`HostLost`) | `OnlineMatchTests.Client_disconnect_mid_game_seat_played_by_bot_until_winner` |
| Host-authoritative, redacted snapshots, pose sync | `Core/TableProtocol.cs`, `Table3D/Net/*`, `RemoteAvatarRepresentation.cs` | `TableProtocolTests`, `OnlineSceneTests` |
| Spectator: transparent, input locked, voice allowed | `CandyTableDirector.cs` (`SpectatorInputLocked`, `IsVoiceSetting`), `SeatedAvatar.SetSpectator` | `OfflineMatchTests.Eliminated_human_is_locked_transparent_spectator_until_winner_and_replay` |
| Vivox voice, voice volume, mic mute | `Table3D/Net/RoomVoiceChat.cs`, `Core/GameSettings.cs` | `OnlineServicesTests.Voice_joins_room_channel_and_leaves_with_room`, `GameSettingsTests` |
| Hand pinch + controllers, tracking-loss safety | `Table3D/VRTableInput.cs` | `RegressionTests.Controller_path_works_and_pinch_never_doubles_input`, `OfflineMatchTests.Tracking_loss_with_*` (pinch not verified on device) |
| Onboarding from rule config, skippable | `Core/OnboardingGuide.cs` | `OnboardingGuideTests`, `RegressionTests.Menu_bot_setup_all_onboarding_pages_and_gameplay_with_screens` |
| Settings (volume, hand, mouth snap, language) persisted | `Core/GameSettings.cs`, `Core/Localization.cs` | `GameSettingsTests`, `RegressionTests.Language_english_by_default_switch_in_settings_applies_now_and_persists` |
| Music/SFX (placeholder tones) | `Table3D/GameplayAudio.cs` | `RegressionTests.Ko_spectator_results_music_sfx_and_telemetry_csv_with_screens` |
| Quest budget, 73 draws/eye, Release APK | `Editor/QuestPerfTestBuilder.cs`, `Editor/CIRunner.cs` | `Docs/Autopilot/CI.md` (CI 9380287: 139/139 EditMode, 41/41 PlayMode, Budget Audit passed) |
| Bot levels: Hard > Normal > Easy in all 80 compositions, ~8 min with 3 bots | `Core/BotBrain.cs`, `Core/Simulation.cs` | `Docs/Balance.md` (BOB-051, 160,000 games) |
| Devpost screenshots from a normal match | `Assets/Tests/PlayMode/DevpostScreenshotTests.cs` | `Docs/Autopilot/screens/devpost-*.png` |
| Meshy characters | `Tools/Blender/bob_meshy_rigged.py`, `Editor/SeatedAvatarBuilder.cs`, `Assets/BiteOrBetray/Art3D/Meshy/` | Budget Audit lists `CherryMesh`, `GentleGiantMesh`, … in `CandyParlour3D` |
| Trailer clips | `Editor/GameplayRecorder.cs` (BOB-045), runner BOB-052 | `GameplayRecorderTests` |
