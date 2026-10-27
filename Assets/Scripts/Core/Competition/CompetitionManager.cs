namespace MassFactory.Core
{
    /// <summary>Round thresholds, submissions and the advance / fail rules.</summary>
    public sealed class CompetitionManager
    {
        private readonly GameSession _session;

        internal CompetitionManager(GameSession session)
        {
            _session = session;
        }

        public int RoundIndex => _session.State.RoundIndex;
        public int RoundNumber => _session.State.RoundIndex + 1;
        public int TotalRounds => _session.Config.Competitions.Count;
        public bool IsFinalRound => RoundIndex >= TotalRounds - 1;

        public CompetitionDefinition Current =>
            _session.Config.Competitions[System.Math.Min(RoundIndex, TotalRounds - 1)];

        public long RequiredScore => Current.RequiredScore;
        public long SubmittedScore => _session.State.SubmittedScore;
        public long RemainingScore => System.Math.Max(0, RequiredScore - SubmittedScore);
        public bool ThresholdMet => SubmittedScore >= RequiredScore;

        /// <summary>0..1 progress for the UI bar.</summary>
        public float Progress => RequiredScore <= 0 ? 1f : (float)System.Math.Min(1.0, (double)SubmittedScore / RequiredScore);

        public bool CanAdvance => _session.State.Phase == RunPhase.InProgress && ThresholdMet;

        internal void AddSubmission(long score)
        {
            _session.State.SubmittedScore = ScoreCalculator.SaturatingAdd(_session.State.SubmittedScore, score);
        }

        internal CompetitionCompletedEvent Complete()
        {
            var competition = Current;
            var evt = new CompetitionCompletedEvent
            {
                RoundIndex = RoundIndex,
                CompetitionName = competition.Name,
                RequiredScore = competition.RequiredScore,
                SubmittedScore = SubmittedScore,
                ExcessScore = System.Math.Max(0, SubmittedScore - competition.RequiredScore),
                ResearchReward = competition.ResearchReward
            };
            _session.Research.Grant(competition.ResearchReward, "Won " + competition.Name);
            _session.State.Stats.RoundsCompleted++;
            _session.Events.Publish(evt);
            return evt;
        }
    }
}
