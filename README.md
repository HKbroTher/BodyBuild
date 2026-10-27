# MASS FACTORY

A 3D push-your-luck roguelite prototype for Unity. You run an underground bodybuilding operation: inject
disposable athletes with fictional substances, push their mass (and score) up exponentially, and cash them out
at competitions before they explode. Inspired by the risk/reward loop of *ForgeTax*, reskinned as dark comedy.

> All substances, side effects and biology are fictional game systems. Nothing here models real compounds,
> doses or medical protocols.

## Status

| Area | State |
|---|---|
| Gameplay core (athletes, injections, competitions, sacrifice, research, relic shop, 14 relics, ANABOLIC RUSH, run state) | Done, 93 automated tests passing under .NET 8 |
| Balancing simulator + report | Done. See [`Docs/BalancingReport.md`](Docs/BalancingReport.md) |
| Unity presentation (procedural gym, primitive bodybuilder with per-muscle growth, syringe, suspense, destruction, IMGUI HUD, shop, title/end screens, procedural audio, PS2 pixel filter, save/resume) | Written and **compile-checked** against UnityEngine 2021.3 reference assemblies. **Not yet run in the Unity Editor** (no editor was available while building it) |
| Editor menu script, PlayMode smoke test | Written; not compiled or run yet (they need the Unity Editor) |

## Getting started in Unity

1. Open the folder with **Unity 6 LTS** (`ProjectSettings/ProjectVersion.txt` says `6000.0.23f1`; any 6000.0.x
   or 2022.3 LTS should work, since the code uses only long-standing APIs and the built-in render pipeline).
   Unity generates the remaining project settings on first open.
2. Press **Play** in any scene. `MassFactoryBootstrap` creates the game automatically when no `GameManager`
   exists, so no scene or asset setup is required.
3. Optional: **Mass Factory ▸ Create Gameplay Scene** creates `Assets/Scenes/Gameplay.unity`, generates the
   ScriptableObject configs in `Assets/ScriptableObjects/`, and adds the scene to Build Settings.

### Controls

| Key | Action |
|---|---|
| Space | Inject selected athlete |
| S | Submit to competition |
| X | Sacrifice for research |
| Tab | Next athlete |
| R | Activate ANABOLIC RUSH |
| B | Relic shop |
| Enter | Advance round / claim the crown |
| F | Toggle fast animations |
| F1 / Esc | Help / back to title |

Buttons disable themselves when an action is invalid; hovering a disabled button shows why.

## Rules (as implemented)

- Each round recruits new athletes (default 4, roster capacity 8). Score = `500 × 1.5^MassLevel`.
- Injection outcomes: MUTATION +2, GROWTH +1, NO EFFECT, ATROPHY −1 (min 0), BODY DESTROYED (athlete is gone).
  Destroy chance = `clamp(3 + 3.2 × level, 3, 65)`. ATROPHY 12 % and NO EFFECT 11 % come next, GROWTH takes the
  remainder up to 62 %, and MUTATION gets the rest. The table is always normalized to exactly 100 %.
- Submit adds the athlete's score to the round total (irreversible). Sacrifice gives `floor(score / 500)` research.
- Advance when the requirement is met. Unused athletes carry over. The run is lost when no athletes remain and the
  requirement is unmet; clearing round 7 wins.
- Shop: one purchase per offering of 3 random relics, paid restocks, 8 relic slots.
- ANABOLIC RUSH: GROWTH gives +1 energy and MUTATION +2. At the threshold (default 6) you can activate it, and the
  next 3 injections cannot destroy.

### Deviations from the design brief (on purpose)

- **Default tuning differs from the brief's starting numbers.** Simulation showed the brief's thresholds are
  unwinnable (0 % wins for every strategy across 5,000 runs; details in the balancing report). The game ships a tuned
  config; the brief's values are kept intact as `GameConfig.CreateSpecBaseline()` / the `SpecBaseline` asset
  preset. The injection probability model itself is exactly the brief's.
- **Two extra relics** (Adrenaline Pump, Overclocked Adrenals) exercise the rush modifiers; 14 in total.
- **Black Market Fridge** is implemented as "the first injection of each athlete each round cannot destroy",
  so the protection shows up in the displayed destruction chance.
- **Human Recycling Program** recruits immediately (respecting roster capacity) rather than next round.
- **Main menu is an in-game title screen** instead of a separate `MainMenu.unity`; there is a single gameplay scene.
- **UI uses IMGUI** so it needs no prefabs, fonts or packages and could be compile-checked without the editor.
  Moving to uGUI/UI Toolkit later only touches `Assets/Scripts/Runtime/UI`.

## Architecture

```
Assets/Scripts/
  Core/                    MassFactory.Core.asmdef (noEngineReferences: pure C#, no UnityEngine)
    Config/                GameConfig (+ CreateDefault / CreateSpecBaseline), all tunables
    Random/                SeededRandom (xorshift64*, serializable state)
    Athletes/              AthleteData, ScoreCalculator (overflow-safe), name generator
    Injection/             OutcomeTable, InjectionProbabilityCalculator, InjectionSystem, InjectionResult
    Competition/           CompetitionManager (thresholds, submissions, completion)
    Economy/               ResearchManager
    Rush/                  RushManager (ANABOLIC RUSH)
    Relics/                RelicDefinition/Catalog, RelicEffect base + 14 effects, RelicManager, RelicShop
    Events/                GameEventBus + event types
    Run/                   GameSession (command facade + validation), RunState (serializable), ActionResult
    Simulation/            Strategies + SimulationRunner (headless, thousands of runs)
  Runtime/                 MassFactory.Runtime.asmdef (Unity presentation)
    GameManager            Owns the session, builds the scene, maps UI requests to commands, plays animations
    Config/                GameConfigAsset, RelicDefinitionAsset, RelicCatalogAsset (ScriptableObjects)
    Presentation/          Muscle rigs (primitive + blend-shape), body factory, athlete visual, syringe,
                           injection sequence, gym builder, camera rig, retro filter, procedural audio
    UI/                    GameplayUIController (IMGUI HUD), UiTheme
    Save/                  SaveSystem (JsonUtility, persistentDataPath)
    Bootstrap/             Auto-creates the game in any scene
  Editor/                  "Mass Factory" menu: default assets + Gameplay scene
Assets/Tests/EditMode/     NUnit tests for the core (also run by Tools/MassFactory.Tests)
Assets/Tests/PlayMode/     Smoke test that boots GameManager and plays a few actions
Tools/                     .NET projects for working outside Unity (see below)
Docs/                      Balancing report and raw simulation output
```

Key decisions:

- **Logic and presentation are separate.** Every rule lives in `GameSession` and its subsystems. The Unity layer
  resolves a command first, then animates the result, so animation can never change an outcome. An input lock
  (`GameManager.Busy`) plus a re-entrancy guard in the core prevent double injections and double submissions.
- **Relics are event-driven.** `RelicEffect` exposes modifier pipelines (outcome table, outcome transform, score
  multiplier, costs, recruit count, rush threshold/duration, sacrifice research) and event hooks (recruited,
  before injection, after outcome, mass changed, destroyed, sacrificed, submitted, competition completed).
  `RelicManager` dispatches them. Gameplay code never checks relic ids, so adding a relic means adding one
  `RelicEffect` subclass, one factory case and one definition.
- **Determinism.** One seeded RNG drives a run; its state is part of `RunState`, so the same seed and the same
  actions replay identically, and save/resume continues the exact random sequence (both covered by tests).
- **Swappable visuals.** `IMuscleRig` has a primitive implementation (used now) and `BlendShapeMuscleRig` for a
  rigged model with blend shapes named per muscle group (Chest, Shoulders, Biceps, Triceps, Back, Quadriceps,
  Calves, OverallMuscleMass). Growth follows the brief's tiers (0–3 normal, 4–7 pro, 8–11 freak, 12+ mutant).

## Working outside Unity

Requires the .NET 8 SDK.

```bash
# Unit tests (the same files Unity runs as EditMode tests)
dotnet test Tools/MassFactory.Tests

# Balancing simulation: runs, seed, optional report path
dotnet run -c Release --project Tools/MassFactory.Sim -- 5000 12345 Docs/SimulationResults.md
dotnet run -c Release --project Tools/MassFactory.Sim -- --sweep 1500

# Compile-check the Unity presentation layer against UnityEngine reference assemblies (NuGet UnityEngine.Modules 2021.3)
dotnet build Tools/MassFactory.UnityCheck
```

## Not verified yet

These still need someone with the Unity Editor:

- Entering Play mode, how the procedural scene and IMGUI layout look at different resolutions, and the feel of the
  animations and audio.
- `Assets/Scripts/Editor/MassFactoryEditorMenu.cs` and `Assets/Tests/PlayMode/GameplaySmokeTests.cs` have not been
  compiled (no UnityEditor / test-framework reference assemblies were available).
- Player builds. The retro filter and procedural materials avoid `Shader.Find`, so they should survive stripping,
  but this hasn't been tested.
- Unity 6 compatibility was not compile-checked (the reference assemblies are from 2021.3). No APIs that changed in
  Unity 6 are used as far as I know.
