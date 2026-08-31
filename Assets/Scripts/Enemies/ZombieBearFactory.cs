using UnityEngine;
using UnityEngine.AI;
using ZombieHouse.Combat;
using ZombieHouse.Level;

namespace ZombieHouse.Enemies
{
    /// <summary>
    /// Builds the zombie bear: a quadruped, but deliberately assembled from the same
    /// named bones as the humanoid walker.
    ///
    /// That naming is the whole trick. ZombieRagdoll's bone chain and ZombieDismemberment's
    /// cut table both work off part names — Skull, Chest, Hips, Thigh, Shin, UpperArm,
    /// Forearm — so calling the front legs "arms" and the rear legs "legs" means the bear
    /// gets working ragdoll physics and limb severing for free, with no changes to either.
    /// Only the pose and the gait differ, and ZombieVisuals handles that with its
    /// quadruped flag.
    /// </summary>
    public static class ZombieBearFactory
    {
        private const float BodyHeight = 1.85f;   // at the shoulder
        private const float LegLength = 0.95f;
        private const float SpineY = 1.35f;

        /// <param name="fangMesh">
        /// The tooth spike, normally cached as an asset by the editor setup. Left null —
        /// a bear spawned purely at runtime — the teeth fall back to a built-in primitive,
        /// so the bear is never toothless just because nobody passed a mesh.
        /// </param>
        public static GameObject Create(string name = "ZombieBear", Mesh fangMesh = null)
        {
            var root = new GameObject(name);

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.85f;              // a bear needs room to path
            agent.height = BodyHeight;
            agent.baseOffset = 0f;
            agent.speed = 1f;
            agent.acceleration = 18f;
            agent.angularSpeed = 160f;
            agent.stoppingDistance = 2.2f;
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

            BuildBody(rig, health, bones, fangMesh);

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

        private static void BuildBody(Transform rig, ZombieHealth health, ZombieBones bones,
                                      Mesh fangMesh)
        {
            // --- hindquarters ("Pelvis") --------------------------------------
            var pelvis = new GameObject("Pelvis").transform;
            pelvis.SetParent(rig, false);
            pelvis.localPosition = new Vector3(0f, SpineY, -0.75f);
            bones.Pelvis = pelvis;

            CreatePart(pelvis, "Hips", PrimitiveType.Capsule, Vector3.zero,
                new Vector3(0.62f, 0.5f, 0.62f), ProtoMaterials.BearFur, health, 0.9f, false)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // --- barrel chest, running forward from the hips ------------------
            var spine = new GameObject("Spine").transform;
            spine.SetParent(rig, false);
            spine.localPosition = new Vector3(0f, SpineY, -0.1f);
            bones.Spine = spine;

            var chest = CreatePart(spine, "Chest", PrimitiveType.Capsule, new Vector3(0f, 0f, 0.35f),
                new Vector3(0.72f, 0.62f, 0.72f), ProtoMaterials.BearFur, health, 1f, false);
            chest.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            CreatePart(spine, "Hump", PrimitiveType.Sphere, new Vector3(0f, 0.28f, 0.1f),
                new Vector3(0.6f, 0.4f, 0.7f), ProtoMaterials.BearFur, null, 0f, false);

            CreatePart(spine, "Wound", PrimitiveType.Sphere, new Vector3(0.22f, 0.12f, 0.35f),
                new Vector3(0.22f, 0.16f, 0.14f), ProtoMaterials.Gore, null, 0f, false);

            // --- head, low and forward on a short neck ------------------------
            var neck = new GameObject("Neck").transform;
            neck.SetParent(spine, false);
            neck.localPosition = new Vector3(0f, -0.05f, 0.82f);
            bones.Neck = neck;

            var head = new GameObject("Head").transform;
            head.SetParent(neck, false);
            head.localPosition = new Vector3(0f, 0f, 0.28f);
            bones.Head = head;

            CreatePart(head, "Skull", PrimitiveType.Sphere, Vector3.zero,
                new Vector3(0.44f, 0.42f, 0.55f), ProtoMaterials.BearFur, health, 2.5f, true);
            CreatePart(head, "Snout", PrimitiveType.Cube, new Vector3(0f, -0.06f, 0.28f),
                new Vector3(0.26f, 0.2f, 0.32f), ProtoMaterials.BearFur, null, 0f, false);
            CreatePart(head, "Jaw", PrimitiveType.Cube, new Vector3(0f, -0.16f, 0.24f),
                new Vector3(0.22f, 0.1f, 0.28f), ProtoMaterials.Gore, null, 0f, false);
            CreatePart(head, "EarL", PrimitiveType.Sphere, new Vector3(-0.17f, 0.2f, -0.1f),
                new Vector3(0.16f, 0.16f, 0.08f), ProtoMaterials.BearFur, null, 0f, false);
            CreatePart(head, "EarR", PrimitiveType.Sphere, new Vector3(0.17f, 0.2f, -0.1f),
                new Vector3(0.16f, 0.16f, 0.08f), ProtoMaterials.BearFur, null, 0f, false);

            BuildEyes(head);
            BuildTeeth(head, fangMesh);

            // --- front legs, named as arms so the ragdoll chain matches --------
            bones.ShoulderLeft = BuildFrontLeg(spine, health, -1f);
            bones.ShoulderRight = BuildFrontLeg(spine, health, 1f);
            bones.ElbowLeft = bones.ShoulderLeft.Find("Elbow_L");
            bones.ElbowRight = bones.ShoulderRight.Find("Elbow_R");

            // --- rear legs ----------------------------------------------------
            bones.HipLeft = BuildRearLeg(rig, health, -1f);
            bones.HipRight = BuildRearLeg(rig, health, 1f);
            bones.KneeLeft = bones.HipLeft.Find("Knee_L");
            bones.KneeRight = bones.HipRight.Find("Knee_R");
        }

        /// <summary>
        /// Two red eyes, each with its own small light living inside the eyeball.
        ///
        /// The light is a child of the eye rather than of the head on purpose: ZombieRagdoll
        /// re-parents collider-free *renderers* onto the nearest bone when the body falls,
        /// so a light parented to the head would stay pinned in mid-air after death — the
        /// ghost bug again, wearing a different hat. Inside the eye it travels with it.
        /// </summary>
        private static void BuildEyes(Transform head)
        {
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;

                GameObject eye = CreatePart(head, i == 0 ? "EyeL" : "EyeR", PrimitiveType.Sphere,
                    new Vector3(0.115f * side, 0.075f, 0.175f),
                    new Vector3(0.085f, 0.075f, 0.07f),
                    ProtoMaterials.BearEye, null, 0f, false);

                var glowObject = new GameObject("EyeGlow");
                glowObject.transform.SetParent(eye.transform, false);

                var glow = glowObject.AddComponent<Light>();
                glow.type = LightType.Point;
                glow.color = new Color(1f, 0.09f, 0.05f);
                glow.range = 1.1f;
                glow.intensity = 1.4f;

                // Never shadow-casting: two per bear, and they exist to be seen rather
                // than to light anything. Auto lets Unity demote distant ones for free.
                glow.shadows = LightShadows.None;
                glow.renderMode = LightRenderMode.Auto;
            }
        }

        /// <summary>
        /// An upper and a lower row of spikes, with the canines longest. Every tooth is
        /// the same mesh at a different scale, so the whole mouth costs one mesh.
        /// </summary>
        private static void BuildTeeth(Transform head, Mesh fangMesh)
        {
            // Along the snout, front to back: the pair at the front are the canines.
            float[] offsets = { 0.035f, 0.085f, 0.135f };
            float[] lengths = { 0.115f, 0.075f, 0.055f };

            for (int i = 0; i < offsets.Length; i++)
            {
                for (int s = 0; s < 2; s++)
                {
                    float side = s == 0 ? -1f : 1f;
                    float x = (0.028f + i * 0.022f) * side;

                    // Upper teeth hang down from the snout, so the spike is turned over.
                    CreateTooth(head, $"ToothUpper_{i}_{s}", fangMesh,
                        new Vector3(x, -0.125f, 0.40f - offsets[i]),
                        lengths[i], Quaternion.Euler(196f, 0f, 0f));

                    // Lower teeth stand up out of the jaw, and are a little shorter.
                    CreateTooth(head, $"ToothLower_{i}_{s}", fangMesh,
                        new Vector3(x, -0.15f, 0.36f - offsets[i]),
                        lengths[i] * 0.78f, Quaternion.Euler(-14f, 0f, 0f));
                }
            }
        }

        private static void CreateTooth(Transform head, string name, Mesh fangMesh,
                                        Vector3 localPosition, float length, Quaternion rotation)
        {
            GameObject tooth;

            if (fangMesh != null)
            {
                tooth = new GameObject(name);
                tooth.transform.SetParent(head, false);
                tooth.AddComponent<MeshFilter>().sharedMesh = fangMesh;
                tooth.AddComponent<MeshRenderer>().sharedMaterial = ProtoMaterials.BearTooth;
            }
            else
            {
                // No generated mesh to hand: a stretched cube still reads as a tooth row.
                tooth = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tooth.name = name;
                tooth.transform.SetParent(head, false);
                tooth.GetComponent<MeshRenderer>().sharedMaterial = ProtoMaterials.BearTooth;

                Collider spare = tooth.GetComponent<Collider>();
                if (Application.isPlaying) Object.Destroy(spare);
                else Object.DestroyImmediate(spare);
            }

            tooth.transform.localPosition = localPosition;
            tooth.transform.localRotation = rotation;
            tooth.transform.localScale = new Vector3(0.032f, length, 0.032f);
        }

        private static Transform BuildFrontLeg(Transform spine, ZombieHealth health, float side)
        {
            string suffix = side < 0f ? "L" : "R";

            var shoulder = new GameObject("Shoulder_" + suffix).transform;
            shoulder.SetParent(spine, false);
            shoulder.localPosition = new Vector3(0.34f * side, -0.22f, 0.5f);

            CreatePart(shoulder, "UpperArm_" + suffix, PrimitiveType.Capsule,
                new Vector3(0f, -LegLength * 0.26f, 0f),
                new Vector3(0.24f, LegLength * 0.28f, 0.24f),
                ProtoMaterials.BearFur, health, 0.7f, false);

            var elbow = new GameObject("Elbow_" + suffix).transform;
            elbow.SetParent(shoulder, false);
            elbow.localPosition = new Vector3(0f, -LegLength * 0.52f, 0f);

            CreatePart(elbow, "Forearm_" + suffix, PrimitiveType.Capsule,
                new Vector3(0f, -LegLength * 0.24f, 0f),
                new Vector3(0.2f, LegLength * 0.26f, 0.2f),
                ProtoMaterials.BearFur, health, 0.7f, false);

            CreatePart(elbow, "Paw_" + suffix, PrimitiveType.Cube,
                new Vector3(0f, -LegLength * 0.5f, 0.06f),
                new Vector3(0.26f, 0.14f, 0.34f), ProtoMaterials.BearFur, null, 0f, false);

            return shoulder;
        }

        private static Transform BuildRearLeg(Transform rig, ZombieHealth health, float side)
        {
            string suffix = side < 0f ? "L" : "R";

            var hip = new GameObject("Hip_" + suffix).transform;
            hip.SetParent(rig, false);
            hip.localPosition = new Vector3(0.34f * side, SpineY - 0.2f, -0.95f);

            CreatePart(hip, "Thigh_" + suffix, PrimitiveType.Capsule,
                new Vector3(0f, -LegLength * 0.26f, 0f),
                new Vector3(0.3f, LegLength * 0.28f, 0.3f),
                ProtoMaterials.BearFur, health, 0.7f, false);

            var knee = new GameObject("Knee_" + suffix).transform;
            knee.SetParent(hip, false);
            knee.localPosition = new Vector3(0f, -LegLength * 0.52f, 0f);

            CreatePart(knee, "Shin_" + suffix, PrimitiveType.Capsule,
                new Vector3(0f, -LegLength * 0.22f, 0f),
                new Vector3(0.22f, LegLength * 0.24f, 0.22f),
                ProtoMaterials.BearFur, health, 0.7f, false);

            CreatePart(knee, "RearPaw_" + suffix, PrimitiveType.Cube,
                new Vector3(0f, -LegLength * 0.46f, 0.05f),
                new Vector3(0.28f, 0.14f, 0.34f), ProtoMaterials.BearFur, null, 0f, false);

            return hip;
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
