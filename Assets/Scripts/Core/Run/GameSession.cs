using System;
using System.Collections.Generic;

namespace MassFactory.Core
{
    /// <summary>
    /// Facade over a single run. All player actions go through the command methods here, which
    /// validate the state transition, apply the rules and publish events. Contains no engine code,
    /// so the full game can be played by tests, the simulator or the Unity presentation layer.
    /// </summary>
    public sealed class GameSession
    {
        private readonly SeededRandom _rng;
        private bool _resolving;

        public GameConfig Config { get; }
        public RelicCatalog Catalog { get; }
        public RunState State { get; }
        public GameEventBus Events { get; }

        public RelicManager Relics { get; }
        public RushManager Rush { get; }
        public CompetitionManager Competition { get; }
        public ResearchManager Research { get; }
        public InjectionSystem Injection { get; }
        public RelicShop Shop { get; }

        public IRandomSource Rng => _rng;

        private GameSession(GameConfig config, RelicCatalog catalog, RunState state, SeededRandom rng)
        {
            Config = config;
            Catalog = catalog;
            State = state;
            Events = new GameEventBus();
            _rng = rng;

            // RelicManager subscribes first so relic reactions run before presentation listeners.
            Relics = new RelicManager(this);
            Rush = new RushManager(this);
            Competition = new CompetitionManager(this);
            Research = new ResearchManager(this);
            Injection = new InjectionSystem(this);
            Shop = new RelicShop(this);
        }

        public static GameSession NewRun(GameConfig config, RelicCatalog catalog, long seed)
        {
            ValidateConfig(config);
            var state = new RunState { Seed = seed, Research = config.Economy.StartingResearch };
            var session = new GameSession(config, catalog ?? RelicCatalog.CreateDefault(), state, new SeededRandom((ulong)seed));
            session.Events.Publish(new RunStartedEvent());
            session.StartRound();
            return session;
        }

        /// <summary>Rebuilds a session from a saved <see cref="RunState"/>.</summary>
        public static GameSession Resume(GameConfig config, RelicCatalog catalog, RunState state)
        {
            ValidateConfig(config);
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Roster == null) state.Roster = new List<AthleteData>();
            if (state.Relics == null) state.Relics = new List<RelicInstance>();
            if (state.Shop == null) state.Shop = new ShopState();
            if (state.Stats == null) state.Stats = new RunStats();
            state.RoundIndex = Math.Max(0, Math.Min(state.RoundIndex, config.Competitions.Count - 1));

            var session = new GameSession(config, catalog ?? RelicCatalog.CreateDefault(), state,
                SeededRandom.FromState((ulong)state.RngState));
            session.RefreshAllScores();
            session.EnsureSelection();
            return session;
        }

        /// <summary>Returns the state with the RNG position written into it, ready to serialize.</summary>
        public RunState CaptureState()
        {
            State.RngState = (long)_rng.State;
            return State;
        }

        private static void ValidateConfig(GameConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            var errors = config.Validate();
            if (errors.Count > 0) throw new ArgumentException("Invalid GameConfig: " + string.Join(" ", errors.ToArray()));
        }

        // ==== Queries ==========================================================================

        public RunPhase Phase => State.Phase;
        public bool IsRunOver => State.Phase != RunPhase.InProgress;
        public IReadOnlyList<AthleteData> Roster => State.Roster;
        public AthleteData SelectedAthlete => GetAthlete(State.SelectedAthleteId);

        public int ActiveAthleteCount
        {
            get
            {
                int count = 0;
                foreach (var athlete in State.Roster) if (athlete.IsActive) count++;
                return count;
            }
        }

        public AthleteData GetAthlete(int athleteId)
        {
            foreach (var athlete in State.Roster)
                if (athlete.AthleteID == athleteId) return athlete;
            return null;
        }

        public OutcomeTable PreviewOutcomes(AthleteData athlete)
        {
            return Injection.BuildOutcomeTable(athlete);
        }

        public double PreviewDestroyChance(AthleteData athlete)
        {
            return athlete == null ? 0 : PreviewOutcomes(athlete)[InjectionOutcome.BodyDestroyed];
        }

        public int PreviewSacrificeResearch(AthleteData athlete)
        {
            return Research.PreviewSacrifice(athlete);
        }

        /// <summary>True when removing this athlete without scoring would end the run.</summary>
        public bool SacrificeWouldEndRun(AthleteData athlete)
        {
            return athlete != null && athlete.IsActive && !Competition.ThresholdMet && ActiveAthleteCount == 1;
        }

        /// <summary>True when submitting this athlete leaves no athletes while the requirement is still unmet.</summary>
        public bool SubmitWouldEndRun(AthleteData athlete)
        {
            return athlete != null && athlete.IsActive && ActiveAthleteCount == 1 &&
                   ScoreCalculator.SaturatingAdd(Competition.SubmittedScore, athlete.CurrentScore) < Competition.RequiredScore;
        }

        public string WhyCannotInject(AthleteData athlete)
        {
            return CommonAthleteCheck(athlete);
        }

        public string WhyCannotSubmit(AthleteData athlete)
        {
            return CommonAthleteCheck(athlete);
        }

        public string WhyCannotSacrifice(AthleteData athlete)
        {
            return CommonAthleteCheck(athlete);
        }

        public string WhyCannotAdvance()
        {
            if (IsRunOver) return "The run is over.";
            if (_resolving) return "Busy.";
            if (!Competition.ThresholdMet)
                return "Need " + Competition.RemainingScore.ToString("N0") + " more score.";
            return null;
        }

        public bool CanInject(AthleteData athlete) { return WhyCannotInject(athlete) == null; }
        public bool CanSubmit(AthleteData athlete) { return WhyCannotSubmit(athlete) == null; }
        public bool CanSacrifice(AthleteData athlete) { return WhyCannotSacrifice(athlete) == null; }
        public bool CanAdvance => WhyCannotAdvance() == null;
        public bool CanSelectNext => !IsRunOver && ActiveAthleteCount > 1;

        private string CommonAthleteCheck(AthleteData athlete)
        {
            if (IsRunOver) return "The run is over.";
            if (_resolving) return "Busy.";
            if (athlete == null) return "No athlete selected.";
            if (!athlete.IsAlive) return "This athlete has been destroyed.";
            if (athlete.IsSubmitted) return "Already submitted.";
            if (athlete.IsSacrificed) return "Already sacrificed.";
            if (!State.Roster.Contains(athlete)) return "Athlete is not on the roster.";
            return null;
        }

        // ==== Commands =========================================================================

        public ActionResult<InjectionResult> Inject(int athleteId)
        {
            var athlete = GetAthlete(athleteId);
            string error = WhyCannotInject(athlete);
            if (error != null) return ActionResult<InjectionResult>.Fail(error);

            InjectionResult result;
            _resolving = true;
            try
            {
                result = Injection.Resolve(athlete);
            }
            finally
            {
                _resolving = false;
            }

            if (result.AthleteDestroyed) AfterAthleteLeftRoster();
            NotifyStateChanged();
            return ActionResult<InjectionResult>.Ok(result);
        }

        public ActionResult<long> Submit(int athleteId)
        {
            var athlete = GetAthlete(athleteId);
            string error = WhyCannotSubmit(athlete);
            if (error != null) return ActionResult<long>.Fail(error);

            long score = athlete.CurrentScore;
            athlete.IsSubmitted = true;
            State.Roster.Remove(athlete);
            Competition.AddSubmission(score);
            State.Stats.Submitted++;
            if (score > State.Stats.BestSubmittedScore) State.Stats.BestSubmittedScore = score;

            Events.Publish(new AthleteSubmittedEvent { Athlete = athlete, Score = score });
            AfterAthleteLeftRoster();
            NotifyStateChanged();
            return ActionResult<long>.Ok(score);
        }

        public ActionResult<int> Sacrifice(int athleteId)
        {
            var athlete = GetAthlete(athleteId);
            string error = WhyCannotSacrifice(athlete);
            if (error != null) return ActionResult<int>.Fail(error);

            int research = Research.PreviewSacrifice(athlete);
            athlete.IsSacrificed = true;
            State.Roster.Remove(athlete);
            State.Stats.Sacrificed++;
            Research.Grant(research, "Sacrificed " + athlete.DisplayName);

            Events.Publish(new AthleteSacrificedEvent { Athlete = athlete, ResearchGained = research });
            AfterAthleteLeftRoster();
            NotifyStateChanged();
            return ActionResult<int>.Ok(research);
        }

        public ActionResult SelectAthlete(int athleteId)
        {
            var athlete = GetAthlete(athleteId);
            if (athlete == null || !athlete.IsActive) return ActionResult.Fail("Athlete is not available.");
            State.SelectedAthleteId = athleteId;
            NotifyStateChanged();
            return ActionResult.Ok();
        }

        public ActionResult SelectNextAthlete()
        {
            if (!CanSelectNext) return ActionResult.Fail("No other athlete to select.");
            int index = State.Roster.IndexOf(SelectedAthlete);
            for (int step = 1; step <= State.Roster.Count; step++)
            {
                var candidate = State.Roster[(index + step + State.Roster.Count) % State.Roster.Count];
                if (candidate.IsActive && candidate.AthleteID != State.SelectedAthleteId)
                    return SelectAthlete(candidate.AthleteID);
            }
            return ActionResult.Fail("No other athlete to select.");
        }

        public ActionResult ActivateRush()
        {
            if (_resolving || !Rush.Activate()) return ActionResult.Fail("ANABOLIC RUSH is not ready.");
            NotifyStateChanged();
            return ActionResult.Ok();
        }

        public ActionResult<RelicDefinition> BuyRelic(int choiceIndex)
        {
            if (_resolving) return ActionResult<RelicDefinition>.Fail("Busy.");
            var result = Shop.Buy(choiceIndex);
            if (result.Success) NotifyStateChanged();
            return result;
        }

        public ActionResult RestockShop()
        {
            if (_resolving) return ActionResult.Fail("Busy.");
            var result = Shop.Restock();
            if (result.Success) NotifyStateChanged();
            return result;
        }

        public ActionResult<CompetitionCompletedEvent> AdvanceRound()
        {
            string error = WhyCannotAdvance();
            if (error != null) return ActionResult<CompetitionCompletedEvent>.Fail(error);

            var completed = Competition.Complete();
            if (Competition.IsFinalRound)
            {
                EndRun(true, "Crowned " + completed.CompetitionName + ". The factory is legendary.");
                NotifyStateChanged();
                return ActionResult<CompetitionCompletedEvent>.Ok(completed);
            }

            State.RoundIndex++;
            State.SubmittedScore = 0;
            foreach (var athlete in State.Roster) athlete.InjectionsThisRound = 0;
            StartRound();
            NotifyStateChanged();
            return ActionResult<CompetitionCompletedEvent>.Ok(completed);
        }

        // ==== Rules shared with subsystems and relic effects ==================================

        /// <summary>Changes an athlete's MassLevel (never below zero), refreshes score and publishes the change.</summary>
        public void AddMassLevels(AthleteData athlete, int delta)
        {
            if (athlete == null || delta == 0) return;
            int oldLevel = athlete.MassLevel;
            int newLevel = Math.Max(0, oldLevel + delta);
            if (newLevel == oldLevel) return;

            athlete.MassLevel = newLevel;
            if (newLevel > athlete.PeakMassLevel) athlete.PeakMassLevel = newLevel;
            if (newLevel > State.Stats.MaxMassLevel) State.Stats.MaxMassLevel = newLevel;
            RefreshScore(athlete);
            Events.Publish(new MassLevelChangedEvent { Athlete = athlete, OldLevel = oldLevel, NewLevel = newLevel });
        }

        public void RefreshScore(AthleteData athlete)
        {
            athlete.CurrentScore = ScoreCalculator.Calculate(athlete.BaseScore, Config.Athletes.LevelMultiplier,
                athlete.MassLevel, athlete.PersonalScoreMultiplier, Relics.GlobalScoreMultiplier());
        }

        public void RefreshAllScores()
        {
            foreach (var athlete in State.Roster) RefreshScore(athlete);
        }

        /// <summary>Explicit extra recruit granted by a relic. Respects roster capacity; returns null when full.</summary>
        public AthleteData RecruitBonusAthlete()
        {
            var athlete = RecruitOne(true);
            if (athlete != null) EnsureSelection();
            return athlete;
        }

        // ==== Internals ========================================================================

        private AthleteData RecruitOne(bool isBonus)
        {
            if (IsRunOver || State.Roster.Count >= Config.Roster.RosterCapacity) return null;

            var athlete = new AthleteData
            {
                AthleteID = State.NextAthleteId++,
                DisplayName = AthleteNameGenerator.Generate(_rng),
                BaseScore = Config.Athletes.BaseScore,
                MassLevel = Math.Max(0, Config.Athletes.StartingMassLevel),
                RecruitedInRound = State.RoundIndex
            };
            athlete.PeakMassLevel = athlete.MassLevel;
            RefreshScore(athlete);
            State.Roster.Add(athlete);
            State.Stats.Recruited++;
            Events.Publish(new AthleteRecruitedEvent { Athlete = athlete, IsBonusRecruit = isBonus });
            return athlete;
        }

        private void StartRound()
        {
            int wanted = Relics.ModifyRecruitCount(Config.Roster.NewAthletesPerRound);
            int recruited = 0;
            for (int i = 0; i < wanted; i++)
                if (RecruitOne(false) != null) recruited++;

            Shop.StartRound();
            EnsureSelection();

            var competition = Competition.Current;
            Events.Publish(new RoundStartedEvent
            {
                RoundIndex = State.RoundIndex,
                CompetitionName = competition.Name,
                RequiredScore = competition.RequiredScore,
                Recruited = recruited,
                RecruitsLostToCapacity = wanted - recruited
            });
            CheckForGameOver();
        }

        private void AfterAthleteLeftRoster()
        {
            EnsureSelection();
            CheckForGameOver();
        }

        private void EnsureSelection()
        {
            var selected = SelectedAthlete;
            if (selected != null && selected.IsActive) return;
            State.SelectedAthleteId = -1;
            foreach (var athlete in State.Roster)
            {
                if (!athlete.IsActive) continue;
                State.SelectedAthleteId = athlete.AthleteID;
                return;
            }
        }

        private void CheckForGameOver()
        {
            if (IsRunOver || Competition.ThresholdMet || ActiveAthleteCount > 0) return;
            EndRun(false, "No athletes left to reach " + Competition.RequiredScore.ToString("N0") +
                          " at " + Competition.Current.Name + ".");
        }

        private void EndRun(bool victory, string reason)
        {
            if (IsRunOver) return;
            State.Phase = victory ? RunPhase.Victory : RunPhase.GameOver;
            State.EndReason = reason;
            Events.Publish(new RunEndedEvent { Victory = victory, Reason = reason });
        }

        private void NotifyStateChanged()
        {
            Events.Publish(new StateChangedEvent());
        }
    }
}
