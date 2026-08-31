using UnityEngine;
using UnityEngine.AI;
using ZombieHouse.Combat;
using ZombieHouse.Level;

namespace ZombieHouse.Enemies
{
    /// <summary>
    /// Builds a humanoid walker from primitives — emaciated, hunched, in filthy clothes.
    /// It is a real skeleton: pelvis, spine, neck, head, and limbs with knee and elbow
    /// joints, so it can both be animated by <see cref="ZombieVisuals"/> and collapse
    /// into a ragdoll by <see cref="ZombieRagdoll"/>.
    ///
    /// Hierarchy (pivots are empty, meshes hang below them):
    ///   Zombie                agent, body, health, AI, rig, visuals, appearance, ragdoll, audio
    ///     Rig
    ///       Pelvis            hips mesh                                   (hitbox 0.9x)
    ///         Spine           chest mesh + shirt                          (hitbox 1.0x)
    ///           Neck
    ///             Head        skull + jaw + hair                          (hitbox 2.5x, critical)
    ///           Shoulder_L/R  upper arm mesh                              (hitbox 0.6x)
    ///             Elbow_L/R   forearm + hand                              (hitbox 0.6x)
    ///         Hip_L/R         thigh mesh + trouser                        (hitbox 0.7x)
    ///           Knee_L/R      shin + foot                                 (hitbox 0.7x)
    ///
    /// Every collider carries a Hitbox, and the ragdoll is built generically by walking
    /// the collider hierarchy — so adding or removing a limb needs no ragdoll changes.
    ///
    /// To swap in a rigged model: parent the model's bones in the same shape, fill in a
    /// ZombieRig, and delete the primitive meshes. Nothing reads the meshes directly.
    /// </summary>
    /// <summary>What this walker was wearing when it stopped being a person.</summary>
    public enum ZombieOutfit { None, Cowboy, Teacher, Kid, Janitor, Mummy }

    public static class ZombieFactory
    {
        public const float BodyHeight = 1.8f;

        // Joint heights, in metres above the feet.
        private const float HipHeight = 0.90f;
        private const float ThighLength = 0.42f;
        private const float ShinLength = 0.40f;
        private const float PelvisHeight = 0.94f;
        private const float SpineHeight = 1.02f;
        private const float ShoulderLocal = 0.40f;   // relative to Spine
        private const float UpperArmLength = 0.28f;
        private const float ForearmLength = 0.26f;
        private const float NeckLocal = 0.46f;       // relative to Spine

        public static GameObject Create(string name = "Zombie", ZombieOutfit outfit = ZombieOutfit.None)
        {
            var root = new GameObject(name);

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.36f;
            agent.height = BodyHeight;
            agent.baseOffset = 0f;
            agent.speed = 1f;
            agent.acceleration = 14f;
            agent.angularSpeed = 220f;
            agent.stoppingDistance = 1.3f;
            agent.autoBraking = true;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

            var rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;

            // Profile first: it rolls the type and feeds the numbers to the rest.
            root.AddComponent<ZombieProfile>();

            var health = root.AddComponent<ZombieHealth>();
            root.AddComponent<ZombieAI>();

            var bones = new ZombieBones();

            var rig = new GameObject("Rig").transform;
            rig.SetParent(root.transform, false);
            bones.Rig = rig;

            BuildTorso(rig, health, bones);
            BuildLegs(rig, health, bones);

            switch (outfit)
            {
                case ZombieOutfit.Cowboy: DressAsCowboy(bones); break;
                case ZombieOutfit.Teacher: DressAsTeacher(bones); break;
                case ZombieOutfit.Kid: DressAsKid(bones); break;
                case ZombieOutfit.Janitor: DressAsJanitor(bones); break;
                case ZombieOutfit.Mummy: DressAsMummy(bones); break;
            }

            var rigHolder = root.AddComponent<ZombieRig>();
            rigHolder.Bones = bones;

            root.AddComponent<ZombieAppearance>();
            root.AddComponent<ZombieVisuals>();
            root.AddComponent<ZombieRagdoll>();
            root.AddComponent<ZombieDismemberment>();
            root.AddComponent<ZombieWallCrawler>();   // disables itself unless the type climbs
            root.AddComponent<ZombieAudio>();

            // Spotting you is now news the others hear. One zombie hunting alone is a small
            // problem; forty converging because one of them called is a horde, and the whole
            // difference is whether detection propagates.
            root.AddComponent<ZombieCallout>();

            return root;
        }

        // ---- torso ----------------------------------------------------------

        private static void BuildTorso(Transform rig, ZombieHealth health, ZombieBones bones)
        {
            // --- pelvis ------------------------------------------------------
            var pelvis = new GameObject("Pelvis").transform;
            pelvis.SetParent(rig, false);
            pelvis.localPosition = new Vector3(0f, PelvisHeight, 0f);
            bones.Pelvis = pelvis;

            CreatePart(pelvis, "Hips", PrimitiveType.Cube,
                new Vector3(0f, -0.02f, 0f), new Vector3(0.27f, 0.20f, 0.19f),
                ProtoMaterials.Skin, health, 0.9f, false);
            CreatePart(pelvis, "Waistband", PrimitiveType.Cube,
                new Vector3(0f, -0.05f, 0f), new Vector3(0.29f, 0.14f, 0.21f),
                ProtoMaterials.Trousers, null, 0f, false);

            // --- spine / chest ----------------------------------------------
            var spine = new GameObject("Spine").transform;
            spine.SetParent(rig, false);
            spine.localPosition = new Vector3(0f, SpineHeight, 0f);
            bones.Spine = spine;

            CreatePart(spine, "Chest", PrimitiveType.Capsule,
                new Vector3(0f, 0.19f, 0f), new Vector3(0.29f, 0.21f, 0.23f),
                ProtoMaterials.Skin, health, 1f, false);

            // Ragged shirt over the chest — cosmetic, so bullets reach the torso hitbox.
            CreatePart(spine, "Shirt", PrimitiveType.Capsule,
                new Vector3(0f, 0.17f, 0f), new Vector3(0.31f, 0.19f, 0.25f),
                ProtoMaterials.Shirt, null, 0f, false);

            // Exposed ribs and an open wound: the silhouette reads as "eaten", not "person".
            CreatePart(spine, "Wound", PrimitiveType.Sphere,
                new Vector3(0.06f, 0.24f, 0.11f), new Vector3(0.13f, 0.10f, 0.06f),
                ProtoMaterials.Gore, null, 0f, false);
            CreatePart(spine, "Wound2", PrimitiveType.Sphere,
                new Vector3(-0.09f, 0.10f, 0.10f), new Vector3(0.09f, 0.07f, 0.05f),
                ProtoMaterials.Gore, null, 0f, false);

            // --- neck and head ----------------------------------------------
            var neck = new GameObject("Neck").transform;
            neck.SetParent(spine, false);
            neck.localPosition = new Vector3(0f, NeckLocal, 0f);
            bones.Neck = neck;

            CreatePart(neck, "NeckMesh", PrimitiveType.Capsule,
                new Vector3(0f, 0.02f, 0f), new Vector3(0.09f, 0.06f, 0.09f),
                ProtoMaterials.Skin, null, 0f, false);

            var head = new GameObject("Head").transform;
            head.SetParent(neck, false);
            head.localPosition = new Vector3(0f, 0.08f, 0f);
            bones.Head = head;

            // Skull is the critical hitbox; jaw and hair hang off it cosmetically.
            CreatePart(head, "Skull", PrimitiveType.Sphere,
                new Vector3(0f, 0.08f, 0f), new Vector3(0.19f, 0.23f, 0.21f),
                ProtoMaterials.Skin, health, 2.5f, true);
            CreatePart(head, "Jaw", PrimitiveType.Cube,
                new Vector3(0f, -0.01f, 0.05f), new Vector3(0.12f, 0.07f, 0.10f),
                ProtoMaterials.Skin, null, 0f, false);
            CreatePart(head, "Hair", PrimitiveType.Sphere,
                new Vector3(0f, 0.12f, -0.02f), new Vector3(0.19f, 0.17f, 0.20f),
                ProtoMaterials.Hair, null, 0f, false);
            CreatePart(head, "EyeSocketL", PrimitiveType.Sphere,
                new Vector3(-0.05f, 0.09f, 0.09f), new Vector3(0.05f, 0.05f, 0.03f),
                ProtoMaterials.Gore, null, 0f, false);
            CreatePart(head, "EyeSocketR", PrimitiveType.Sphere,
                new Vector3(0.05f, 0.09f, 0.09f), new Vector3(0.05f, 0.05f, 0.03f),
                ProtoMaterials.Gore, null, 0f, false);

            // --- arms --------------------------------------------------------
            bones.ShoulderLeft = BuildArm(spine, health, -1f);
            bones.ShoulderRight = BuildArm(spine, health, 1f);
            bones.ElbowLeft = bones.ShoulderLeft.Find("Elbow_L");
            bones.ElbowRight = bones.ShoulderRight.Find("Elbow_R");
        }

        private static Transform BuildArm(Transform spine, ZombieHealth health, float side)
        {
            string suffix = side < 0f ? "L" : "R";

            var shoulder = new GameObject("Shoulder_" + suffix).transform;
            shoulder.SetParent(spine, false);
            shoulder.localPosition = new Vector3(0.17f * side, ShoulderLocal, 0f);

            CreatePart(shoulder, "UpperArm_" + suffix, PrimitiveType.Capsule,
                new Vector3(0f, -UpperArmLength * 0.5f, 0f), new Vector3(0.095f, UpperArmLength * 0.5f, 0.095f),
                ProtoMaterials.Skin, health, 0.6f, false);
            CreatePart(shoulder, "Sleeve_" + suffix, PrimitiveType.Capsule,
                new Vector3(0f, -UpperArmLength * 0.35f, 0f), new Vector3(0.11f, UpperArmLength * 0.32f, 0.11f),
                ProtoMaterials.Shirt, null, 0f, false);

            var elbow = new GameObject("Elbow_" + suffix).transform;
            elbow.SetParent(shoulder, false);
            elbow.localPosition = new Vector3(0f, -UpperArmLength, 0f);

            CreatePart(elbow, "Forearm_" + suffix, PrimitiveType.Capsule,
                new Vector3(0f, -ForearmLength * 0.5f, 0f), new Vector3(0.082f, ForearmLength * 0.5f, 0.082f),
                ProtoMaterials.Skin, health, 0.6f, false);
            CreatePart(elbow, "Hand_" + suffix, PrimitiveType.Cube,
                new Vector3(0f, -ForearmLength - 0.04f, 0.01f), new Vector3(0.085f, 0.11f, 0.05f),
                ProtoMaterials.Skin, null, 0f, false);

            return shoulder;
        }

        // ---- legs -----------------------------------------------------------

        private static void BuildLegs(Transform rig, ZombieHealth health, ZombieBones bones)
        {
            bones.HipLeft = BuildLeg(rig, health, -1f);
            bones.HipRight = BuildLeg(rig, health, 1f);
            bones.KneeLeft = bones.HipLeft.Find("Knee_L");
            bones.KneeRight = bones.HipRight.Find("Knee_R");
        }

        private static Transform BuildLeg(Transform rig, ZombieHealth health, float side)
        {
            string suffix = side < 0f ? "L" : "R";

            var hip = new GameObject("Hip_" + suffix).transform;
            hip.SetParent(rig, false);
            hip.localPosition = new Vector3(0.105f * side, HipHeight, 0f);

            CreatePart(hip, "Thigh_" + suffix, PrimitiveType.Capsule,
                new Vector3(0f, -ThighLength * 0.5f, 0f), new Vector3(0.125f, ThighLength * 0.5f, 0.125f),
                ProtoMaterials.Skin, health, 0.7f, false);
            CreatePart(hip, "TrouserLeg_" + suffix, PrimitiveType.Capsule,
                new Vector3(0f, -ThighLength * 0.45f, 0f), new Vector3(0.14f, ThighLength * 0.48f, 0.14f),
                ProtoMaterials.Trousers, null, 0f, false);

            var knee = new GameObject("Knee_" + suffix).transform;
            knee.SetParent(hip, false);
            knee.localPosition = new Vector3(0f, -ThighLength, 0f);

            CreatePart(knee, "Shin_" + suffix, PrimitiveType.Capsule,
                new Vector3(0f, -ShinLength * 0.5f, 0f), new Vector3(0.10f, ShinLength * 0.5f, 0.10f),
                ProtoMaterials.Skin, health, 0.7f, false);
            CreatePart(knee, "Foot_" + suffix, PrimitiveType.Cube,
                new Vector3(0f, -ShinLength - 0.03f, 0.06f), new Vector3(0.11f, 0.07f, 0.24f),
                ProtoMaterials.Shirt, null, 0f, false);

            return hip;
        }

        /// <summary>
        /// A hat and a pair of boots, added after the body is built rather than threaded
        /// through every limb method — they hang off bones that already exist, and a
        /// dressed walker is otherwise identical to an undressed one.
        ///
        /// All of it is cosmetic: no colliders, so a hat cannot soak a headshot. The
        /// ragdoll's re-parenting pass moves it onto the nearest bone when the body
        /// falls, so hats come off with the head and boots stay on the feet.
        /// </summary>
        private static void DressAsCowboy(ZombieBones bones)
        {
            if (bones.Head != null)
            {
                // Brim first, wide and slightly askew — nothing about this is tidy.
                var brim = CreatePart(bones.Head, "HatBrim", PrimitiveType.Cylinder,
                    new Vector3(0f, 0.20f, -0.01f), new Vector3(0.34f, 0.012f, 0.30f),
                    ProtoMaterials.HatLeather, null, 0f, false);
                brim.transform.localRotation = Quaternion.Euler(6f, 0f, 9f);

                var crown = CreatePart(bones.Head, "HatCrown", PrimitiveType.Cylinder,
                    new Vector3(0f, 0.27f, -0.01f), new Vector3(0.20f, 0.09f, 0.19f),
                    ProtoMaterials.HatLeather, null, 0f, false);
                crown.transform.localRotation = Quaternion.Euler(6f, 0f, 9f);

                // The band is what makes it read as a hat rather than a bucket.
                var band = CreatePart(bones.Head, "HatBand", PrimitiveType.Cylinder,
                    new Vector3(0f, 0.215f, -0.01f), new Vector3(0.21f, 0.016f, 0.20f),
                    ProtoMaterials.Gore, null, 0f, false);
                band.transform.localRotation = Quaternion.Euler(6f, 0f, 9f);
            }

            AddBoot(bones.KneeLeft, "L");
            AddBoot(bones.KneeRight, "R");
        }

        /// <summary>
        /// A cardigan, a lanyard and reading glasses. The lanyard is the giveaway at a
        /// distance — a bright rectangle swinging at chest height is unmistakably a
        /// staff badge, and nothing else in the game has one.
        /// </summary>
        private static void DressAsTeacher(ZombieBones bones)
        {
            if (bones.Spine != null)
            {
                CreatePart(bones.Spine, "Cardigan", PrimitiveType.Capsule,
                    new Vector3(0f, 0.17f, 0f), new Vector3(0.33f, 0.21f, 0.27f),
                    ProtoMaterials.Cardigan, null, 0f, false);

                CreatePart(bones.Spine, "Lanyard", PrimitiveType.Cube,
                    new Vector3(0f, 0.24f, 0.12f), new Vector3(0.03f, 0.20f, 0.01f),
                    ProtoMaterials.Trim, null, 0f, false);

                CreatePart(bones.Spine, "StaffBadge", PrimitiveType.Cube,
                    new Vector3(0f, 0.12f, 0.13f), new Vector3(0.07f, 0.09f, 0.008f),
                    ProtoMaterials.StaffBadge, null, 0f, false);
            }

            if (bones.Head == null) return;

            // Glasses: two lenses and a bridge, sitting where the eye sockets are.
            for (int s = -1; s <= 1; s += 2)
            {
                CreatePart(bones.Head, "Lens_" + s, PrimitiveType.Cylinder,
                    new Vector3(0.05f * s, 0.09f, 0.10f), new Vector3(0.055f, 0.005f, 0.055f),
                    ProtoMaterials.Glass, null, 0f, false)
                    .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }

            CreatePart(bones.Head, "GlassesBridge", PrimitiveType.Cube,
                new Vector3(0f, 0.09f, 0.10f), new Vector3(0.03f, 0.006f, 0.006f),
                ProtoMaterials.Trim, null, 0f, false);
        }

        /// <summary>
        /// A school-age child: a bright t-shirt and a backpack. The archetype's Scale does
        /// the height, so all this has to do is make the silhouette read as a kid rather
        /// than as a short adult — and the backpack is what does that.
        /// </summary>
        private static void DressAsKid(ZombieBones bones)
        {
            if (bones.Spine == null) return;

            CreatePart(bones.Spine, "Tshirt", PrimitiveType.Capsule,
                new Vector3(0f, 0.17f, 0f), new Vector3(0.32f, 0.20f, 0.26f),
                ProtoMaterials.KidShirt, null, 0f, false);

            CreatePart(bones.Spine, "Backpack", PrimitiveType.Cube,
                new Vector3(0f, 0.18f, -0.16f), new Vector3(0.26f, 0.28f, 0.14f),
                ProtoMaterials.Backpack, null, 0f, false);

            for (int s = -1; s <= 1; s += 2)
            {
                CreatePart(bones.Spine, "Strap_" + s, PrimitiveType.Cube,
                    new Vector3(0.10f * s, 0.20f, 0.06f), new Vector3(0.035f, 0.22f, 0.02f),
                    ProtoMaterials.Backpack, null, 0f, false);
            }
        }

        /// <summary>
        /// Dark coveralls, a tool belt, and the mop.
        ///
        /// The mop hangs off the right forearm rather than the hand, because the forearm
        /// is a real bone in the rig and survives dismemberment sensibly: cut the arm off
        /// and the mop goes with it, which is exactly right.
        /// </summary>
        private static void DressAsJanitor(ZombieBones bones)
        {
            if (bones.Spine != null)
            {
                CreatePart(bones.Spine, "Coveralls", PrimitiveType.Capsule,
                    new Vector3(0f, 0.17f, 0f), new Vector3(0.34f, 0.22f, 0.28f),
                    ProtoMaterials.Coveralls, null, 0f, false);

                CreatePart(bones.Spine, "ToolBelt", PrimitiveType.Cube,
                    new Vector3(0f, -0.02f, 0f), new Vector3(0.31f, 0.06f, 0.25f),
                    ProtoMaterials.BootLeather, null, 0f, false);
            }

            if (bones.Pelvis != null)
            {
                CreatePart(bones.Pelvis, "CoverallLegs", PrimitiveType.Cube,
                    new Vector3(0f, -0.05f, 0f), new Vector3(0.30f, 0.15f, 0.22f),
                    ProtoMaterials.Coveralls, null, 0f, false);
            }

            BuildMop(bones.ElbowRight);
            AddMopAnimator(bones);
        }

        /// <summary>
        /// The mop: a long handle and a head of grey strands, held out in front.
        ///
        /// Cosmetic only — no collider anywhere on it. The reach it implies is the
        /// janitor archetype's AttackRange, not this geometry: a collider here would stop
        /// bullets meant for the janitor behind it and would carve the NavMesh if one ever
        /// died standing still. The prop's job is to explain the reach, not to be it.
        /// </summary>
        private static void BuildMop(Transform forearm)
        {
            if (forearm == null) return;

            var mop = new GameObject("Mop").transform;
            mop.SetParent(forearm, false);
            mop.localPosition = new Vector3(0.05f, -0.22f, 0.10f);
            mop.localRotation = Quaternion.Euler(62f, 0f, 14f);

            CreatePart(mop, "MopHandle", PrimitiveType.Cylinder,
                new Vector3(0f, 0.62f, 0f), new Vector3(0.035f, 0.62f, 0.035f),
                ProtoMaterials.Wood, null, 0f, false);

            CreatePart(mop, "MopCollar", PrimitiveType.Cylinder,
                new Vector3(0f, 1.20f, 0f), new Vector3(0.07f, 0.04f, 0.07f),
                ProtoMaterials.Metal, null, 0f, false);

            // The head: a handful of strands at slightly different angles, so it hangs
            // like something wet rather than like a brush.
            for (int i = 0; i < 7; i++)
            {
                float angle = i * (360f / 7f);
                Quaternion spread = Quaternion.Euler(Random.Range(6f, 20f), angle, 0f);

                var strand = CreatePart(mop, "MopStrand_" + i, PrimitiveType.Capsule,
                    spread * new Vector3(0f, 0f, 0.05f) + new Vector3(0f, 1.34f, 0f),
                    new Vector3(0.028f, 0.11f, 0.028f),
                    ProtoMaterials.MopHead, null, 0f, false);

                strand.transform.localRotation = spread;
            }
        }

        /// <summary>
        /// The component that swings the mop. Added here rather than in the factory's
        /// component list at the top, because it only makes sense on something that has
        /// one, and a MopAnimator with no mop to find would be dead weight on every
        /// walker in four other levels.
        /// </summary>
        /// <summary>
        /// Bandages: overlapping bands round every limb and the torso, a few of them
        /// hanging loose, and a wrapped head with the sockets left dark.
        ///
        /// The trick to making wrapping read as wrapping rather than as a white costume is
        /// **alternating tone** — clean linen against filthy linen, band by band. A single
        /// colour at this scale just looks like a bodysuit.
        /// </summary>
        private static void DressAsMummy(ZombieBones bones)
        {
            WrapLimb(bones.Spine, "Torso", 5, 0.34f, 0.055f, 0.14f);
            WrapLimb(bones.Pelvis, "Hips", 2, 0.30f, 0.05f, -0.02f);

            WrapLimb(bones.ShoulderLeft, "ArmL", 3, 0.15f, 0.045f, -0.12f);
            WrapLimb(bones.ShoulderRight, "ArmR", 3, 0.15f, 0.045f, -0.12f);
            WrapLimb(bones.ElbowLeft, "ForearmL", 3, 0.13f, 0.04f, -0.11f);
            WrapLimb(bones.ElbowRight, "ForearmR", 3, 0.13f, 0.04f, -0.11f);

            WrapLimb(bones.HipLeft, "ThighL", 3, 0.18f, 0.05f, -0.16f);
            WrapLimb(bones.HipRight, "ThighR", 3, 0.18f, 0.05f, -0.16f);
            WrapLimb(bones.KneeLeft, "ShinL", 3, 0.15f, 0.045f, -0.14f);
            WrapLimb(bones.KneeRight, "ShinR", 3, 0.15f, 0.045f, -0.14f);

            if (bones.Head != null)
            {
                WrapLimb(bones.Head, "Head", 4, 0.22f, 0.05f, 0.02f);

                // The sockets stay dark: a fully wrapped head with no face is a bandage
                // ball, and the two holes are what make it look back at you.
                for (int s = -1; s <= 1; s += 2)
                {
                    CreatePart(bones.Head, "Socket_" + s, PrimitiveType.Sphere,
                        new Vector3(0.05f * s, 0.09f, 0.10f), new Vector3(0.055f, 0.05f, 0.03f),
                        ProtoMaterials.Gore, null, 0f, false);
                }
            }

            // Loose ends trailing from the arms — the thing that says "unravelling".
            for (int i = 0; i < 3; i++)
            {
                Transform from = i == 0 ? bones.ElbowLeft
                               : i == 1 ? bones.ElbowRight : bones.Spine;
                if (from == null) continue;

                var tail = CreatePart(from, "LooseWrap_" + i, PrimitiveType.Cube,
                    new Vector3(0.05f, -0.16f - i * 0.03f, 0.02f),
                    new Vector3(0.035f, 0.26f + i * 0.05f, 0.012f),
                    ProtoMaterials.BandageDark, null, 0f, false);

                tail.transform.localRotation = Quaternion.Euler(9f * i, 14f * i, 6f * i);
            }
        }

        /// <summary>
        /// Bands of linen round one bone, alternating clean and filthy so the wrapping
        /// reads as separate strips rather than as a sleeve.
        /// </summary>
        private static void WrapLimb(Transform bone, string label, int bands,
                                     float width, float thickness, float startY)
        {
            if (bone == null) return;

            for (int i = 0; i < bands; i++)
            {
                float y = startY - i * (thickness * 1.7f);

                var band = CreatePart(bone, $"Wrap_{label}_{i}", PrimitiveType.Cylinder,
                    new Vector3(0f, y, 0f),
                    new Vector3(width, thickness, width * 0.92f),
                    i % 2 == 0 ? ProtoMaterials.Bandage : ProtoMaterials.BandageDark,
                    null, 0f, false);

                // A few degrees of tilt per band, so they are wound rather than stacked.
                band.transform.localRotation = Quaternion.Euler(4f * ((i % 2 == 0) ? 1f : -1f), 0f,
                                                                6f * ((i % 3 == 0) ? 1f : -0.6f));
            }
        }

        private static void AddMopAnimator(ZombieBones bones)
        {
            if (bones.Rig == null || bones.Rig.parent == null) return;

            GameObject root = bones.Rig.parent.gameObject;
            if (root.GetComponent<MopAnimator>() == null) root.AddComponent<MopAnimator>();
        }

        private static void AddBoot(Transform knee, string suffix)
        {
            if (knee == null) return;

            // The shaft comes up the shin, which is the whole silhouette of a cowboy boot.
            CreatePart(knee, "BootShaft_" + suffix, PrimitiveType.Capsule,
                new Vector3(0f, -ShinLength * 0.72f, 0f), new Vector3(0.13f, 0.13f, 0.13f),
                ProtoMaterials.BootLeather, null, 0f, false);

            CreatePart(knee, "BootFoot_" + suffix, PrimitiveType.Cube,
                new Vector3(0f, -ShinLength - 0.03f, 0.07f), new Vector3(0.13f, 0.09f, 0.27f),
                ProtoMaterials.BootLeather, null, 0f, false);

            // A stacked heel, set back, and a spur behind it.
            CreatePart(knee, "BootHeel_" + suffix, PrimitiveType.Cube,
                new Vector3(0f, -ShinLength - 0.09f, -0.03f), new Vector3(0.11f, 0.05f, 0.09f),
                ProtoMaterials.BootLeather, null, 0f, false);

            var spur = CreatePart(knee, "Spur_" + suffix, PrimitiveType.Cylinder,
                new Vector3(0f, -ShinLength - 0.06f, -0.10f), new Vector3(0.07f, 0.008f, 0.07f),
                ProtoMaterials.Metal, null, 0f, false);
            spur.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        // ---- helpers --------------------------------------------------------

        /// <summary>
        /// Creates one body part. A part with a hit multiplier keeps its collider and
        /// gets a Hitbox; everything else is decoration and loses its collider so it
        /// never blocks a bullet meant for the body underneath.
        /// </summary>
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
