#!/bin/bash
# Fixed version (L7) — DRAFT, not applied to the repo.
# Target path: Tools/Autopilot/check.sh
# Change vs HEAD 7c03afb: fail-early pre-flight checks BEFORE the flock, each naming the
# missing piece and the setup step (make_compile_kit.py / install.sh). The compile + Core
# test loop itself is unchanged.
# Automatic check on the VPS (no Unity): compile every game assembly, then run the Core rules tests.
# Exit 0 = passed. Used by agents before committing and by agent.sh before pushing.
set -uo pipefail
export PATH=$HOME/.dotnet:$PATH DOTNET_ROOT=$HOME/.dotnet DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
# No build servers/nodes left running: leftover processes hold the agent lock and make the next run skip.
export MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0 UseSharedCompilation=false
REPO=${REPO:-$PWD}
KIT=$HOME/bob/compilekit
fail=0

# --- Pre-flight: fail early with a clear message instead of a cryptic error. ---
# Setup (once per machine, see Docs/Autopilot/README.md):
#   1. dotnet: install the .NET SDK (expected at $HOME/.dotnet).
#   2. compile kit: build it on the laptop with Unity:
#        python Tools/Autopilot/make_compile_kit.py ~/bob/compilekit
#      (rebuild after upgrading Unity or packages).
#   3. coretest + scripts: run from a repo checkout:
#        bash Tools/Autopilot/install.sh
#      (creates ~/bob/bin, ~/bob/logs, ~/bob/coretest and installs CoreTests.csproj).
setup_fail=0
if ! command -v dotnet >/dev/null 2>&1; then
  echo "CHECK FAILED (setup): 'dotnet' not found on PATH." >&2
  echo "  Install the .NET SDK (expected at \$HOME/.dotnet) and re-run." >&2
  setup_fail=1
fi
if [ ! -d "$KIT" ] || [ -z "$(ls "$KIT"/*.csproj 2>/dev/null)" ]; then
  echo "CHECK FAILED (setup): compile kit not found at \$HOME/bob/compilekit (no .csproj files there)." >&2
  echo "  Build it on the laptop with Unity: python Tools/Autopilot/make_compile_kit.py ~/bob/compilekit" >&2
  echo "  (see Tools/Autopilot/make_compile_kit.py and Docs/Autopilot/README.md)." >&2
  setup_fail=1
fi
if [ ! -f "$HOME/bob/coretest/CoreTests.csproj" ]; then
  echo "CHECK FAILED (setup): Core test project not found at \$HOME/bob/coretest/CoreTests.csproj." >&2
  echo "  Run from a repo checkout: bash Tools/Autopilot/install.sh" >&2
  echo "  (it installs CoreTests.csproj from Tools/Autopilot/CoreTests.csproj.template)." >&2
  setup_fail=1
fi
if [ "$setup_fail" -ne 0 ]; then
  echo "CHECK FAILED (setup): run the setup step(s) above, then re-run check.sh." >&2
  exit 1
fi

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
