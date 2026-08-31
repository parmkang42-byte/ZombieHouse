using UnityEngine;
using UnityEngine.AI;
using ZombieHouse.Combat;
using ZombieHouse.Level;

namespace ZombieHouse.Enemies
{
    /// <summary>
    /// The jungle's three: a snake, a jaguar and a monkey.
    ///
    /// All three are built on the same named bones as the bear, the horse and the scarab —
    /// front limbs are "arms", rear limbs are "legs" — so ZombieRagdoll and
    /// ZombieDismemberment work on every one of them unchanged. That convention has now
    /// paid for six creatures, and it is the single most valuable rule in this codebase.
    ///
    /// They are deliberately three different *problems* rather than three different skins:
    /// the snake is low and almost invisible until it strikes, the jaguar is fast and hits
    /// once very hard, and the monkeys are weak individually and arrive in numbers.
    /// </summary>
    public static class JungleFactory
    {
        // ---- the snake ---------------------------------------------------------

        /// <summary>
        /// A serpent: a chain of segments that keeps its head low and its body on the
        /// ground. The four "legs" are stubs hidden inside the body — the ragdoll needs
        /// them and nothing ever sees them.
        /// </summary>
        public static GameObject CreateSnake(string name = "Snake")
        {
            var root = new GameObject(name);
            var bones = SetUp(root, radius: 0.32f, height: 0.5f, acceleration: 20f,
                              angularSpeed: 300f, stopping: 1.2f);

            var health = root.GetComponent<ZombieHealth>();
            Transform rig = bones.Rig;

            var pelvis = MakeBone(rig, "Pelvis", new Vector3(0f, 0.18f, -1.1f));
            bones.Pelvis = pelvis;
            Part(pelvis, "Hips", PrimitiveType.Sphere, Vector3.zero,
                 new Vector3(0.26f, 0.22f, 0.5f), ProtoMaterials.SnakeScale, health, 0.8f, false);

            // The tail: five tapering segments behind the hips.
            for (int i = 0; i < 5; i++)
            {
                float t = i / 4f;
                Part(pelvis, "Tail_" + i, PrimitiveType.Sphere,
                     new Vector3(0f, 0f, -0.3f - i * 0.26f),
                     new Vector3(0.22f - t * 0.15f, 0.18f - t * 0.12f, 0.3f),
                     ProtoMaterials.SnakeScale, null, 0f, false);
            }

            var spine = MakeBone(rig, "Spine", new Vector3(0f, 0.2f, -0.3f));
            bones.Spine = spine;
            Part(spine, "Chest", PrimitiveType.Sphere, Vector3.zero,
                 new Vector3(0.32f, 0.26f, 0.7f), ProtoMaterials.SnakeScale, health, 1f, false);

            // Banding along the back, which is most of what reads as "snake" at a glance.
            for (int i = 0; i < 4; i++)
            {
                Part(spine, "Band_" + i, PrimitiveType.Sphere,
                     new Vector3(0f, 0.03f, -0.24f + i * 0.18f),
                     new Vector3(0.33f, 0.2f, 0.08f), ProtoMaterials.SnakeBand, null, 0f, false);
            }

            var neck = MakeBone(spine, "Neck", new Vector3(0f, 0.06f, 0.42f));
            bones.Neck = neck;
            Part(neck, "NeckMesh", PrimitiveType.Sphere, new Vector3(0f, 0f, 0.14f),
                 new Vector3(0.24f, 0.2f, 0.42f), ProtoMaterials.SnakeScale, null, 0f, false);

            var head = MakeBone(neck, "Head", new Vector3(0f, 0.02f, 0.42f));
            bones.Head = head;
            Part(head, "Skull", PrimitiveType.Sphere, Vector3.zero,
                 new Vector3(0.26f, 0.16f, 0.34f), ProtoMaterials.SnakeScale, health, 2.4f, true);
            Part(head, "Snout", PrimitiveType.Cube, new Vector3(0f, -0.02f, 0.18f),
                 new Vector3(0.16f, 0.09f, 0.14f), ProtoMaterials.SnakeScale, null, 0f, false);
            Part(head, "Jaw", PrimitiveType.Cube, new Vector3(0f, -0.07f, 0.12f),
                 new Vector3(0.15f, 0.05f, 0.2f), ProtoMaterials.Gore, null, 0f, false);

            for (int s = -1; s <= 1; s += 2)
            {
                Part(head, "Fang_" + s, PrimitiveType.Capsule, new Vector3(0.05f * s, -0.08f, 0.16f),
                     new Vector3(0.03f, 0.06f, 0.03f), ProtoMaterials.BearTooth, null, 0f, false);

                Part(head, "Eye_" + s, PrimitiveType.Sphere, new Vector3(0.09f * s, 0.05f, 0.06f),
                     new Vector3(0.06f, 0.055f, 0.05f), ProtoMaterials.SnakeEye, null, 0f, false);
            }

            // Vestigial limbs: the ragdoll walks the bone chain, so they have to exist.
            // Buried inside the body at a size nobody will ever pick out.
            bones.ShoulderLeft = MakeStub(spine, "Shoulder_L", "UpperArm_L", "Elbow_L", "Forearm_L", -1f, health);
            bones.ShoulderRight = MakeStub(spine, "Shoulder_R", "UpperArm_R", "Elbow_R", "Forearm_R", 1f, health);
            bones.ElbowLeft = bones.ShoulderLeft.Find("Elbow_L");
            bones.ElbowRight = bones.ShoulderRight.Find("Elbow_R");

            bones.HipLeft = MakeStub(pelvis, "Hip_L", "Thigh_L", "Knee_L", "Shin_L", -1f, health);
            bones.HipRight = MakeStub(pelvis, "Hip_R", "Thigh_R", "Knee_R", "Shin_R", 1f, health);
            bones.KneeLeft = bones.HipLeft.Find("Knee_L");
            bones.KneeRight = bones.HipRight.Find("Knee_R");

            GameObject snake = Finish(root, bones, quadruped: true);

            // No feet, so no footsteps. A soft tap following a snake around was the most
            // obviously wrong sound in the valley, and it came free with reusing the
            // quadruped rig — which is otherwise exactly what you want.
            snake.GetComponent<ZombieAudio>().SilenceFootsteps();
            return snake;
        }

        // ---- the jaguar ---------------------------------------------------------

        /// <summary>A big cat: long, low, and built entirely around one pounce.</summary>
        public static GameObject CreateJaguar(string name = "Jaguar")
        {
            var root = new GameObject(name);
            var bones = SetUp(root, radius: 0.55f, height: 1.1f, acceleration: 30f,
                              angularSpeed: 300f, stopping: 1.8f);

            var health = root.GetComponent<ZombieHealth>();
            Transform rig = bones.Rig;

            var pelvis = MakeBone(rig, "Pelvis", new Vector3(0f, 0.78f, -0.55f));
            bones.Pelvis = pelvis;
            Part(pelvis, "Hips", PrimitiveType.Capsule, Vector3.zero,
                 new Vector3(0.42f, 0.34f, 0.42f), ProtoMaterials.JaguarPelt, health, 0.9f, false)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            for (int i = 0; i < 5; i++)
            {
                Part(pelvis, "Tail_" + i, PrimitiveType.Capsule,
                     new Vector3(0f, 0.05f + i * 0.04f, -0.34f - i * 0.22f),
                     new Vector3(0.09f - i * 0.008f, 0.14f, 0.09f - i * 0.008f),
                     ProtoMaterials.JaguarPelt, null, 0f, false);
            }

            var spine = MakeBone(rig, "Spine", new Vector3(0f, 0.8f, 0.1f));
            bones.Spine = spine;
            Part(spine, "Chest", PrimitiveType.Capsule, new Vector3(0f, 0f, 0.2f),
                 new Vector3(0.48f, 0.42f, 0.48f), ProtoMaterials.JaguarPelt, health, 1f, false)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // Rosettes. Twelve dark patches is enough for the eye to call it a jaguar.
            for (int i = 0; i < 12; i++)
            {
                float angle = i * 1.9f;
                Part(spine, "Rosette_" + i, PrimitiveType.Sphere,
                     new Vector3(Mathf.Cos(angle) * 0.22f, Mathf.Sin(angle) * 0.16f,
                                 -0.2f + (i % 4) * 0.2f),
                     new Vector3(0.12f, 0.1f, 0.12f), ProtoMaterials.JaguarSpot, null, 0f, false);
            }

            var neck = MakeBone(spine, "Neck", new Vector3(0f, 0.08f, 0.5f));
            bones.Neck = neck;
            Part(neck, "NeckMesh", PrimitiveType.Capsule, new Vector3(0f, 0f, 0.12f),
                 new Vector3(0.28f, 0.2f, 0.28f), ProtoMaterials.JaguarPelt, null, 0f, false)
                .transform.localRotation = Quaternion.Euler(80f, 0f, 0f);

            var head = MakeBone(neck, "Head", new Vector3(0f, 0f, 0.34f));
            bones.Head = head;
            Part(head, "Skull", PrimitiveType.Sphere, Vector3.zero,
                 new Vector3(0.34f, 0.32f, 0.36f), ProtoMaterials.JaguarPelt, health, 2.5f, true);
            Part(head, "Muzzle", PrimitiveType.Cube, new Vector3(0f, -0.06f, 0.2f),
                 new Vector3(0.2f, 0.16f, 0.16f), ProtoMaterials.JaguarPelt, null, 0f, false);
            Part(head, "Jaw", PrimitiveType.Cube, new Vector3(0f, -0.13f, 0.16f),
                 new Vector3(0.18f, 0.07f, 0.16f), ProtoMaterials.Gore, null, 0f, false);

            for (int s = -1; s <= 1; s += 2)
            {
                Part(head, "Ear_" + s, PrimitiveType.Sphere, new Vector3(0.14f * s, 0.18f, -0.04f),
                     new Vector3(0.12f, 0.14f, 0.06f), ProtoMaterials.JaguarPelt, null, 0f, false);
                Part(head, "Eye_" + s, PrimitiveType.Sphere, new Vector3(0.11f * s, 0.07f, 0.15f),
                     new Vector3(0.07f, 0.065f, 0.05f), ProtoMaterials.JaguarEye, null, 0f, false);
                Part(head, "Fang_" + s, PrimitiveType.Capsule, new Vector3(0.06f * s, -0.14f, 0.2f),
                     new Vector3(0.035f, 0.06f, 0.035f), ProtoMaterials.BearTooth, null, 0f, false);
            }

            bones.ShoulderLeft = BuildCatLeg(spine, health, -1f, true, 0.62f);
            bones.ShoulderRight = BuildCatLeg(spine, health, 1f, true, 0.62f);
            bones.ElbowLeft = bones.ShoulderLeft.Find("Elbow_L");
            bones.ElbowRight = bones.ShoulderRight.Find("Elbow_R");

            bones.HipLeft = BuildCatLeg(rig, health, -1f, false, 0.62f);
            bones.HipRight = BuildCatLeg(rig, health, 1f, false, 0.62f);
            bones.KneeLeft = bones.HipLeft.Find("Knee_L");
            bones.KneeRight = bones.HipRight.Find("Knee_R");

            return Finish(root, bones, quadruped: true);
        }

        // ---- the monkey ----------------------------------------------------------

        /// <summary>
        /// A howler the size of a large dog, and the only jungle creature that stands.
        /// Upright, long-armed and quick, which is why it uses the humanoid gait rather
        /// than the quadruped one.
        /// </summary>
        public static GameObject CreateMonkey(string name = "Monkey")
        {
            var root = new GameObject(name);
            var bones = SetUp(root, radius: 0.32f, height: 1.1f, acceleration: 24f,
                              angularSpeed: 400f, stopping: 1.1f);

            var health = root.GetComponent<ZombieHealth>();
            Transform rig = bones.Rig;

            var pelvis = MakeBone(rig, "Pelvis", new Vector3(0f, 0.52f, 0f));
            bones.Pelvis = pelvis;
            Part(pelvis, "Hips", PrimitiveType.Capsule, Vector3.zero,
                 new Vector3(0.22f, 0.14f, 0.2f), ProtoMaterials.MonkeyFur, health, 0.9f, false);

            for (int i = 0; i < 6; i++)
            {
                float t = i / 5f;
                var seg = Part(pelvis, "Tail_" + i, PrimitiveType.Capsule,
                    new Vector3(0f, -0.05f - i * 0.02f, -0.16f - i * 0.14f),
                    new Vector3(0.05f - t * 0.02f, 0.09f, 0.05f - t * 0.02f),
                    ProtoMaterials.MonkeyFur, null, 0f, false);

                seg.transform.localRotation = Quaternion.Euler(62f + i * 5f, 0f, 0f);
            }

            var spine = MakeBone(rig, "Spine", new Vector3(0f, 0.62f, 0f));
            bones.Spine = spine;
            Part(spine, "Chest", PrimitiveType.Capsule, new Vector3(0f, 0.12f, 0f),
                 new Vector3(0.26f, 0.18f, 0.22f), ProtoMaterials.MonkeyFur, health, 1f, false);

            var neck = MakeBone(spine, "Neck", new Vector3(0f, 0.3f, 0f));
            bones.Neck = neck;

            var head = MakeBone(neck, "Head", new Vector3(0f, 0.08f, 0.02f));
            bones.Head = head;
            Part(head, "Skull", PrimitiveType.Sphere, Vector3.zero,
                 new Vector3(0.2f, 0.2f, 0.2f), ProtoMaterials.MonkeyFur, health, 2.5f, true);
            Part(head, "Face", PrimitiveType.Sphere, new Vector3(0f, -0.02f, 0.09f),
                 new Vector3(0.14f, 0.13f, 0.09f), ProtoMaterials.MonkeyFace, null, 0f, false);
            Part(head, "Jaw", PrimitiveType.Cube, new Vector3(0f, -0.08f, 0.09f),
                 new Vector3(0.11f, 0.05f, 0.09f), ProtoMaterials.Gore, null, 0f, false);

            for (int s = -1; s <= 1; s += 2)
            {
                Part(head, "Ear_" + s, PrimitiveType.Sphere, new Vector3(0.11f * s, 0.02f, -0.01f),
                     new Vector3(0.07f, 0.09f, 0.04f), ProtoMaterials.MonkeyFace, null, 0f, false);
                Part(head, "Eye_" + s, PrimitiveType.Sphere, new Vector3(0.05f * s, 0.02f, 0.13f),
                     new Vector3(0.045f, 0.045f, 0.03f), ProtoMaterials.JaguarEye, null, 0f, false);
                Part(head, "Fang_" + s, PrimitiveType.Capsule, new Vector3(0.04f * s, -0.09f, 0.12f),
                     new Vector3(0.022f, 0.035f, 0.022f), ProtoMaterials.BearTooth, null, 0f, false);
            }

            // Long arms, short legs: the proportions are the whole silhouette.
            bones.ShoulderLeft = BuildApeArm(spine, health, -1f);
            bones.ShoulderRight = BuildApeArm(spine, health, 1f);
            bones.ElbowLeft = bones.ShoulderLeft.Find("Elbow_L");
            bones.ElbowRight = bones.ShoulderRight.Find("Elbow_R");

            bones.HipLeft = BuildApeLeg(pelvis, health, -1f);
            bones.HipRight = BuildApeLeg(pelvis, health, 1f);
            bones.KneeLeft = bones.HipLeft.Find("Knee_L");
            bones.KneeRight = bones.HipRight.Find("Knee_R");

            return Finish(root, bones, quadruped: false);
        }

        // ---- shared construction --------------------------------------------------

        private static ZombieBones SetUp(GameObject root, float radius, float height,
                                         float acceleration, float angularSpeed, float stopping)
        {
            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = radius;
            agent.height = height;
            agent.baseOffset = 0f;
            agent.speed = 1f;
            agent.acceleration = acceleration;
            agent.angularSpeed = angularSpeed;
            agent.stoppingDistance = stopping;
            agent.autoBraking = true;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            root.AddComponent<ZombieProfile>();
            root.AddComponent<ZombieHealth>();
            root.AddComponent<ZombieAI>();

            var bones = new ZombieBones();
            var rig = new GameObject("Rig").transform;
            rig.SetParent(root.transform, false);
            bones.Rig = rig;

            return bones;
        }

        private static GameObject Finish(GameObject root, ZombieBones bones, bool quadruped)
        {
            root.AddComponent<ZombieRig>().Bones = bones;
            root.AddComponent<ZombieAppearance>();
            root.AddComponent<ZombieVisuals>().SetQuadruped(quadruped);
            root.AddComponent<ZombieRagdoll>();
            root.AddComponent<ZombieDismemberment>();
            root.AddComponent<ZombieAudio>();

            // Spotting you is now news the others hear. One zombie hunting alone is a small
            // problem; forty converging because one of them called is a horde, and the whole
            // difference is whether detection propagates.
            root.AddComponent<ZombieCallout>();
            return root;
        }

        private static Transform MakeBone(Transform parent, string name, Vector3 localPosition)
        {
            var bone = new GameObject(name).transform;
            bone.SetParent(parent, false);
            bone.localPosition = localPosition;
            return bone;
        }

        /// <summary>A limb the ragdoll needs and nobody sees — used for the snake.</summary>
        private static Transform MakeStub(Transform parent, string upperName, string upperMesh,
                                          string jointName, string lowerMesh, float side,
                                          ZombieHealth health)
        {
            var upper = MakeBone(parent, upperName, new Vector3(0.06f * side, -0.02f, 0f));
            Part(upper, upperMesh, PrimitiveType.Sphere, Vector3.zero,
                 new Vector3(0.06f, 0.06f, 0.06f), ProtoMaterials.SnakeScale, health, 0.5f, false);

            var joint = MakeBone(upper, jointName, new Vector3(0.02f * side, -0.02f, 0f));
            Part(joint, lowerMesh, PrimitiveType.Sphere, Vector3.zero,
                 new Vector3(0.05f, 0.05f, 0.05f), ProtoMaterials.SnakeScale, health, 0.5f, false);

            return upper;
        }

        private static Transform BuildCatLeg(Transform parent, ZombieHealth health,
                                             float side, bool front, float length)
        {
            string suffix = side < 0f ? "L" : "R";
            string upperName = front ? "Shoulder_" : "Hip_";
            string upperMesh = front ? "UpperArm_" : "Thigh_";
            string jointName = front ? "Elbow_" : "Knee_";
            string lowerMesh = front ? "Forearm_" : "Shin_";

            var upper = MakeBone(parent, upperName + suffix, front
                ? new Vector3(0.24f * side, -0.1f, 0.3f)
                : new Vector3(0.24f * side, 0.7f, -0.62f));

            Part(upper, upperMesh + suffix, PrimitiveType.Capsule,
                 new Vector3(0f, -length * 0.24f, 0f),
                 new Vector3(0.15f, length * 0.26f, 0.15f),
                 ProtoMaterials.JaguarPelt, health, 0.7f, false);

            var joint = MakeBone(upper, jointName + suffix, new Vector3(0f, -length * 0.5f, 0f));

            Part(joint, lowerMesh + suffix, PrimitiveType.Capsule,
                 new Vector3(0f, -length * 0.22f, 0f),
                 new Vector3(0.12f, length * 0.24f, 0.12f),
                 ProtoMaterials.JaguarPelt, health, 0.7f, false);

            Part(joint, "Paw_" + suffix, PrimitiveType.Sphere,
                 new Vector3(0f, -length * 0.46f, 0.04f),
                 new Vector3(0.17f, 0.11f, 0.2f), ProtoMaterials.JaguarPelt, null, 0f, false);

            return upper;
        }

        private static Transform BuildApeArm(Transform spine, ZombieHealth health, float side)
        {
            string suffix = side < 0f ? "L" : "R";

            var shoulder = MakeBone(spine, "Shoulder_" + suffix, new Vector3(0.2f * side, 0.2f, 0f));
            Part(shoulder, "UpperArm_" + suffix, PrimitiveType.Capsule, new Vector3(0f, -0.14f, 0f),
                 new Vector3(0.09f, 0.16f, 0.09f), ProtoMaterials.MonkeyFur, health, 0.6f, false);

            var elbow = MakeBone(shoulder, "Elbow_" + suffix, new Vector3(0f, -0.3f, 0f));
            Part(elbow, "Forearm_" + suffix, PrimitiveType.Capsule, new Vector3(0f, -0.14f, 0f),
                 new Vector3(0.08f, 0.16f, 0.08f), ProtoMaterials.MonkeyFur, health, 0.6f, false);
            Part(elbow, "Hand_" + suffix, PrimitiveType.Cube, new Vector3(0f, -0.32f, 0.02f),
                 new Vector3(0.09f, 0.12f, 0.06f), ProtoMaterials.MonkeyFace, null, 0f, false);

            return shoulder;
        }

        private static Transform BuildApeLeg(Transform pelvis, ZombieHealth health, float side)
        {
            string suffix = side < 0f ? "L" : "R";

            var hip = MakeBone(pelvis, "Hip_" + suffix, new Vector3(0.12f * side, -0.06f, 0f));
            Part(hip, "Thigh_" + suffix, PrimitiveType.Capsule, new Vector3(0f, -0.11f, 0f),
                 new Vector3(0.11f, 0.13f, 0.11f), ProtoMaterials.MonkeyFur, health, 0.7f, false);

            var knee = MakeBone(hip, "Knee_" + suffix, new Vector3(0f, -0.24f, 0f));
            Part(knee, "Shin_" + suffix, PrimitiveType.Capsule, new Vector3(0f, -0.1f, 0f),
                 new Vector3(0.09f, 0.12f, 0.09f), ProtoMaterials.MonkeyFur, health, 0.7f, false);
            Part(knee, "Foot_" + suffix, PrimitiveType.Cube, new Vector3(0f, -0.24f, 0.04f),
                 new Vector3(0.09f, 0.05f, 0.16f), ProtoMaterials.MonkeyFace, null, 0f, false);

            return hip;
        }

        private static GameObject Part(Transform parent, string name, PrimitiveType type,
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
