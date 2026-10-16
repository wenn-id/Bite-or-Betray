"""Paket compile-check tanpa Unity untuk agent di VPS.

Membaca csproj buatan Unity (di root project, dibuat ulang setiap Unity dibuka) untuk assembly game, menyalin DLL
referensinya ke <out>/refs, lalu menulis csproj netstandard2.1 per assembly yang meng-glob folder asmdef-nya. Hasilnya
bisa di-build dengan `dotnet build` di Linux untuk mengecek compile semua script game, termasuk file baru.

    python Tools/Autopilot/make_compile_kit.py <out_dir>

Jalankan ulang setelah upgrade Unity atau package (referensi DLL ikut berubah).
"""
import hashlib
import os
import re
import shutil
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ASSEMBLIES = {
    "BiteOrBetray.Core": "Assets/Scripts/Core",
    "BiteOrBetray.Art3D": "Assets/BiteOrBetray/Art3D/Scripts",
    "BiteOrBetray.Table3D": "Assets/Scripts/Table3D",
    "BiteOrBetray.DebugTools": "Assets/Scripts/DebugTools",
    "BiteOrBetray.PerfTest": "Assets/Scripts/PerfTest",
    "BiteOrBetray.Editor": "Assets/Scripts/Editor",
    "BiteOrBetray.Tests.EditMode": "Assets/Tests/EditMode",
}


def main(out):
    refs_dir = os.path.join(out, "refs")
    os.makedirs(refs_dir, exist_ok=True)
    copied = {}
    for name, folder in ASSEMBLIES.items():
        text = open(os.path.join(ROOT, name + ".csproj"), encoding="utf-8-sig").read()
        defines = re.search(r"<DefineConstants>([^<]*)</DefineConstants>", text).group(1)
        lang = re.search(r"<LangVersion>([^<]*)</LangVersion>", text).group(1)
        unsafe = re.search(r"<AllowUnsafeBlocks>([^<]*)</AllowUnsafeBlocks>", text)
        project_refs = re.findall(r'<ProjectReference Include="([^"]+)"', text)
        refs = []
        for include, hint in re.findall(r'<Reference Include="([^"]+)">\s*<HintPath>([^<]+)</HintPath>', text):
            src = hint if os.path.isabs(hint) else os.path.join(ROOT, hint)
            if src not in copied:
                tag = hashlib.sha1(src.lower().encode()).hexdigest()[:8]
                rel = f"{tag}/{os.path.basename(src)}"
                os.makedirs(os.path.join(refs_dir, tag), exist_ok=True)
                shutil.copy2(src, os.path.join(refs_dir, rel))
                copied[src] = rel
            refs.append((include, copied[src]))
        lines = [
            '<Project Sdk="Microsoft.NET.Sdk">',
            "  <PropertyGroup>",
            "    <TargetFramework>netstandard2.1</TargetFramework>",
            f"    <AssemblyName>{name}</AssemblyName>",
            f"    <LangVersion>{lang}</LangVersion>",
            f"    <DefineConstants>{defines}</DefineConstants>",
            f"    <AllowUnsafeBlocks>{unsafe.group(1) if unsafe else 'False'}</AllowUnsafeBlocks>",
            "    <EnableDefaultItems>false</EnableDefaultItems>",
            "    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>",
            "    <NoStdLib>true</NoStdLib>",
            "    <NoConfig>true</NoConfig>",
            "    <DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>",
            "    <NoWarn>0169;CS0649;CS0414;MSB3277</NoWarn>",
            "    <RepoRoot Condition=\"'$(RepoRoot)' == ''\">$(MSBuildThisFileDirectory)../repo/</RepoRoot>",
            "  </PropertyGroup>",
            "  <ItemGroup>",
            f'    <Compile Include="$(RepoRoot){folder}/**/*.cs" />',
        ]
        # Semua assembly package yang sudah dikompilasi Unity ikut direferensikan, supaya referensi asmdef yang baru
        # ditambah agent (mis. Unity.Netcode.Runtime) tetap ter-compile. Assembly runtime tidak diberi DLL Editor.
        editor = name in ("BiteOrBetray.Editor", "BiteOrBetray.Tests.EditMode")
        have = {os.path.basename(r).lower() for _, r in refs}
        own = {a.lower() + ".dll" for a in ASSEMBLIES}
        script_dir = os.path.join(ROOT, "Library", "ScriptAssemblies")
        for dll in sorted(os.listdir(script_dir)):
            low = dll.lower()
            if not low.endswith(".dll") or low in have or low in own or (not editor and "editor" in low):
                continue
            src = os.path.join(script_dir, dll)
            if src not in copied:
                os.makedirs(os.path.join(refs_dir, "sa"), exist_ok=True)
                shutil.copy2(src, os.path.join(refs_dir, "sa", dll))
                copied[src] = f"sa/{dll}"
            refs.append((dll[:-4], copied[src]))
        lines += [f'    <ProjectReference Include="{p}" />' for p in project_refs]
        lines += [f'    <Reference Include="{inc}"><HintPath>$(MSBuildThisFileDirectory)refs/{rel}</HintPath><Private>false</Private></Reference>'
                  for inc, rel in refs]
        lines += ["  </ItemGroup>", "</Project>", ""]
        open(os.path.join(out, name + ".csproj"), "w", encoding="utf-8").write("\n".join(lines))
        print(f"{name}: {len(refs)} referensi, {len(project_refs)} project")
    total = sum(os.path.getsize(os.path.join(refs_dir, r)) for r in copied.values())
    print(f"{len(copied)} DLL, {total / 1e6:.1f} MB -> {refs_dir}")


if __name__ == "__main__":
    main(sys.argv[1])
