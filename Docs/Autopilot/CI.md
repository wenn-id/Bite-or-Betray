# CI Unity (runner laptop)

Commit `78745db` · 2026-10-16 23:32 WIB · **GAGAL**

| Cek | Hasil |
|---|---|
| EditMode tests | 79/79 lulus, 0 gagal |
| Build APK Development | Failed; mode=Development; errors=2; warnings=21; bytes=134205176; durasi=1283.8s; path=C:\Users\acer\bob-ci\repo\Builds\BiteOrBetray_Quest_Development.apk; laporan=C:\Users\acer\bob-ci\repo\Builds\reports\BiteOrBetray_Quest_Development_20261016-231043_78745db-dirty.txt |
| Error shader | 1 |
| Error compile | 0 |

## Error shader

- `Shader error in 'Universal Render Pipeline/Lit': Lost connection with shader compiler process. Suspected crash in FXC`

## Budget Audit

```
== Render settings ==
Default RP asset: Assets/Settings/Mobile_RPAsset.asset
Quality[0] Mobile: RP=Assets/Settings/Mobile_RPAsset.asset (exists)
Quality[1] PC: RP=Assets/Settings/PC_RPAsset.asset (exists)
Quality[2] Meta Quest (Build Profile): RP=Assets/Settings/Mobile_RPAsset.asset (exists)
  Assets/Settings/Mobile_RPAsset.asset: renderScale=1.6, MSAA=4x, HDR=False, mainShadows=True res=2048, addLights=PerPixel, softShadows=False, depthTex=False, opaqueTex=False, SRPBatcher=True
  Assets/Settings/PC_RPAsset.asset: renderScale=1, MSAA=4x, HDR=True, mainShadows=True res=2048, addLights=PerPixel, softShadows=True, depthTex=True, opaqueTex=False, SRPBatcher=True
  Packages/com.unity.render-pipelines.universal/Runtime/Data/UniversalRenderPipelineAsset.asset: renderScale=1, MSAA=1x, HDR=True, mainShadows=True res=2048, addLights=PerPixel, softShadows=False, depthTex=False, opaqueTex=False, SRPBatcher=True

== CandyParlour3D (dilihat dari kursi pemain, FOV 100) ==
Renderer terlihat: 117 | draw call (submesh): 132 | +shadow pass: 131 | segitiga terlihat: 216,253 (seluruh scene 217,517, 138 draw)
Material unik: 36 | shader unik: 1 | material transparan: 1
Skinned mesh: 9 (109,165 vertex di-skin per frame) | lampu non-baked: 1 | Volume post-process: 1
Tekstur dipakai yang terlihat: 19, memori (editor, perkiraan): 20.9 MB
Shader: Universal Render Pipeline/Lit

== QuestPerfTest (dilihat dari kursi pemain, FOV 100) ==
Renderer terlihat: 118 | draw call (submesh): 133 | +shadow pass: 132 | segitiga terlihat: 259,886 (seluruh scene 261,150, 139 draw)
Material unik: 32 | shader unik: 2 | material transparan: 2
Skinned mesh: 9 (137,025 vertex di-skin per frame) | lampu non-baked: 1 | Volume post-process: 1
Tekstur dipakai yang terlihat: 5, memori (editor, perkiraan): 6.7 MB
Shader: Universal Render Pipeline/Lit, GUI/Text Shader
```
