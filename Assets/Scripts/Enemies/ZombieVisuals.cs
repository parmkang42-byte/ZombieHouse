using System;
using UnityEngine;
using ZombieHouse.Core;

namespace ZombieHouse.Enemies
{
    /// <summary>
    /// Procedural animation for the humanoid walker.
    ///
    /// The gait is deliberately wrong in the ways a walker's gait is wrong: stiff knees,
    /// a hunched spine, a lolling head, arms hanging until it sees you, and an uneven
    /// limp that differs per individual. The cycle advances with distance travelled, not
    /// time, so the feet never skate whatever speed the AI is moving at.
    ///
    /// Rotation conventions on this rig (limbs hang along -Y from their pivot):
    ///   hip / shoulder  +X swings the limb backward, -X swings it forward
    ///   knee / elbow    +X bends the joint the way a real one bends
    ///   spine           +X hunches forward
    ///
    /// Replacing this with an Animator: drive it from ZombieAI's State, PlanarSpeed and
    /// AttackStarted, and delete this component. Nothing else touches these transforms.
    /// </summary>
    public class ZombieVisuals : MonoBehaviour
    {
        [Header("Walk cycle")]
        [Tooltip("Stride cycles per metre travelled — higher means shorter, faster steps.")]
        [SerializeField] private float strideCyclesPerMetre = 0.62f;
        [SerializeField] private float legSwingDegrees = 26f;
        [SerializeField] private float kneeBaseBend = 9f;
        [SerializeField] private float kneeSwingBend = 26f;
        [SerializeField] private float bobHeight = 0.055f;
        [SerializeField] private float lurchRollDegrees = 5f;
        [SerializeField] private float idleSwayDegrees = 2f;

        [Header("Posture")]
        [SerializeField] private float hunchDegrees = 17f;
        [SerializeField] private float hunchWhenHunting = 26f;
        [SerializeField] private float armHangDegrees = 8f;
        [SerializeField] private float armReachDegrees = 68f;
        [SerializeField] private float armSwayDegrees = 9f;
        [SerializeField] private float elbowBaseBend = 22f;
        [SerializeField] private float elbowReachBend = 34f;
        [SerializeField] private float headLollDegrees = 6f;
        [SerializeField] private float postureLerpSpeed = 3.5f;

        [Header("Limp")]
        [Tooltip("How much a limp shortens the bad leg's stride and stiffens its knee.")]
        [SerializeField] private float limpStrideLoss = 0.55f;
        [SerializeField] private float limpDrop = 0.045f;

        [Header("Attack")]
        [SerializeField] private float attackRaiseDegrees = 38f;
        [SerializeField] private float attackStrikeDegrees = 104f;
        [SerializeField] private float attackLungeDistance = 0.24f;
        [SerializeField] private float attackRecoverySeconds = 0.3f;

        [Header("Reactions")]
        [SerializeField] private float hitJoltDegrees = 13f;
        [SerializeField] private float hitJoltSeconds = 0.22f;

        /// <summary>Fired each time a foot plants — audio hangs off this.</summary>
        public event Action Footstep;

        private ZombieAI _ai;
        private ZombieHealth _health;
        private ZombieAppearance _appearance;
        private ZombieBones _bones;

        private float _stridePhase;
        private float _previousStrideSign = 1f;
        private float _reach;
        private float _hunch;

        private float _attackTime = -1f;
        private float _attackLength = 1f;
        private float _hitTime = -1f;
        private bool _dead;

        [Header("Quadruped")]
        [Tooltip("Four legs on the ground: front and rear pairs swing in diagonal pairs " +
                 "and the body stays level instead of standing upright.")]
        [SerializeField] private bool quadruped;

        /// <summary>Set by the bear factory before Awake.</summary>
        public void SetQuadruped(bool value) { quadruped = value; }

        private float _limpSeverity;
        private int _limpSide = 1;
        private float _headTilt;
        private float _headTurn;
        private float _shoulderDroop;
        private float _pace = 1f;

        private void Awake()
        {
            _ai = GetComponent<ZombieAI>();
            _health = GetComponent<ZombieHealth>();
            _appearance = GetComponent<ZombieAppearance>();

            var rig = GetComponent<ZombieRig>();
            if (rig != null) _bones = rig.Bones;

            // Gait comes from the type: a brute lurches heavily and takes long strides,
            // a runner takes short quick ones. It should be readable at a distance.
            var profile = GetComponent<ZombieProfile>();
            if (profile != null && profile.Archetype != null)
            {
                strideCyclesPerMetre = profile.Archetype.StrideCyclesPerMetre;
                lurchRollDegrees = profile.Archetype.LurchDegrees;
            }

            if (_appearance != null)
            {
                _limpSeverity = _appearance.LimpSeverity;
                _limpSide = _appearance.LimpSide;
                _headTilt = _appearance.HeadTiltDegrees;
                _headTurn = _appearance.HeadTurnDegrees;
                _shoulderDroop = _appearance.ShoulderDroopDegrees;
                _pace = _appearance.PaceMultiplier;
            }

            _reach = armHangDegrees;
            _hunch = hunchDegrees;

            // Desynchronise the horde — identical phase makes a crowd look like one puppet.
            _stridePhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        }

        private void OnEnable()
        {
            if (_ai != null)
            {
                _ai.AttackStarted += OnAttackStarted;
                _ai.StateChanged += OnStateChanged;
            }
            if (_health != null)
            {
                _health.Damaged += OnDamaged;
                _health.Died += OnDied;
            }
        }

        private void OnDisable()
        {
            if (_ai != null)
            {
                _ai.AttackStarted -= OnAttackStarted;
                _ai.StateChanged -= OnStateChanged;
            }
            if (_health != null)
            {
                _health.Damaged -= OnDamaged;
                _health.Died -= OnDied;
            }
        }

        private void OnAttackStarted()
        {
            _attackTime = 0f;
            _attackLength = (_ai != null ? _ai.AttackWindup : 0.45f) + attackRecoverySeconds;
        }

        private void OnStateChanged(ZombieState state)
        {
            if (state == ZombieState.Dead) _dead = true;
        }

        private void OnDamaged(DamageInfo info) { _hitTime = 0f; }

        private void OnDied(DamageInfo info) { _dead = true; }

        private void Update()
        {
            if (_dead || _bones == null || !_bones.IsComplete) return;
            if (!GameManager.GameplayActive) return;

            float dt = Time.deltaTime;
            float speed = _ai != null ? _ai.PlanarSpeed : 0f;
            bool hunting = _ai != null && (_ai.State == ZombieState.Chase || _ai.State == ZombieState.Attack);

            AdvanceStride(speed, dt);

            float targetReach = hunting ? armReachDegrees : armHangDegrees;
            float targetHunch = hunting ? hunchWhenHunting : hunchDegrees;
            _reach = Mathf.Lerp(_reach, targetReach, postureLerpSpeed * dt);
            _hunch = Mathf.Lerp(_hunch, targetHunch, postureLerpSpeed * dt);

            if (_attackTime >= 0f) _attackTime += dt;
            if (_hitTime >= 0f) _hitTime += dt;

            ApplyPose(speed);
        }

        private void AdvanceStride(float speed, float dt)
        {
            if (speed > 0.05f)
                _stridePhase += speed * dt * strideCyclesPerMetre * _pace * Mathf.PI * 2f;
            else
                _stridePhase += dt * 0.9f;   // idle sway rather than a frozen statue

            if (_stridePhase > Mathf.PI * 2f) _stridePhase -= Mathf.PI * 2f;

            float sign = Mathf.Sign(Mathf.Sin(_stridePhase));
            if (speed > 0.4f && sign != _previousStrideSign)
            {
                Footstep?.Invoke();
                _previousStrideSign = sign;
            }
            else if (speed <= 0.4f)
            {
                _previousStrideSign = sign;
            }
        }

        private void ApplyPose(float speed)
        {
            if (quadruped)
            {
                ApplyQuadrupedPose(speed);
                return;
            }

            float swing = Mathf.Sin(_stridePhase);
            float moving = Mathf.Clamp01(speed / 2.2f);

            ResolveAttack(out float attackBlend, out float armStrike, out float lunge);
            float jolt = ResolveJolt();

            // --- legs --------------------------------------------------------
            // The limping leg takes a shorter stride and keeps a stiffer knee.
            float leftLimp = _limpSide < 0 ? _limpSeverity : 0f;
            float rightLimp = _limpSide > 0 ? _limpSeverity : 0f;

            PoseLeg(_bones.HipLeft, _bones.KneeLeft, swing, moving, leftLimp);
            PoseLeg(_bones.HipRight, _bones.KneeRight, -swing, moving, rightLimp);

            // --- body --------------------------------------------------------
            // Dip on each footfall, plus an extra lurch when the bad leg takes weight.
            float bob = -Mathf.Abs(Mathf.Cos(_stridePhase)) * bobHeight * moving;
            float limpDip = _limpSeverity * limpDrop * Mathf.Clamp01(swing * _limpSide) * moving;

            float roll = swing * lurchRollDegrees * moving
                         + Mathf.Sin(_stridePhase * 0.5f) * idleSwayDegrees * (1f - moving)
                         + _limpSeverity * 3.5f * _limpSide * moving;

            _bones.Rig.localPosition = new Vector3(0f, bob - limpDip, lunge);
            _bones.Rig.localRotation = Quaternion.Euler(jolt, 0f, roll);

            // --- spine -------------------------------------------------------
            if (_bones.Spine != null)
                _bones.Spine.localRotation = Quaternion.Euler(_hunch + attackBlend * 10f, 0f, -roll * 0.3f);

            // --- head --------------------------------------------------------
            if (_bones.Neck != null)
            {
                float loll = -swing * headLollDegrees * moving;
                _bones.Neck.localRotation = Quaternion.Euler(
                    -_hunch * 0.55f,
                    _headTurn,
                    _headTilt + loll);
            }

            // --- arms --------------------------------------------------------
            float armBase = _reach + armStrike;
            float sway = swing * armSwayDegrees * moving;
            float elbow = Mathf.Lerp(elbowBaseBend, elbowReachBend, Mathf.InverseLerp(armHangDegrees, armReachDegrees, _reach));

            PoseArm(_bones.ShoulderLeft, _bones.ElbowLeft, armBase, -sway, elbow, _shoulderDroop);
            PoseArm(_bones.ShoulderRight, _bones.ElbowRight, armBase, sway, elbow, -_shoulderDroop * 0.4f);
        }

        /// <summary>
        /// A four-legged gait. The legs move in diagonal pairs — front-left with rear-right
        /// — which is what a walking quadruped actually does and what stops it looking like
        /// a pantomime horse. The body stays level and the head leads, dipping as it runs.
        /// </summary>
        private void ApplyQuadrupedPose(float speed)
        {
            float swing = Mathf.Sin(_stridePhase);
            float opposite = Mathf.Sin(_stridePhase + Mathf.PI);
            float moving = Mathf.Clamp01(speed / 3f);

            ResolveAttack(out float attackBlend, out float armStrike, out float lunge);
            float jolt = ResolveJolt();

            float stride = legSwingDegrees * 1.3f * Mathf.Max(0.1f, moving);

            // Diagonal pairs: front-left swings with rear-right.
            PoseQuadLeg(_bones.ShoulderLeft, _bones.ElbowLeft, swing * stride, moving);
            PoseQuadLeg(_bones.ShoulderRight, _bones.ElbowRight, opposite * stride, moving);
            PoseQuadLeg(_bones.HipLeft, _bones.KneeLeft, opposite * stride, moving);
            PoseQuadLeg(_bones.HipRight, _bones.KneeRight, swing * stride, moving);

            // The whole animal rises and falls on the stride, and rolls with it.
            float bob = -Mathf.Abs(Mathf.Cos(_stridePhase)) * bobHeight * 1.6f * moving;
            float roll = swing * lurchRollDegrees * 0.8f * moving;

            _bones.Rig.localPosition = new Vector3(0f, bob, lunge);
            _bones.Rig.localRotation = Quaternion.Euler(jolt, 0f, roll);

            // Spine level; it lowers its shoulders as it builds speed.
            if (_bones.Spine != null)
                _bones.Spine.localRotation = Quaternion.Euler(moving * 5f + attackBlend * 6f, 0f, -roll * 0.4f);

            // The head leads and swings side to side as it hunts.
            if (_bones.Neck != null)
            {
                float sweep = swing * 5f * moving;
                float lunge2 = armStrike * 0.25f;
                _bones.Neck.localRotation = Quaternion.Euler(-lunge2 + moving * 4f, sweep + _headTurn * 0.4f, _headTilt * 0.3f);
            }
        }

        private void PoseQuadLeg(Transform upper, Transform lower, float angle, float moving)
        {
            if (upper == null) return;

            upper.localRotation = Quaternion.Euler(angle, 0f, 0f);

            // A little knee flex on the backswing keeps the legs from looking like sticks.
            if (lower != null)
                lower.localRotation = Quaternion.Euler(kneeBaseBend + Mathf.Max(0f, angle) * 0.45f * moving, 0f, 0f);
        }

        private void PoseLeg(Transform hip, Transform knee, float swing, float moving, float limp)
        {
            if (hip == null || knee == null) return;

            float stride = legSwingDegrees * Mathf.Max(0.12f, moving) * (1f - limp * limpStrideLoss);
            hip.localRotation = Quaternion.Euler(swing * stride, 0f, 0f);

            // Knee bends as the leg travels backward; a limp locks it nearly straight.
            float bend = kneeBaseBend + Mathf.Max(0f, swing) * kneeSwingBend * moving;
            bend *= 1f - limp * 0.7f;
            knee.localRotation = Quaternion.Euler(bend, 0f, 0f);
        }

        private void PoseArm(Transform shoulder, Transform elbow, float reach, float sway, float elbowBend, float droop)
        {
            if (shoulder == null) return;

            // Negative X on the shoulder swings the arm forward.
            shoulder.localRotation = Quaternion.Euler(-(reach + sway), 0f, droop);
            if (elbow != null) elbow.localRotation = Quaternion.Euler(elbowBend, 0f, 0f);
        }

        private void ResolveAttack(out float blend, out float armStrike, out float lunge)
        {
            blend = 0f;
            armStrike = 0f;
            lunge = 0f;

            if (_attackTime < 0f) return;

            if (_attackTime > _attackLength)
            {
                _attackTime = -1f;
                return;
            }

            float windup = _ai != null ? _ai.AttackWindup : 0.45f;

            if (_attackTime < windup)
            {
                // Coil: arms drag back, weight shifts away.
                float t = Mathf.Clamp01(_attackTime / Mathf.Max(0.01f, windup));
                blend = Mathf.SmoothStep(0f, 1f, t);
                armStrike = -attackRaiseDegrees * blend;
                lunge = -attackLungeDistance * 0.3f * blend;
            }
            else
            {
                // Strike: everything forward, then settle.
                float t = Mathf.Clamp01((_attackTime - windup) / Mathf.Max(0.01f, attackRecoverySeconds));
                blend = 1f - t;
                armStrike = Mathf.Lerp(attackStrikeDegrees, 0f, Mathf.SmoothStep(0f, 1f, t));
                lunge = attackLungeDistance * (1f - t);
            }
        }

        private float ResolveJolt()
        {
            if (_hitTime < 0f) return 0f;

            if (_hitTime > hitJoltSeconds)
            {
                _hitTime = -1f;
                return 0f;
            }

            float t = 1f - _hitTime / hitJoltSeconds;
            return Mathf.Sin(t * Mathf.PI * 3f) * hitJoltDegrees * t;
        }

        /// <summary>Called by the ragdoll so animation stops before physics takes the body.</summary>
        public void StopAnimating()
        {
            _dead = true;
        }
    }
}
