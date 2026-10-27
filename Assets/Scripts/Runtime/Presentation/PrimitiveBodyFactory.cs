using UnityEngine;

namespace MassFactory.Presentation
{
    /// <summary>Handles to the parts of a generated placeholder body.</summary>
    public sealed class PrimitiveBody
    {
        public GameObject Root;
        public PrimitiveMuscleRig Rig;
        public Transform LeftArmPivot;
        public Transform RightArmPivot;
        public Transform InjectionTarget;
        public Material SkinMaterial;
    }

    /// <summary>
    /// Builds a low-poly bodybuilder out of Unity primitives. The character faces -Z (towards the camera),
    /// feet at the local origin, about 1.85 units tall.
    /// </summary>
    public static class PrimitiveBodyFactory
    {
        public static PrimitiveBody Build(Transform parent, Color skin, Color shorts, Color hair)
        {
            var root = new GameObject("PlaceholderBody");
            root.transform.SetParent(parent, false);
            var rig = root.AddComponent<PrimitiveMuscleRig>();

            var skinMat = MaterialFactory.Create(skin, 0.45f);
            var shortsMat = MaterialFactory.Create(shorts, 0.2f);
            var hairMat = MaterialFactory.Shared(hair, 0.05f);
            var darkMat = MaterialFactory.Shared(new Color(0.05f, 0.05f, 0.05f));
            var shoeMat = MaterialFactory.Shared(new Color(0.12f, 0.12f, 0.14f));
            var t = root.transform;

            // Torso
            rig.Register(Part(PrimitiveType.Sphere, "Pelvis", t, new Vector3(0, 0.95f, 0), new Vector3(0.36f, 0.22f, 0.24f), shortsMat),
                MuscleGroup.Quadriceps, new Vector3(0.25f, 0.1f, 0.25f), Vector3.zero, 0.1f, false);
            rig.Register(Part(PrimitiveType.Capsule, "Abdomen", t, new Vector3(0, 1.15f, 0), new Vector3(0.3f, 0.2f, 0.2f), skinMat),
                MuscleGroup.OverallMuscleMass, new Vector3(0.1f, 0f, 0.15f), Vector3.zero, 0.1f);
            rig.Register(Part(PrimitiveType.Sphere, "Ribcage", t, new Vector3(0, 1.38f, 0), new Vector3(0.42f, 0.36f, 0.28f), skinMat),
                MuscleGroup.Back, new Vector3(0.35f, 0.12f, 0.25f), Vector3.zero, 0.1f);
            rig.Register(Part(PrimitiveType.Sphere, "Pec_L", t, new Vector3(-0.1f, 1.42f, -0.1f), new Vector3(0.2f, 0.15f, 0.12f), skinMat),
                MuscleGroup.Chest, new Vector3(0.45f, 0.4f, 0.6f), new Vector3(-0.04f, 0f, -0.05f), 0.1f);
            rig.Register(Part(PrimitiveType.Sphere, "Pec_R", t, new Vector3(0.1f, 1.42f, -0.1f), new Vector3(0.2f, 0.15f, 0.12f), skinMat),
                MuscleGroup.Chest, new Vector3(0.45f, 0.4f, 0.6f), new Vector3(0.04f, 0f, -0.05f), 0.1f);
            rig.Register(Part(PrimitiveType.Sphere, "Lat_L", t, new Vector3(-0.17f, 1.3f, 0.03f), new Vector3(0.13f, 0.32f, 0.2f), skinMat),
                MuscleGroup.Back, new Vector3(0.8f, 0.3f, 0.4f), new Vector3(-0.08f, 0f, 0.02f), 0.1f);
            rig.Register(Part(PrimitiveType.Sphere, "Lat_R", t, new Vector3(0.17f, 1.3f, 0.03f), new Vector3(0.13f, 0.32f, 0.2f), skinMat),
                MuscleGroup.Back, new Vector3(0.8f, 0.3f, 0.4f), new Vector3(0.08f, 0f, 0.02f), 0.1f);
            rig.Register(Part(PrimitiveType.Sphere, "Traps", t, new Vector3(0, 1.57f, 0.04f), new Vector3(0.3f, 0.12f, 0.16f), skinMat),
                MuscleGroup.Shoulders, new Vector3(0.4f, 0.9f, 0.4f), new Vector3(0f, 0.03f, 0f), 0.1f);
            rig.Register(Part(PrimitiveType.Cylinder, "Neck", t, new Vector3(0, 1.62f, 0), new Vector3(0.12f, 0.06f, 0.12f), skinMat),
                MuscleGroup.Shoulders, new Vector3(0.6f, 0f, 0.6f), Vector3.zero, 0.05f);

            // Head stays the same size, which is exactly why big levels look absurd.
            var head = Part(PrimitiveType.Sphere, "Head", t, new Vector3(0, 1.78f, 0), new Vector3(0.2f, 0.24f, 0.22f), skinMat);
            rig.Register(head, MuscleGroup.OverallMuscleMass, Vector3.zero, new Vector3(0f, 0.015f, 0f), 0f);
            Part(PrimitiveType.Sphere, "Hair", head.transform, new Vector3(0, 0.32f, 0.05f), new Vector3(1.05f, 0.5f, 1.0f), hairMat);
            Part(PrimitiveType.Sphere, "Eye_L", head.transform, new Vector3(-0.22f, 0.05f, -0.45f), new Vector3(0.14f, 0.12f, 0.1f), darkMat);
            Part(PrimitiveType.Sphere, "Eye_R", head.transform, new Vector3(0.22f, 0.05f, -0.45f), new Vector3(0.14f, 0.12f, 0.1f), darkMat);
            Part(PrimitiveType.Cube, "Brow", head.transform, new Vector3(0, 0.16f, -0.44f), new Vector3(0.7f, 0.08f, 0.1f), hairMat);

            // Shoulders
            rig.Register(Part(PrimitiveType.Sphere, "Delt_L", t, new Vector3(-0.25f, 1.5f, 0), new Vector3(0.17f, 0.16f, 0.17f), skinMat),
                MuscleGroup.Shoulders, new Vector3(0.7f, 0.6f, 0.7f), new Vector3(-0.1f, 0.02f, 0f), 0.1f);
            rig.Register(Part(PrimitiveType.Sphere, "Delt_R", t, new Vector3(0.25f, 1.5f, 0), new Vector3(0.17f, 0.16f, 0.17f), skinMat),
                MuscleGroup.Shoulders, new Vector3(0.7f, 0.6f, 0.7f), new Vector3(0.1f, 0.02f, 0f), 0.1f);

            var leftPivot = Arm(rig, t, -1, skinMat, out var leftBiceps);
            var rightPivot = Arm(rig, t, 1, skinMat, out _);

            // Legs
            for (int side = -1; side <= 1; side += 2)
            {
                string s = side < 0 ? "_L" : "_R";
                rig.Register(Part(PrimitiveType.Capsule, "Quad" + s, t, new Vector3(0.11f * side, 0.68f, -0.01f), new Vector3(0.17f, 0.24f, 0.17f), skinMat),
                    MuscleGroup.Quadriceps, new Vector3(0.7f, 0.12f, 0.7f), new Vector3(0.04f * side, 0f, 0f), 0.1f);
                rig.Register(Part(PrimitiveType.Capsule, "Calf" + s, t, new Vector3(0.11f * side, 0.3f, 0.02f), new Vector3(0.11f, 0.17f, 0.12f), skinMat),
                    MuscleGroup.Calves, new Vector3(0.6f, 0.1f, 0.7f), new Vector3(0.03f * side, 0f, 0f), 0.08f);
                rig.Register(Part(PrimitiveType.Cube, "Foot" + s, t, new Vector3(0.11f * side, 0.04f, -0.05f), new Vector3(0.1f, 0.08f, 0.24f), shoeMat),
                    MuscleGroup.Calves, new Vector3(0.15f, 0f, 0.05f), new Vector3(0.03f * side, 0f, 0f), 0f, false);
            }

            rig.SetSkinMaterial(skinMat);
            return new PrimitiveBody
            {
                Root = root,
                Rig = rig,
                LeftArmPivot = leftPivot,
                RightArmPivot = rightPivot,
                InjectionTarget = leftBiceps,
                SkinMaterial = skinMat
            };
        }

        private static Transform Arm(PrimitiveMuscleRig rig, Transform parent, int side, Material skinMat, out Transform biceps)
        {
            string s = side < 0 ? "_L" : "_R";
            var pivot = new GameObject("ArmPivot" + s).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = new Vector3(0.27f * side, 1.48f, 0f);
            // The pivot moves outward as the lats and delts grow so the arms never clip into the torso.
            rig.Register(pivot, MuscleGroup.Back, Vector3.zero, new Vector3(0.12f * side, 0f, 0f), 0f, false);

            // Left arm gets the mutant asymmetry: one absurd bicep at high levels.
            float asymmetry = side < 0 ? 0.5f : 0f;
            rig.Register(Part(PrimitiveType.Capsule, "UpperArm" + s, pivot, new Vector3(0, -0.18f, 0), new Vector3(0.1f, 0.17f, 0.1f), skinMat),
                MuscleGroup.OverallMuscleMass, new Vector3(0.5f, 0f, 0.5f), Vector3.zero, 0f);
            var bicepsPart = Part(PrimitiveType.Sphere, "Biceps" + s, pivot, new Vector3(0, -0.17f, -0.04f), new Vector3(0.11f, 0.18f, 0.11f), skinMat);
            rig.Register(bicepsPart, MuscleGroup.Biceps, new Vector3(0.8f, 0.35f, 0.8f), new Vector3(0f, 0f, -0.03f), 0.1f, true, asymmetry);
            rig.Register(Part(PrimitiveType.Sphere, "Triceps" + s, pivot, new Vector3(0, -0.15f, 0.04f), new Vector3(0.1f, 0.2f, 0.1f), skinMat),
                MuscleGroup.Triceps, new Vector3(0.7f, 0.3f, 0.7f), new Vector3(0f, 0f, 0.02f), 0.1f, true, asymmetry * 0.5f);
            rig.Register(Part(PrimitiveType.Capsule, "Forearm" + s, pivot, new Vector3(0, -0.45f, 0), new Vector3(0.09f, 0.14f, 0.09f), skinMat),
                MuscleGroup.Biceps, new Vector3(0.45f, 0.1f, 0.45f), new Vector3(0f, -0.03f, 0f), 0.08f);
            rig.Register(Part(PrimitiveType.Sphere, "Hand" + s, pivot, new Vector3(0, -0.62f, 0), new Vector3(0.08f, 0.09f, 0.06f), skinMat),
                MuscleGroup.Biceps, new Vector3(0.15f, 0.05f, 0.15f), new Vector3(0f, -0.04f, 0f), 0.05f);

            biceps = bicepsPart.transform;
            return pivot;
        }

        private static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            return MaterialFactory.Primitive(type, name, parent, position, scale, material);
        }
    }
}
