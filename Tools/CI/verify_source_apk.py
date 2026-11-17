"""Verify the committed Unity inputs against the tested APK's source receipt."""
import hashlib
import json
import subprocess
from pathlib import Path

root = Path(__file__).resolve().parents[2]
receipt = json.loads((root / 'Docs/Release/source-apk-20261117.json').read_text())
result = subprocess.run(['git', 'ls-tree', '-rz', 'HEAD', '--', 'Assets', 'Packages', 'ProjectSettings'],
                        cwd=root, capture_output=True, check=True)
records = []
for entry in result.stdout.split(b'\0'):
    if not entry:
        continue
    metadata, path = entry.split(b'\t', 1)
    mode, kind, oid = metadata.split()
    records.append(mode + b' ' + oid + b' ' + path)
fingerprint = hashlib.sha256(b'\n'.join(sorted(records)) + b'\n').hexdigest()
if len(records) != receipt['unityInputFiles'] or fingerprint != receipt['unityInputGitSHA256']:
    raise SystemExit('FAIL: committed Unity inputs differ from the tested source receipt')
dirty = subprocess.run(['git', 'diff', '--quiet', 'HEAD', '--', 'Assets', 'Packages', 'ProjectSettings'], cwd=root)
if dirty.returncode:
    raise SystemExit('FAIL: Unity inputs have local modifications')
untracked = subprocess.run(['git', 'ls-files', '--others', '--exclude-standard', '--',
                            'Assets', 'Packages', 'ProjectSettings'], cwd=root, capture_output=True, check=True)
if untracked.stdout:
    raise SystemExit('FAIL: untracked Unity inputs are not in the source snapshot')
print(f'PASS: {len(records)} committed Unity input files match the tested source receipt')
apk = root / receipt['apk']
if apk.exists():
    digest = hashlib.sha256(apk.read_bytes()).hexdigest()
    if digest != receipt['apkSHA256'] or apk.stat().st_size != receipt['apkBytes']:
        raise SystemExit('FAIL: local APK differs from the tested candidate')
    print('PASS: local APK matches the tested SHA-256 and size')
else:
    print('APK not present locally; its expected SHA-256 and size are recorded in the receipt')
