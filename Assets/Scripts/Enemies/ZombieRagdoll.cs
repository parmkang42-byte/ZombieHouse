using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ZombieHouse.Core;

namespace ZombieHouse.Enemies
{
    /// <summary>
    /// Turns the animated body into a physics ragdoll the moment it dies, then freezes it
    /// once it has settled so the corpse stays on the floor forever at no ongoing cost.
    ///
    /// The ragdoll is built generically: every collider becomes a rigidbody, and each one
    /// is jointed to the nearest collider above it in the hierarchy. Change the skeleton
    /// and the ragdoll follows automatically.
    ///
    /// It is built lazily at death rather than up front — while a zombie is alive it has
    /// no rigidbodies at all, which keeps a house full of walkers cheap.
    /// </summary>
    public class ZombieRagdoll : MonoBehaviour
    {
        [Header("Mass distribution (kg)")]
        [SerializeField] private float pelvisMass = 12f;
        [SerializeField] private float chestMass = 20f;
        [SerializeField] private float headMass = 5f;
        [SerializeField] private float thighMass = 8f;
        [SerializeField] private float shinMass = 4f;
        [SerializeField] private float upperArmMass = 3f;
        [SerializeField] private float forearmMass = 2f;

        [Header("Joint limits (degrees)")]
        [SerializeField] private float swing1Limit = 42f;
        [SerializeField] private float swing2Limit = 22f;
        [SerializeField] private float twistLimit = 18f;

        [Header("Death impulse")]
        [SerializeField] private float impulseScale = 0.055f;
        [SerializeField] private float maximumImpulse = 9f;

        [Header("Settling")]
        [SerializeField] private float settleSpeedThreshold = 0.14f;
        [SerializeField] private float settleHoldSeconds = 1.5f;
        [SerializeField] private float settleTimeoutSeconds = 12f;

        [Header("Corpse")]
        [Tooltip("Corpses move to this layer so the player does not trip over bodies.")]
        [SerializeField] private string corpseLayerName = "Corpse";

        public bool IsRagdolled { get; private set; }
        public bool HasSettled { get; private set; }

        private readonly List<Rigidbody> _bodies = new List<Rigidbody>();
        private static bool _layerCollisionConfigured;

        /// <summary>Collapses the body. Safe to call once; later calls are ignored.</summary>
        public void Collapse(DamageInfo killingBlow)
        {
            if (IsRagdolled) return;
            IsRagdolled = true;

            ConfigureLayerCollisionsOnce();

            // Animation must stop before physics takes over, or the two fight each other.
            var visuals = GetComponent<ZombieVisuals>();
            if (visuals != null)
            {
                visuals.StopAnimating();
                visuals.enabled = false;
            }

            var agent = GetComponent<NavMeshAgent>();
            if (agent != null && agent.isActiveAndEnabled) agent.enabled = false;

            var rootBody = GetComponent<Rigidbody>();
            if (rootBody != null) rootBody.isKinematic = true;

            Build();
            AttachCosmeticsToBones();
            MoveToCorpseLayer();
            ApplyImpulse(killingBlow);

            var appearance = GetComponent<ZombieAppearance>();
            if (appearance != null) appearance.ApplyDeadTint();

            SprayDeathBlood(killingBlow);

            StartCoroutine(SettleThenFreeze());
        }

        // ---- construction ---------------------------------------------------

        /// <summary>
        /// The physical skeleton, stated explicitly because it is NOT the transform
        /// hierarchy. Limbs hang off empty pivots so they can be animated from the joint,
        /// which leaves every collider a leaf in its own branch — the chest is a sibling
        /// of the hips, not a child. Walking up the hierarchy therefore finds no parent
        /// at all, so the bone chain is declared here instead.
        ///
        /// If you rename a part in ZombieFactory, rename it here too.
        /// </summary>
        private static string PhysicalParentOf(string partName)
        {
            if (partName.StartsWith("Chest")) return "Hips";
            if (partName.StartsWith("Skull")) return "Chest";
            if (partName.StartsWith("Thigh")) return "Hips";
            if (partName.StartsWith("Shin_L")) return "Thigh_L";
            if (partName.StartsWith("Shin_R")) return "Thigh_R";
            if (partName.StartsWith("UpperArm")) return "Chest";
            if (partName.StartsWith("Forearm_L")) return "UpperArm_L";
            if (partName.StartsWith("Forearm_R")) return "UpperArm_R";
            return null;   // Hips is the free root of the ragdoll
        }

        /// <summary>
        /// Where the joint pins the limb to its parent, in the limb's own local space.
        /// Unity's capsule and sphere primitives span +/-1 and +/-0.5 locally, so a limb
        /// hangs from its top at (0, 1, 0). Anchoring at the body centre instead makes
        /// the ragdoll look like it is made of loosely strung beads.
        /// </summary>
        private static Vector3 AnchorFor(string partName)
        {
            if (partName.StartsWith("Chest")) return new Vector3(0f, -1f, 0f);   // joins down to the hips
            if (partName.StartsWith("Skull")) return new Vector3(0f, -0.5f, 0f); // sphere, joins at the neck
            return new Vector3(0f, 1f, 0f);                                      // limbs hang from the top
        }

        private void Build()
        {
            var colliders = GetComponentsInChildren<Collider>();
            var bodyByName = new Dictionary<string, Rigidbody>();

            // Pass one: a rigidbody on every part that has a collider.
            foreach (Collider collider in colliders)
            {
                if (collider.isTrigger) continue;

                var body = collider.gameObject.GetComponent<Rigidbody>();
                if (body == null) body = collider.gameObject.AddComponent<Rigidbody>();

                body.mass = MassFor(collider.gameObject.name);
                body.isKinematic = false;
                body.useGravity = true;
                body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                body.interpolation = RigidbodyInterpolation.Interpolate;

                // Limb capsules touch at the joints. Without this, PhysX resolves the
                // overlap on the first frame by flinging them apart.
                body.maxDepenetrationVelocity = 1.5f;

                bodyByName[collider.gameObject.name] = body;
                _bodies.Add(body);
            }

            // Pass two: joint each part to its parent bone.
            int jointCount = 0;

            foreach (KeyValuePair<string, Rigidbody> entry in bodyByName)
            {
                string parentName = PhysicalParentOf(entry.Key);
                if (parentName == null) continue;

                Rigidbody parent;
                if (!bodyByName.TryGetValue(parentName, out parent))
                {
                    // Never leave a limb unattached — a loose part just falls off the body.
                    if (!bodyByName.TryGetValue("Hips", out parent)) continue;
                }

                if (parent == entry.Value) continue;

                var joint = entry.Value.gameObject.AddComponent<CharacterJoint>();
                joint.connectedBody = parent;
                joint.enablePreprocessing = false;

                joint.autoConfigureConnectedAnchor = true;
                joint.anchor = AnchorFor(entry.Key);

                // Keep joints from pulling apart under the impulse.
                joint.enableProjection = true;
                joint.projectionDistance = 0.08f;
                joint.projectionAngle = 20f;

                joint.lowTwistLimit = new SoftJointLimit { limit = -twistLimit };
                joint.highTwistLimit = new SoftJointLimit { limit = twistLimit };
                joint.swing1Limit = new SoftJointLimit { limit = swing1Limit };
                joint.swing2Limit = new SoftJointLimit { limit = swing2Limit };

                jointCount++;
            }

            if (jointCount == 0 && _bodies.Count > 1)
                Debug.LogWarning("[ZombieRagdoll] No joints were created — the corpse will fall apart. " +
                                 "Part names in ZombieFactory and PhysicalParentOf have drifted apart.");
        }

        /// <summary>
        /// Re-parents the clothing, hair, hands and feet onto the nearest bone.
        ///
        /// Only parts with colliders become rigidbodies. Everything else is still
        /// parented to the animation pivots, which do not move once the body is dead —
        /// so without this the skeleton drops to the floor and leaves a shirt, a pair of
        /// trousers and a head of hair standing upright in mid-air.
        ///
        /// Nearest-bone matching is reliable here because every cosmetic piece sits on
        /// the bone it belongs to: hair and jaw land on the skull, the shirt on the
        /// chest, boots on the shins.
        /// </summary>
        private void AttachCosmeticsToBones()
        {
            var loose = new List<Transform>();

            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            {
                if (renderer.GetComponent<Collider>() != null) continue;   // already a ragdoll part
                loose.Add(renderer.transform);
            }

            foreach (Transform part in loose)
            {
                Rigidbody bone = ClosestBody(part.position);
                if (bone == null || bone.transform == part) continue;

                // worldPositionStays: the piece must not move, only change owner.
                part.SetParent(bone.transform, true);
            }
        }

        private float MassFor(string partName)
        {
            if (partName.StartsWith("Hips")) return pelvisMass;
            if (partName.StartsWith("Chest")) return chestMass;
            if (partName.StartsWith("Skull")) return headMass;
            if (partName.StartsWith("Thigh")) return thighMass;
            if (partName.StartsWith("Shin")) return shinMass;
            if (partName.StartsWith("UpperArm")) return upperArmMass;
            if (partName.StartsWith("Forearm")) return forearmMass;
            return 2f;
        }

        private void ApplyImpulse(DamageInfo killingBlow)
        {
            if (_bodies.Count == 0) return;

            Vector3 direction = killingBlow.Direction;
            if (direction.sqrMagnitude < 0.001f) direction = transform.forward;
            direction.Normalize();

            float force = Mathf.Min(killingBlow.Amount * impulseScale, maximumImpulse);

            // Push the part that was actually hit — a headshot should snap the head back.
            Rigidbody target = ClosestBody(killingBlow.Point);
            if (target != null)
            {
                target.AddForceAtPosition(direction * force, killingBlow.Point, ForceMode.Impulse);
            }

            // A gentler shove through the whole body so it topples rather than spins.
            foreach (Rigidbody body in _bodies)
                body.AddForce(direction * (force * 0.18f), ForceMode.Impulse);
        }

        /// <summary>
        /// A final spray as the body goes down, so a kill leaves a mark on the floor
        /// where it fell rather than only where the last bullet landed.
        /// </summary>
        private void SprayDeathBlood(DamageInfo killingBlow)
        {
            var impacts = FindAnyObjectByType<Fx.ImpactSystem>();
            if (impacts == null) return;

            Vector3 point = killingBlow.Point.sqrMagnitude > 0.001f
                ? killingBlow.Point
                : transform.position + Vector3.up * 1.2f;

            Vector3 normal = killingBlow.Normal.sqrMagnitude > 0.001f ? killingBlow.Normal : Vector3.up;

            impacts.SpawnFleshFx(point, normal, killingBlow.IsCritical, 1.8f);
        }

        private Rigidbody ClosestBody(Vector3 point)
        {
            Rigidbody closest = null;
            float bestDistance = float.MaxValue;

            foreach (Rigidbody body in _bodies)
            {
                float distance = (body.worldCenterOfMass - point).sqrMagnitude;
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                closest = body;
            }

            return closest;
        }

        // ---- settling -------------------------------------------------------

        private IEnumerator SettleThenFreeze()
        {
            float quietFor = 0f;
            float elapsed = 0f;
            var wait = new WaitForSeconds(0.25f);

            while (elapsed < settleTimeoutSeconds)
            {
                yield return wait;
                elapsed += 0.25f;

                if (!GameManager.GameplayActive) continue;

                quietFor = FastestBodySpeed() < settleSpeedThreshold ? quietFor + 0.25f : 0f;
                if (quietFor >= settleHoldSeconds) break;
            }

            Freeze();
        }

        private float FastestBodySpeed()
        {
            float fastest = 0f;

            foreach (Rigidbody body in _bodies)
            {
                if (body == null || body.isKinematic) continue;
#if UNITY_6000_0_OR_NEWER
                fastest = Mathf.Max(fastest, body.linearVelocity.magnitude);
#else
                fastest = Mathf.Max(fastest, body.velocity.magnitude);
#endif
            }

            return fastest;
        }

        /// <summary>
        /// Locks the corpse in place. The body stays exactly where it fell with its
        /// colliders intact, but costs the physics engine nothing from here on.
        /// </summary>
        private void Freeze()
        {
            foreach (Rigidbody body in _bodies)
            {
                if (body == null) continue;
                body.isKinematic = true;
            }

            // The joints have done their job; without them the frozen parts are just
            // static colliders sitting in a heap.
            foreach (CharacterJoint joint in GetComponentsInChildren<CharacterJoint>())
                Destroy(joint);

            HasSettled = true;
        }

        private void MoveToCorpseLayer()
        {
            int layer = LayerMask.NameToLayer(corpseLayerName);
            if (layer < 0) return;

            foreach (Rigidbody body in _bodies)
                if (body != null) body.gameObject.layer = layer;
        }

        /// <summary>
        /// Corpses must not shove the player around or block doorways, but should still
        /// stop bullets and rest on the floor.
        /// </summary>
        private static void ConfigureLayerCollisionsOnce()
        {
            if (_layerCollisionConfigured) return;
            _layerCollisionConfigured = true;

            int corpse = LayerMask.NameToLayer("Corpse");
            int player = LayerMask.NameToLayer("Player");
            int enemy = LayerMask.NameToLayer("Enemy");

            if (corpse < 0) return;

            if (player >= 0) Physics.IgnoreLayerCollision(corpse, player, true);
            if (enemy >= 0) Physics.IgnoreLayerCollision(corpse, enemy, true);
        }
    }
}
