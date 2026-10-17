"""Pemeriksa statis scene Unity tanpa Unity: GUID yang hilang dan nama objek yang diandalkan kode.

Membaca YAML scene langsung (hierarki GameObject/Transform, termasuk instance prefab/FBX dan nama override-nya),
lalu mengecek:
- GUID: setiap `guid:` di scene harus punya file .meta di Assets/, Packages/ (package embedded), atau
  Library/PackageCache/ (ada di laptop/CI setelah Unity membuka project). GUID bawaan Unity dilewati.
- Nama objek yang dicari CandyTableDirector.Setup() dan VRTableInput.FindAnchors() (lihat REQUIRED/OPTIONAL).

Tanpa Library/PackageCache (misalnya di VPS) GUID milik package tidak bisa dibuktikan ada, jadi dilaporkan sebagai
"belum terverifikasi" dan tidak menggagalkan, kecuali dengan --strict.

    python3 Tools/CI/check_scene.py                       # CandyParlour3D
    python3 Tools/CI/check_scene.py --tree                # tambah cetak hierarki
    python3 Tools/CI/check_scene.py --strict path.unity   # GUID package yang tak terverifikasi ikut gagal

Exit 0 = lulus, 1 = ada GUID hilang atau objek wajib tidak ditemukan, 2 = argumen/file salah.
"""
import argparse
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SCENE = "Assets/BiteOrBetray/Scenes/CandyParlour3D.unity"
# GUID bawaan Unity (resource built-in, extra, default) tidak punya .meta di project.
BUILTIN = {"0000000000000000e000000000000000", "0000000000000000f000000000000000",
           "0000000000000000d000000000000000", "0000000000000000c000000000000000",
           "00000000000000000000000000000000"}


def code_names(path, pattern):
    """Daftar nama dari kode (enum/array C#), supaya daftar objek ikut berubah kalau kode berubah."""
    with open(os.path.join(ROOT, path), encoding="utf-8") as f:
        m = re.search(pattern, f.read(), re.S)
    return re.findall(r"\w+", re.sub(r"//.*", "", m.group(1)))


BOT_COLORS = code_names("Assets/Scripts/Table3D/CandyTableDirector.cs", r"string\[\] Colors = \{(.*?)\}")
CANDIES = code_names("Assets/Scripts/Core/CoreTypes.cs", r"enum CandyColor \{(.*?)\}")
ITEMS = code_names("Assets/Scripts/Core/CoreTypes.cs", r"enum ItemType\s*\{(.*?)\}")

# (jalur, keterangan). "a/b" = b anak langsung a (Transform.Find); "a//b" = b di mana saja di bawah a (FindDeep).
# Segmen pertama dicari di mana saja di scene (GameObject.Find). Nama anchor mengikuti OVRCameraRig (nama objek
# rig-nya sendiri bebas, misalnya "[BuildingBlock] Camera Rig").
REQUIRED = [
    ("BoB_Art3D", "akar art; tanpanya director gagal setup"),
    ("BoB_Art3D/Seats", "kursi pemain dan bot"),
    ("BoB_Art3D/Seats/Seat_0", "kursi pemain manusia"),
    ("BoB_Art3D/Seats/Seat_0/HeadAnchor", "posisi kepala pemain (cadangannya Seat_0)"),
] + [(f"BoB_Art3D/Seats//Bot_{c}", f"bot kursi {c}") for c in BOT_COLORS] + [
    ("TrackingSpace/LeftHandAnchor", "anchor tangan kiri OVRCameraRig"),
    ("TrackingSpace/RightHandAnchor", "anchor tangan kanan OVRCameraRig"),
    ("TrackingSpace/LeftHandAnchor/LeftControllerAnchor", "anchor controller kiri (pointer VR)"),
    ("TrackingSpace/RightHandAnchor/RightControllerAnchor", "anchor controller kanan (pointer VR)"),
    ("TrackingSpace/CenterEyeAnchor", "kamera mata VR"),
]
# Punya cadangan di kode: hilang = peringatan, tidak menggagalkan.
OPTIONAL = [
    ("BoB_Art3D/ItemMat", "alas item per kursi"),
    ("BoB_Art3D/Waiter", "animasi pelayan"),
    ("BoB_Art3D/Dispenser", "pusat meja"),
    ("BoB_Art3D//CandySpawnPoint", "titik muncul permen (cadangannya Dispenser)"),
    ("BoB_Art3D/CandyTray", "posisi baki permen"),
] + [(f"BoB_Art3D/Candy_{c}", f"cetakan permen {c}") for c in CANDIES] + [
    (f"BoB_Art3D/Item Models/{i}", f"cetakan item {i}") for i in ITEMS]

HEADER = re.compile(r"^--- !u!(\d+) &(-?\d+)( stripped)?", re.M)
GUID = re.compile(r"guid: ([0-9a-f]{32})")
REF = r"\{fileID: (-?\d+)(?:, guid: ([0-9a-f]{32}))?"


class Node:
    def __init__(self, name):
        self.name, self.parent, self.children = name, None, []


def field(body, key):
    m = re.search(rf"^  {key}: {REF}", body, re.M)
    return m.group(1) if m else None


def unquote(value):
    """Nama YAML yang dikutip Unity ('[BuildingBlock] Camera Rig') jadi nama aslinya."""
    value = value.strip()
    if len(value) > 1 and value[0] == value[-1] == "'":
        return value[1:-1].replace("''", "'")
    if len(value) > 1 and value[0] == value[-1] == '"':
        return value[1:-1].replace('\\"', '"')
    return value


def meta_guids(sources):
    """GUID -> path aset, dari semua .meta di folder sumber."""
    found = {}
    for base in sources:
        for dirpath, dirnames, filenames in os.walk(base):
            dirnames[:] = [d for d in dirnames if not d.startswith(".")]
            for name in filenames:
                if not name.endswith(".meta"):
                    continue
                path = os.path.join(dirpath, name)
                with open(path, encoding="utf-8", errors="replace") as f:
                    m = re.search(r"^guid: ([0-9a-f]{32})", f.read(2048), re.M)
                if m:
                    found[m.group(1)] = os.path.relpath(path[:-5], ROOT)
    return found


def build_tree(text, guid_paths):
    """Hierarki scene dari YAML. Instance prefab jadi satu node dengan nama override m_Name (kalau ada) atau nama
    file sumbernya; objek yang ditambahkan ke dalam instance digantung ke node instance itu."""
    docs = {}
    heads = list(HEADER.finditer(text))
    for i, h in enumerate(heads):
        end = heads[i + 1].start() if i + 1 < len(heads) else len(text)
        docs[h.group(2)] = (int(h.group(1)), bool(h.group(3)), text[h.end():end])

    nodes, transform_node, parent_of = {}, {}, {}
    for fid, (cls, stripped, body) in docs.items():
        if cls == 1 and not stripped:
            m = re.search(r"^  m_Name: (.*)$", body, re.M)
            nodes[fid] = Node(unquote(m.group(1)) if m else "")
        elif cls == 1001:
            names = re.findall(r"propertyPath: m_Name\n\s+value: (.*)", body)
            src = re.search(rf"m_SourcePrefab: {REF}", body)
            asset = guid_paths.get(src.group(2), "") if src and src.group(2) else ""
            name = unquote(names[0]) if names else os.path.splitext(os.path.basename(asset))[0] or f"<prefab {fid}>"
            nodes[fid] = Node(name)
            parent = re.search(rf"m_TransformParent: {REF}", body)
            parent_of[fid] = parent.group(1) if parent else "0"
    for fid, (cls, stripped, body) in docs.items():
        if cls not in (4, 224):
            continue
        owner = field(body, "m_PrefabInstance") if stripped else field(body, "m_GameObject")
        if owner in nodes:
            transform_node[fid] = nodes[owner]
            if not stripped:
                parent_of[owner] = field(body, "m_Father") or "0"
    for fid, father in parent_of.items():
        parent = transform_node.get(father)
        if parent is not None and parent is not nodes[fid]:
            nodes[fid].parent = parent
            parent.children.append(nodes[fid])
    return [n for n in nodes.values() if n.parent is None], list(nodes.values())


def find(all_nodes, path):
    """Node yang cocok dengan jalur REQUIRED/OPTIONAL; segmen pertama dicari di seluruh scene."""
    steps = re.split(r"(//|/)", path)
    current = [n for n in all_nodes if n.name == steps[0]]
    for i in range(1, len(steps), 2):
        name, deep = steps[i + 1], steps[i] == "//"
        nxt = []
        for n in current:
            stack = list(n.children)
            while stack:
                c = stack.pop()
                if c.name == name:
                    nxt.append(c)
                if deep:
                    stack.extend(c.children)
        current = nxt
    return current


def print_tree(roots, depth=0):
    for n in sorted(roots, key=lambda n: n.name):
        print("  " * depth + n.name)
        print_tree(n.children, depth + 1)


def main():
    ap = argparse.ArgumentParser(description="Pemeriksa statis scene Unity (GUID + nama objek wajib).")
    ap.add_argument("scene", nargs="?", default=SCENE, help=f"path scene relatif ke root repo (default {SCENE})")
    ap.add_argument("--strict", action="store_true", help="GUID yang tidak terverifikasi tanpa PackageCache ikut gagal")
    ap.add_argument("--tree", action="store_true", help="cetak hierarki scene hasil parse")
    args = ap.parse_args()

    scene_path = os.path.join(ROOT, args.scene)
    if not os.path.isfile(scene_path):
        print(f"Scene tidak ditemukan: {args.scene}")
        return 2
    with open(scene_path, encoding="utf-8", errors="replace") as f:
        text = f.read()
    if not text.startswith("%YAML"):
        print(f"{args.scene} bukan YAML teks (Asset Serialization harus Force Text, atau file masih pointer LFS).")
        return 2

    cache = os.path.join(ROOT, "Library", "PackageCache")
    has_cache = os.path.isdir(cache)
    sources = [os.path.join(ROOT, "Assets"), os.path.join(ROOT, "Packages")] + ([cache] if has_cache else [])
    guid_paths = meta_guids(sources)
    fail = False

    print(f"== {args.scene}")
    print(f"== GUID (sumber .meta: Assets, Packages{', Library/PackageCache' if has_cache else '; Library/PackageCache tidak ada'})")
    refs, key = {}, "?"
    for line_no, line in enumerate(text.splitlines(), 1):
        # Elemen daftar ("- {fileID: ..}") memakai nama properti di baris induknya.
        m = re.match(r"\s*(?:- )?(\w+):", line)
        if m:
            key = m.group(1)
        for g in GUID.findall(line):
            refs.setdefault(g, (line_no, key))
    missing = sorted((ref, g) for g, ref in refs.items() if g not in guid_paths and g not in BUILTIN)
    print(f"  {len(refs)} GUID unik dirujuk, {len(refs) - len(missing)} ditemukan")
    if missing:
        label = "HILANG" if has_cache or args.strict else "BELUM TERVERIFIKASI (kemungkinan package)"
        fail |= has_cache or args.strict
        for (line, key), g in missing:
            print(f"  {label}: {g} ({key}, baris {line})")
        if not has_cache:
            print("  Jalankan di project yang sudah dibuka Unity (ada Library/PackageCache) untuk membedakan GUID package"
                  " dari aset yang benar-benar terhapus.")

    roots, all_nodes = build_tree(text, guid_paths)
    if args.tree:
        print("== Hierarki")
        print_tree(roots, 1)
    print("== Objek wajib (dipakai kode)")
    for path, why in REQUIRED:
        hits = find(all_nodes, path)
        if hits:
            print(f"  OK     {path}" + (f" ({len(hits)}x)" if len(hits) > 1 else ""))
        else:
            fail = True
            print(f"  HILANG {path}: {why}")
    print("== Objek opsional (kode punya cadangan)")
    for path, why in OPTIONAL:
        hits = find(all_nodes, path)
        print(f"  OK     {path}" if hits else f"  PERINGATAN {path} tidak ada: {why}")

    print("SCENE GAGAL" if fail else "SCENE LULUS")
    return 1 if fail else 0


if __name__ == "__main__":
    sys.exit(main())
