# Presentation polish

Installed in `CandyParlour3D` and copied to `QuestPerfTest`. Completed 16 November 2026.

The visual direction is a cosy candy parlour with teal, cream and gold, based on the owner's supplied reference. The heads and candy dish remain the focal points. Gameplay information belongs on the table, with short effects sharing one local notice area.

Design Read: social VR tabletop game for seated players, playful candy-shop furniture and restrained gameplay notices. ENERGY 2 / RHYTHM 2 / MOTION 1. Size separates persistent seat information from action feedback; motion is reserved for tracked hands and short-lived effects.

## Installed changes

- Twelve rounded ivory gloves, mint cuffs and gold stars. Each hand has 2,846 triangles and eleven bones. Controller wrist placement is calibrated around the palm; fingers have a slightly curled resting pose. Thumb opposition and finger curls support holding candy.
- Serving cloche: 1,136 triangles. The existing reaper has a baked serving pose and 12,000 triangles, preserving its texture UVs.
- A cream ceiling with teal coffer beams and gold moulding. Shared materials, merged static geometry and no new realtime lights.
- Compact health hearts and item/status counts at each mat. The local score card sits inside the mat. Gameplay cards use depth testing and fixed world anchors.
- One local feedback area: local action effects temporarily replace narration. Other seats show at most one effect each. Effects expire within 2.2 seconds; finished narration clears after four seconds. Human action instructions remain while an action is required.
- Waiter narration uses the shared table notice. Duplicate overhead waiter text and floating effect stacks are removed. Positive/gold feedback colors were darkened for readable contrast.

## Evidence

- `../../Logs/PresentationPolishTests.xml`: 15 passed, 0 failed. Tests cover imported geometry, all six seats, controller pose, finger movement, persistent card anchors, effect replacement, narration expiry, menu click, focus/pause, interior integrity, round announcements and rendered text contrast.
- `InGame/gameplay-player.png`: player-seat gameplay and local health card.
- `InGame/gameplay-overview.png`: complete table layout.
- `InGame/gloves-relaxed.png`, `InGame/gloves-gripping.png`, `InGame/glove-candy-detail.png`: relaxed fingers and actual candy in a closed glove.
- `InGame/waiter-cloche.png`: serving pose and cloche.
- `InGame/ceiling.png`: ceiling and mouldings.
- `../../Builds/reports/QuestBudgetAudit.txt`: conservative static audit. Main scene maximum 270,106 triangles / 84 draws / 42 materials; performance scene maximum 270,162 / 85 / 43. Previous main-scene maximum was 426,667 triangles. This is a 36.7% reduction, but the 250,000-triangle static target still fails. Runtime UI and hardware timings require a Quest profile.

## Editing and regeneration

`Tools/Blender/bob_presentation_polish.py` generates the gloves, atlas, cloche and their native Blender files. `Tools/Blender/bob_reaper_serving_pose.py` starts from the unchanged original `Logs/ChibiRig/Reaper_Rig.blend` and generates the serving pose. Previous FBX assets are backed up here as `previous-*.fbx`.

Run Blender generators with the Unity Editor closed, then execute `BiteOrBetray.EditorTools.NewCharacterSceneBuilder.ApplyPresentationPolish` in Unity batch mode, or use **Bite or Betray > Polish Hands, Waiter and Ceiling** in Edit Mode. The regular character/interior builders contain the same pose, hand binding and ceiling changes.

`Validation.xml` and `QuestBudgetAudit.txt` here preserve the final test and budget reports. Reimporting original Meshy models with `bob_new_characters.py` overwrites polished FBX assets; run both polish generators again before installing the scene.

Physical controller comfort, optical readability through the headset and Quest FPS have not been checked on hardware. Screenshots and tests use desktop rendering and synthetic tracking samples.
