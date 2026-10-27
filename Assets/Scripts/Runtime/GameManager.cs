using System.Collections;
using MassFactory.Core;
using UnityEngine;

namespace MassFactory.Presentation
{
    /// <summary>
    /// Unity entry point. Owns the current <see cref="GameSession"/>, builds the placeholder 3D scene,
    /// turns UI requests into core commands and plays the matching presentation. Gameplay rules never live here.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        public enum AppScreen
        {
            Title,
            Playing
        }

        [Header("Configuration (optional, defaults are used when empty)")]
        public GameConfigAsset configAsset;
        public RelicCatalogAsset relicCatalogAsset;

        [Header("Run")]
        [Tooltip("0 = random seed every run.")]
        public long fixedSeed;
        public bool fastAnimations;
        public bool retroFilter = true;
        [Tooltip("Save the run to disk after every action so it can be resumed.")]
        public bool persistRuns = true;

        public GameSession Session { get; private set; }
        public AppScreen CurrentScreen { get; private set; } = AppScreen.Title;
        public FeedbackChannel Feedback { get; } = new FeedbackChannel();

        /// <summary>True while an animation is playing; all commands are ignored until it ends.</summary>
        public bool Busy { get; private set; }

        /// <summary>Athlete shown in the character panel while an animation is running (may already be destroyed).</summary>
        public AthleteData PresentedAthlete { get; private set; }
        public long DisplayedScore { get; private set; }
        public int DisplayedLevel { get; private set; }

        public bool HasSave => persistRuns && SaveSystem.HasSave;

        private GameConfig _config;
        private RelicCatalog _catalog;
        private AthleteVisualController _visual;
        private SyringeVisual _syringe;
        private CameraRig _cameraRig;
        private RetroRenderer _retro;
        private ProceduralAudio _audio;
        private InjectionSequencePlayer _sequence;
        private long _scoreFrom;
        private long _scoreTo;
        private float _scoreTween = 1f;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            _config = configAsset != null ? configAsset.BuildConfig() : GameConfig.CreateDefault();
            _catalog = relicCatalogAsset != null ? relicCatalogAsset.BuildCatalog() : RelicCatalog.CreateDefault();
            BuildScene();
            if (GetComponent<GameplayUIController>() == null) gameObject.AddComponent<GameplayUIController>();
        }

        private void BuildScene()
        {
            GymEnvironmentBuilder.Build(transform);

            var existing = Camera.main;
            if (existing != null) existing.gameObject.SetActive(false);
            var cameraGo = new GameObject("PresentationCamera");
            cameraGo.transform.SetParent(transform, false);
            cameraGo.tag = "MainCamera";
            var cam = cameraGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.03f, 0.03f);
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 40f;
            cameraGo.AddComponent<AudioListener>();
            _cameraRig = cameraGo.AddComponent<CameraRig>();
            _retro = cameraGo.AddComponent<RetroRenderer>();
            _retro.retroEnabled = retroFilter;

            var athleteGo = new GameObject("AthleteStage");
            athleteGo.transform.SetParent(transform, false);
            athleteGo.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            _visual = athleteGo.AddComponent<AthleteVisualController>();
            _syringe = SyringeVisual.Create(transform);

            _audio = gameObject.AddComponent<ProceduralAudio>();
            _sequence = gameObject.AddComponent<InjectionSequencePlayer>();
            _sequence.Visual = _visual;
            _sequence.Syringe = _syringe;
            _sequence.CameraRig = _cameraRig;
            _sequence.Audio = _audio;
            _sequence.Feedback = Feedback;
        }

        // ==== Run lifecycle ======================================================================

        public void NewRun()
        {
            if (Busy) return;
            long seed = fixedSeed != 0 ? fixedSeed : System.DateTime.UtcNow.Ticks & 0x7FFFFFFFFFFF;
            StartSession(GameSession.NewRun(_config, _catalog, seed));
            Feedback.ShowBanner("ROUND 1: " + Session.Competition.Current.Name.ToUpperInvariant(),
                "Reach " + Session.Competition.RequiredScore.ToString("N0") + " competition score");
            Persist();
        }

        public void ContinueRun()
        {
            if (Busy) return;
            var state = persistRuns ? SaveSystem.Load() : null;
            if (state == null)
            {
                NewRun();
                return;
            }
            StartSession(GameSession.Resume(_config, _catalog, state));
            Feedback.ShowBanner("WELCOME BACK", "Round " + Session.Competition.RoundNumber + ": " + Session.Competition.Current.Name);
        }

        public void ReturnToTitle()
        {
            if (Busy) return;
            Persist();
            CurrentScreen = AppScreen.Title;
            _visual.ClearBody();
        }

        public void SetRetro(bool enabled)
        {
            retroFilter = enabled;
            if (_retro != null) _retro.retroEnabled = enabled;
        }

        private void StartSession(GameSession session)
        {
            Session = session;
            Feedback.Clear();
            Subscribe(session.Events);
            CurrentScreen = AppScreen.Playing;
            _visual.ClearBody();
            PresentedAthlete = null;
            Busy = false;
        }

        private void Subscribe(GameEventBus bus)
        {
            bus.Subscribe<RelicTriggeredEvent>(e =>
            {
                Feedback.Write(e.Relic.DisplayName + ": " + e.Message);
                Feedback.Float(e.Message, new Color(1f, 0.85f, 0.4f), 30f, 1.8f, 120f);
            });
            bus.Subscribe<RoundStartedEvent>(e =>
            {
                Feedback.Write("Round " + (e.RoundIndex + 1) + ": " + e.Recruited + " new recruits" +
                               (e.RecruitsLostToCapacity > 0 ? " (" + e.RecruitsLostToCapacity + " turned away, roster full)" : ""));
            });
            bus.Subscribe<CompetitionCompletedEvent>(e =>
            {
                _audio.Play(Sfx.Fanfare);
                Feedback.Write("Won " + e.CompetitionName + " (" + e.SubmittedScore.ToString("N0") + " / " +
                               e.RequiredScore.ToString("N0") + "), +" + e.ResearchReward + " research");
            });
            bus.Subscribe<AthleteRecruitedEvent>(e =>
            {
                if (e.IsBonusRecruit) Feedback.Write("Bonus recruit: " + e.Athlete.DisplayName);
            });
            bus.Subscribe<RelicPurchasedEvent>(e => Feedback.Write("Installed " + e.Relic.DisplayName + " (-" + e.Cost + " research)"));
            bus.Subscribe<RelicConsumedEvent>(e => Feedback.Write(e.Relic.DisplayName + " was used up."));
            bus.Subscribe<RushActivatedEvent>(e =>
            {
                _audio.Play(Sfx.Rush);
                Feedback.Flash(InjectionSequencePlayer.RushColor, 0.3f);
                Feedback.Float("ANABOLIC RUSH!", InjectionSequencePlayer.RushColor, 80f, 1.6f);
                Feedback.Write("ANABOLIC RUSH: no destruction for " + e.Duration + " injections.");
            });
            bus.Subscribe<RunEndedEvent>(e =>
            {
                _audio.Play(e.Victory ? Sfx.Fanfare : Sfx.Fail);
                if (persistRuns) SaveSystem.Delete();
            });
            bus.Subscribe<StateChangedEvent>(e => Persist());
        }

        private void Persist()
        {
            if (persistRuns && Session != null && !Session.IsRunOver) SaveSystem.Save(Session.CaptureState());
        }

        // ==== Commands from the UI ================================================================

        public void Inject()
        {
            var athlete = Session?.SelectedAthlete;
            if (!CanAct() || athlete == null) return;
            var result = Session.Inject(athlete.AthleteID);
            if (!result.Success)
            {
                Feedback.Write(result.Error);
                return;
            }
            StartCoroutine(PlayInjection(athlete, result.Value));
        }

        public void Submit()
        {
            var athlete = Session?.SelectedAthlete;
            if (!CanAct() || athlete == null) return;
            long scoreBefore = athlete.CurrentScore;
            int level = athlete.MassLevel;
            bool wasMet = Session.Competition.ThresholdMet;
            var result = Session.Submit(athlete.AthleteID);
            if (!result.Success)
            {
                Feedback.Write(result.Error);
                return;
            }
            StartCoroutine(PlayExit(athlete, scoreBefore, level, true, () =>
            {
                _audio.Play(Sfx.Submit);
                Feedback.Float("+" + result.Value.ToString("N0") + " SUBMITTED", new Color(1f, 0.85f, 0.2f), 60f);
                Feedback.Write(athlete.DisplayName + " competed for " + result.Value.ToString("N0") + ".");
                if (!wasMet && Session.Competition.ThresholdMet)
                    Feedback.ShowBanner("REQUIREMENT MET", Session.Competition.IsFinalRound ? "Claim the crown when ready" : "Advance when ready");
            }));
        }

        public void Sacrifice()
        {
            var athlete = Session?.SelectedAthlete;
            if (!CanAct() || athlete == null) return;
            long scoreBefore = athlete.CurrentScore;
            int level = athlete.MassLevel;
            var result = Session.Sacrifice(athlete.AthleteID);
            if (!result.Success)
            {
                Feedback.Write(result.Error);
                return;
            }
            StartCoroutine(PlayExit(athlete, scoreBefore, level, false, () =>
            {
                _audio.Play(Sfx.Sacrifice);
                Feedback.Float("+" + result.Value + " RESEARCH", new Color(0.3f, 0.9f, 1f), 60f);
                Feedback.Write(athlete.DisplayName + " was donated to science (+" + result.Value + ").");
            }));
        }

        public void SelectAthlete(int athleteId)
        {
            if (!CanAct()) return;
            if (Session.SelectAthlete(athleteId).Success) _audio.Play(Sfx.Click, 0.5f);
        }

        public void SelectNext()
        {
            if (!CanAct()) return;
            if (Session.SelectNextAthlete().Success) _audio.Play(Sfx.Click, 0.5f);
        }

        public void ActivateRush()
        {
            if (!CanAct()) return;
            var result = Session.ActivateRush();
            if (!result.Success) Feedback.Write(result.Error);
        }

        public void BuyRelic(int choiceIndex)
        {
            if (!CanAct()) return;
            var result = Session.BuyRelic(choiceIndex);
            if (result.Success)
            {
                _audio.Play(Sfx.Submit, 0.7f, 1.3f);
                Feedback.Float(result.Value.DisplayName.ToUpperInvariant(), new Color(1f, 0.85f, 0.4f), 50f);
            }
            else Feedback.Write(result.Error);
        }

        public void RestockShop()
        {
            if (!CanAct()) return;
            var result = Session.RestockShop();
            if (result.Success) _audio.Play(Sfx.Click);
            else Feedback.Write(result.Error);
        }

        public void Advance()
        {
            if (!CanAct()) return;
            var result = Session.AdvanceRound();
            if (!result.Success)
            {
                Feedback.Write(result.Error);
                return;
            }
            if (Session.IsRunOver) return;
            var competition = Session.Competition;
            Feedback.ShowBanner("ROUND " + competition.RoundNumber + ": " + competition.Current.Name.ToUpperInvariant(),
                "Reach " + competition.RequiredScore.ToString("N0") + " competition score");
        }

        private bool CanAct()
        {
            return Session != null && !Busy && CurrentScreen == AppScreen.Playing;
        }

        // ==== Presentation coroutines ============================================================

        private IEnumerator PlayInjection(AthleteData athlete, InjectionResult result)
        {
            BeginBusy(athlete, result.ScoreBefore, result.LevelBefore);
            yield return _sequence.Play(result, fastAnimations, () =>
            {
                DisplayedLevel = result.LevelAfter;
                TweenScore(result.ScoreBefore, result.ScoreAfter);
            });
            EndBusy();
        }

        private IEnumerator PlayExit(AthleteData athlete, long score, int level, bool submitted, System.Action onStart)
        {
            BeginBusy(athlete, score, level);
            onStart();
            float duration = fastAnimations ? 0.3f : 0.7f;
            if (submitted)
            {
                _visual.Pump(0.4f);
                yield return new WaitForSeconds(fastAnimations ? 0.15f : 0.5f);
                yield return _visual.WalkOut(duration);
            }
            else
            {
                yield return _visual.SinkAway(duration);
            }
            EndBusy();
        }

        private IEnumerator SwapTo(AthleteData athlete)
        {
            BeginBusy(athlete, athlete.CurrentScore, athlete.MassLevel);
            float duration = fastAnimations ? 0.15f : 0.35f;
            if (_visual.HasBody) yield return _visual.WalkOut(duration, false);
            _visual.ShowAthlete(athlete);
            yield return _visual.WalkIn(duration + 0.1f);
            EndBusy();
        }

        private void BeginBusy(AthleteData athlete, long score, int level)
        {
            Busy = true;
            PresentedAthlete = athlete;
            DisplayedScore = score;
            DisplayedLevel = level;
            _scoreTween = 1f;
        }

        private void EndBusy()
        {
            Busy = false;
            PresentedAthlete = null;
        }

        private void TweenScore(long from, long to)
        {
            _scoreFrom = from;
            _scoreTo = to;
            _scoreTween = 0f;
        }

        private void Update()
        {
            Feedback.Tick(Time.deltaTime);

            if (_scoreTween < 1f)
            {
                _scoreTween = Mathf.Min(1f, _scoreTween + Time.deltaTime / 0.5f);
                double k = 1.0 - (1.0 - _scoreTween) * (1.0 - _scoreTween);
                DisplayedScore = _scoreFrom + (long)((_scoreTo - _scoreFrom) * k);
            }

            if (CurrentScreen != AppScreen.Playing || Session == null || Busy) return;

            // Keep the 3D athlete in sync with the selected athlete.
            var selected = Session.SelectedAthlete;
            if (selected == null)
            {
                if (_visual.HasBody) _visual.ClearBody();
            }
            else if (_visual.AthleteId != selected.AthleteID || !_visual.HasBody)
            {
                StartCoroutine(SwapTo(selected));
            }
            else
            {
                _visual.TargetLevel = selected.MassLevel;
            }
        }
    }
}
