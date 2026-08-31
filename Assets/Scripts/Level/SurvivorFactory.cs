using UnityEngine;

namespace ZombieHouse.Level
{
    /// <summary>
    /// Builds a survivor: someone crouched against the wall with a lantern, hands over
    /// their head, waiting for anything at all to happen.
    ///
    /// Built entirely at runtime by <see cref="LevelDirector"/> rather than saved as a
    /// prefab, so nothing here has to survive a scene save — the same reason the pickups
    /// are made this way.
    ///
    /// Two things matter more than the modelling. They must not read as a walker for even
    /// a moment, so they are lit, warm-coloured, upright-headed and completely still,
    /// where a zombie is dark, green, lurching and never still. And they carry no collider
    /// at all: you cannot shoot the person you came to rescue.
    /// </summary>
    public static class SurvivorFactory
    {
        public static GameObject Create(string name = "Survivor", bool bound = false)
        {
            var root = new GameObject(name);

            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);

            if (bound) BuildBound(body);
            else BuildHuddled(body);

            BuildLantern(root.transform, bound);

            var survivor = root.AddComponent<Survivor>();
            survivor.SetBound(bound);
            return root;
        }

        /// <summary>Hiding: crouched, knees up, back against something.</summary>
        private static void BuildHuddled(Transform body)
        {
            Part(body, "Torso", PrimitiveType.Capsule, new Vector3(0f, 0.52f, 0f),
                 new Vector3(0.34f, 0.30f, 0.34f), ProtoMaterials.SurvivorCoat,
                 Quaternion.Euler(18f, 0f, 0f));

            Part(body, "Head", PrimitiveType.Sphere, new Vector3(0f, 0.86f, 0.05f),
                 new Vector3(0.21f, 0.23f, 0.21f), ProtoMaterials.Skin, Quaternion.identity);

            Part(body, "Hair", PrimitiveType.Sphere, new Vector3(0f, 0.91f, 0.01f),
                 new Vector3(0.215f, 0.19f, 0.22f), ProtoMaterials.Hair, Quaternion.identity);

            // Knees drawn up in front, which is what makes the pose read as frightened
            // rather than as someone sitting down for a rest.
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;

                Part(body, "Thigh" + i, PrimitiveType.Capsule, new Vector3(0.11f * side, 0.30f, 0.16f),
                     new Vector3(0.15f, 0.20f, 0.15f), ProtoMaterials.Trousers,
                     Quaternion.Euler(72f, 0f, 0f));

                Part(body, "Shin" + i, PrimitiveType.Capsule, new Vector3(0.11f * side, 0.13f, 0.30f),
                     new Vector3(0.13f, 0.16f, 0.13f), ProtoMaterials.Trousers,
                     Quaternion.Euler(14f, 0f, 0f));

                Part(body, "Arm" + i, PrimitiveType.Capsule, new Vector3(0.20f * side, 0.52f, 0.16f),
                     new Vector3(0.11f, 0.19f, 0.11f), ProtoMaterials.SurvivorCoat,
                     Quaternion.Euler(52f, 0f, 18f * side));
            }
        }

        /// <summary>
        /// A hostage: on their feet, upright against the post, arms pulled behind and
        /// roped at the chest. The silhouette has to be different from the huddled pose
        /// at any distance, because in the town it is the thing you are looking for.
        /// </summary>
        private static void BuildBound(Transform body)
        {
            Part(body, "Torso", PrimitiveType.Capsule, new Vector3(0f, 1.02f, 0f),
                 new Vector3(0.34f, 0.36f, 0.30f), ProtoMaterials.SurvivorCoat, Quaternion.identity);

            Part(body, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.52f, 0.02f),
                 new Vector3(0.21f, 0.23f, 0.21f), ProtoMaterials.Skin,
                 Quaternion.Euler(14f, 0f, 0f));

            Part(body, "Hair", PrimitiveType.Sphere, new Vector3(0f, 1.58f, -0.02f),
                 new Vector3(0.215f, 0.19f, 0.22f), ProtoMaterials.Hair, Quaternion.identity);

            // The rope: two turns around the chest, and it is the give-away at a distance.
            for (int i = 0; i < 2; i++)
            {
                var rope = Part(body, "Rope" + i, PrimitiveType.Cylinder,
                    new Vector3(0f, 1.10f - i * 0.16f, -0.02f),
                    new Vector3(0.40f, 0.022f, 0.36f), ProtoMaterials.Linen, Quaternion.identity);
                rope.transform.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 3f : -3f);
            }

            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;

                // Arms behind the back rather than at the sides.
                Part(body, "Arm" + i, PrimitiveType.Capsule, new Vector3(0.19f * side, 1.02f, -0.13f),
                     new Vector3(0.10f, 0.24f, 0.10f), ProtoMaterials.SurvivorCoat,
                     Quaternion.Euler(-14f, 0f, 6f * side));

                Part(body, "Leg" + i, PrimitiveType.Capsule, new Vector3(0.11f * side, 0.44f, 0f),
                     new Vector3(0.14f, 0.44f, 0.14f), ProtoMaterials.Trousers, Quaternion.identity);

                Part(body, "Boot" + i, PrimitiveType.Cube, new Vector3(0.11f * side, 0.06f, 0.05f),
                     new Vector3(0.13f, 0.12f, 0.26f), ProtoMaterials.BootLeather, Quaternion.identity);
            }
        }

        /// <summary>
        /// The lantern is the gameplay, not the decoration: it is how you find them in a
        /// dark wood at all. Warm on purpose — every other light in the level is cold.
        /// </summary>
        private static void BuildLantern(Transform root, bool bound = false)
        {
            var lantern = new GameObject("Lantern").transform;
            lantern.SetParent(root, false);

            // Someone tied up is not holding anything: the lantern is on the ground at
            // their feet, left there by whoever put them there.
            lantern.localPosition = bound
                ? new Vector3(0.55f, 0.14f, 0.15f)
                : new Vector3(0.34f, 0.16f, -0.1f);

            Part(lantern, "Glass", PrimitiveType.Sphere, Vector3.zero,
                 new Vector3(0.13f, 0.17f, 0.13f), ProtoMaterials.LanternGlass, Quaternion.identity);

            Part(lantern, "Cap", PrimitiveType.Cylinder, new Vector3(0f, 0.10f, 0f),
                 new Vector3(0.08f, 0.02f, 0.08f), ProtoMaterials.Metal, Quaternion.identity);

            var lightObject = new GameObject("LanternLight");
            lightObject.transform.SetParent(lantern, false);

            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.76f, 0.42f);
            light.range = 7.5f;
            light.intensity = 1.5f;
            light.shadows = LightShadows.None;
        }

        private static GameObject Part(Transform parent, string name, PrimitiveType type,
                                       Vector3 localPosition, Vector3 localScale,
                                       Material material, Quaternion localRotation)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = localRotation;
            part.transform.localScale = localScale;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;

            // No colliders anywhere on a survivor: bullets, blades and blasts all pass
            // straight through, so there is no way to kill the person you came for.
            Collider collider = part.GetComponent<Collider>();
            if (collider == null) return part;

            if (Application.isPlaying) Object.Destroy(collider);
            else Object.DestroyImmediate(collider);

            return part;
        }
    }
}
