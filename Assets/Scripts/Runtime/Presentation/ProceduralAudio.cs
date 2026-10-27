using System.Collections.Generic;
using UnityEngine;

namespace MassFactory.Presentation
{
    public enum Sfx
    {
        Click,
        Inject,
        Tick,
        Growth,
        Mutation,
        NoEffect,
        Atrophy,
        Destroyed,
        Submit,
        Sacrifice,
        Rush,
        Fanfare,
        Fail,
        Hum
    }

    /// <summary>Synthesizes every sound effect at startup so the game needs no audio files.</summary>
    public sealed class ProceduralAudio : MonoBehaviour
    {
        private const int SampleRate = 44100;
        private readonly Dictionary<Sfx, AudioClip> _clips = new Dictionary<Sfx, AudioClip>();
        private AudioSource _oneShots;
        private AudioSource _ambience;
        private System.Random _noise;

        public float Volume = 0.7f;

        private void Awake()
        {
            _noise = new System.Random(1337);
            _oneShots = gameObject.AddComponent<AudioSource>();
            _oneShots.playOnAwake = false;
            _ambience = gameObject.AddComponent<AudioSource>();
            _ambience.loop = true;
            _ambience.volume = 0.12f;

            _clips[Sfx.Click] = Make("click", 0.05f, t => Square(t, 900) * Env(t, 0.05f, 0.001f));
            _clips[Sfx.Inject] = Make("inject", 0.35f, t => (Noise() * 0.3f + Sine(t, 180 + 400 * t)) * Env(t, 0.35f, 0.02f) * 0.6f);
            _clips[Sfx.Tick] = Make("tick", 0.12f, t => (Sine(t, 60) + Sine(t, 120) * 0.5f) * Env(t, 0.12f, 0.005f));
            _clips[Sfx.Growth] = Make("growth", 0.45f, t => (Square(t, 220 + 440 * t) * 0.4f + Sine(t, 440 + 600 * t) * 0.6f) * Env(t, 0.45f, 0.01f) * 0.6f);
            _clips[Sfx.Mutation] = Make("mutation", 1.1f, t => (Sine(t, 110) + Sine(t, 165) + Sine(t, 220 * (1 + t)) + Square(t, 55) * 0.4f) * 0.3f * Env(t, 1.1f, 0.02f));
            _clips[Sfx.NoEffect] = Make("noeffect", 0.3f, t => Sine(t, 200) * Env(t, 0.3f, 0.01f) * 0.5f);
            _clips[Sfx.Atrophy] = Make("atrophy", 0.6f, t => (Sine(t, 400 - 300 * t) + Square(t, 200 - 150 * t) * 0.3f) * Env(t, 0.6f, 0.01f) * 0.5f);
            _clips[Sfx.Destroyed] = Make("destroyed", 1.2f, t => (Noise() * 0.8f + Sine(t, 50 - 30 * t)) * Env(t, 1.2f, 0.002f) * 0.9f);
            _clips[Sfx.Submit] = Make("submit", 0.5f, t => (Sine(t, t < 0.12f ? 880 : 1320) + Sine(t, 1760) * 0.3f) * Env(t, 0.5f, 0.005f) * 0.5f);
            _clips[Sfx.Sacrifice] = Make("sacrifice", 0.8f, t => (Noise() * 0.5f * Mathf.Abs(Mathf.Sin(t * 60)) + Sine(t, 90)) * Env(t, 0.8f, 0.01f) * 0.6f);
            _clips[Sfx.Rush] = Make("rush", 1.0f, t => Square(t, 440 + 220 * Mathf.Sin(t * 30)) * Env(t, 1.0f, 0.02f) * 0.35f);
            _clips[Sfx.Fanfare] = Make("fanfare", 1.4f, t => (Square(t, Arp(t, 262, 330, 392, 523)) * 0.35f + Sine(t, Arp(t, 523, 659, 784, 1046)) * 0.4f) * Env(t, 1.4f, 0.01f));
            _clips[Sfx.Fail] = Make("fail", 1.4f, t => (Square(t, Arp(t, 392, 370, 349, 262)) * 0.35f) * Env(t, 1.4f, 0.01f));
            _clips[Sfx.Hum] = Make("hum", 2.0f, t => (Sine(t, 60) * 0.6f + Sine(t, 120) * 0.3f + Noise() * 0.05f) * 0.5f);

            _ambience.clip = _clips[Sfx.Hum];
            _ambience.Play();
        }

        public void Play(Sfx sfx, float volumeScale = 1f, float pitch = 1f)
        {
            if (!_clips.TryGetValue(sfx, out var clip)) return;
            _oneShots.pitch = pitch;
            _oneShots.PlayOneShot(clip, Volume * volumeScale);
        }

        private AudioClip Make(string name, float seconds, System.Func<float, float> wave)
        {
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[count];
            for (int i = 0; i < count; i++) data[i] = Mathf.Clamp(wave(i / (float)SampleRate), -1f, 1f);
            var clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Sine(float t, float freq) { return Mathf.Sin(2f * Mathf.PI * freq * t); }
        private static float Square(float t, float freq) { return Sine(t, freq) >= 0 ? 1f : -1f; }
        private float Noise() { return (float)(_noise.NextDouble() * 2.0 - 1.0); }

        /// <summary>Attack/decay envelope.</summary>
        private static float Env(float t, float length, float attack)
        {
            float a = attack <= 0 ? 1f : Mathf.Clamp01(t / attack);
            float d = Mathf.Clamp01(1f - t / length);
            return a * d * d;
        }

        private static float Arp(float t, float a, float b, float c, float d)
        {
            int step = Mathf.Clamp((int)(t / 0.18f), 0, 3);
            return step == 0 ? a : step == 1 ? b : step == 2 ? c : d;
        }
    }
}
