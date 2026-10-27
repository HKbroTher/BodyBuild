using UnityEngine;

namespace MassFactory.Presentation
{
    /// <summary>Random fluorescent-tube flicker for a light, an emissive renderer or a neon TextMesh.</summary>
    public sealed class FluorescentFlicker : MonoBehaviour
    {
        private Light _light;
        private Renderer _renderer;
        private TextMesh _text;
        private Color _textColor;
        private float _baseIntensity;
        private float _chancePerSecond;
        private float _offTimer;
        private System.Random _random;

        public void Setup(Light light, Renderer tubeRenderer, float flickerChancePerSecond, int seed)
        {
            _light = light;
            _renderer = tubeRenderer;
            _baseIntensity = light.intensity;
            _chancePerSecond = flickerChancePerSecond;
            _random = new System.Random(seed);
        }

        public void SetupText(TextMesh text, float flickerChancePerSecond, int seed)
        {
            _text = text;
            _textColor = text.color;
            _chancePerSecond = flickerChancePerSecond;
            _random = new System.Random(seed);
        }

        private void Update()
        {
            if (_random == null) return;
            if (_offTimer > 0)
            {
                _offTimer -= Time.deltaTime;
                SetOn(_offTimer <= 0);
                return;
            }
            if (_random.NextDouble() < _chancePerSecond * Time.deltaTime)
            {
                _offTimer = 0.04f + (float)_random.NextDouble() * 0.18f;
                SetOn(false);
            }
        }

        private void SetOn(bool on)
        {
            if (_light != null) _light.intensity = on ? _baseIntensity : _baseIntensity * 0.15f;
            if (_renderer != null) _renderer.enabled = on;
            if (_text != null) _text.color = on ? _textColor : _textColor * 0.3f;
        }
    }
}
