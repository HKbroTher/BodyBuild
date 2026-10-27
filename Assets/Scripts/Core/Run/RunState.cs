using System;
using System.Collections.Generic;

namespace MassFactory.Core
{
    public enum RunPhase
    {
        InProgress = 0,
        Victory = 1,
        GameOver = 2
    }

    [Serializable]
    public class RelicInstance
    {
        public string RelicId;
        /// <summary>Generic per-relic counters (e.g. destroyed athletes counted by a relic).</summary>
        public int Counter;
        public int Counter2;
    }

    [Serializable]
    public class ShopState
    {
        public List<string> OfferIds = new List<string>();
        public bool Purchased;
        public int RestockCount;
    }

    [Serializable]
    public class RunStats
    {
        public int Recruited;
        public int Destroyed;
        public int Submitted;
        public int Sacrificed;
        public int Injections;
        public int Mutations;
        public int Growths;
        public int NoEffects;
        public int Atrophies;
        public int MaxMassLevel;
        public long BestSubmittedScore;
        public int ResearchEarned;
        public int RoundsCompleted;
        public int RushesActivated;
    }

    /// <summary>
    /// Complete, serializable state of a run. Contains no behaviour and no engine types,
    /// so it can be saved with Unity JsonUtility and restored into a new GameSession.
    /// </summary>
    [Serializable]
    public class RunState
    {
        public long Seed;
        public long RngState;
        public RunPhase Phase = RunPhase.InProgress;
        public string EndReason = "";

        public int RoundIndex;
        public long SubmittedScore;
        public int Research;

        public int NextAthleteId = 1;
        public int SelectedAthleteId = -1;
        public List<AthleteData> Roster = new List<AthleteData>();

        public List<RelicInstance> Relics = new List<RelicInstance>();
        public ShopState Shop = new ShopState();

        public int RushEnergy;
        public int RushRemaining;

        public RunStats Stats = new RunStats();
    }
}
