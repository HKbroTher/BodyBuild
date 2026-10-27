# Tuned default (GameConfig.CreateDefault)

Thresholds: 2,000 / 4,000 / 7,500 / 12,000 / 19,000 / 30,000 / 50,000; recruits/round 4; roster cap 8; rush threshold 6.

## Single athlete analysis (no relics, no rush)

Inject one fresh athlete until it reaches the target MassLevel (or dies).

| Target lvl | Destroy % at lvl | Score at lvl | P(reach) | Avg injections | Expected score | Expected research if sacrificed |
|---:|---:|---:|---:|---:|---:|---:|
| 1 | 6.2 | 750 | 96.1 % | 1.3 | 721 | 1.0 |
| 2 | 9.4 | 1,125 | 89.5 % | 2.5 | 1,007 | 1.8 |
| 3 | 12.6 | 1,688 | 78.9 % | 3.7 | 1,332 | 2.4 |
| 4 | 15.8 | 2,531 | 65.7 % | 4.7 | 1,663 | 3.3 |
| 5 | 19.0 | 3,797 | 51.4 % | 5.7 | 1,951 | 3.6 |
| 6 | 22.2 | 5,695 | 37.2 % | 6.5 | 2,116 | 4.1 |
| 7 | 25.4 | 8,543 | 25.6 % | 7.0 | 2,191 | 4.4 |
| 8 | 28.6 | 12,814 | 16.4 % | 7.4 | 2,099 | 4.1 |
| 9 | 31.8 | 19,222 | 9.8 % | 7.6 | 1,882 | 3.7 |
| 10 | 35.0 | 28,833 | 5.3 % | 7.7 | 1,526 | 3.0 |
| 11 | 38.2 | 43,249 | 2.7 % | 7.8 | 1,180 | 2.3 |
| 12 | 41.4 | 64,873 | 1.3 % | 7.9 | 820 | 1.6 |
| 13 | 44.6 | 97,310 | 0.5 % | 7.9 | 524 | 1.0 |
| 14 | 47.8 | 145,965 | 0.2 % | 7.9 | 330 | 0.7 |

Note: reaching a level by MUTATION can overshoot the target, so the real score is slightly higher than listed.

## Full run simulation (5000 runs per row, seeds 12345..17344)

Strategies (all buy the best-ranked affordable relic and use the rush when the next destroy chance is >= 12%):

- **Conservative**: Inject while next destroy chance < 15%
- **Balanced**: Inject while next destroy chance < 25%
- **Aggressive**: Inject while next destroy chance < 35%
- **ScoreTarget**: Inject until score >= remaining requirement / active athletes
- **Conservative+Sacrifice**: Inject while next destroy chance < 15%, sacrifice surplus once the threshold is met (keep 1)

| Strategy | Scenario | Runs | Win rate | Avg injections | Survival | Avg destroy lvl | Avg submit lvl | Avg score / submitted | Avg research | Relics bought | Stalls | Cap hits |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Conservative | Shop on | 5000 | 0.0 % | 98.3 | 75.6 % | 1.53 | 4.39 | 3,692 | 42.1 | 4.12 | 0 | 0 |
| Balanced | Shop on | 5000 | 12.5 % | 133.3 | 47.5 % | 3.14 | 7.04 | 11,050 | 48.9 | 4.26 | 0 | 0 |
| Aggressive | Shop on | 5000 | 1.9 % | 64.7 | 13.7 % | 4.83 | 10.02 | 31,820 | 8.8 | 0.78 | 0 | 0 |
| ScoreTarget | Shop on | 5000 | 2.8 % | 64.3 | 79.8 % | 2.55 | 2.62 | 2,540 | 33.0 | 3.37 | 0 | 0 |
| Conservative+Sacrifice | Shop on | 5000 | 0.0 % | 69.4 | 81.6 % | 1.56 | 4.40 | 3,697 | 36.5 | 3.60 | 0 | 0 |
| Balanced | No shop | 5000 | 0.6 % | 137.2 | 41.8 % | 3.28 | 7.26 | 9,762 | 39.3 | 0.00 | 0 | 0 |
| ScoreTarget | No shop | 5000 | 0.2 % | 61.6 | 77.7 % | 2.52 | 2.46 | 2,107 | 28.4 | 0.00 | 0 | 0 |
| Balanced | All relics | 5000 | 99.1 % | 98.8 | 77.2 % | 3.10 | 7.80 | 29,015 | 242.1 | 0.00 | 0 | 0 |
| Aggressive | All relics | 5000 | 94.1 % | 120.1 | 59.8 % | 4.96 | 9.61 | 55,718 | 359.9 | 0.00 | 0 | 0 |

Round completion probability (share of runs that cleared each competition):

| Strategy | Scenario | R1 | R2 | R3 | R4 | R5 | R6 | R7 |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| Conservative | Shop on | 99.6 % | 99.2 % | 98.3 % | 92.0 % | 44.9 % | 2.4 % | 0.0 % |
| Balanced | Shop on | 88.0 % | 84.8 % | 83.7 % | 79.4 % | 68.4 % | 43.0 % | 12.5 % |
| Aggressive | Shop on | 37.9 % | 19.8 % | 12.4 % | 8.1 % | 5.8 % | 3.6 % | 1.9 % |
| ScoreTarget | Shop on | 100.0 % | 94.2 % | 76.0 % | 53.0 % | 27.4 % | 9.9 % | 2.8 % |
| Conservative+Sacrifice | Shop on | 99.6 % | 98.6 % | 93.1 % | 61.1 % | 9.1 % | 0.2 % | 0.0 % |
| Balanced | No shop | 88.0 % | 84.1 % | 82.3 % | 74.3 % | 53.0 % | 17.0 % | 0.6 % |
| ScoreTarget | No shop | 100.0 % | 93.4 % | 70.1 % | 42.9 % | 16.6 % | 2.7 % | 0.2 % |
| Balanced | All relics | 100.0 % | 100.0 % | 100.0 % | 100.0 % | 100.0 % | 100.0 % | 99.1 % |
| Aggressive | All relics | 96.6 % | 96.2 % | 96.0 % | 95.9 % | 95.7 % | 95.2 % | 94.1 % |

Where runs die (share of all runs failing at each competition):

| Strategy | Scenario | R1 | R2 | R3 | R4 | R5 | R6 | R7 |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| Conservative | Shop on | 0.4 % | 0.4 % | 0.9 % | 6.4 % | 47.1 % | 42.5 % | 2.4 % |
| Balanced | Shop on | 12.0 % | 3.2 % | 1.2 % | 4.3 % | 11.0 % | 25.4 % | 30.5 % |
| Aggressive | Shop on | 62.1 % | 18.1 % | 7.4 % | 4.2 % | 2.3 % | 2.3 % | 1.7 % |
| ScoreTarget | Shop on | 0.0 % | 5.8 % | 18.2 % | 23.0 % | 25.6 % | 17.6 % | 7.0 % |
| Conservative+Sacrifice | Shop on | 0.4 % | 1.0 % | 5.5 % | 32.0 % | 52.0 % | 8.8 % | 0.2 % |
| Balanced | No shop | 12.0 % | 3.9 % | 1.8 % | 7.9 % | 21.3 % | 36.0 % | 16.4 % |
| ScoreTarget | No shop | 0.0 % | 6.6 % | 23.2 % | 27.3 % | 26.3 % | 13.8 % | 2.5 % |
| Balanced | All relics | 0.0 % | 0.0 % | 0.0 % | 0.0 % | 0.0 % | 0.0 % | 0.8 % |
| Aggressive | All relics | 3.4 % | 0.4 % | 0.2 % | 0.1 % | 0.2 % | 0.5 % | 1.1 % |

Loop / runaway checks:

- Conservative / Shop on: max recruits in one round = 8, action-cap hits = 0, stalled runs = 0
- Balanced / Shop on: max recruits in one round = 8, action-cap hits = 0, stalled runs = 0
- Aggressive / Shop on: max recruits in one round = 8, action-cap hits = 0, stalled runs = 0
- ScoreTarget / Shop on: max recruits in one round = 8, action-cap hits = 0, stalled runs = 0
- Conservative+Sacrifice / Shop on: max recruits in one round = 8, action-cap hits = 0, stalled runs = 0
- Balanced / No shop: max recruits in one round = 8, action-cap hits = 0, stalled runs = 0
- ScoreTarget / No shop: max recruits in one round = 8, action-cap hits = 0, stalled runs = 0
- Balanced / All relics: max recruits in one round = 9, action-cap hits = 0, stalled runs = 0
- Aggressive / All relics: max recruits in one round = 9, action-cap hits = 0, stalled runs = 0

Aggressive (shop on): MassLevel at destruction

| Level | Count | Share |
|---:|---:|---:|
| 0 | 1562 | 5.0 % |
| 1 | 2624 | 8.4 % |
| 2 | 3742 | 11.9 % |
| 3 | 2848 | 9.1 % |
| 4 | 2993 | 9.5 % |
| 5 | 3242 | 10.3 % |
| 6 | 4130 | 13.1 % |
| 7 | 4322 | 13.8 % |
| 8 | 3536 | 11.3 % |
| 9 | 2413 | 7.7 % |


# Spec baseline (GameConfig.CreateSpecBaseline)

Thresholds: 2,000 / 5,000 / 10,000 / 20,000 / 40,000 / 80,000 / 150,000; recruits/round 3; roster cap 6; rush threshold 8.

## Single athlete analysis (no relics, no rush)

Inject one fresh athlete until it reaches the target MassLevel (or dies).

| Target lvl | Destroy % at lvl | Score at lvl | P(reach) | Avg injections | Expected score | Expected research if sacrificed |
|---:|---:|---:|---:|---:|---:|---:|
| 1 | 6.2 | 750 | 96.1 % | 1.3 | 721 | 1.0 |
| 2 | 9.4 | 1,125 | 89.5 % | 2.5 | 1,007 | 1.8 |
| 3 | 12.6 | 1,688 | 78.9 % | 3.7 | 1,332 | 2.4 |
| 4 | 15.8 | 2,531 | 65.7 % | 4.7 | 1,663 | 3.3 |
| 5 | 19.0 | 3,797 | 51.4 % | 5.7 | 1,951 | 3.6 |
| 6 | 22.2 | 5,695 | 37.2 % | 6.5 | 2,116 | 4.1 |
| 7 | 25.4 | 8,543 | 25.6 % | 7.0 | 2,191 | 4.4 |
| 8 | 28.6 | 12,814 | 16.4 % | 7.4 | 2,099 | 4.1 |
| 9 | 31.8 | 19,222 | 9.8 % | 7.6 | 1,882 | 3.7 |
| 10 | 35.0 | 28,833 | 5.3 % | 7.7 | 1,526 | 3.0 |
| 11 | 38.2 | 43,249 | 2.7 % | 7.8 | 1,180 | 2.3 |
| 12 | 41.4 | 64,873 | 1.3 % | 7.9 | 820 | 1.6 |
| 13 | 44.6 | 97,310 | 0.5 % | 7.9 | 524 | 1.0 |
| 14 | 47.8 | 145,965 | 0.2 % | 7.9 | 330 | 0.7 |

Note: reaching a level by MUTATION can overshoot the target, so the real score is slightly higher than listed.

## Full run simulation (5000 runs per row, seeds 12345..17344)

Strategies (all buy the best-ranked affordable relic and use the rush when the next destroy chance is >= 12%):

- **Conservative**: Inject while next destroy chance < 15%
- **Balanced**: Inject while next destroy chance < 25%
- **Aggressive**: Inject while next destroy chance < 35%
- **ScoreTarget**: Inject until score >= remaining requirement / active athletes
- **Conservative+Sacrifice**: Inject while next destroy chance < 15%, sacrifice surplus once the threshold is met (keep 1)

| Strategy | Scenario | Runs | Win rate | Avg injections | Survival | Avg destroy lvl | Avg submit lvl | Avg score / submitted | Avg research | Relics bought | Stalls | Cap hits |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Conservative | Shop on | 5000 | 0.0 % | 54.0 | 72.3 % | 1.61 | 4.34 | 3,284 | 12.8 | 1.57 | 0 | 0 |
| Balanced | Shop on | 5000 | 0.0 % | 65.6 | 39.2 % | 3.29 | 7.08 | 9,580 | 10.3 | 1.23 | 0 | 0 |
| Aggressive | Shop on | 5000 | 0.0 % | 39.0 | 12.0 % | 4.53 | 10.07 | 30,633 | 2.5 | 0.21 | 0 | 0 |
| ScoreTarget | Shop on | 5000 | 0.0 % | 41.1 | 71.7 % | 2.82 | 2.90 | 2,730 | 11.4 | 1.29 | 0 | 0 |
| Conservative+Sacrifice | Shop on | 5000 | 0.0 % | 44.5 | 76.3 % | 1.67 | 4.40 | 3,255 | 12.5 | 1.59 | 0 | 0 |
| Balanced | No shop | 5000 | 0.0 % | 64.7 | 36.6 % | 3.38 | 7.17 | 9,384 | 9.4 | 0.00 | 0 | 0 |
| ScoreTarget | No shop | 5000 | 0.0 % | 40.5 | 71.3 % | 2.81 | 2.83 | 2,475 | 10.8 | 0.00 | 0 | 0 |
| Balanced | All relics | 5000 | 0.5 % | 100.7 | 63.5 % | 3.33 | 6.96 | 19,313 | 111.2 | 0.00 | 0 | 0 |
| Aggressive | All relics | 5000 | 23.3 % | 128.1 | 40.8 % | 4.63 | 9.96 | 70,032 | 340.5 | 0.00 | 0 | 0 |

Round completion probability (share of runs that cleared each competition):

| Strategy | Scenario | R1 | R2 | R3 | R4 | R5 | R6 | R7 |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| Conservative | Shop on | 96.5 % | 92.8 % | 68.6 % | 2.0 % | 0.0 % | 0.0 % | 0.0 % |
| Balanced | Shop on | 73.6 % | 63.1 % | 44.1 % | 19.3 % | 1.2 % | 0.0 % | 0.0 % |
| Aggressive | Shop on | 30.4 % | 11.6 % | 5.6 % | 3.1 % | 0.9 % | 0.1 % | 0.0 % |
| ScoreTarget | Shop on | 98.7 % | 75.6 % | 40.5 % | 13.9 % | 2.6 % | 0.1 % | 0.0 % |
| Conservative+Sacrifice | Shop on | 96.5 % | 90.5 % | 44.6 % | 0.2 % | 0.0 % | 0.0 % | 0.0 % |
| Balanced | No shop | 73.6 % | 63.1 % | 40.8 % | 12.7 % | 0.2 % | 0.0 % | 0.0 % |
| ScoreTarget | No shop | 98.7 % | 75.6 % | 38.1 % | 10.5 % | 1.1 % | 0.0 % | 0.0 % |
| Balanced | All relics | 99.5 % | 99.5 % | 99.3 % | 98.4 % | 85.9 % | 20.5 % | 0.5 % |
| Aggressive | All relics | 91.9 % | 88.0 % | 85.9 % | 84.1 % | 77.5 % | 58.4 % | 23.3 % |

Where runs die (share of all runs failing at each competition):

| Strategy | Scenario | R1 | R2 | R3 | R4 | R5 | R6 | R7 |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| Conservative | Shop on | 3.5 % | 3.7 % | 24.2 % | 66.6 % | 2.0 % | 0.0 % | 0.0 % |
| Balanced | Shop on | 26.4 % | 10.5 % | 19.0 % | 24.8 % | 18.2 % | 1.2 % | 0.0 % |
| Aggressive | Shop on | 69.6 % | 18.8 % | 6.0 % | 2.5 % | 2.2 % | 0.8 % | 0.1 % |
| ScoreTarget | Shop on | 1.3 % | 23.2 % | 35.1 % | 26.6 % | 11.3 % | 2.4 % | 0.1 % |
| Conservative+Sacrifice | Shop on | 3.5 % | 6.0 % | 46.0 % | 44.4 % | 0.2 % | 0.0 % | 0.0 % |
| Balanced | No shop | 26.4 % | 10.5 % | 22.3 % | 28.1 % | 12.5 % | 0.2 % | 0.0 % |
| ScoreTarget | No shop | 1.3 % | 23.2 % | 37.5 % | 27.6 % | 9.5 % | 1.1 % | 0.0 % |
| Balanced | All relics | 0.5 % | 0.0 % | 0.2 % | 0.9 % | 12.5 % | 65.4 % | 20.0 % |
| Aggressive | All relics | 8.1 % | 3.8 % | 2.1 % | 1.9 % | 6.5 % | 19.1 % | 35.1 % |

Loop / runaway checks:

- Conservative / Shop on: max recruits in one round = 6, action-cap hits = 0, stalled runs = 0
- Balanced / Shop on: max recruits in one round = 6, action-cap hits = 0, stalled runs = 0
- Aggressive / Shop on: max recruits in one round = 6, action-cap hits = 0, stalled runs = 0
- ScoreTarget / Shop on: max recruits in one round = 6, action-cap hits = 0, stalled runs = 0
- Conservative+Sacrifice / Shop on: max recruits in one round = 6, action-cap hits = 0, stalled runs = 0
- Balanced / No shop: max recruits in one round = 6, action-cap hits = 0, stalled runs = 0
- ScoreTarget / No shop: max recruits in one round = 6, action-cap hits = 0, stalled runs = 0
- Balanced / All relics: max recruits in one round = 7, action-cap hits = 0, stalled runs = 0
- Aggressive / All relics: max recruits in one round = 7, action-cap hits = 0, stalled runs = 0

Aggressive (shop on): MassLevel at destruction

| Level | Count | Share |
|---:|---:|---:|
| 0 | 1060 | 5.3 % |
| 1 | 1730 | 8.7 % |
| 2 | 2374 | 11.9 % |
| 3 | 2271 | 11.4 % |
| 4 | 2447 | 12.3 % |
| 5 | 2451 | 12.3 % |
| 6 | 2451 | 12.3 % |
| 7 | 2153 | 10.8 % |
| 8 | 1681 | 8.4 % |
| 9 | 1309 | 6.6 % |


