# Glove revision

This revision replaces the previous procedural glove meshes in CandyParlour3D and QuestPerfTest. The previous FBX files and candy-detail screenshot are preserved in `Before/`.

## Shape and rig

The glove mesh is derived from the installed Meta XR Interaction SDK `OpenXRLeftHand.fbx` and `OpenXRRightHand.fbx`, retaining its continuous hand topology and deform weights. The surface is softened and the proportions shortened, with cream cloth, a mint cuff and a gold star. The cuff bottom is closed.

Each hand contains 3,098 triangles and a 26-joint source skeleton. Fifteen finger joints are animated: three joints per digit. Idle hands are cupped, with a more relaxed index finger. Squeezing the grip closes the remaining fingers; trigger controls the index finger.

The imported mesh is aligned using PalmCenter, PalmNormal and FingerForward references. PalmCenter is placed at the tracking origin. The previous wrist translation, pitch and roll corrections are removed. Bone curl axes use actual child-joint positions and the palm normal, rather than the imported bone's assumed forward axis.

The anatomy check is explicit: with fingers forward and palms down, the left thumb faces right and the right thumb faces left. This avoids a mirrored-hand/palm-side error that a simple transform-following test cannot catch.

## Evidence

`Validation.xml` contains the final PlayMode results. Coverage includes controller translation, pitch, yaw, roll and combined rotations for both hands; anatomical side; palm position; cupped idle pose; every fingertip moving toward candy; tracked grips; menu input; focus/pause; and the existing room and text checks.

`InGame/controller-pose-0.png` through `controller-pose-4.png` show synthetic controller poses rendered in Unity. `InGame/gloves-relaxed.png`, `InGame/gloves-gripping.png` and `InGame/glove-candy-detail.png` show the final mesh in the scene. These are software checks, not recordings from a physical controller.

Physical Quest comfort, actual hand-tracking gestures and Quest GPU/CPU timings have not been verified. This revision meets the existing 3,600-triangle budget per glove; it does not establish that the entire scene meets the global Quest budget.

## Regeneration

With Unity closed:

1. Run `Tools/Blender/bob_presentation_polish.py -- --only HandRight,HandLeft` with Blender background mode.
2. Run Unity batch method `BiteOrBetray.EditorTools.NewCharacterSceneBuilder.ApplyGloveRevision`, or use **Bite or Betray > Apply Glove Revision** in Edit Mode.

The native Blender files here include the meshes, bones, reference markers and packed atlas. The generator locates the installed Meta XR Interaction SDK package in Library/PackageCache. Meta's accompanying license is preserved in `META_SDK_LICENSE.md` and beside the imported assets as `META_SDK_GLOVES_LICENSE.txt`.
