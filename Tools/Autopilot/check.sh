#!/bin/bash
# Pemeriksaan otomatis di VPS (tanpa Unity): compile semua assembly game lalu jalankan tes aturan Core.
# Exit 0 = lulus. Dipakai agent sebelum commit dan oleh agent.sh sebelum push.
set -uo pipefail
export PATH=$HOME/.dotnet:$PATH DOTNET_ROOT=$HOME/.dotnet DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
# Tanpa build server/node yang tetap hidup: proses sisa ikut memegang lock agent dan membuat jadwal berikutnya terlewat.
export MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0 UseSharedCompilation=false
REPO=${REPO:-$PWD}
KIT=$HOME/bob/compilekit
fail=0
# Dua agent berbagi compilekit/coretest: build dijalankan bergantian.
exec 8>"$HOME/bob/check.lock"; flock 8

echo "== compile (semua assembly game, referensi Unity dari compilekit)"
# Editor mereferensikan semua assembly runtime; Tests.EditMode, DebugTools dan PerfTest dicek terpisah.
for p in BiteOrBetray.Editor BiteOrBetray.Tests.EditMode BiteOrBetray.DebugTools BiteOrBetray.PerfTest; do
  out=$(dotnet build "$KIT/$p.csproj" -nologo -v q -nodeReuse:false -p:UseSharedCompilation=false -p:RepoRoot="$REPO/" 2>&1)
  if echo "$out" | grep -q "Build succeeded"; then echo "  OK  $p"; else echo "  GAGAL $p"; echo "$out" | grep -E "error" | sort -u | head -30; fail=1; fi
done

echo "== tes aturan Core (NUnit)"
out=$(cd "$HOME/bob/coretest" && dotnet test -nologo -nodeReuse:false -p:UseSharedCompilation=false -p:RepoRoot="$REPO/" 2>&1)
echo "$out" | grep -E "Passed!|Failed!|error" | head -20
echo "$out" | grep -q "Passed!" || fail=1

[ $fail -eq 0 ] && echo "CHECK LULUS" || echo "CHECK GAGAL"
exit $fail
