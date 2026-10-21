# Bot balance — BOB-051

Measured on 18 October 2026. BotBrain baseline: `eedf88cb762001ef88e85a78f2ec2d5a0d163c37`.
After: the BOB-051 changes in the commit that contains this report.

## Method

- Each version: **80 configurations × 2,000 games = 160,000 games**, engine seeds 1–2000;
  decision seed `seed * 31 + 7`. `SimulationMatrix.Combos(2..6)` covers every
  Easy/Normal/Hard composition: 6, 10, 15, 21 and 28 configurations. This represents one player + 1–5 bots.
- All decisions are automatic. One Normal seat stands in for the human to measure time;
  if there is no Normal, the first seat of the configuration is used. `HumanProxyIndex` follows `ConfigIndex`
  after the d20 shuffles the order. The proxy seat's strategy is still BotBrain, not human behavior data.
- Identical default RuleConfig: HP 4/5, 2 items/round, maximum 6 items, base bad ratio 0.38–0.52
  plus 0.04 per seat above two. Item spawn distribution is uniform in both versions.
- Win rate = wins for that level / number of games / number of seats at that level. This is **per seat**,
  so two Normals are not automatically considered stronger than one Hard. Levels that are absent are written as “—”.
- Limit of 2,000 steps/game; invalid decisions are counted before fallback. The baseline uses the
  old BotBrain with the new Simulation instrumentation so the diagnostics and time model are the same.

Full data: [before](Balance-before.csv) and [after](Balance-after.csv), 80 rows each.

## Win rate and duration before/after

E = Easy, N = Normal, H = Hard. The win rate triples below are in the order **E / N / H**.
Duration is the average estimated minutes per match, including the human proxy seat.

| Bots + human proxy | Composition of all seats | Win rate before E / N / H | After E / N / H | Minutes before | After |
|---|---|---|---|---:|---:|
| 1 bot | EN | 26.65% / 73.35% / — | 20.55% / 79.45% / — | 4.76 | 4.21 |
| 1 bot | NH | — / 44.95% / 55.05% | — / 34.75% / 65.25% | 5.94 | 4.76 |
| 1 bot | EH | 21.20% / — / 78.80% | 8.75% / — / 91.25% | 4.68 | 3.76 |
| 2 bots | ENH | 10.95% / 39.95% / 49.10% | 5.70% / 32.85% / 61.45% | 7.09 | 6.36 |
| 3 bots | ENNH | 6.65% / 28.80% / 35.75% | 2.95% / 24.60% / 47.85% | 8.62 | 7.79 |
| 4 bots | EENNH | 4.53% / 28.20% / 34.55% | 2.78% / 25.70% / 43.05% | 9.27 | 8.79 |
| 5 bots | EENNHH | 2.73% / 21.28% / 26.00% | 1.58% / 18.23% / 30.20% | 10.21 | 9.64 |

Across all 80 configurations after the change, every pair of levels present is in the correct order.
There are 35 E–N, 35 N–H and 35 E–H comparisons. The minimum N–H gap rose from
**2.72 to 7.82 percentage points**. The minimum E–N gap after the change is **11.40 points**.
In the N–H duel, Hard wins **55.05% → 65.25%**; in the E–N duel, Normal **73.35% → 79.45%**.
Sample figures do not guarantee the order in every game; fixed seeds keep regressions reproducible.

## Three-bot target: 5–10 minutes on average

The proxy seat is always Normal in this table. Each row has 2,000 games per version.
Bot win rate is counted per seat, including when all seats are Normal.

| Level of the three bots | Bot win rate before | After | Minutes before | After | P10–P90 before | P10–P90 after |
|---|---:|---:|---:|---:|---|---|
| Easy | 14.85% | 13.08% | 7.76 | 7.43 | 5.16–10.59 | 4.99–10.16 |
| Normal | 25.00% | 25.00% | 8.90 | 7.91 | 5.66–12.85 | 4.90–11.56 |
| Hard | 25.92% | 28.37% | 8.95 | 7.87 | 5.65–13.08 | 5.10–11.18 |

Mixed E/N/H + N proxy: **8.62 → 7.79 minutes**, P90 **12.34 → 11.26 minutes**.
All 15 four-seat compositions after the change average 6.78–7.95 minutes.
The average target is met; the tail of matches can still exceed 10 minutes, as the P90 in the table shows.

### Time assumptions used for both versions

The `BalanceTests.OfflineTime` profile uses `tempo = 1` and the delays in
[`CandyTableDirector`](../Assets/Scripts/Table3D/CandyTableDirector.cs): bots think for a random 1.2–2.4 seconds,
`AnnounceRound` waits 3 seconds, `SyncCandy` moves for 0.6 + waits 0.3 seconds.
Eat/feed estimates are rounded up from `Consume` and the 0.5-second delay after an action,
to leave room for BAD/paralyze reactions and interaction. Items use the default duration allowance.

| TimeModel component | Seconds |
|---|---:|
| GameIntro / GameOutro | 30 / 15 |
| WaiterFirstRound / WaiterNextRounds | 3 / 3 |
| Dispense | 0.9 |
| Human ThinkPerDecision / BotThinkPerDecision | 7 / 1.8 |
| TurnThinkLimit | 30 |
| Eat / Give / SkipTurn | 4 / 4.5 / 2 |
| Fork / Hammer / Milk / Gloves / Cupcake | 4 / 5 / 4 / 4 / 4 |
| LittlePocket + PocketChoice / GrapplingHook / Clover | 4 + 4 / 7 / 3 |

`AssumeAllHuman = false`. The intro/outro includes an allowance for player orientation. The timer in the model
caps the thinking estimate; this simulation does not trigger human timeouts or simulate VR input.
This profile is an estimate from code, **not yet Unity/Quest telemetry**. The old TimeModel defaults remain for
comparison with the prototype/GDD; the old `TargetMinutes` of 11–16 minutes for 3–4 players refers to that profile.
The BOB-051 evaluation uses the 5–10 minute target and the explicit profile above, identical before/after.

## Decision changes

- Normal is more careful at full HP (good-chance threshold 0.65), and uses Clover
  on a certain bad or when the bad chance is dominant; yellow is favorable because good does not heal.
- Hard compares heal/damage value by color, the Clover damage cap, KO risk, the benefit of
  finishing off an opponent, and the chance to continue the turn. It still uses only public information.
- Hard steals Milk when it needs HP and keeps Fork when it is already immune via Cupcake.
  Hard prefers attacking with a bad candy over destroying it with a Hammer from inventory/hook.
- Normal/Hard use the 0%/100% certainty of the public count without a Fork. Clover is not spent
  on a yellow good that gives no heal; the Pocket choice score follows that usefulness.

## Progress and regressions

| Bulk check | Before | After |
|---|---:|---:|
| Matches finished | 160,000/160,000 | 160,000/160,000 |
| Invalid decisions before fallback | 0 | 0 |
| Maximum steps/game (limit 2,000) | 176 | 160 |
| Maximum rounds/game | 22 | 16 |
| Active Clover/game, four Normal seats | 0.00 | 2.20 |

No stuck matches were found in this sample. Steps are bounded, items decrease when used,
and eating/attacks finish the match; the diagnostics record fallbacks that were previously hidden
by `BotBrain.Step`. Duration dropped together with more effective Clover use.

`BalanceTests` runs 20 cases: tiered duels, per-seat order at mixed 3/4/6 tables,
three-bot duration at each level, a small matrix of all 80 configurations (16 games/configuration),
Fork/Cupcake, certain counts, KO risk, equal decisions when hidden flavors are swapped,
and the human proxy seat after the d20 shuffle. The order cases use 128 games with fixed seeds;
the daily statistical batch uses 2,432 games, far smaller than the measurement batch.

### Rerunning without Unity

From the repo root, use the compilekit/CoreTests already installed by Autopilot (no new package needed):

```bash
BOB_BALANCE_GAMES=2000 BOB_BALANCE_OUTPUT=/tmp/BOB-051-after.csv \
  ~/.dotnet/dotnet test ~/bob/coretest/CoreTests.csproj \
  -p:RepoRoot="$PWD/" \
  --filter FullyQualifiedName~BalanceTests.Matrix_finishes_without_invalid_decisions
~/bob/bin/check.sh
```

The baseline can be repeated without replacing working files:

```bash
baseline_dir=$(mktemp -d)
mkdir -p "$baseline_dir/Assets/Scripts/Core" "$baseline_dir/Assets/Tests/EditMode"
cp Assets/Scripts/Core/*.cs "$baseline_dir/Assets/Scripts/Core/"
cp Assets/Tests/EditMode/BalanceTests.cs "$baseline_dir/Assets/Tests/EditMode/"
git show eedf88cb762001ef88e85a78f2ec2d5a0d163c37:Assets/Scripts/Core/BotBrain.cs \
  > "$baseline_dir/Assets/Scripts/Core/BotBrain.cs"
BOB_BALANCE_GAMES=2000 BOB_BALANCE_OUTPUT=/tmp/BOB-051-before.csv \
  ~/.dotnet/dotnet test ~/bob/coretest/CoreTests.csproj \
  -p:RepoRoot="$baseline_dir/" \
  --filter FullyQualifiedName~BalanceTests.Matrix_finishes_without_invalid_decisions
```

## CI evidence

The before/after measurements and Core tests run on .NET 8 without Unity. `~/bob/bin/check.sh`
**CHECK PASSED**: all assemblies including PlayMode tests compile, **139/139 Core tests pass**.
Both bulk batches (before and after) also pass via the test filter listed above.
Task status is **needs_unity_check** until the laptop runner produces a **PASS** CI.md for this commit
(EditMode, PlayMode, Budget Audit, shaders and build). The old CI.md is not evidence for the BOB-051 changes.
That verification is automatic; it does not require a manual check with a headset.
