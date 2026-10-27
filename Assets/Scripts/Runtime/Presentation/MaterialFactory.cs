using System.Collections.Generic;
using UnityEngine;

namespace MassFactory.Presentation
{
    /// <summary>
    /// Builds simple colored materials without shipping any material assets. Clones the built-in
    /// default material of a primitive so it works in the editor and in player builds.
    /// </summary>
    public static class MaterialFactory
    {
        private static Material _template;
        private static readonly Dictionary<int, Material> Cache = new Dictionary<int, Material>();

        private static Material Template
        {
            get
            {
                if (_template != null) return _template;
                var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _template = new Material(probe.GetComponent<Renderer>().sharedMaterial);
                Object.Destroy(probe);
                return _template;
            }
        }

        /// <summary>Shared material for a color. Do not modify the returned instance.</summary>
        public static Material Shared(Color color, float smoothness = 0.1f)
        {
            int key = (color.GetHashCode() * 397) ^ smoothness.GetHashCode();
            if (Cache.TryGetValue(key, out var material) && material != null) return material;
            material = Create(color, smoothness);
            Cache[key] = material;
            return material;
        }

        /// <summary>New material instance, safe to animate.</summary>
        public static Material Create(Color color, float smoothness = 0.1f)
        {
            var material = new Material(Template) { color = color };
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            return material;
        }

        public static Material Emissive(Color color, float intensity)
        {
            var material = Create(color, 0.2f);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * intensity);
            }
            return material;
        }

        /// <summary>Creates a primitive with a material and no collider.</summary>
        public static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPosition,
            Vector3 localScale, Material material, Quaternion? localRotation = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.transform.localRotation = localRotation ?? Quaternion.identity;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }
    }
}
