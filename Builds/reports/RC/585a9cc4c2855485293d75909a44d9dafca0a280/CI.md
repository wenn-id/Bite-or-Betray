# Unity CI (laptop runner)

Commit `585a9cc4c2855485293d75909a44d9dafca0a280` · 2026-10-21 12:39 WIB · **FAILED**

| Check | Result |
|---|---|
| EditMode tests | 139/139 passed, 0 failed, 0 skipped; exit=0 |
| PlayMode tests (real scene) | Unity produced no test results (exit 3); see 20261021-1230-585a9cc-playmode.log |
| Build APK Release | Succeeded; mode=Release; errors=0; warnings=3; bytes=1682175788; duration=288.2s; path=C:\Users\acer\bob-ci\repo\Builds\BiteOrBetray_Quest_Release.apk; report=C:\Users\acer\bob-ci\repo\Builds\reports\BiteOrBetray_Quest_Release_20261021-123414_585a9cc4c2855485293d75909a44d9dafca0a280-dirty.txt |
| Shader errors | 0 |
| Compile errors | 0 |
| Budget Audit | FAILED |

- PlayMode: Unity produced no test results (exit 3); see 20261021-1230-585a9cc-playmode.log

- Build Release: Succeeded; mode=Release; errors=0; warnings=3; bytes=1682175788; duration=288.2s; path=C:\Users\acer\bob-ci\repo\Builds\BiteOrBetray_Quest_Release.apk; report=C:\Users\acer\bob-ci\repo\Builds\reports\BiteOrBetray_Quest_Release_20261021-123414_585a9cc4c2855485293d75909a44d9dafca0a280-dirty.txt; exit=1

## Budget Audit failed

See the Budget Audit report below; a successful APK build does not yet meet the gameplay budget.

## Budget Audit

```
AUDIT FAILED
System.InvalidOperationException: Meshy base color texture could not be imported: Assets/BiteOrBetray/Art3D/Meshy/RedGrouch/RedGrouch_Full_BaseColor.png (file missing; file still does not match Git LFS object b3aee419f95a after checkout)
  at BiteOrBetray.EditorTools.SeatedAvatarBuilder.PrepareMeshyMaterials () [0x0016a] in C:\Users\acer\bob-ci\repo\Assets\Scripts\Editor\SeatedAvatarBuilder.cs:242 
  at BiteOrBetray.EditorTools.GameplayPerformanceBuilder.PrepareScenes () [0x0004a] in C:\Users\acer\bob-ci\repo\Assets\Scripts\Editor\GameplayPerformanceBuilder.cs:31 
  at BiteOrBetray.EditorTools.CIRunner.Run () [0x00023] in C:\Users\acer\bob-ci\repo\Assets\Scripts\Editor\CIRunner.cs:21
```

RC archive for this commit: [manifest and full evidence](../../Builds/reports/RC/585a9cc4c2855485293d75909a44d9dafca0a280/manifest.json).
