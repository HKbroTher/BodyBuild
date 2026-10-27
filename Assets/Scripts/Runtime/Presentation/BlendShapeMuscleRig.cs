using System;
using System.Collections.Generic;
using UnityEngine;

namespace MassFactory.Presentation
{
    /// <summary>
    /// Drives a SkinnedMeshRenderer with blend shapes named after the muscle groups.
    /// Use it instead of the primitive rig once a rigged bodybuilder model is available.
    /// </summary>
    public sealed class BlendShapeMuscleRig : MonoBehaviour, IMuscleRig
    {
        [Serializable]
        public sealed class Mapping
        {
            public MuscleGroup group;
            public string blendShapeName;
            [Tooltip("Blend shape weight (0-100) reached at muscle amount 1.")]
            public float weightPerAmount = 40f;
        }

        public SkinnedMeshRenderer target;
        public List<Mapping> mappings = new List<Mapping>();
        public string skinColorProperty = "_Color";

        private readonly Dictionary<MuscleGroup, List<KeyValuePair<int, float>>> _indices =
            new Dictionary<MuscleGroup, List<KeyValuePair<int, float>>>();

        private void Awake()
        {
            if (target == null) target = GetComponentInChildren<SkinnedMeshRenderer>();
            if (target == null || target.sharedMesh == null) return;
            foreach (var mapping in mappings)
            {
                int index = target.sharedMesh.GetBlendShapeIndex(mapping.blendShapeName);
                if (index < 0) continue;
                if (!_indices.TryGetValue(mapping.group, out var list))
                {
                    list = new List<KeyValuePair<int, float>>();
                    _indices[mapping.group] = list;
                }
                list.Add(new KeyValuePair<int, float>(index, mapping.weightPerAmount));
            }
        }

        public void SetMuscle(MuscleGroup group, float amount)
        {
            if (target == null || !_indices.TryGetValue(group, out var list)) return;
            foreach (var pair in list) target.SetBlendShapeWeight(pair.Key, Mathf.Clamp(amount * pair.Value, 0f, 100f));
        }

        public void SetSkinTint(Color color)
        {
            if (target != null && target.material.HasProperty(skinColorProperty))
                target.material.SetColor(skinColorProperty, color);
        }
    }
}
