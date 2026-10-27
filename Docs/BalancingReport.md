# MASS FACTORY — Balancing Report

All numbers below come from the headless simulator (`Tools/MassFactory.Sim`), 5,000 seeded runs per row
(seeds 12345–17344). Full tables, including the per-level destruction histogram, are in
[`SimulationResults.md`](SimulationResults.md). Regenerate with:

```bash
cd Tools/MassFactory.Sim
dotnet run -c Release -- 5000 12345 ../../Docs/SimulationResults.md   # full report
dotnet run -c Release -- --sweep 1500                                 # config variant sweep
```

The bots are simple and deterministic, not optimal players. Treat win rates as relative signals between configs
and play styles, not as the win rate a human will see.

## 1. The core gamble (spec probability model, unchanged)

Injecting one fresh athlete until it reaches a target level:

| Target lvl | Next destroy % | Score | P(reach) | Expected score |
|---:|---:|---:|---:|---:|
| 3 | 12.6 | 1,688 | 78.9 % | 1,332 |
| 5 | 19.0 | 3,797 | 51.4 % | 1,951 |
| 7 | 25.4 | 8,543 | 25.6 % | **2,191** (peak) |
| 9 | 31.8 | 19,222 | 9.8 % | 1,882 |
| 12 | 41.4 | 64,873 | 1.3 % | 820 |

- The expected value curve is flat between level 5 and level 9, peaking at level 7. A risk-neutral player is
  close to indifferent across that range, so "one more injection?" stays a real decision there. That is the
  tension the design wants, so the **injection model was kept exactly as specified**.
- Average athlete reaching destruction dies at MassLevel ≈ 4.8 under an aggressive strategy; the histogram is
  flat from level 2 to level 7.

## 2. Spec baseline thresholds are not beatable

Spec config: thresholds 2k / 5k / 10k / 20k / 40k / 80k / 150k, 3 recruits per round, roster cap 6, rush at 8.

| Strategy (shop on) | Win rate | Clears R3 | Clears R4 | Clears R5 |
|---|---:|---:|---:|---:|
| Conservative (stop at 15% risk) | 0.0 % | 68.6 % | 2.0 % | 0.0 % |
| Balanced (stop at 25% risk) | 0.0 % | 44.1 % | 19.3 % | 1.2 % |
| Aggressive (stop at 35% risk) | 0.0 % | 5.6 % | 3.1 % | 0.9 % |
| ScoreTarget | 0.0 % | 40.5 % | 13.9 % | 2.6 % |
| Aggressive with **all 14 relics** | 23.3 % | 85.9 % | 84.1 % | 77.5 % |

**Bottleneck:** the requirement doubles every round, but athlete supply is flat (3 per round) and each athlete's
expected score tops out around 2,200. From round 3 the requirement per recruit (≥ 3,300) exceeds the best
expected value per athlete, and by round 7 it needs ~50,000 per recruit. Even holding every relic at once, the
best bot wins only 23 % of runs. Research income is also starved (≈ 10–13 research per run for most bots), so
they afford only 1–1.6 relics per run.

## 3. Tuned default (what the game ships with)

`GameConfig.CreateDefault()` changes only round-economy values; the spec baseline stays available as
`GameConfig.CreateSpecBaseline()` (and as a `GameConfigAsset` preset).

| Value | Spec | Tuned |
|---|---|---|
| Thresholds | 2k / 5k / 10k / 20k / 40k / 80k / 150k | 2k / 4k / 7.5k / 12k / 19k / 30k / 50k |
| Recruits per round | 3 | 4 |
| Roster capacity | 6 | 8 |
| Competition research rewards | 4 / 5 / 6 / 7 / 8 / 10 | 6 / 8 / 10 / 12 / 14 / 16 |
| Rush activation threshold | 8 | 6 |

Results:

| Strategy | Win rate | R1 | R3 | R5 | R6 | Avg relics bought |
|---|---:|---:|---:|---:|---:|---:|
| Conservative | 0.0 % | 99.6 % | 98.3 % | 44.9 % | 2.4 % | 4.1 |
| **Balanced** | **12.5 %** | 88.0 % | 83.7 % | 68.4 % | 43.0 % | 4.3 |
| Aggressive | 1.9 % | 37.9 % | 12.4 % | 5.8 % | 3.6 % | 0.8 |
| ScoreTarget | 2.8 % | 100.0 % | 76.0 % | 27.4 % | 9.9 % | 3.4 |
| Balanced, no shop | 0.6 % | 88.0 % | 82.3 % | 53.0 % | 17.0 % | 0 |
| Balanced, all relics | 99.1 % | 100 % | 100 % | 100 % | 100 % | — |

What this gives the player:

- **Play styles differ:** pure caution dies in rounds 5–6 (it can't produce big athletes), pure greed dies in
  round 1–2, and a middle path wins sometimes. The best fixed policy still loses ~87 % of runs, a reasonable target
  for a roguelite where humans adapt their risk per situation.
- **Relics matter:** the same Balanced policy goes from 0.6 % to 12.5 % wins just by shopping, and to 99 % with
  every relic. The 8-slot cap and one purchase per offering keep that ceiling out of reach in a normal run.
- **Round 1 is not free:** 12 % of Balanced runs die in round 1, the price of pushing to 25 % risk with only
  four fresh athletes.

## 4. Other findings

- **Sacrificing surplus athletes is currently a trap for these bots.** "Conservative+Sacrifice" (keep one athlete,
  sacrifice the rest once the requirement is met) wins 0 % and dies earlier than plain Conservative (R5 clear
  9.1 % vs 44.9 %). Carrying athletes over is worth more than `score / 500` research. Candidates if playtests agree:
  raise sacrifice yield (e.g. `score / 350`), or make research relics stronger.
- **No infinite loops found.** No run hit the 20,000-action cap and no bot stalled, including the stress test
  with all 14 relics equipped (Human Recycling + Underground Laboratory + rush relics). The most recruits seen
  in a single round was 9 with all relics (8 normally = roster cap). Rush cannot chain because it gains no
  energy while active.
- **Aggressive play is under-rewarded.** With the spec curve, stopping at 35 % risk gives 31,800 score per
  submitted athlete but only 13.7 % of athletes survive. If you want more "big swing" wins, the lever is the
  destruction slope (`DestroyChancePerLevel`, currently 3.2), not the thresholds. Lowering the slope alone did not
  help in the sweep (2.6 slope, 4 recruits, 90k final threshold: Aggressive still 0.0 % wins, because a lower slope
  also means it injects longer before stopping), so big-swing play needs relic support (Fridge, Second Heart).

## 5. Recommended next tuning passes (needs human playtests)

1. Verify that humans land near the 10–20 % win band with the tuned config, then adjust the final two thresholds.
2. Revisit sacrifice yield (see above) so that "sell the athlete for science" is a real alternative.
3. Make relic choice smarter in the bots (they buy by a fixed preference list) before reading too much into
   individual relic power. Fake Muscle Oil and Olympia Coach look strongest on paper (×1.5 score each).
4. Track human data on rush usage; the bots fire it as soon as the next destroy chance hits 12 %.
