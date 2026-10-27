using System.Collections;
using System.Collections.Generic;
using MassFactory.Core;
using UnityEngine;

namespace MassFactory.Presentation
{
    /// <summary>
    /// Shows the focused athlete on the posing platform: smooth muscle growth, idle breathing,
    /// flexing, suspense shaking, walk on/off and the stylized destruction effect.
    /// </summary>
    public sealed class AthleteVisualController : MonoBehaviour
    {
        private static readonly Color[] SkinTones =
        {
            new Color(0.96f, 0.80f, 0.69f), new Color(0.87f, 0.67f, 0.52f), new Color(0.72f, 0.52f, 0.38f),
            new Color(0.55f, 0.38f, 0.26f), new Color(0.40f, 0.27f, 0.19f), new Color(0.93f, 0.62f, 0.45f)
        };

        private static readonly Color[] ShortsColors =
        {
            new Color(0.8f, 0.1f, 0.1f), new Color(0.1f, 0.2f, 0.8f), new Color(0.95f, 0.75f, 0.05f),
            new Color(0.1f, 0.6f, 0.2f), new Color(0.6f, 0.1f, 0.6f), new Color(0.05f, 0.05f, 0.05f)
        };

        private static readonly Color[] HairColors =
        {
            new Color(0.08f, 0.06f, 0.05f), new Color(0.35f, 0.2f, 0.08f), new Color(0.85f, 0.7f, 0.3f), new Color(0.5f, 0.5f, 0.5f)
        };

        private readonly float[] _amounts = new float[MuscleProfile.GroupCount];
        private PrimitiveBody _body;
        private IMuscleRig _rig;
        private Color _baseSkin;
        private float _displayedLevel;
        private float _levelVelocity;
        private float _pump;
        private float _flexTimer;
        private float _shake;
        private Vector3 _offset;
        private bool _exploded;

        public int AthleteId { get; private set; } = -1;
        public int TargetLevel { get; set; }
        public bool HasBody => _body != null && !_exploded;

        /// <summary>World position where the syringe goes in.</summary>
        public Vector3 InjectionPoint => _body != null && _body.InjectionTarget != null
            ? _body.InjectionTarget.position
            : transform.position + Vector3.up * 1.3f;

        /// <summary>0..1 nervous shaking used during the suspense beat.</summary>
        public float Shake
        {
            get => _shake;
            set => _shake = Mathf.Clamp01(value);
        }

        public void ShowAthlete(AthleteData athlete)
        {
            ClearBody();
            AthleteId = athlete.AthleteID;
            int hash = Mathf.Abs(athlete.AthleteID * 7919 + 17);
            _baseSkin = SkinTones[hash % SkinTones.Length];
            _body = PrimitiveBodyFactory.Build(transform, _baseSkin, ShortsColors[(hash / 7) % ShortsColors.Length],
                HairColors[(hash / 13) % HairColors.Length]);
            _rig = _body.Rig;
            _exploded = false;
            TargetLevel = athlete.MassLevel;
            _displayedLevel = athlete.MassLevel;
            _levelVelocity = 0;
            _pump = 0;
            ApplyMuscles();
        }

        public void ClearBody()
        {
            if (_body != null && _body.Root != null) Destroy(_body.Root);
            _body = null;
            _rig = null;
            AthleteId = -1;
        }

        /// <summary>Brief over-pump on growth; bigger for mutations.</summary>
        public void Pump(float strength)
        {
            _pump = Mathf.Max(_pump, strength);
            _flexTimer = 1.1f;
        }

        public IEnumerator WalkIn(float duration)
        {
            yield return Slide(new Vector3(-3.5f, 0, 0), Vector3.zero, duration);
        }

        public IEnumerator WalkOut(float duration, bool toTheRight = true)
        {
            yield return Slide(Vector3.zero, new Vector3(toTheRight ? 3.5f : -3.5f, 0, 0), duration);
            ClearBody();
        }

        /// <summary>Sinks into the floor (sacrifice for research).</summary>
        public IEnumerator SinkAway(float duration)
        {
            yield return Slide(Vector3.zero, new Vector3(0, -2.2f, 0), duration);
            ClearBody();
        }

        private IEnumerator Slide(Vector3 from, Vector3 to, float duration)
        {
            float t = 0;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0, 1, t / duration);
                _offset = Vector3.Lerp(from, to, k);
                yield return null;
            }
            _offset = to;
            if (to != Vector3.zero) _offset = Vector3.zero;
        }

        /// <summary>Stylized destruction: the body bursts into glowing chunks that bounce and fade.</summary>
        public void Explode()
        {
            if (_body == null || _exploded) return;
            _exploded = true;
            var toxic = MaterialFactory.Emissive(new Color(0.55f, 0.95f, 0.2f), 1.5f);
            var chunks = new List<Transform>();
            foreach (var renderer in _body.Root.GetComponentsInChildren<Renderer>())
            {
                var chunk = renderer.transform;
                chunk.SetParent(null, true);
                if (Random.value < 0.5f) renderer.sharedMaterial = toxic;
                var collider = chunk.gameObject.AddComponent<SphereCollider>();
                collider.radius = 0.4f;
                var body = chunk.gameObject.AddComponent<Rigidbody>();
                body.mass = 0.3f;
                var center = transform.position + Vector3.up * 1.2f;
                body.AddExplosionForce(9f, center + Random.insideUnitSphere * 0.3f, 4f, 0.6f, ForceMode.Impulse);
                body.AddTorque(Random.insideUnitSphere * 5f, ForceMode.Impulse);
                chunks.Add(chunk);
            }
            var flash = MaterialFactory.Primitive(PrimitiveType.Sphere, "Burst", null, transform.position + Vector3.up * 1.2f,
                Vector3.one * 0.2f, MaterialFactory.Emissive(new Color(1f, 0.3f, 0.2f), 3f));
            StartCoroutine(Burst(flash.transform));
            StartCoroutine(FadeChunks(chunks, 2.4f));
            Destroy(_body.Root);
            _body = null;
            _rig = null;
        }

        private IEnumerator Burst(Transform flash)
        {
            float t = 0;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                flash.localScale = Vector3.one * Mathf.Lerp(0.2f, 2.6f, t / 0.35f);
                yield return null;
            }
            Destroy(flash.gameObject);
        }

        private static IEnumerator FadeChunks(List<Transform> chunks, float delay)
        {
            yield return new WaitForSeconds(delay);
            float t = 0;
            var scales = new List<Vector3>();
            foreach (var c in chunks) scales.Add(c != null ? c.localScale : Vector3.zero);
            while (t < 0.6f)
            {
                t += Time.deltaTime;
                for (int i = 0; i < chunks.Count; i++)
                    if (chunks[i] != null) chunks[i].localScale = Vector3.Lerp(scales[i], Vector3.zero, t / 0.6f);
                yield return null;
            }
            foreach (var c in chunks) if (c != null) Destroy(c.gameObject);
        }

        private void Update()
        {
            if (_body == null) return;

            _displayedLevel = Mathf.SmoothDamp(_displayedLevel, TargetLevel, ref _levelVelocity, 0.35f);
            _pump = Mathf.MoveTowards(_pump, 0, Time.deltaTime * 0.9f);
            _flexTimer = Mathf.Max(0, _flexTimer - Time.deltaTime);
            ApplyMuscles();

            float time = Time.time;
            var jitter = _shake > 0 ? Random.insideUnitSphere * 0.02f * _shake : Vector3.zero;
            _body.Root.transform.localPosition = _offset + new Vector3(jitter.x, Mathf.Abs(jitter.y), jitter.z);
            _body.Root.transform.localRotation = Quaternion.Euler(0, Mathf.Sin(time * 0.7f) * 4f, 0);

            // Arms hang wider as the lats grow; flexing raises them into a double-biceps pose.
            float spread = 8f + _amounts[(int)MuscleGroup.Back] * 9f;
            float flex = _flexTimer > 0 ? Mathf.Sin(Mathf.Clamp01(_flexTimer / 1.1f) * Mathf.PI) * 75f : 0f;
            _body.LeftArmPivot.localRotation = Quaternion.Euler(0, 0, -(spread + flex));
            _body.RightArmPivot.localRotation = Quaternion.Euler(0, 0, spread + flex);
        }

        private void ApplyMuscles()
        {
            if (_rig == null) return;
            float level = Mathf.Max(0, _displayedLevel + _pump);
            MuscleProfile.Evaluate(level, _amounts);
            float breath = Mathf.Sin(Time.time * 2.2f) * 0.04f;
            _amounts[(int)MuscleGroup.Chest] += breath;
            _amounts[(int)MuscleGroup.Back] += breath * 0.5f;
            for (int i = 0; i < MuscleProfile.GroupCount; i++) _rig.SetMuscle((MuscleGroup)i, _amounts[i]);
            _rig.SetSkinTint(MuscleProfile.SkinTint(_baseSkin, level));
        }
    }
}
