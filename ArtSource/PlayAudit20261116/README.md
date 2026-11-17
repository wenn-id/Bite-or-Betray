# Play audit, 2026-11-16

Unity 6000.6.3f1, Windows Editor, desktop rendering. Tested the installed CandyParlour3D scene through Unity PlayMode, real director coroutines, table actions, injected controller/pinch samples, and local NGO transport. This is software integration testing, not a physical headset play session.

## Confirmed bugs fixed

- Returning to menu or restarting during candy pickup left `ChibiHands.carrying` true. Stopping the director's nested coroutine did not reliably run its cleanup. Seat reset now explicitly clears the hand's carry state and candy reference. Both reproduction tests failed before the fix and passed afterward: `CancellationBefore.xml` and `FixVerification.xml` in `Logs/PlayAudit20261116/`.
- The desktop overview main menu could shrink into almost unreadable strips below projected character bounds. Its existing buttons now form a readable lower-left column in overview. The XR eye-camera layout retains its previous placement. All four buttons are checked for projected size and ray-hit target; Play opens bot setup.
- Nearby bot difficulty labels, especially Peach and Charcoal, overlapped in overview. Head labels now reserve space for previously placed head labels while keeping the horizontal association with their own character.
- Batched furniture disabled the dispenser's editable source renderers. The UI layout then stopped reserving its visible geometry, allowing tutorial, results and candy-status panels over it. Disabled source geometry now remains a layout obstacle while its object is active. An isolated layout regression also covers this case.
- The round board touched the dispenser rim, and local narration could overlap the player's score card. Their fixed tabletop anchors now have more separation. Obsolete sign/tag reservations are only used with the original room model.

## Test maintenance

The initial run had seven failures. Two checked obsolete character assets/rigs, and five visual checks assumed the old first-person desktop camera or old room geometry. Those failures were investigated rather than counted as seven production bugs.

Updated checks cover the new six-character materials, fifteen finger joints, visible local head in overview and hidden local head in first person, current dispenser source geometry after batching, and depth-tested score cards on each owner's mat. Negative controls still deliberately overlap controls, cover a head, move a label to the wrong mat, lift a label off the table, and break results rendering order.

## Remaining limits

- Tutorial and secondary setup text are still small in desktop overview. Main-menu readability is fixed; a broader desktop panel redesign remains separate work. The automated layout tests check clipping, spacing and interaction, not comfortable reading of every instruction.
- Quest GPU/CPU time, stereo rendering, physical hand tracking, controller alignment and headset comfort require a physical device.
- Local NGO networking is covered. Cloud Sessions/Relay, internet latency, another physical client, microphone and Vivox were not exercised.
- The conservative static Quest budget is reported separately from the desktop performance sample. Desktop frame times and draw counts must not be treated as Quest FPS.
- No Android APK build or 100,000-match optional soak was run for this audit.

## Final verification

| Suite | Passed | Failed | Skipped | Coverage |
| --- | ---: | ---: | ---: | --- |
| PlayMode | 58 | 0 | 0 | Scene, controllers/pinch/focus, hand cancellation, grips and facial animation, all eight items, rounds, KO/spectator/results, repeated restart, local host/client paths, materials and UI layout |
| EditMode | 140 | 0 | 0 | Rules, bot balance, room setup, settings, localization, onboarding, standings and wire protocol; includes 2,000 seeded matches |

The final PlayMode run took 202.84 seconds and EditMode took 17.48 seconds. Results are in `PlayModeFinal.xml` and `EditModeFinal.xml`; full Unity logs remain in `../../Logs/PlayAudit20261116/`.

`EditorPerformance.txt` records the separate automated six-player match: peak 90 draw calls, peak 45 SetPass calls, average 4.38 ms per sampled Editor frame. The whole-Editor GC average was about 8.7 KB/frame; Editor/TestRunner overhead is included. This is neither a zero-allocation claim nor a Quest frame-rate measurement.

`LayoutChecks.txt` records projected label bounds and screenshot draw budgets, including deliberate failing controls. Screenshots in `Screens/` are desktop test renders. `tutorial.png` and `bot-setup.png` also document the remaining small-text limitation.

## Quest static budget: not passed

`QuestBudgetAudit.txt` records 24 sampled directions from the local seat per scene, with FOV 110 and no occlusion/LOD discount. The main scene peaks at **273,130 triangles** against a 250,000 target, an excess of 23,130 (9.25%). The perf scene peaks at 273,186. Their estimated draw/material maxima are 84/42 and 85/43, within targets of 100/50. Pipeline, lighting and batching checks passed.

This audit intentionally exits with code 1 when the static budget is exceeded. That result is separate from the 198 passing software tests. No threshold was raised and no geometry was removed to make the audit pass.

## Scoped UI delivery review

The visual direction remains the owner's teal/cream/gold candy parlour. ENERGY 2 / RHYTHM 2 / MOTION 1 is inherited from the existing presentation work. The reviewed changes are the overview main-menu size/column and spacing between existing labels. Colors, typography, navigation destinations and input bindings are preserved.

- Hard gate PASS for the changed main-menu layout: four projected button heights exceed .035 of the viewport; each ray hits its own selectable; Play opens setup. Existing Settings/language, controller/pinch and focus regressions pass. Menu clipping and deliberate overlap checks pass in wide and square views. No new copy, imagery, claims or navigation targets were introduced. Existing shader contrast tests pass.
- Purpose gate PASS: the lower-left column protects the six-character table view while keeping controls readable. Fixed table-card separation protects the dispenser and the local score card. Vertically separating nearby bot labels retains each label's horizontal association with its character.
- Liveliness PASS within this change: the room and central candy dish remain the visual focus; menu spacing is deliberate; the existing palette and trim provide identity without added motion, decoration or effects. See `Screens/menu-overview.png`.
- Craftsmanship PASS within automated scope: 58 PlayMode tests cover the existing scene interactions, clipping, overlaps, material preservation and negative controls. `Screens/` records the rendered outcome. No physical headset comfort, keyboard Tab navigation, cloud room creation or overall tutorial legibility claim is made.

The small tutorial/setup text is a recorded existing limitation, not a completed UI redesign. The earlier detailed unchanged-style review remains at `../PresentationPolish20261115/ANTISLOP_REVIEW.md`.
