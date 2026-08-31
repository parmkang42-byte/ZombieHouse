using UnityEngine;
using UnityEngine.AI;
using ZombieHouse.Combat;
using ZombieHouse.Level;

namespace ZombieHouse.Enemies
{
    /// <summary>
    /// A scarab the size of a large dog: low, fast, armoured on top and soft underneath.
    ///
    /// Built on the same named bones as the bear and the horse — front legs are "arms",
    /// rear legs are "legs" — so <see cref="ZombieRagdoll"/> and
    /// <see cref="ZombieDismemberment"/> work on it with no changes at all, which is now
    /// the third creature that trick has paid for.
    ///
    /// A real beetle has six legs and this has four load-bearing ones, with a middle pair
    /// hung off the thorax as decoration. That is deliberate: the bone chain the ragdoll
    /// walks is the four, and adding a fifth and sixth would mean teaching the ragdoll
    /// about them for something nobody will ever count mid-fight.
    ///
    /// The shell is the design. Rifle rounds barely scratch it and the Uzi struggles, but
    /// the underside is soft — so the answer is the gatling gun, or letting one come at you
    /// and putting a .50 through it as it rears.
    /// </summary>
    public static class ScarabFactory
    {
        private const float BodyHeight = 0.55f;   // at the shell
        private const float LegLength = 0.42f;
        private const float SpineY = 0.42f;

        public static GameObject Create(string name = "Scarab")
        {
            var root = new GameObject(name);

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.34f;
            agent.height = BodyHeight;
            agent.baseOffset = 0f;
            agent.speed = 1f;
            agent.acceleration = 26f;      // it starts moving faster than anything else
            agent.angularSpeed = 420f;     // and turns on the spot
            agent.stoppingDistance = 0.9f;
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

            // Up the walls, across the ceiling, and down on top of you — with the legs
            // still working the whole way, which ZombieVisuals cannot do because it takes
            // its stride from an agent that is switched off during a climb.
            root.AddComponent<ScarabCeilingCrawler>();
            root.AddComponent<ScarabGait>();
            root.AddComponent<ZombieAudio>();

            // Spotting you is now news the others hear. One zombie hunting alone is a small
            // problem; forty converging because one of them called is a horde, and the whole
            // difference is whether detection propagates.
            root.AddComponent<ZombieCallout>();

            return root;
        }

        private static void BuildBody(Transform rig, ZombieHealth health, ZombieBones bones)
        {
            // --- abdomen, under the shell -------------------------------------
            var pelvis = new GameObject("Pelvis").transform;
            pelvis.SetParent(rig, false);
            pelvis.localPosition = new Vector3(0f, SpineY, -0.22f);
            bones.Pelvis = pelvis;

            CreatePart(pelvis, "Hips", PrimitiveType.Sphere, Vector3.zero,
                new Vector3(0.46f, 0.30f, 0.52f), ProtoMaterials.ScarabShell, health, 0.85f, false);

            // --- thorax --------------------------------------------------------
            var spine = new GameObject("Spine").transform;
            spine.SetParent(rig, false);
            spine.localPosition = new Vector3(0f, SpineY, 0.12f);
            bones.Spine = spine;

            CreatePart(spine, "Chest", PrimitiveType.Sphere, Vector3.zero,
                new Vector3(0.42f, 0.28f, 0.40f), ProtoMaterials.ScarabShell, health, 1f, false);

            // The elytra: two halves of a hard wing case, split down the middle. This is
            // the silhouette — a beetle is a seam with legs.
            for (int s = -1; s <= 1; s += 2)
            {
                var shell = CreatePart(spine, "Elytra_" + s, PrimitiveType.Sphere,
                    new Vector3(0.11f * s, 0.10f, -0.14f),
                    new Vector3(0.26f, 0.22f, 0.60f), ProtoMaterials.ScarabShell, null, 0f, false);

                shell.transform.localRotation = Quaternion.Euler(0f, 0f, -7f * s);
            }

            // The soft underside, which is the only part worth shooting.
            CreatePart(spine, "Underside", PrimitiveType.Sphere, new Vector3(0f, -0.13f, -0.10f),
                new Vector3(0.34f, 0.12f, 0.52f), ProtoMaterials.Gore, null, 0f, false);

            // Middle legs, hung off the thorax as decoration — not in the bone chain.
            for (int s = -1; s <= 1; s += 2)
            {
                var leg = CreatePart(spine, "MidLeg_" + s, PrimitiveType.Capsule,
                    new Vector3(0.24f * s, -0.10f, -0.02f),
                    new Vector3(0.05f, 0.20f, 0.05f), ProtoMaterials.ScarabLimb, null, 0f, false);

                leg.transform.localRotation = Quaternion.Euler(0f, 0f, 58f * s);
            }

            // --- head, horn and mandibles ---------------------------------------
            var neck = new GameObject("Neck").transform;
            neck.SetParent(spine, false);
            neck.localPosition = new Vector3(0f, -0.02f, 0.26f);
            bones.Neck = neck;

            var head = new GameObject("Head").transform;
            head.SetParent(neck, false);
            head.localPosition = new Vector3(0f, 0f, 0.12f);
            bones.Head = head;

            CreatePart(head, "Skull", PrimitiveType.Sphere, Vector3.zero,
                new Vector3(0.26f, 0.18f, 0.24f), ProtoMaterials.ScarabShell, health, 2.2f, true);

            // The horn a scarab is named for, curving up off the front of the head.
            var horn = CreatePart(head, "Horn", PrimitiveType.Capsule, new Vector3(0f, 0.14f, 0.10f),
                new Vector3(0.05f, 0.14f, 0.05f), ProtoMaterials.ScarabLimb, null, 0f, false);
            horn.transform.localRotation = Quaternion.Euler(-38f, 0f, 0f);

            for (int s = -1; s <= 1; s += 2)
            {
                var jaw = CreatePart(head, "Mandible_" + s, PrimitiveType.Capsule,
                    new Vector3(0.07f * s, -0.04f, 0.16f),
                    new Vector3(0.035f, 0.10f, 0.035f), ProtoMaterials.ScarabLimb, null, 0f, false);

                jaw.transform.localRotation = Quaternion.Euler(72f, 0f, -26f * s);

                CreatePart(head, "Eye_" + s, PrimitiveType.Sphere,
                    new Vector3(0.09f * s, 0.05f, 0.09f),
                    new Vector3(0.055f, 0.05f, 0.045f), ProtoMaterials.ScarabEye, null, 0f, false);

                CreatePart(head, "Antenna_" + s, PrimitiveType.Capsule,
                    new Vector3(0.06f * s, 0.09f, 0.13f),
                    new Vector3(0.02f, 0.09f, 0.02f), ProtoMaterials.ScarabLimb, null, 0f, false)
                    .transform.localRotation = Quaternion.Euler(-52f, 0f, -30f * s);
            }

            // --- the four legs the ragdoll knows about ---------------------------
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
        /// One leg, out and down: an insect leg goes sideways before it goes under, which
        /// is most of what separates a beetle's stance from a dog's.
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
                ? new Vector3(0.20f * side, -0.06f, 0.16f)
                : new Vector3(0.20f * side, SpineY - 0.06f, -0.34f);

            var femur = CreatePart(upper, upperMesh + suffix, PrimitiveType.Capsule,
                new Vector3(0.10f * side, -0.06f, 0f),
                new Vector3(0.055f, LegLength * 0.34f, 0.055f),
                ProtoMaterials.ScarabLimb, health, 0.6f, false);

            femur.transform.localRotation = Quaternion.Euler(0f, 0f, 62f * side);

            var joint = new GameObject(jointName + suffix).transform;
            joint.SetParent(upper, false);
            joint.localPosition = new Vector3(0.20f * side, -0.10f, 0f);

            var tibia = CreatePart(joint, lowerMesh + suffix, PrimitiveType.Capsule,
                new Vector3(0.02f * side, -0.13f, 0f),
                new Vector3(0.045f, LegLength * 0.36f, 0.045f),
                ProtoMaterials.ScarabLimb, health, 0.6f, false);

            tibia.transform.localRotation = Quaternion.Euler(0f, 0f, 12f * side);

            CreatePart(joint, "Claw_" + suffix, PrimitiveType.Capsule,
                new Vector3(0.03f * side, -0.26f, 0.02f),
                new Vector3(0.03f, 0.05f, 0.03f), ProtoMaterials.ScarabLimb, null, 0f, false);

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
