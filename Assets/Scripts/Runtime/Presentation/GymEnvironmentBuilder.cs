using UnityEngine;

namespace MassFactory.Presentation
{
    /// <summary>Procedurally builds the dark underground gym: concrete, fluorescent tubes, rusty equipment.</summary>
    public static class GymEnvironmentBuilder
    {
        public static Transform Build(Transform parent)
        {
            var root = new GameObject("Gym").transform;
            root.SetParent(parent, false);

            var concrete = MaterialFactory.Shared(new Color(0.23f, 0.23f, 0.22f), 0.05f);
            var darkConcrete = MaterialFactory.Shared(new Color(0.13f, 0.13f, 0.13f), 0.05f);
            var dirtyWall = MaterialFactory.Shared(new Color(0.28f, 0.29f, 0.26f), 0.02f);
            var rust = MaterialFactory.Shared(new Color(0.38f, 0.2f, 0.12f), 0.2f);
            var iron = MaterialFactory.Shared(new Color(0.12f, 0.12f, 0.13f), 0.5f);
            var hazardYellow = MaterialFactory.Shared(new Color(0.85f, 0.7f, 0.05f), 0.1f);
            var platformTop = MaterialFactory.Shared(new Color(0.3f, 0.05f, 0.05f), 0.3f);

            // Room shell. The floor keeps its collider so destruction chunks bounce on it.
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(root, false);
            floor.transform.localPosition = new Vector3(0, -0.05f, 2f);
            floor.transform.localScale = new Vector3(16f, 0.1f, 16f);
            floor.GetComponent<Renderer>().sharedMaterial = darkConcrete;

            Box(root, "BackWall", new Vector3(0, 3f, 6f), new Vector3(16f, 6.2f, 0.3f), dirtyWall);
            Box(root, "LeftWall", new Vector3(-7f, 3f, 2f), new Vector3(0.3f, 6.2f, 16f), dirtyWall);
            Box(root, "RightWall", new Vector3(7f, 3f, 2f), new Vector3(0.3f, 6.2f, 16f), dirtyWall);
            Box(root, "Ceiling", new Vector3(0, 5.2f, 2f), new Vector3(16f, 0.2f, 16f), concrete);
            for (int i = -3; i <= 3; i++)
                Box(root, "Pillar", new Vector3(i * 2.2f, 3f, 5.7f), new Vector3(0.35f, 6f, 0.35f), concrete);

            // Hazard stripes along the back wall.
            for (int i = -7; i <= 7; i++)
                Box(root, "Stripe", new Vector3(i * 1.0f, 0.25f, 5.83f), new Vector3(0.5f, 0.5f, 0.05f), i % 2 == 0 ? hazardYellow : iron,
                    Quaternion.Euler(0, 0, 35));

            // Posing platform.
            var platform = MaterialFactory.Primitive(PrimitiveType.Cylinder, "Platform", root, new Vector3(0, 0.1f, 0),
                new Vector3(1.6f, 0.1f, 1.6f), iron);
            MaterialFactory.Primitive(PrimitiveType.Cylinder, "PlatformTop", platform.transform, new Vector3(0, 1.01f, 0),
                new Vector3(0.92f, 0.02f, 0.92f), platformTop);

            // Equipment: dumbbell rack, bench, barrels of "supplements".
            var rack = new GameObject("DumbbellRack").transform;
            rack.SetParent(root, false);
            rack.localPosition = new Vector3(-3.6f, 0f, 2.6f);
            rack.localRotation = Quaternion.Euler(0, 25, 0);
            Box(rack, "RackFrame", new Vector3(0, 0.55f, 0), new Vector3(2.2f, 0.08f, 0.5f), iron);
            Box(rack, "RackLegL", new Vector3(-1f, 0.27f, 0), new Vector3(0.08f, 0.55f, 0.45f), iron);
            Box(rack, "RackLegR", new Vector3(1f, 0.27f, 0), new Vector3(0.08f, 0.55f, 0.45f), iron);
            for (int i = 0; i < 6; i++)
            {
                float x = -0.85f + i * 0.34f;
                float size = 0.11f + i * 0.015f;
                MaterialFactory.Primitive(PrimitiveType.Cylinder, "Handle", rack, new Vector3(x, 0.68f, 0), new Vector3(0.03f, 0.13f, 0.03f), iron,
                    Quaternion.Euler(90, 0, 0));
                MaterialFactory.Primitive(PrimitiveType.Sphere, "Weight", rack, new Vector3(x, 0.68f, -0.14f), Vector3.one * size, rust);
                MaterialFactory.Primitive(PrimitiveType.Sphere, "Weight", rack, new Vector3(x, 0.68f, 0.14f), Vector3.one * size, rust);
            }

            var bench = new GameObject("Bench").transform;
            bench.SetParent(root, false);
            bench.localPosition = new Vector3(3.4f, 0f, 2.2f);
            bench.localRotation = Quaternion.Euler(0, -30, 0);
            Box(bench, "Pad", new Vector3(0, 0.45f, 0), new Vector3(0.45f, 0.1f, 1.4f), MaterialFactory.Shared(new Color(0.25f, 0.03f, 0.03f)));
            Box(bench, "Leg", new Vector3(0, 0.2f, 0.55f), new Vector3(0.35f, 0.4f, 0.08f), iron);
            Box(bench, "Leg", new Vector3(0, 0.2f, -0.55f), new Vector3(0.35f, 0.4f, 0.08f), iron);
            MaterialFactory.Primitive(PrimitiveType.Cylinder, "Bar", bench, new Vector3(0, 1.05f, -0.4f), new Vector3(0.035f, 1.1f, 0.035f), iron,
                Quaternion.Euler(0, 0, 90));
            MaterialFactory.Primitive(PrimitiveType.Cylinder, "Plate", bench, new Vector3(-0.85f, 1.05f, -0.4f), new Vector3(0.45f, 0.04f, 0.45f), rust,
                Quaternion.Euler(0, 0, 90));
            MaterialFactory.Primitive(PrimitiveType.Cylinder, "Plate", bench, new Vector3(0.85f, 1.05f, -0.4f), new Vector3(0.45f, 0.04f, 0.45f), rust,
                Quaternion.Euler(0, 0, 90));

            var barrelGlow = MaterialFactory.Emissive(new Color(0.3f, 0.9f, 0.2f), 0.8f);
            Vector3[] barrels = { new Vector3(4.8f, 0.45f, 4.6f), new Vector3(5.6f, 0.45f, 4.1f), new Vector3(5.2f, 0.45f, 3.2f), new Vector3(-5.4f, 0.45f, 4.5f) };
            foreach (var position in barrels)
            {
                var barrel = MaterialFactory.Primitive(PrimitiveType.Cylinder, "Barrel", root, position, new Vector3(0.6f, 0.45f, 0.6f), rust);
                MaterialFactory.Primitive(PrimitiveType.Cylinder, "Ooze", barrel.transform, new Vector3(0, 1.0f, 0), new Vector3(0.85f, 0.02f, 0.85f), barrelGlow);
            }

            // Neon sign.
            var sign = new GameObject("Sign").transform;
            sign.SetParent(root, false);
            sign.localPosition = new Vector3(0, 3.9f, 5.8f);
            Box(sign, "SignBack", Vector3.zero, new Vector3(4.6f, 0.9f, 0.08f), iron);
            AddSignText(sign, "MASS FACTORY", new Color(1f, 0.15f, 0.1f));

            // Lighting: harsh fluorescent tubes plus a hard spot on the platform.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.12f, 0.13f, 0.12f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.05f, 0.06f, 0.05f);
            RenderSettings.fogStartDistance = 6f;
            RenderSettings.fogEndDistance = 16f;

            var tubeMat = MaterialFactory.Emissive(new Color(0.85f, 0.95f, 1f), 2.2f);
            Vector3[] tubes = { new Vector3(-2.6f, 4.9f, 1.2f), new Vector3(0f, 4.9f, -0.6f), new Vector3(2.6f, 4.9f, 1.2f), new Vector3(0f, 4.9f, 3.6f) };
            for (int i = 0; i < tubes.Length; i++)
            {
                var tube = MaterialFactory.Primitive(PrimitiveType.Cylinder, "Tube", root, tubes[i], new Vector3(0.08f, 0.8f, 0.08f), tubeMat,
                    Quaternion.Euler(0, 0, 90));
                var lightGo = new GameObject("TubeLight");
                lightGo.transform.SetParent(root, false);
                lightGo.transform.localPosition = tubes[i] + Vector3.down * 0.25f;
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 7.5f;
                light.intensity = 1.1f;
                light.color = new Color(0.82f, 0.95f, 1f);
                var flicker = lightGo.AddComponent<FluorescentFlicker>();
                flicker.Setup(light, tube.GetComponent<Renderer>(), i == 3 ? 0.35f : 0.03f, i * 17);
            }

            var spotGo = new GameObject("PlatformSpot");
            spotGo.transform.SetParent(root, false);
            spotGo.transform.localPosition = new Vector3(0.4f, 4.6f, -1.2f);
            spotGo.transform.LookAt(new Vector3(0, 1f, 0));
            var spot = spotGo.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.spotAngle = 42f;
            spot.range = 9f;
            spot.intensity = 2.4f;
            spot.color = new Color(1f, 0.94f, 0.85f);
            spot.shadows = LightShadows.Hard;

            return root;
        }

        private static void AddSignText(Transform parent, string text, Color color)
        {
            var go = new GameObject("SignText");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, 0, -0.06f);
            var mesh = go.AddComponent<TextMesh>();
            var font = LoadBuiltinFont();
            if (font == null)
            {
                Object.Destroy(go);
                return;
            }
            mesh.font = font;
            mesh.text = text;
            mesh.characterSize = 0.12f;
            mesh.fontSize = 64;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            go.AddComponent<FluorescentFlicker>().SetupText(mesh, 0.15f, 99);
        }

        public static Font LoadBuiltinFont()
        {
            // The built-in font was renamed in Unity 2022.2.
            Font font = null;
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (font == null)
            {
                try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
            }
            return font;
        }

        private static void Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material, Quaternion? rotation = null)
        {
            MaterialFactory.Primitive(PrimitiveType.Cube, name, parent, position, scale, material, rotation);
        }
    }
}
