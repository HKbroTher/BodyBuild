using System;

namespace MassFactory.Core
{
    /// <summary>
    /// Runtime data of one bodybuilder. Plain public fields so the whole run can be
    /// serialized with Unity's JsonUtility for save/resume.
    /// </summary>
    [Serializable]
    public class AthleteData
    {
        public int AthleteID;
        public string DisplayName;
        public int MassLevel;
        public double BaseScore;
        public long CurrentScore;
        public int InjectionCount;
        public bool IsAlive = true;
        public bool IsSubmitted;
        public bool IsSacrificed;

        /// <summary>Per-athlete score multiplier (e.g. Forbidden Formula mutations).</summary>
        public double PersonalScoreMultiplier = 1.0;
        public int PeakMassLevel;
        public int MutationCount;
        public int ConsecutiveAtrophy;
        public int InjectionsThisRound;
        public int RecruitedInRound;

        /// <summary>Still in the roster and able to act.</summary>
        public bool IsActive => IsAlive && !IsSubmitted && !IsSacrificed;
    }
}
