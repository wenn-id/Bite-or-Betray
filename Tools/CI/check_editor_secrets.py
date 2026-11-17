"""Check a local APK for known DevAgent tokens without printing their values."""
import re
import subprocess
import sys
import zipfile
from pathlib import Path

root = Path(__file__).resolve().parents[2]
asset = 'Assets/Resources/DevAgentSettings.asset'
texts = [(root / asset).read_text()]
previous = subprocess.run(['git', 'show', 'HEAD:' + asset], cwd=root,
                          capture_output=True, text=True)
if previous.returncode == 0:
    texts.append(previous.stdout)
tokens = set()
for text in texts:
    for key, value in re.findall(r'^[ \t]*(accessToken|witClientAccessToken):[ \t]*([^\r\n]*)', text, re.M):
        value = value.strip().strip('\"\'')
        if len(value) > 16:
            tokens.add((key, value))

apk = Path(sys.argv[1]) if len(sys.argv) > 1 else root / 'Builds/BiteOrBetray_Quest_Release.apk'
matches = set()
with zipfile.ZipFile(apk) as archive:
    for item in archive.infolist():
        if item.is_dir() or not item.filename.startswith('assets/'):
            continue
        with archive.open(item) as stream:
            tail = b''
            while chunk := stream.read(1024 * 1024):
                data = tail + chunk
                for key, token in tokens:
                    if token.encode() in data or token.encode('utf-16le') in data:
                        matches.add((key, item.filename))
                tail = data[-4096:]
print(f'Checked {len(tokens)} known Editor token values; matches: {len(matches)}')
for key, entry in sorted(matches):
    print(f'FAIL: {key} in {entry}')
print('FAIL' if matches else 'PASS: no known Editor tokens in APK assets')
sys.exit(1 if matches else 0)
