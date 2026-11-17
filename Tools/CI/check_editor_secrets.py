"""Check a local APK for known DevAgent tokens without printing their values."""
import re
import subprocess
import sys
import zipfile
from pathlib import Path

root = Path(__file__).resolve().parents[2]
asset = 'Assets/Resources/DevAgentSettings.asset'
texts = [(root / asset).read_text()]
for ref in ('HEAD', '8bf63e548cf02f02cb5db58a7d180b7988697237'):
    previous = subprocess.run(['git', 'show', ref + ':' + asset], cwd=root,
                              capture_output=True, text=True)
    if previous.returncode == 0:
        texts.append(previous.stdout)
tokens = set()
for text in texts:
    for key, value in re.findall(r'^[ \t]*(accessToken|witClientAccessToken):[ \t]*([^\r\n]*)', text, re.M):
        value = value.strip().strip('\"\'')
        if len(value) > 16:
            tokens.add((key, value))

try:
    import winreg
    with winreg.OpenKey(winreg.HKEY_CURRENT_USER, r'Software\Unity Technologies\Unity Editor 5.x') as preferences:
        for i in range(winreg.QueryInfoKey(preferences)[1]):
            name, value, _ = winreg.EnumValue(preferences, i)
            if 'RemoteServer_AccessToken' not in name:
                continue
            data = value if isinstance(value, bytes) else value.encode()
            for encoding in ('utf-8', 'utf-16le'):
                tokens.update(('current Editor token', token) for token in
                              re.findall(r'[0-9a-f]{32}', data.decode(encoding, errors='ignore')))
except (ImportError, FileNotFoundError):
    pass

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
