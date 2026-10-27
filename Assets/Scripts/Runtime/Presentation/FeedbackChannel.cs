using System.Collections.Generic;
using UnityEngine;

namespace MassFactory.Presentation
{
    /// <summary>Transient UI feedback produced by gameplay: floating text, log lines, banners and screen flashes.</summary>
    public sealed class FeedbackChannel
    {
        public sealed class FloatingText
        {
            public string Text;
            public Color Color;
            public float Size;
            public float Age;
            public float Lifetime;
            public float YOffset;
        }

        private const int MaxLogLines = 6;

        public readonly List<FloatingText> Floating = new List<FloatingText>();
        public readonly List<string> Log = new List<string>();

        public string Banner { get; private set; }
        public string BannerSub { get; private set; }
        public float BannerTime { get; private set; }
        public Color FlashColor { get; private set; }
        public float FlashAlpha { get; private set; }

        public void Float(string text, Color color, float size = 64f, float lifetime = 1.6f, float yOffset = 0f)
        {
            // Stack secondary messages below the main outcome so they never overlap.
            float stack = 0;
            foreach (var f in Floating) if (f.Age < 0.6f) stack += 46f;
            Floating.Add(new FloatingText { Text = text, Color = color, Size = size, Lifetime = lifetime, YOffset = yOffset + stack });
        }

        public void Write(string line)
        {
            Log.Add(line);
            while (Log.Count > MaxLogLines) Log.RemoveAt(0);
        }

        public void ShowBanner(string title, string subtitle, float seconds = 2.6f)
        {
            Banner = title;
            BannerSub = subtitle;
            BannerTime = seconds;
        }

        public void Flash(Color color, float alpha = 0.45f)
        {
            FlashColor = color;
            FlashAlpha = Mathf.Max(FlashAlpha, alpha);
        }

        public void Tick(float dt)
        {
            for (int i = Floating.Count - 1; i >= 0; i--)
            {
                Floating[i].Age += dt;
                if (Floating[i].Age >= Floating[i].Lifetime) Floating.RemoveAt(i);
            }
            BannerTime = Mathf.Max(0, BannerTime - dt);
            FlashAlpha = Mathf.MoveTowards(FlashAlpha, 0, dt * 1.4f);
        }

        public void Clear()
        {
            Floating.Clear();
            Log.Clear();
            BannerTime = 0;
            FlashAlpha = 0;
        }
    }
}
