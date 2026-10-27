using System;
using System.Collections.Generic;
using UnityEngine;

namespace MassFactory.Presentation
{
    /// <summary>
    /// Placeholder body made of primitives. Each part scales and moves with one muscle group plus the
    /// overall mass group, so growth reads as shape change rather than uniform scaling.
    /// </summary>
    public sealed class PrimitiveMuscleRig : MonoBehaviour, IMuscleRig
    {
        [Serializable]
        public sealed class Part
        {
            public Transform transform;
            public MuscleGroup group;
            public Vector3 baseScale;
            public Vector3 basePosition;
            public Vector3 scaleGrowth;
            public Vector3 positionGrowth;
            public float overallScaleGrowth;
            public bool isSkin = true;
            /// <summary>Extra growth applied only to mutants, to make them lopsided.</summary>
            public float mutantAsymmetry;
        }

        public List<Part> parts = new List<Part>();
        public List<Renderer> skinRenderers = new List<Renderer>();
        private readonly float[] _amounts = new float[MuscleProfile.GroupCount];
        private Material _skinMaterial;

        public void Register(Transform t, MuscleGroup group, Vector3 scaleGrowth, Vector3 positionGrowth,
            float overallScaleGrowth = 0.15f, bool isSkin = true, float mutantAsymmetry = 0f)
        {
            parts.Add(new Part
            {
                transform = t,
                group = group,
                baseScale = t.localScale,
                basePosition = t.localPosition,
                scaleGrowth = scaleGrowth,
                positionGrowth = positionGrowth,
                overallScaleGrowth = overallScaleGrowth,
                isSkin = isSkin,
                mutantAsymmetry = mutantAsymmetry
            });
            if (isSkin)
            {
                var renderer = t.GetComponent<Renderer>();
                if (renderer != null) skinRenderers.Add(renderer);
            }
        }

        public void Register(GameObject go, MuscleGroup group, Vector3 scaleGrowth, Vector3 positionGrowth,
            float overallScaleGrowth = 0.15f, bool isSkin = true, float mutantAsymmetry = 0f)
        {
            Register(go.transform, group, scaleGrowth, positionGrowth, overallScaleGrowth, isSkin, mutantAsymmetry);
        }

        public void SetSkinMaterial(Material material)
        {
            _skinMaterial = material;
            foreach (var renderer in skinRenderers) renderer.sharedMaterial = material;
        }

        public void SetMuscle(MuscleGroup group, float amount)
        {
            _amounts[(int)group] = amount;
        }

        public void SetSkinTint(Color color)
        {
            if (_skinMaterial != null) _skinMaterial.color = color;
        }

        private void LateUpdate()
        {
            float overall = _amounts[(int)MuscleGroup.OverallMuscleMass];
            float mutant = Mathf.Max(0, overall / 0.6f - 2.36f);
            foreach (var part in parts)
            {
                if (part.transform == null) continue;
                float amount = _amounts[(int)part.group] + mutant * part.mutantAsymmetry;
                var growth = Vector3.one + part.scaleGrowth * amount + Vector3.one * (part.overallScaleGrowth * overall);
                part.transform.localScale = Vector3.Scale(part.baseScale, growth);
                part.transform.localPosition = part.basePosition + part.positionGrowth * amount;
            }
        }
    }
}
