using System.Collections;
using UnityEngine;

namespace MassFactory.Presentation
{
    /// <summary>Oversized cartoon syringe that flies in, injects, and flies out.</summary>
    public sealed class SyringeVisual : MonoBehaviour
    {
        private Transform _plunger;
        private Transform _liquid;
        private Material _liquidMaterial;

        public static SyringeVisual Create(Transform parent)
        {
            var root = new GameObject("Syringe");
            root.transform.SetParent(parent, false);
            var syringe = root.AddComponent<SyringeVisual>();
            syringe.Build();
            root.SetActive(false);
            return syringe;
        }

        private void Build()
        {
            var t = transform;
            var glass = MaterialFactory.Shared(new Color(0.85f, 0.9f, 0.95f), 0.9f);
            var metal = MaterialFactory.Shared(new Color(0.6f, 0.6f, 0.65f), 0.8f);
            var rubber = MaterialFactory.Shared(new Color(0.15f, 0.15f, 0.15f));
            _liquidMaterial = MaterialFactory.Emissive(new Color(0.35f, 1f, 0.25f), 1.2f);

            // Built along +Y with the needle tip at the local origin, so "forward" is -Y.
            MaterialFactory.Primitive(PrimitiveType.Cylinder, "Needle", t, new Vector3(0, 0.09f, 0), new Vector3(0.012f, 0.09f, 0.012f), metal);
            MaterialFactory.Primitive(PrimitiveType.Cylinder, "Hub", t, new Vector3(0, 0.19f, 0), new Vector3(0.04f, 0.02f, 0.04f), metal);
            MaterialFactory.Primitive(PrimitiveType.Cylinder, "Barrel", t, new Vector3(0, 0.36f, 0), new Vector3(0.075f, 0.16f, 0.075f), glass);
            _liquid = MaterialFactory.Primitive(PrimitiveType.Cylinder, "Liquid", t, new Vector3(0, 0.34f, 0),
                new Vector3(0.065f, 0.13f, 0.065f), _liquidMaterial).transform;
            _plunger = new GameObject("Plunger").transform;
            _plunger.SetParent(t, false);
            _plunger.localPosition = new Vector3(0, 0.6f, 0);
            MaterialFactory.Primitive(PrimitiveType.Cylinder, "Rod", _plunger, new Vector3(0, 0f, 0), new Vector3(0.02f, 0.1f, 0.02f), metal);
            MaterialFactory.Primitive(PrimitiveType.Cylinder, "Thumb", _plunger, new Vector3(0, 0.1f, 0), new Vector3(0.09f, 0.01f, 0.09f), rubber);
            MaterialFactory.Primitive(PrimitiveType.Cube, "Flange", t, new Vector3(0, 0.52f, 0), new Vector3(0.2f, 0.015f, 0.05f), glass);
        }

        /// <summary>Full injection animation into <paramref name="target"/>. Duration scales with <paramref name="speed"/>.</summary>
        public IEnumerator Inject(Vector3 target, Color liquidColor, float speed)
        {
            gameObject.SetActive(true);
            _liquidMaterial.color = liquidColor;
            if (_liquidMaterial.HasProperty("_EmissionColor")) _liquidMaterial.SetColor("_EmissionColor", liquidColor * 1.2f);
            _liquid.localScale = new Vector3(0.065f, 0.13f, 0.065f);
            _liquid.localPosition = new Vector3(0, 0.34f, 0);
            _plunger.localPosition = new Vector3(0, 0.6f, 0);

            // Approach from the camera side, needle pointing into the arm.
            var direction = new Vector3(-0.55f, 0.35f, -0.75f).normalized;
            var rotation = Quaternion.FromToRotation(Vector3.up, direction);
            transform.rotation = rotation;
            var start = target + direction * 1.2f;
            var stop = target + direction * 0.02f;

            yield return Move(start, stop, 0.22f / speed);
            float t = 0, push = 0.3f / speed;
            while (t < push)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / push);
                _plunger.localPosition = new Vector3(0, Mathf.Lerp(0.6f, 0.36f, k), 0);
                _liquid.localScale = new Vector3(0.065f, Mathf.Lerp(0.13f, 0.005f, k), 0.065f);
                _liquid.localPosition = new Vector3(0, Mathf.Lerp(0.34f, 0.215f, k), 0);
                yield return null;
            }
            yield return Move(stop, start, 0.18f / speed);
            gameObject.SetActive(false);
        }

        private IEnumerator Move(Vector3 from, Vector3 to, float duration)
        {
            float t = 0;
            while (t < duration)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, t / duration));
                yield return null;
            }
            transform.position = to;
        }
    }
}
