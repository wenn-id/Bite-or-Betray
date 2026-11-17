# Player size, 2026-11-17

Six player heads and twelve gloves are uniformly scaled from 1.0 to 1.15 in CandyParlour3D and QuestPerfTest. The character builder uses the same scale for newly generated heads and replacement gloves. Seat/root transforms are preserved; heads grow around their existing head pivot and gloves around their calibrated palm origin. The existing eye reference and grip anchor inherit the enlarged visual scale.

Scene readback verified all 18 changes in each scene. Positions, rotations, mesh bindings and every other scene record match the pre-change snapshots exactly. Evidence: `SceneVerification.json`. Mesh topology and triangle counts were not changed. `Before/` contains the original scenes and earlier table render for comparison.

PlayMode/render verification of the new size could not run: Unity crashes during startup, before executing editor methods. Windows Application Error events identify OVRPlugin.dll with exception 0xc0000409; an attempt with its Editor import temporarily disabled also failed during native startup. The plugin metadata was restored byte-for-byte and its restored hash was checked. No persistent Meta XR plugin or runtime setting was changed. The orphan MetaXRSimulator process was closed during troubleshooting.

The size changes were therefore applied to the existing serialized transform records and independently read back, rather than represented as a successful Unity render/test run. Previous PlayMode results predate this size revision. Check physical Quest tracking and visual clearance once the native Editor startup issue is resolved.

Diagnostic Unity logs: `../../Logs/CharacterScale20261117*.log`.

## Follow-up verification

The startup blocker is now resolved: an orphan ADB server held the SDK's `TRiMSchema.json` cache open. After restarting ADB and refreshing that cache, native startup succeeded, a real Meta XR Simulator PlayMode smoke passed, and all eleven character/layout/rig tests passed with the original OVRPlugin enabled. See `../OVRCrashFix20261117/README.md` and `PlayMode.xml`. `verified-table-clean.png` is the new Unity-rendered overview with the 15% larger heads and gloves. Physical headset testing remains outstanding.

The subsequent grip/eating audit reproduced and fixed incorrect mouth endpoints, retained bot hand displacement, and premature mouth snapping. All 63 selected PlayMode tests and a separate real Meta XR Simulator scene smoke now pass with the 115% scale preserved. See `../HandEatAudit20261117/README.md` for measurements, test results, and screenshots.
