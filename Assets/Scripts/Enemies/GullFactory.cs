using UnityEngine;
using ZombieHouse.Combat;
using ZombieHouse.Level;

namespace ZombieHouse.Enemies
{
    /// <summary>
    /// A herring gull, dead about a week and still working the ship.
    ///
    /// Built on the same named bones every other creature here uses — wings are "arms",
    /// legs are "legs" — which is the fourth time that trick has paid for itself: the
    /// hitbox layout and the dismemberment both work on it with no changes, and a wing
    /// comes off exactly the way an arm does.
    ///
    /// No NavMeshAgent and no ZombieAI, which makes it the only creature in the game with
    /// neither. Both exist to walk, and this thing has never walked anywhere;
    /// <see cref="GullFlight"/> owns its position instead. ZombieHealth still registers it
    /// with the GameManager, so gulls count towards the level's kill quota like everything
    /// else — they are part of the population, not scenery with a hitbox.
    ///
    /// Deliberately kept the size of a real gull. The temptation with a flying enemy is to
    /// make it big enough to shoot easily; the whole character of these is that they are
    /// small, fast and come from a direction you were not covering, and a gull the size of
    /// a dog would just be a slower dog.
    /// </summary>
    public static class GullFactory
    {
        private const float BodyLength = 0.42f;
        private const float WingSpan = 0.55f;    // per wing, folded length

        public static GameObject Create(string name = "Gull")
        {
            var root = new GameObject(name);

            // Kinematic while it flies — GullFlight writes the transform directly. It goes
            // dynamic on death so the corpse falls out of the air onto the deck.
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.mass = 1.2f;

            root.AddComponent<ZombieProfile>();
            var health = root.AddComponent<ZombieHealth>();

            var bones = new ZombieBones();

            var rig = new GameObject("Rig").transform;
            rig.SetParent(root.transform, false);
            bones.Rig = rig;

            BuildBody(rig, health, bones);
            BuildWings(rig, health, bones);
            BuildLegs(rig, health, bones);

            var rigHolder = root.AddComponent<ZombieRig>();
            rigHolder.Bones = bones;

            root.AddComponent<ZombieAppearance>();
            root.AddComponent<ZombieDismemberment>();
            root.AddComponent<GullFlight>();
            root.AddComponent<ZombieAudio>();

            return root;
        }

        private static void BuildBody(Transform rig, ZombieHealth health, ZombieBones bones)
        {
            // The pelvis carries the tail end, the spine the breast. Same two bones as
            // everything else, laid along Z instead of stacked up Y.
            var pelvis = new GameObject("Pelvis").transform;
            pelvis.SetParent(rig, false);
            pelvis.localPosition = new Vector3(0f, 0.12f, -BodyLength * 0.30f);
            bones.Pelvis = pelvis;

            CreatePart(pelvis, "Rump", PrimitiveType.Sphere, Vector3.zero,
                new Vector3(0.17f, 0.15f, 0.26f), ProtoMaterials.GullFeather, health, 0.9f, false);

            // The tail: a wedge, and the thing that says "bird" from behind more than the
            // wings do.
            var tail = CreatePart(pelvis, "Tail", PrimitiveType.Cube,
                new Vector3(0f, 0.01f, -0.20f), new Vector3(0.19f, 0.02f, 0.20f),
                ProtoMaterials.GullFeather, null, 0f, false);
            tail.transform.localRotation = Quaternion.Euler(-9f, 0f, 0f);

            var spine = new GameObject("Spine").transform;
            spine.SetParent(pelvis, false);
            spine.localPosition = new Vector3(0f, 0.02f, BodyLength * 0.34f);
            bones.Spine = spine;

            CreatePart(spine, "Breast", PrimitiveType.Sphere, Vector3.zero,
                new Vector3(0.19f, 0.18f, 0.28f), ProtoMaterials.GullFeather, health, 1f, false);

            // The mantle — the grey saddle over the back and the top of the wings. It is
            // the only marking a gull really has, and without it this is a white blob.
            CreatePart(spine, "Mantle", PrimitiveType.Sphere,
                new Vector3(0f, 0.055f, -0.03f), new Vector3(0.175f, 0.10f, 0.30f),
                ProtoMaterials.GullMantle, null, 0f, false);

            var neck = new GameObject("Neck").transform;
            neck.SetParent(spine, false);
            neck.localPosition = new Vector3(0f, 0.07f, 0.11f);
            bones.Neck = neck;

            CreatePart(neck, "Throat", PrimitiveType.Capsule,
                new Vector3(0f, 0.03f, 0.01f), new Vector3(0.085f, 0.07f, 0.085f),
                ProtoMaterials.GullFeather, null, 0f, false);

            var head = new GameObject("Head").transform;
            head.SetParent(neck, false);
            head.localPosition = new Vector3(0f, 0.09f, 0.03f);
            bones.Head = head;

            // Critical, and generous: the head is a third of what you can see of a gull
            // coming at you, and a flying target with a pinpoint weak spot is not a target,
            // it is a coin toss.
            CreatePart(head, "Skull", PrimitiveType.Sphere, Vector3.zero,
                new Vector3(0.115f, 0.115f, 0.135f), ProtoMaterials.GullFeather,
                health, 2.5f, true);

            CreatePart(head, "Beak", PrimitiveType.Cube,
                new Vector3(0f, -0.005f, 0.10f), new Vector3(0.035f, 0.033f, 0.115f),
                ProtoMaterials.GullBeak, null, 0f, false);

            // The red gonydeal spot. On a real gull it is what the chicks peck at; here it
            // is simply the one warm pixel on an otherwise white bird, at head height.
            CreatePart(head, "BeakSpot", PrimitiveType.Cube,
                new Vector3(0f, -0.017f, 0.135f), new Vector3(0.026f, 0.016f, 0.02f),
                ProtoMaterials.GullBeakSpot, null, 0f, false);

            for (int s = -1; s <= 1; s += 2)
            {
                CreatePart(head, "Eye_" + s, PrimitiveType.Sphere,
                    new Vector3(0.045f * s, 0.022f, 0.045f), new Vector3(0.03f, 0.03f, 0.03f),
                    ProtoMaterials.GullEye, null, 0f, false);
            }
        }

        private static void BuildWings(Transform rig, ZombieHealth health, ZombieBones bones)
        {
            if (bones.Spine == null) return;

            for (int s = -1; s <= 1; s += 2)
            {
                var shoulder = new GameObject(s < 0 ? "Shoulder_L" : "Shoulder_R").transform;
                shoulder.SetParent(bones.Spine, false);
                shoulder.localPosition = new Vector3(0.075f * s, 0.05f, -0.01f);

                if (s < 0) bones.ShoulderLeft = shoulder; else bones.ShoulderRight = shoulder;

                CreatePart(shoulder, "WingInner", PrimitiveType.Cube,
                    new Vector3(WingSpan * 0.26f * s, 0f, -0.02f),
                    new Vector3(WingSpan * 0.52f, 0.028f, 0.20f),
                    ProtoMaterials.GullMantle, health, 0.5f, false);

                var elbow = new GameObject(s < 0 ? "Elbow_L" : "Elbow_R").transform;
                elbow.SetParent(shoulder, false);
                elbow.localPosition = new Vector3(WingSpan * 0.52f * s, 0f, -0.02f);

                if (s < 0) bones.ElbowLeft = elbow; else bones.ElbowRight = elbow;

                CreatePart(elbow, "WingOuter", PrimitiveType.Cube,
                    new Vector3(WingSpan * 0.24f * s, 0f, -0.03f),
                    new Vector3(WingSpan * 0.48f, 0.022f, 0.15f),
                    ProtoMaterials.GullMantle, health, 0.5f, false);

                // Black primaries at the tip. The one detail that makes a white cube read as
                // a wing rather than a plank.
                CreatePart(elbow, "Primaries", PrimitiveType.Cube,
                    new Vector3(WingSpan * 0.44f * s, 0f, -0.04f),
                    new Vector3(WingSpan * 0.16f, 0.02f, 0.12f),
                    ProtoMaterials.GullPrimary, null, 0f, false);
            }
        }

        private static void BuildLegs(Transform rig, ZombieHealth health, ZombieBones bones)
        {
            if (bones.Pelvis == null) return;

            for (int s = -1; s <= 1; s += 2)
            {
                var hip = new GameObject(s < 0 ? "Hip_L" : "Hip_R").transform;
                hip.SetParent(bones.Pelvis, false);
                hip.localPosition = new Vector3(0.045f * s, -0.06f, 0.02f);

                if (s < 0) bones.HipLeft = hip; else bones.HipRight = hip;

                CreatePart(hip, "Shank", PrimitiveType.Cylinder,
                    new Vector3(0f, -0.035f, 0f), new Vector3(0.022f, 0.035f, 0.022f),
                    ProtoMaterials.GullFoot, health, 0.4f, false);

                var knee = new GameObject(s < 0 ? "Knee_L" : "Knee_R").transform;
                knee.SetParent(hip, false);
                knee.localPosition = new Vector3(0f, -0.07f, 0f);

                if (s < 0) bones.KneeLeft = knee; else bones.KneeRight = knee;

                CreatePart(knee, "Foot", PrimitiveType.Cube,
                    new Vector3(0f, -0.01f, 0.02f), new Vector3(0.05f, 0.014f, 0.075f),
                    ProtoMaterials.GullFoot, null, 0f, false);
            }
        }

        /// <summary>
        /// Same contract as the other factories: a part with a hit multiplier keeps its
        /// collider and gets a Hitbox, and everything else loses its collider so it cannot
        /// stop a bullet meant for the bird underneath it.
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
