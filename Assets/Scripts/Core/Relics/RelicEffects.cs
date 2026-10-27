using System;

namespace MassFactory.Core
{
    // Starter relic behaviours. Numbers come from RelicDefinition.Value / Value2 so they stay tunable.

    /// <summary>Value = percentage points moved into MUTATION.</summary>
    public sealed class GoldenSyringeEffect : RelicEffect
    {
        public override void ModifyOutcomeTable(RelicContext ctx, AthleteData athlete, OutcomeTable table)
        {
            table.Shift(InjectionOutcome.Mutation, ctx.Definition.Value,
                InjectionOutcome.Growth, InjectionOutcome.NoEffect, InjectionOutcome.Atrophy);
        }
    }

    /// <summary>First injection of each athlete each round cannot destroy them.</summary>
    public sealed class BlackMarketFridgeEffect : RelicEffect
    {
        public override int ProbabilityPriority => 100;

        public override void ModifyOutcomeTable(RelicContext ctx, AthleteData athlete, OutcomeTable table)
        {
            if (athlete.InjectionsThisRound == 0)
                table[InjectionOutcome.BodyDestroyed] = 0;
        }
    }

    /// <summary>Value = extra starting MassLevel for every recruit.</summary>
    public sealed class OlympiaCoachEffect : RelicEffect
    {
        public override void OnAthleteRecruited(RelicContext ctx, AthleteData athlete)
        {
            ctx.Session.AddMassLevels(athlete, (int)Math.Round(ctx.Definition.Value));
        }
    }

    /// <summary>Value = percent chance that ATROPHY becomes GROWTH.</summary>
    public sealed class MutantGeneticsEffect : RelicEffect
    {
        public override InjectionOutcome TransformOutcome(RelicContext ctx, AthleteData athlete, InjectionOutcome outcome)
        {
            if (outcome != InjectionOutcome.Atrophy) return outcome;
            if (ctx.Session.Rng.NextDouble() * 100.0 < ctx.Definition.Value)
            {
                ctx.Announce("ATROPHY -> GROWTH");
                return InjectionOutcome.Growth;
            }
            return outcome;
        }
    }

    /// <summary>Value = global score multiplier, Value2 = destroy percentage points added.</summary>
    public sealed class FakeMuscleOilEffect : RelicEffect
    {
        public override double ModifyGlobalScoreMultiplier(RelicContext ctx, double multiplier)
        {
            return multiplier * ctx.Definition.Value;
        }

        public override void ModifyOutcomeTable(RelicContext ctx, AthleteData athlete, OutcomeTable table)
        {
            table.Shift(InjectionOutcome.BodyDestroyed, ctx.Definition.Value2,
                InjectionOutcome.Growth, InjectionOutcome.Mutation, InjectionOutcome.NoEffect, InjectionOutcome.Atrophy);
        }
    }

    /// <summary>Counter = pending bonus levels. Value = levels per death, Value2 = max stored bonus.</summary>
    public sealed class UndergroundLaboratoryEffect : RelicEffect
    {
        public override void OnAthleteDestroyed(RelicContext ctx, AthleteData athlete)
        {
            int cap = Math.Max(1, (int)Math.Round(ctx.Definition.Value2));
            ctx.Instance.Counter = Math.Min(cap, ctx.Instance.Counter + (int)Math.Round(ctx.Definition.Value));
        }

        public override void OnAthleteRecruited(RelicContext ctx, AthleteData athlete)
        {
            if (ctx.Instance.Counter <= 0) return;
            int bonus = ctx.Instance.Counter;
            ctx.Instance.Counter = 0;
            ctx.Session.AddMassLevels(athlete, bonus);
            ctx.Announce("Recycled tissue: +" + bonus + " MASS");
        }
    }

    /// <summary>Counter = deaths since last bonus recruit. Value = deaths required.</summary>
    public sealed class HumanRecyclingProgramEffect : RelicEffect
    {
        public override void OnAthleteDestroyed(RelicContext ctx, AthleteData athlete)
        {
            int required = Math.Max(1, (int)Math.Round(ctx.Definition.Value));
            ctx.Instance.Counter++;
            if (ctx.Instance.Counter < required) return;
            ctx.Instance.Counter -= required;
            if (ctx.Session.RecruitBonusAthlete() != null)
                ctx.Announce("A new volunteer has been assembled");
        }
    }

    /// <summary>Value = research per Value2 points of excess score.</summary>
    public sealed class CorporateSponsorEffect : RelicEffect
    {
        public override void OnCompetitionCompleted(RelicContext ctx, CompetitionCompletedEvent evt)
        {
            if (evt.ExcessScore <= 0 || ctx.Definition.Value2 <= 0) return;
            int research = (int)Math.Floor(evt.ExcessScore / ctx.Definition.Value2 * ctx.Definition.Value);
            if (research <= 0) return;
            ctx.Session.Research.Grant(research, "Corporate Sponsor");
            ctx.Announce("Sponsor bonus +" + research + " research");
        }
    }

    /// <summary>Value = per-athlete score multiplier applied on every MUTATION.</summary>
    public sealed class ForbiddenFormulaEffect : RelicEffect
    {
        public override void AfterOutcomeResolved(RelicContext ctx, AthleteData athlete, InjectionResult result)
        {
            if (result.Outcome != InjectionOutcome.Mutation || !athlete.IsAlive) return;
            athlete.PersonalScoreMultiplier *= ctx.Definition.Value;
            ctx.Session.RefreshScore(athlete);
            result.ScoreAfter = athlete.CurrentScore;
            ctx.Announce("Forbidden Formula x" + ctx.Definition.Value.ToString("0.##"));
        }
    }

    /// <summary>Cancels one destruction, then removes itself.</summary>
    public sealed class SecondHeartEffect : RelicEffect
    {
        public override bool PreventsNextDestruction(RelicContext ctx, AthleteData athlete) { return true; }

        public override InjectionOutcome TransformOutcome(RelicContext ctx, AthleteData athlete, InjectionOutcome outcome)
        {
            if (outcome != InjectionOutcome.BodyDestroyed) return outcome;
            ctx.Session.Relics.Consume(ctx.Instance);
            ctx.Announce("SECOND HEART kicks in!");
            return InjectionOutcome.NoEffect;
        }
    }

    /// <summary>Value = price multiplier for relics and restocks.</summary>
    public sealed class CheapNeedlesEffect : RelicEffect
    {
        public override double ModifyCostMultiplier(RelicContext ctx, double multiplier)
        {
            return multiplier * ctx.Definition.Value;
        }
    }

    /// <summary>Value = points per consecutive ATROPHY stack, Value2 = max stacks.</summary>
    public sealed class NoPainNoGainEffect : RelicEffect
    {
        public override void ModifyOutcomeTable(RelicContext ctx, AthleteData athlete, OutcomeTable table)
        {
            int stacks = Math.Min(athlete.ConsecutiveAtrophy, Math.Max(0, (int)Math.Round(ctx.Definition.Value2)));
            if (stacks <= 0) return;

            double amount = stacks * ctx.Definition.Value;
            double growth = table[InjectionOutcome.Growth];
            double mutation = table[InjectionOutcome.Mutation];
            double mutationShare = growth + mutation > 0 ? mutation / (growth + mutation) : 0;

            table.Shift(InjectionOutcome.Mutation, amount * mutationShare, InjectionOutcome.Atrophy, InjectionOutcome.NoEffect);
            table.Shift(InjectionOutcome.Growth, amount * (1 - mutationShare), InjectionOutcome.Atrophy, InjectionOutcome.NoEffect);
        }
    }

    /// <summary>Value = rush energy threshold reduction.</summary>
    public sealed class AdrenalinePumpEffect : RelicEffect
    {
        public override int ModifyRushThreshold(RelicContext ctx, int threshold)
        {
            return threshold - (int)Math.Round(ctx.Definition.Value);
        }
    }

    /// <summary>Value = extra rush injections.</summary>
    public sealed class OverclockedAdrenalsEffect : RelicEffect
    {
        public override int ModifyRushDuration(RelicContext ctx, int duration)
        {
            return duration + (int)Math.Round(ctx.Definition.Value);
        }
    }

    public static class RelicEffectFactory
    {
        public static RelicEffect Create(RelicEffectType type)
        {
            switch (type)
            {
                case RelicEffectType.GoldenSyringe: return new GoldenSyringeEffect();
                case RelicEffectType.BlackMarketFridge: return new BlackMarketFridgeEffect();
                case RelicEffectType.OlympiaCoach: return new OlympiaCoachEffect();
                case RelicEffectType.MutantGenetics: return new MutantGeneticsEffect();
                case RelicEffectType.FakeMuscleOil: return new FakeMuscleOilEffect();
                case RelicEffectType.UndergroundLaboratory: return new UndergroundLaboratoryEffect();
                case RelicEffectType.HumanRecyclingProgram: return new HumanRecyclingProgramEffect();
                case RelicEffectType.CorporateSponsor: return new CorporateSponsorEffect();
                case RelicEffectType.ForbiddenFormula: return new ForbiddenFormulaEffect();
                case RelicEffectType.SecondHeart: return new SecondHeartEffect();
                case RelicEffectType.CheapNeedles: return new CheapNeedlesEffect();
                case RelicEffectType.NoPainNoGain: return new NoPainNoGainEffect();
                case RelicEffectType.AdrenalinePump: return new AdrenalinePumpEffect();
                case RelicEffectType.OverclockedAdrenals: return new OverclockedAdrenalsEffect();
                default: throw new ArgumentOutOfRangeException(nameof(type), type, "No effect registered for relic type.");
            }
        }
    }
}
