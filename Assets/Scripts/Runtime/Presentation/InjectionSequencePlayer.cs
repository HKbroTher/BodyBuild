using System.Collections;
using MassFactory.Core;
using UnityEngine;

namespace MassFactory.Presentation
{
    /// <summary>
    /// Presents an already-resolved injection: syringe, suspense, reveal, sound, score tween and muscle change.
    /// The outcome is decided by the core before this starts, so animation can never change the result.
    /// </summary>
    public sealed class InjectionSequencePlayer : MonoBehaviour
    {
        public static readonly Color MutationColor = new Color(0.8f, 0.35f, 1f);
        public static readonly Color GrowthColor = new Color(0.35f, 1f, 0.35f);
        public static readonly Color NoEffectColor = new Color(0.75f, 0.75f, 0.75f);
        public static readonly Color AtrophyColor = new Color(1f, 0.6f, 0.2f);
        public static readonly Color DestroyedColor = new Color(1f, 0.15f, 0.1f);
        public static readonly Color RushColor = new Color(1f, 0.85f, 0.1f);

        public AthleteVisualController Visual;
        public SyringeVisual Syringe;
        public CameraRig CameraRig;
        public ProceduralAudio Audio;
        public FeedbackChannel Feedback;

        public static Color ColorFor(InjectionOutcome outcome)
        {
            switch (outcome)
            {
                case InjectionOutcome.Mutation: return MutationColor;
                case InjectionOutcome.Growth: return GrowthColor;
                case InjectionOutcome.Atrophy: return AtrophyColor;
                case InjectionOutcome.BodyDestroyed: return DestroyedColor;
                default: return NoEffectColor;
            }
        }

        /// <param name="onReveal">Called at the reveal moment so the UI can start the score tween.</param>
        public IEnumerator Play(InjectionResult result, bool fast, System.Action onReveal)
        {
            float speed = fast ? 2.5f : 1f;
            var liquid = result.DuringRush ? RushColor : new Color(0.35f, 1f, 0.25f);

            Audio.Play(Sfx.Inject, 0.8f);
            yield return Syringe.Inject(Visual.InjectionPoint, liquid, speed);

            // Suspense grows with the risk the player just took.
            float suspense = fast ? 0.15f : 0.3f + (float)result.DestroyChance / 100f * 1.1f;
            CameraRig.SetZoom(0.6f);
            float t = 0, nextTick = 0;
            int ticks = 0;
            while (t < suspense)
            {
                t += Time.deltaTime;
                Visual.Shake = t / suspense;
                if (t >= nextTick)
                {
                    Audio.Play(Sfx.Tick, 0.6f, 1f + ticks * 0.12f);
                    ticks++;
                    nextTick += 0.16f;
                }
                yield return null;
            }
            Visual.Shake = 0;
            CameraRig.SetZoom(0f);

            Reveal(result);
            onReveal?.Invoke();

            if (result.AthleteDestroyed) yield return new WaitForSeconds(fast ? 0.5f : 1.1f);
            else yield return new WaitForSeconds(fast ? 0.1f : 0.35f);
        }

        private void Reveal(InjectionResult result)
        {
            var color = ColorFor(result.Outcome);
            switch (result.Outcome)
            {
                case InjectionOutcome.Mutation:
                    Visual.TargetLevel = result.LevelAfter;
                    Visual.Pump(1.6f);
                    CameraRig.Shake(0.08f, 0.5f);
                    Audio.Play(Sfx.Mutation);
                    Feedback.Flash(color, 0.35f);
                    Feedback.Float("MUTATION +" + result.LevelDelta, color, 84f, 1.8f);
                    break;
                case InjectionOutcome.Growth:
                    Visual.TargetLevel = result.LevelAfter;
                    Visual.Pump(0.7f);
                    Audio.Play(Sfx.Growth);
                    Feedback.Float("GROWTH +" + result.LevelDelta, color, 72f);
                    break;
                case InjectionOutcome.NoEffect:
                    Audio.Play(Sfx.NoEffect);
                    Feedback.Float("NO EFFECT", color, 60f);
                    break;
                case InjectionOutcome.Atrophy:
                    Visual.TargetLevel = result.LevelAfter;
                    Audio.Play(Sfx.Atrophy);
                    Feedback.Float(result.LevelDelta < 0 ? "ATROPHY " + result.LevelDelta : "ATROPHY (already minimum)", color, 64f);
                    break;
                case InjectionOutcome.BodyDestroyed:
                    Visual.Explode();
                    CameraRig.Shake(0.16f, 0.7f);
                    Audio.Play(Sfx.Destroyed);
                    Feedback.Flash(color, 0.6f);
                    Feedback.Float("BODY DESTROYED", color, 96f, 2.2f);
                    break;
            }

            if (!string.IsNullOrEmpty(result.TransformedBy))
                Feedback.Float(result.TransformedBy + ": " + result.RolledOutcome.ToLabel() + " -> " + result.Outcome.ToLabel(),
                    Color.white, 34f, 2.2f);
            if (result.RushEnded) Feedback.Write("ANABOLIC RUSH wore off.");
        }
    }
}
