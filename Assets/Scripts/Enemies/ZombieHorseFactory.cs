using UnityEngine;
using UnityEngine.AI;
using ZombieHouse.Combat;
using ZombieHouse.Level;

namespace ZombieHouse.Enemies
{
    /// <summary>
    /// The zombie horse: the fastest thing in the game and the worst at corners.
    ///
    /// Assembled from the same named bones as the walker and the bear — front legs are
    /// "arms", rear legs are "legs" — so <see cref="ZombieRagdoll"/> and
    /// <see cref="ZombieDismemberment"/> work on it with no changes at all. Only the
    /// proportions and the gait differ, and ZombieVisuals handles the gait with its
    /// quadruped flag.
    ///
    /// Two things are meant to be visible before anything else: **bright pink eyes**,
    /// which is the only colour of that kind anywhere in the game, and **metal hooves**,
    /// which catch the torch beam and tell you what is coming down the street.
    /// </summary>
    public static class ZombieHorseFactory
    {
        private const float WitherHeight = 2.05f;   // at the shoulder
        private const float LegLength = 1.25f;
        private const float SpineY = 1.55f;

        public static GameObject Create(string name = "ZombieHorse")
        {
            var root = new GameObject(name);

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.8f;
            agent.height = WitherHeight;
            agent.baseOffset = 0f;
            agent.speed = 1f;
            agent.acceleration = 22f;       // it gets going alarmingly fast
            agent.angularSpeed = 120f;      // and cannot turn once it has
            agent.stoppingDistance = 2.4f;
            agent.autoBraking = true;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            root.AddComponent<ZombieProfile>();
            var health = root.AddComponent<ZombieHealth>();
            root.AddComponent<ZombieAI>();

            var bones = new ZombieBones();

            var rig = new GameObject("Rig").transform;
            rig.SetParent(root.transform, false);
            bones.Rig = rig;

            BuildBody(rig, health, bones);

            var rigHolder = root.AddComponent<ZombieRig>();
            rigHolder.Bones = bones;

            root.AddComponent<ZombieAppearance>();

            var visuals = root.AddComponent<ZombieVisuals>();
            visuals.SetQuadruped(true);

            root.AddComponent<ZombieRagdoll>();
            root.AddComponent<ZombieDismemberment>();
            root.AddComponent<ZombieAudio>();

            // Spotting you is now news the others hear. One zombie hunting alone is a small
            // problem; forty converging because one of them called is a horde, and the whole
            // difference is whether detection propagates.
            root.AddComponent<ZombieCallout>();

            return root;
        }

        private static void BuildBody(Transform rig, ZombieHealth health, ZombieBones bones)
        {
            // --- hindquarters -------------------------------------------------
            var pelvis = new GameObject("Pelvis").transform;
            pelvis.SetParent(rig, false);
            pelvis.localPosition = new Vector3(0f, SpineY, -0.85f);
            bones.Pelvis = pelvis;

            CreatePart(pelvis, "Hips", PrimitiveType.Capsule, Vector3.zero,
                new Vector3(0.58f, 0.52f, 0.58f), ProtoMaterials.HorseHide, health, 0.9f, false)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // A long tail of matted hair, which is most of what says "horse" from behind.
            for (int i = 0; i < 4; i++)
            {
                CreatePart(pelvis, "Tail_" + i, PrimitiveType.Capsule,
                    new Vector3(0f, -0.06f * i, -0.38f - 0.16f * i),
                    new Vector3(0.09f - i * 0.012f, 0.13f, 0.09f - i * 0.012f),
                    ProtoMaterials.HorseMane, null, 0f, false);
            }

            // --- barrel -------------------------------------------------------
            var spine = new GameObject("Spine").transform;
            spine.SetParent(rig, false);
            spine.localPosition = new Vector3(0f, SpineY, -0.1f);
            bones.Spine = spine;

            var chest = CreatePart(spine, "Chest", PrimitiveType.Capsule, new Vector3(0f, 0f, 0.3f),
                new Vector3(0.66f, 0.72f, 0.66f), ProtoMaterials.HorseHide, health, 1f, false);
            chest.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // Ribs showing through the hide, and the hole where something got in.
            CreatePart(spine, "Ribs", PrimitiveType.Sphere, new Vector3(0.26f, 0.02f, 0.2f),
                new Vector3(0.14f, 0.34f, 0.5f), ProtoMaterials.Skin, null, 0f, false);
            CreatePart(spine, "Wound", PrimitiveType.Sphere, new Vector3(-0.24f, 0.06f, 0.34f),
                new Vector3(0.2f, 0.22f, 0.16f), ProtoMaterials.Gore, null, 0f, false);

            // --- neck, carried low and forward like something stalking ---------
            var neck = new GameObject("Neck").transform;
            neck.SetParent(spine, false);
            neck.localPosition = new Vector3(0f, 0.18f, 0.78f);
            bones.Neck = neck;

            CreatePart(neck, "NeckMesh", PrimitiveType.Capsule, new Vector3(0f, 0.02f, 0.26f),
                new Vector3(0.32f, 0.34f, 0.32f), ProtoMaterials.HorseHide, null, 0f, false)
                .transform.localRotation = Quaternion.Euler(66f, 0f, 0f);

            for (int i = 0; i < 5; i++)
            {
                CreatePart(neck, "Mane_" + i, PrimitiveType.Capsule,
                    new Vector3(0f, 0.16f - i * 0.02f, 0.08f + i * 0.12f),
                    new Vector3(0.07f, 0.11f, 0.07f), ProtoMaterials.HorseMane, null, 0f, false);
            }

            // --- head ----------------------------------------------------------
            var head = new GameObject("Head").transform;
            head.SetParent(neck, false);
            head.localPosition = new Vector3(0f, 0.02f, 0.62f);
            bones.Head = head;

            CreatePart(head, "Skull", PrimitiveType.Capsule, Vector3.zero,
                new Vector3(0.26f, 0.34f, 0.26f), ProtoMaterials.HorseHide, health, 2.5f, true)
                .transform.localRotation = Quaternion.Euler(78f, 0f, 0f);

            CreatePart(head, "Muzzle", PrimitiveType.Cube, new Vector3(0f, -0.07f, 0.34f),
                new Vector3(0.19f, 0.17f, 0.28f), ProtoMaterials.HorseHide, null, 0f, false);
            CreatePart(head, "Teeth", PrimitiveType.Cube, new Vector3(0f, -0.13f, 0.42f),
                new Vector3(0.16f, 0.06f, 0.14f), ProtoMaterials.BearTooth, null, 0f, false);
            CreatePart(head, "Jaw", PrimitiveType.Cube, new Vector3(0f, -0.18f, 0.28f),
                new Vector3(0.16f, 0.08f, 0.22f), ProtoMaterials.Gore, null, 0f, false);

            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                CreatePart(head, i == 0 ? "EarL" : "EarR", PrimitiveType.Capsule,
                    new Vector3(0.09f * side, 0.22f, -0.06f),
                    new Vector3(0.05f, 0.09f, 0.05f), ProtoMaterials.HorseHide, null, 0f, false);
            }

            BuildEyes(head);

            // --- legs, named as arms and legs so the ragdoll chain matches ------
            bones.ShoulderLeft = BuildLeg(spine, health, -1f, true);
            bones.ShoulderRight = BuildLeg(spine, health, 1f, true);
            bones.ElbowLeft = bones.ShoulderLeft.Find("Elbow_L");
            bones.ElbowRight = bones.ShoulderRight.Find("Elbow_R");

            bones.HipLeft = BuildLeg(rig, health, -1f, false);
            bones.HipRight = BuildLeg(rig, health, 1f, false);
            bones.KneeLeft = bones.HipLeft.Find("Knee_L");
            bones.KneeRight = bones.HipRight.Find("Knee_R");
        }

        /// <summary>
        /// Bright pink, and lit from inside. Each eye carries its own small light, and the
        /// light is a child of the eyeball rather than of the head — ZombieRagdoll
        /// re-parents renderers when the body falls, not lights, so an eye light mounted
        /// on the head would hang in the air over the corpse.
        /// </summary>
        private static void BuildEyes(Transform head)
        {
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;

                GameObject eye = CreatePart(head, i == 0 ? "EyeL" : "EyeR", PrimitiveType.Sphere,
                    new Vector3(0.115f * side, 0.09f, 0.12f),
                    new Vector3(0.085f, 0.085f, 0.07f),
                    ProtoMaterials.HorseEye, null, 0f, false);

                var glowObject = new GameObject("EyeGlow");
                glowObject.transform.SetParent(eye.transform, false);

                var glow = glowObject.AddComponent<Light>();
                glow.type = LightType.Point;
                glow.color = new Color(1f, 0.22f, 0.72f);
                glow.range = 1.6f;
                glow.intensity = 2f;
                glow.shadows = LightShadows.None;
                glow.renderMode = LightRenderMode.Auto;
            }
        }

        /// <summary>
        /// One leg. Front and rear differ only in where they hang from and which way the
        /// middle joint folds, which is enough to read as a horse rather than a table.
        /// </summary>
        private static Transform BuildLeg(Transform parent, ZombieHealth health, float side, bool front)
        {
            string suffix = side < 0f ? "L" : "R";
            string upperName = front ? "Shoulder_" : "Hip_";
            string upperMesh = front ? "UpperArm_" : "Thigh_";
            string jointName = front ? "Elbow_" : "Knee_";
            string lowerMesh = front ? "Forearm_" : "Shin_";

            var upper = new GameObject(upperName + suffix).transform;
            upper.SetParent(parent, false);
            upper.localPosition = front
                ? new Vector3(0.3f * side, -0.28f, 0.52f)
                : new Vector3(0.3f * side, SpineY - 0.26f, -1.05f);

            CreatePart(upper, upperMesh + suffix, PrimitiveType.Capsule,
                new Vector3(0f, -LegLength * 0.24f, 0f),
                new Vector3(0.19f, LegLength * 0.26f, 0.19f),
                ProtoMaterials.HorseHide, health, 0.7f, false);

            var joint = new GameObject(jointName + suffix).transform;
            joint.SetParent(upper, false);
            joint.localPosition = new Vector3(0f, -LegLength * 0.5f, 0f);

            CreatePart(joint, lowerMesh + suffix, PrimitiveType.Capsule,
                new Vector3(0f, -LegLength * 0.24f, 0f),
                new Vector3(0.13f, LegLength * 0.26f, 0.13f),
                ProtoMaterials.HorseHide, health, 0.7f, false);

            // The hoof: shod, and the shoe is the part that shows.
            CreatePart(joint, "Fetlock_" + suffix, PrimitiveType.Capsule,
                new Vector3(0f, -LegLength * 0.46f, 0f),
                new Vector3(0.12f, 0.07f, 0.12f), ProtoMaterials.HorseHide, null, 0f, false);

            CreatePart(joint, "Hoof_" + suffix, PrimitiveType.Cylinder,
                new Vector3(0f, -LegLength * 0.54f, 0.01f),
                new Vector3(0.17f, 0.05f, 0.17f), ProtoMaterials.HorseHoof, null, 0f, false);

            return upper;
        }

        private static GameObject CreatePart(Transform parent, string name, PrimitiveType type,
                                             Vector3 localPosition, Vector3 localScale,
                                             Material material, ZombieHealth health,
                                             float hitMultiplier, bool critical)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;

            var collider = part.GetComponent<Collider>();

            if (health != null && hitMultiplier > 0f)
            {
                part.AddComponent<Hitbox>().Configure(health, hitMultiplier, critical);
            }
            else if (collider != null)
            {
                if (Application.isPlaying) Object.Destroy(collider);
                else Object.DestroyImmediate(collider);
            }

            return part;
        }
    }
}
