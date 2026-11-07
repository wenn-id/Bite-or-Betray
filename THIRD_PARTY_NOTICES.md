# Third-party notices — Bite or Betray

This file lists third-party material used by this project and its licence
status **as verified in the repository at HEAD `7c03afb` (2026-10-28)**.
Anything marked **unverified** below was not found in the repo — do not
assume a licence for it. If new third-party material is added, list it here.

The project's own code and documentation are licensed under the
**Apache License 2.0** — see [LICENSE](LICENSE).
Copyright 2026 Alwan Juliawan.

## 1. Meshy-generated characters — licence UNVERIFIED

- **What:** the six character models in `Assets/BiteOrBetray/Art3D/Meshy/`
  (Cherry, Blue Gentleman, Red Grouch, Lavender Woman, Gentle Giant, and
  the Striped Chef waiter), built with Meshy from multi-view image
  references and auto-rigged, then reduced to ~20k triangles and seated
  with `Tools/Blender/bob_meshy_rigged.py`.
- **Licence status: UNVERIFIED.** No Meshy licence, terms text, or
  generation receipt was found anywhere in the repo. The owner must
  confirm the licence terms of the Meshy account/plan that generated
  these models before the characters are redistributed, sold, or used
  outside this project. Until then, treat the character models as
  **not covered by the Apache-2.0 licence** of the surrounding code.
- **Related, also unverified:** the Blender script header mentions
  "Meshy auto-rig / Mixamo". If any rigging or animation data came from
  Mixamo, its licence is likewise not documented in the repo.

## 2. Unity packages — governed by Unity's terms, not by Apache-2.0

These packages are declared in `Packages/manifest.json` and fetched
through the Unity Package Manager; they are **not vendored** in this repo
(`Packages/` contains only `manifest.json` and `packages-lock.json`).
They remain under their own Unity package licences/terms — the
Apache-2.0 licence in this repo does **not** cover them.

| Package | Version (manifest.json) |
|---|---|
| Meta XR SDK (`com.meta.xr.sdk.all`) | 207.0.0 |
| Universal Render Pipeline (`com.unity.render-pipelines.universal`) | 17.6.0 |
| Netcode for GameObjects (`com.unity.netcode.gameobjects`) | 2.13.3 |
| Unity Multiplayer Services (`com.unity.services.multiplayer`) | 2.4.0 |
| Vivox (`com.unity.services.vivox`) | 16.12.1 |
| Input System (`com.unity.inputsystem`) | 1.20.0 |
| AI Navigation (`com.unity.ai.navigation`) | 2.0.14 |
| XR OpenXR (`com.unity.xr.openxr`) | 1.18.0 |
| Test Framework (`com.unity.test-framework`) | 1.8.0 |
| Timeline, UGUI, Visual Scripting, IDE integrations, built-in modules | see `Packages/manifest.json` |

## 3. Audio — no third-party audio

Music and sound effects are runtime-generated placeholder tones
(`GameplayAudio.MakePlaceholder`); no licensed music or SFX track is
bundled. Owner decision 2026-10-22 (see `Docs/Release/RC.md`): the
placeholders stay for this release and must be replaced or approved
before any release that claims final audio.

## 4. `.gitattributes` template — MIT

The header of the repo's top-level `.gitattributes` states it is the
Unity `.gitattributes` template from
`github.com/gitattributes/gitattributes`, distributed under the
**MIT License**. That licence applies to the template text only.

## 5. What was checked and NOT found

- No vendored JavaScript libraries (no `hls.js` or similar) and no
  `.unitypackage` files were found in the repo.
- No third-party font files (`.ttf`/`.otf`) were found in `Assets/`.
- No other store-bought art, third-party code, or licence files were
  found in the repo at HEAD `7c03afb`. If any such material exists
  outside the repo (e.g. on the owner's laptop only), it is not
  covered by this file until it is added and listed here.
