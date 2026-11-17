CANDY JAR PROTOTYPE - 2026-11-15

Three 30 cm shelf-decoration jars: red, blue and yellow. Each is 584
triangles and uses one material. All three share CandyJarSharedAtlas.png,
a 1024x1024 color atlas. The jar body is opaque: packed candy shading and
glass-like highlights are painted into its texture. The cap, neck, base
and colored knob are modeled. Candy geometry hidden under the cap was
removed; this version has no individual candy meshes or transparent shell.

This is intended for shelf viewing distance. It does not provide glass
transparency, interior parallax or physically movable contents at close range.
The existing gameplay candy meshes are independent of this decorative prop.

CandyJarsPrototype.blend is editable and packs the shared atlas. Individual
FBX files are exported at the origin before arranging the three preview jars.
The native generator checks closed mesh components, finite vertices,
30 cm height and a maximum 700 triangles. The shared atlas permits batching
the color variants with one material when integrated into Unity.

preview.png is a Blender Cycles render, not a Unity or Quest capture.
These prototypes are not installed in CandyParlour3D yet. Android texture
compression, rack placement, draw calls and Quest frame rate are untested.
For scale: 36 copies would contain 21,024 triangles before any LOD reduction.

Regenerate with Blender using Tools/Blender/bob_candy_jar_prototype.py.
