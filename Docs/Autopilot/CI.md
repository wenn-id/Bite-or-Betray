# Unity CI (laptop runner)

Commit `65f0372f0ccfb4a79e96066f6cdb2c5bd171591f` · 2026-10-21 10:42 WIB · **FAILED**

| Check | Result |
|---|---|
| EditMode tests | 139/139 passed, 0 failed, 0 skipped; exit=0 |
| PlayMode tests (real scene) | Unity produced no test results (exit 3); see 20261021-1030-65f0372-playmode.log |
| Build APK Release | Succeeded; mode=Release; errors=0; warnings=3; bytes=1682175788; duration=322.5s; path=C:\Users\acer\bob-ci\repo\Builds\BiteOrBetray_Quest_Release.apk; report=C:\Users\acer\bob-ci\repo\Builds\reports\BiteOrBetray_Quest_Release_20261021-103622_65f0372f0ccfb4a79e96066f6cdb2c5bd171591f-dirty.txt |
| Shader errors | 0 |
| Compile errors | 0 |
| Budget Audit | FAILED |

- PlayMode: Unity produced no test results (exit 3); see 20261021-1030-65f0372-playmode.log

- Build Release: Succeeded; mode=Release; errors=0; warnings=3; bytes=1682175788; duration=322.5s; path=C:\Users\acer\bob-ci\repo\Builds\BiteOrBetray_Quest_Release.apk; report=C:\Users\acer\bob-ci\repo\Builds\reports\BiteOrBetray_Quest_Release_20261021-103622_65f0372f0ccfb4a79e96066f6cdb2c5bd171591f-dirty.txt; exit=1

## Budget Audit failed

See the Budget Audit report below; a successful APK build does not yet meet the gameplay budget.

## Budget Audit

```
AUDIT FAILED
System.InvalidOperationException: Meshy base color texture could not be imported: Assets/BiteOrBetray/Art3D/Meshy/RedGrouch/RedGrouch_Full_BaseColor.png (4565094 bytes, PNG chunk IDAT at byte 4274498 fails its CRC: corrupt file, restore it from Git LFS)
  at BiteOrBetray.EditorTools.SeatedAvatarBuilder.PrepareMeshyMaterials () [0x00154] in C:\Users\acer\bob-ci\repo\Assets\Scripts\Editor\SeatedAvatarBuilder.cs:239 
  at BiteOrBetray.EditorTools.GameplayPerformanceBuilder.PrepareScenes () [0x0004a] in C:\Users\acer\bob-ci\repo\Assets\Scripts\Editor\GameplayPerformanceBuilder.cs:31 
  at BiteOrBetray.EditorTools.CIRunner.Run () [0x00023] in C:\Users\acer\bob-ci\repo\Assets\Scripts\Editor\CIRunner.cs:21
```

RC archive for this commit: [manifest and full evidence](../../Builds/reports/RC/65f0372f0ccfb4a79e96066f6cdb2c5bd171591f/manifest.json).
