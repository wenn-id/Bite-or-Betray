#!/bin/bash
# Automatic check on the VPS (no Unity): compile every game assembly, then run the Core rules tests.
# Exit 0 = passed. Used by agents before committing and by agent.sh before pushing.
set -uo pipefail
export PATH=$HOME/.dotnet:$PATH DOTNET_ROOT=$HOME/.dotnet DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
# No build servers/nodes left running: leftover processes hold the agent lock and make the next run skip.
export MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0 UseSharedCompilation=false
REPO=${REPO:-$PWD}
KIT=$HOME/bob/compilekit
fail=0
# Both agents share compilekit/coretest: builds take turns.
exec 8>"$HOME/bob/check.lock"; flock 8

echo "== compile (all game assemblies, Unity references from compilekit)"
# Editor references every runtime assembly; the tests, DebugTools and PerfTest are checked separately.
for p in BiteOrBetray.Editor BiteOrBetray.Tests.EditMode BiteOrBetray.Tests.PlayMode BiteOrBetray.DebugTools BiteOrBetray.PerfTest; do
  out=$(dotnet build "$KIT/$p.csproj" -nologo -v q -nodeReuse:false -p:UseSharedCompilation=false -p:RepoRoot="$REPO/" 2>&1)
  if echo "$out" | grep -q "Build succeeded"; then echo "  OK     $p"; else echo "  FAILED $p"; echo "$out" | grep -E "error" | sort -u | head -30; fail=1; fi
done

echo "== Core rules tests (NUnit)"
out=$(cd "$HOME/bob/coretest" && dotnet test -nologo -nodeReuse:false -p:UseSharedCompilation=false -p:RepoRoot="$REPO/" 2>&1)
echo "$out" | grep -E "Passed!|Failed!|error" | head -20
echo "$out" | grep -q "Passed!" || fail=1

[ $fail -eq 0 ] && echo "CHECK PASSED" || echo "CHECK FAILED"
exit $fail
