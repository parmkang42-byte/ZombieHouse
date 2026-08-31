using System.Collections.Generic;
using UnityEngine;
using ZombieHouse.Combat;
using ZombieHouse.Core;
using ZombieHouse.Level;

namespace ZombieHouse.Enemies
{
    public enum SeveredPart { None, Head, Arm, Leg }

    /// <summary>
    /// Takes limbs and heads off with a blade.
    ///
    /// A cut severs at the joint above whatever was hit — catch a forearm and the arm
    /// comes off at the elbow, catch an upper arm and it goes at the shoulder. The
    /// detached piece keeps its own visuals, gets a rigidbody, and is thrown clear.
    ///
    /// Severing works on the pivot subtree rather than the mesh, so the clothing, hand
    /// and boot attached to that limb leave with it instead of being left hanging.
    /// </summary>
    public class ZombieDismemberment : MonoBehaviour
    {
        [Header("Consequences")]
        [Tooltip("Losing the head is always fatal. Losing a leg drops them too.")]
        [SerializeField] private bool legLossIsFatal = true;
        [SerializeField] private float armLossDamage = 25f;

        [Header("Severed piece")]
        [SerializeField] private float pieceMass = 3f;
        [SerializeField] private float throwForce = 3.2f;
        [SerializeField] private float spinForce = 6f;
        [SerializeField] private float pieceLifetime = 0f;   // 0 = keep it on the floor forever

        [Header("Gore")]
        [SerializeField] private int stumpBloodBurst = 40;
        [SerializeField] private float stumpSprayScale = 2.2f;

        [Header("Decapitation")]
        [Tooltip("Seconds the neck keeps pumping after the head comes off.")]
        [SerializeField] private float neckFountainSeconds = 2.6f;
        [SerializeField] private float neckPulseInterval = 0.16f;
        [SerializeField] private int neckPulseParticles = 26;
        [SerializeField] private float headThrowMultiplier = 2.4f;

        private ZombieHealth _health;
        private ZombieRig _rig;
        private readonly HashSet<Transform> _severed = new HashSet<Transform>();

        private void Awake()
        {
            Initialise();
        }

        /// <summary>
        /// Resolves the components this needs. Public so the editor's dismemberment test
        /// can drive it outside play mode, where Awake never runs.
        /// </summary>
        public void Initialise()
        {
            if (_health == null) _health = GetComponent<ZombieHealth>();
            if (_rig == null) _rig = GetComponent<ZombieRig>();
        }

        private static void DestroySafely(Object target)
        {
            if (target == null) return;

            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        /// <summary>
        /// Cuts off whatever body part the given collider belongs to.
        /// Returns what came off, or None if that part cannot be severed.
        /// </summary>
        public SeveredPart Sever(Collider hitCollider, Vector3 bladeDirection, Vector3 hitPoint)
        {
            if (hitCollider == null || _health == null || !_health.IsAlive) return SeveredPart.None;

            Transform cutAt;
            SeveredPart kind = ResolveCut(hitCollider.gameObject.name, out cutAt);

            if (kind == SeveredPart.None || cutAt == null) return SeveredPart.None;
            if (!_severed.Add(cutAt)) return SeveredPart.None;   // already taken off

            DetachPiece(cutAt, bladeDirection, kind == SeveredPart.Head ? headThrowMultiplier : 1f);
            SprayStump(hitPoint, bladeDirection, kind);

            if (kind == SeveredPart.Head) OpenNeck();

            ApplyConsequence(kind, bladeDirection, hitPoint);
            return kind;
        }

        /// <summary>
        /// Maps the collider that was hit to the joint the cut goes through.
        /// Names come from ZombieFactory; keep the two in step.
        /// </summary>
        private SeveredPart ResolveCut(string partName, out Transform pivot)
        {
            pivot = null;
            if (_rig == null || _rig.Bones == null) return SeveredPart.None;

            ZombieBones bones = _rig.Bones;

            if (partName.StartsWith("Skull"))
            {
                pivot = bones.Head;
                return SeveredPart.Head;
            }

            // Forearms come off at the elbow, upper arms at the shoulder.
            if (partName.StartsWith("Forearm_L")) { pivot = bones.ElbowLeft; return SeveredPart.Arm; }
            if (partName.StartsWith("Forearm_R")) { pivot = bones.ElbowRight; return SeveredPart.Arm; }
            if (partName.StartsWith("UpperArm_L")) { pivot = bones.ShoulderLeft; return SeveredPart.Arm; }
            if (partName.StartsWith("UpperArm_R")) { pivot = bones.ShoulderRight; return SeveredPart.Arm; }

            // Shins at the knee, thighs at the hip.
            if (partName.StartsWith("Shin_L")) { pivot = bones.KneeLeft; return SeveredPart.Leg; }
            if (partName.StartsWith("Shin_R")) { pivot = bones.KneeRight; return SeveredPart.Leg; }
            if (partName.StartsWith("Thigh_L")) { pivot = bones.HipLeft; return SeveredPart.Leg; }
            if (partName.StartsWith("Thigh_R")) { pivot = bones.HipRight; return SeveredPart.Leg; }

            // The torso stays attached — cutting a zombie in half is a Stage 4 problem.
            return SeveredPart.None;
        }

        /// <summary>
        /// Opens the neck up: a ragged stump where the head was, and a couple of seconds
        /// of arterial pumping. The pulses are what sell it — one big burst reads as a
        /// puff of red, whereas a stump that keeps going as the body staggers reads as a
        /// body that has just lost its head.
        /// </summary>
        private void OpenNeck()
        {
            if (_rig == null || _rig.Bones == null || _rig.Bones.Neck == null) return;

            Transform neck = _rig.Bones.Neck;

            // A torn stump so the neck is not a clean flat disc.
            var stump = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            stump.name = "NeckStump";
            stump.transform.SetParent(neck, false);
            stump.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            stump.transform.localScale = new Vector3(0.115f, 0.075f, 0.115f);
            stump.GetComponent<MeshRenderer>().sharedMaterial = ProtoMaterials.Gore;

            var stumpCollider = stump.GetComponent<Collider>();
            if (stumpCollider != null) Destroy(stumpCollider);

            var spine = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            spine.name = "NeckSpine";
            spine.transform.SetParent(neck, false);
            spine.transform.localPosition = new Vector3(0f, 0.10f, 0f);
            spine.transform.localScale = new Vector3(0.028f, 0.045f, 0.028f);
            spine.GetComponent<MeshRenderer>().sharedMaterial = ProtoMaterials.Skin;

            var spineCollider = spine.GetComponent<Collider>();
            if (spineCollider != null) Destroy(spineCollider);

            StartCoroutine(NeckFountain(neck));
        }

        private System.Collections.IEnumerator NeckFountain(Transform neck)
        {
            var impacts = FindAnyObjectByType<Fx.ImpactSystem>();
            if (impacts == null) yield break;

            float until = Time.time + neckFountainSeconds;
            var wait = new WaitForSeconds(neckPulseInterval);

            while (Time.time < until && neck != null)
            {
                if (Core.GameManager.GameplayActive)
                {
                    // Pressure falls as it empties, so the spray shortens over time.
                    float remaining = Mathf.InverseLerp(until - neckFountainSeconds, until, Time.time);
                    float strength = 1f - remaining * 0.75f;

                    Vector3 origin = neck.position + neck.up * 0.08f;
                    Vector3 direction = (neck.up + Random.insideUnitSphere * 0.35f).normalized;

                    impacts.BurstBlood(origin, direction, Mathf.RoundToInt(neckPulseParticles * strength));
                    impacts.SpawnFleshFx(origin, direction, false, 0.9f * strength);
                }

                yield return wait;
            }
        }

        private void DetachPiece(Transform pivot, Vector3 bladeDirection, float throwMultiplier)
        {
            // Take the whole subtree: the limb, its clothing, and anything below the cut.
            pivot.SetParent(null, true);
            pivot.gameObject.name = "SeveredPart";

            // It is no longer part of a living body, so it must not take damage or count
            // as a target — otherwise you could keep hitting a severed arm on the floor.
            foreach (Hitbox hitbox in pivot.GetComponentsInChildren<Hitbox>())
                DestroySafely(hitbox);

            int corpseLayer = LayerMask.NameToLayer("Corpse");

            foreach (Collider collider in pivot.GetComponentsInChildren<Collider>())
            {
                collider.enabled = true;
                if (corpseLayer >= 0) collider.gameObject.layer = corpseLayer;
            }

            // The rigidbody goes on the PIVOT, not on the bone that happens to carry the
            // collider. Unity treats child colliders as one compound body, so the whole
            // subtree moves together — the sleeve, the hand and the boot travel with the
            // limb. Putting it on the bone instead moves only the bone and leaves the
            // clothing hanging in mid-air exactly where the limb used to be.
            var rigidbody = pivot.gameObject.GetComponent<Rigidbody>();
            if (rigidbody == null) rigidbody = pivot.gameObject.AddComponent<Rigidbody>();

            rigidbody.mass = pieceMass;
            rigidbody.isKinematic = false;
            rigidbody.useGravity = true;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rigidbody.maxDepenetrationVelocity = 1.5f;

            Vector3 throwDirection = (bladeDirection.normalized + Vector3.up * 0.55f).normalized;
            rigidbody.AddForce(throwDirection * (throwForce * throwMultiplier), ForceMode.Impulse);
            rigidbody.AddTorque(Random.insideUnitSphere * (spinForce * throwMultiplier), ForceMode.Impulse);

            if (pieceLifetime > 0f) Destroy(pivot.gameObject, pieceLifetime);
        }

        private void SprayStump(Vector3 point, Vector3 bladeDirection, SeveredPart kind)
        {
            var impacts = FindAnyObjectByType<Fx.ImpactSystem>();
            if (impacts == null) return;

            // Spray back towards the attacker, and harder for a decapitation.
            float scale = kind == SeveredPart.Head ? stumpSprayScale * 1.4f : stumpSprayScale;
            Vector3 normal = -bladeDirection.normalized;

            impacts.SpawnFleshFx(point, normal, kind == SeveredPart.Head, scale);
            impacts.SpawnFleshFx(point, Vector3.up, false, scale * 0.8f);
            impacts.BurstBlood(point, normal, stumpBloodBurst);
        }

        private void ApplyConsequence(SeveredPart kind, Vector3 bladeDirection, Vector3 hitPoint)
        {
            switch (kind)
            {
                case SeveredPart.Head:
                    // Nothing survives losing its head, however much health it had left.
                    _health.Kill(new DamageInfo(_health.Max * 10f, hitPoint, -bladeDirection,
                                                bladeDirection, gameObject, true));
                    break;

                case SeveredPart.Leg:
                    if (legLossIsFatal)
                    {
                        _health.Kill(new DamageInfo(_health.Max * 10f, hitPoint, -bladeDirection,
                                                    bladeDirection, gameObject, false));
                    }
                    break;

                case SeveredPart.Arm:
                    // Survivable, but it costs them.
                    _health.TakeDamage(new DamageInfo(armLossDamage, hitPoint, -bladeDirection,
                                                      bladeDirection, gameObject, false));
                    break;
            }
        }
    }
}
