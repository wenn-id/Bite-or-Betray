SUPPLIED MESHY CANDY JAR - RETEXTURED AND OPTIMIZED
2026-11-15

Source: C:/Users/acer/Downloads/Meshy_AI_Object_Glass_cookie_jar_filled_with_20261115_texture.blend
The supplied GLB has the same 10,000-triangle shape. The native Blender
source was used because its UV seam vertices were already welded.
Original downloads are unchanged. This uses the supplied jar, lid, knob
and clasps; it does not substitute the earlier cylindrical prototype.

Each optimized color variant has 1,000 triangles (90% fewer), one opaque
material, backface culling and a height of .30 m. Red, blue and yellow
variants share CandyJarShared_BaseColor.png, a 1024x1024 atlas. In Unity,
remap all FBX variants to the same material to share the atlas and batching.
Suggested import settings: URP Lit / Opaque, white base tint, ASTC 6x6,
maximum 1024, mipmaps enabled. These settings are now applied to the shared
material and texture under Assets/BiteOrBetray/NewInterior.

The old cookie-photo texture was replaced with colored candy, ivory lid,
gold collar/clasps and a matching knob. Candy depth and glass-like glints
are baked color detail on the opaque body. This is a shelf-decoration
asset: it does not contain separate candy meshes or transparent glass.

Validation: closed manifold geometry, finite vertices, normalized height,
triangle/material limits and self-contained GLB material/texture metadata.
Compared with the 30 cm source, 95% of source vertices are within 2.245 mm
of the optimized surface; maximum nearest-surface error is 4.190 mm.
The exported GLBs each contain 1,000 triangles, one material and one atlas.
At 36 copies, the jars alone would use 36,000 triangles before any LOD.

CandyJars_Quest.blend packs the atlas and contains all three editable jars,
plus the studio floor/lights/camera used for preview.png. Individual FBX
and GLB files were exported at the origin before arranging this preview.
preview.png is a Blender Cycles render, not an in-game or Quest capture.
Game integration and Android import settings passed 3/3 Unity tests in
Logs/NewInterior/jar-install.xml. Both CandyParlour3D and QuestPerfTest have
36 jars on their four racks. Per-rack batches share one material/atlas.
Real Unity captures: ArtSource/InteriorReferences/InstalledCandyJars20261115/.
Physical Quest FPS remains untested; the full scene exceeds its triangle
target because of the existing character and glove meshes.

Rebuild with Tools/Blender/bob_supplied_candy_jar.py using Blender -b -Y.
Build report: manifest.json
