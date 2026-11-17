# Enlarged character grip and eating audit — 2026-11-17

Tested the actual CandyParlour3D scene with six heads and twelve glove wrappers at 1.15 scale. Fixed the defects reproduced by the new scene integration tests; no mesh, texture, or triangle-count changes were needed. Mouth calibration adds one empty transform per head, also copied into QuestPerfTest.

## Defects reproduced and corrected

- Eating used a generic offset from the head instead of the visible lips. The six animated candy endpoints missed the authored mouth surface by 0.144–0.275 m. Each head now has a mouth anchor calibrated from its own mouth artwork and mesh surface in the Editor. Eating follows that moving anchor; remote avatar clones retain the calibration for their own face.
- Bot hands retained the carry displacement after eating, remaining about 0.49–0.52 m from their starting position. The carry driver now removes its previous offset before applying the current animation and eases back to the normal hand pose. Reacquired controller tracking takes priority.
- Mouth snapping ran before the visible grip was positioned and overwrote it with a controller offset. A candy visibly outside the snap radius could already be eaten. Snapping now runs after the avatar/glove pose and uses the aligned visible candy position. Both hands are covered, including exactly-once consumption.

The original three reproductions failed in `Before.xml`. Main and performance scene snapshots before mouth calibration are in `Before/`. The scene builder preserves the 115% head/glove scale and recalculates anchors when characters are regenerated.

## Verification

| Run | Result | Scope |
| --- | --- | --- |
| `PlayModeFinal.xml` | 63/63 passed, 0 skipped | Offline match flow, items, grip/eating, input/focus regressions, cancel/restart, online avatars and host leave, character layout/rig, camera, interior, text layout, gameplay performance assertions |
| `Closeup.xml` | 1/1 passed | Repeated the six actual eating sequences with a clearer screenshot camera; only the test camera changed after the 63-test run |
| `MetaXRSmoke.xml` | 1/1 passed | Separate PlayMode scene load with the original OVRPlugin enabled and actual Meta XR Simulator runtime 207.0.0; no native crash |

Final six-character eating measurements from `PlayModeFinal.xml`:

| Character | Pickup jump | Candy-to-authored-lip error | Hand return error |
| --- | --- | --- | --- |
| Blond / local | 0.0 mm | 3.2 mm | 0.0 mm |
| Silver | 0.0 mm | 7.9 mm | 0.0 mm |
| Mint | 0.0 mm | 17.5 mm | 0.0 mm |
| Navy | 0.0 mm | 6.8 mm | 0.0 mm |
| Peach | 0.0 mm | 7.1 mm | 0.0 mm |
| Charcoal | 0.0 mm | 10.0 mm | 0.0 mm |

Attachment was checked every carried frame against the visible grip anchor with a 3 mm tolerance. Return motion was sampled at 60 simulation FPS; the largest step was 38.3 mm, below the 90 mm teleport threshold. Independent lip landmarks come from the authored face artwork, rather than comparing the anchor with itself. Static mouth errors are 1.2–11.0 mm. Animated mouth deformation means a fixed head-local anchor is approximate, rather than a vertex constraint.

Screenshots in `Screens/closeup/` show pickup, carrying, mouth arrival, and return for all six characters. `Screens/final/mouth-*.png` contains test-only red/green calibration markers; those spheres are not saved in the game scene. Representative sequence: `Screens/closeup/carry-2.png`, `mouth-arrival-2.png`, and `returned-2.png`.

Unity logs: `../../Logs/HandEatAudit20261117-final.log`, `HandEatAudit20261117-closeup.log`, and `HandEatAudit20261117-meta-xr-smoke.log`. The Meta runtime reports some unsupported optional extension functions; the scene smoke still passed. Native plugin binaries/import settings were not modified.

## Limits

Scene interaction tests use deterministic controller samples through the existing input and gameplay code. The separate simulator smoke verifies XR startup and scene loading, not a complete manual simulator session. Physical Quest controller/hand tracking, headset comfort, a two-device network session, and device FPS were not tested. Passing these checks does not guarantee that every possible gameplay path is bug-free.
