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

        [Header("Carriage")]
        [Tooltip("How far the hips rotate about the spine as each leg swings through. " +
                 "The single biggest difference between a walking body and a puppet.")]
        [SerializeField] private float pelvisTwistDegrees = 7f;

        [Tooltip("How much of that the shoulders give back the other way. 1 would be a " +
                 "perfectly counter-rotating athlete; a walker is stiffer than that.")]
        [SerializeField] private float shoulderCounterRotation = 0.75f;

        [Tooltip("How far the body shifts sideways over the leg carrying it, in metres.")]
        [SerializeField] private float weightShift = 0.035f;

        [Tooltip("How uneven the step is. 0 is a metronome; higher spends less time on " +
                 "the collapse and more on the drag.")]
        [Range(0f, 0.8f)]
        [SerializeField] private float strideAsymmetry = 0.38f;

        [Tooltip("How fast the head catches up with the body under it.")]
        [SerializeField] private float headFollowSpeed = 8f;

        [Tooltip("How much of the lag actually shows. 0 bolts the head to the spine.")]
        [Range(0f, 1f)]
        [SerializeField] private float headLagStrength = 0.8f;

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

        [Header("Foot placement")]
        [Tooltip("Put the feet on the floor that is actually there, rather than on the " +
                 "floor the walk cycle assumes is there.")]
        [SerializeField] private bool footPlacement = true;

        [Tooltip("Past this, foot placement is skipped. Two raycasts per walker per frame " +
                 "is not free, and nobody can see a foot at this range anyway.")]
        [SerializeField] private float footPlacementDistance = 25f;

        [SerializeField] private float footRayAbove = 0.7f;
        [SerializeField] private float footRayBelow = 1.4f;

        [Tooltip("How fast a foot and the hips chase the ground. Instant looks like a " +
                 "glitch when a foot crosses a step edge.")]
        [SerializeField] private float footFollowSpeed = 14f;

        [Tooltip("How far the hips may sink so the lower foot can reach. Past this the " +
                 "walker would be doing the splits, so the foot gives up instead.")]
        [SerializeField] private float maxHipDrop = 0.4f;

        [SerializeField] private float maxFootPitchDegrees = 38f;

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

        private Transform _footLeft;
        private Transform _footRight;
        private float _thighLength;
        private float _shinLength;
        private float _shinLean;
        private float _footSoleDrop;
        private int _groundMask;
        private float _hipDrop;
        private float _footLift;
        private float _footRise;
        private float _footPitchLeft;
        private float _footPitchRight;
        private Camera _eye;

        private float _limpSeverity;
        private int _limpSide = 1;
        private float _headTrail;
        private float _headTilt;
        private float _headTurn;
        private float _shoulderDroop;
        private float _pace = 1f;

        private void Awake()
        {
            Initialise();
        }

        /// <summary>
        /// Wires up the rig and rolls this individual's gait.
        ///
        /// Public because Awake does not run in edit mode, and a test that wants to know
        /// where this walker puts its feet on a staircase has to be able to pose one.
        /// </summary>
        public void Initialise()
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

            CacheLegs();
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
            if (!GameManager.GameplayActive) return;
            Tick(Time.deltaTime);
        }

        /// <summary>
        /// One frame of pose. Public for the same reason <see cref="Initialise"/> is:
        /// nothing steps a MonoBehaviour in edit mode, so a test has to drive it.
        /// </summary>
        public void Tick(float dt)
        {
            Tick(dt, _ai != null ? _ai.PlanarSpeed : 0f);
        }

        /// <summary>
        /// One frame of pose at a stated speed.
        ///
        /// The speed normally comes from the NavMeshAgent, and an agent cannot move in
        /// edit mode — there is no NavMesh under it and nothing steps the simulation — so
        /// a test of the walk cycle would otherwise only ever see a walker standing
        /// perfectly still. This is the seam that lets one watch the thing actually walk.
        /// </summary>
        public void Tick(float dt, float planarSpeed)
        {
            if (_dead || _bones == null || !_bones.IsComplete) return;

            float speed = planarSpeed;
            bool hunting = _ai != null && (_ai.State == ZombieState.Chase || _ai.State == ZombieState.Attack);

            AdvanceStride(speed, dt);

            float targetReach = hunting ? armReachDegrees : armHangDegrees;
            float targetHunch = hunting ? hunchWhenHunting : hunchDegrees;
            _reach = Mathf.Lerp(_reach, targetReach, postureLerpSpeed * dt);
            _hunch = Mathf.Lerp(_hunch, targetHunch, postureLerpSpeed * dt);

            if (_attackTime >= 0f) _attackTime += dt;
            if (_hitTime >= 0f) _hitTime += dt;

            ApplyPose(speed, dt);
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

        private void ApplyPose(float speed, float dt)
        {
            if (quadruped)
            {
                ApplyQuadrupedPose(speed);
                return;
            }

            // The step is not a metronome, and a dead one least of all. Warping the phase
            // before it is read spends less time on the collapse and more on the drag that
            // follows it, so the two halves of a stride take visibly different lengths of
            // time. A pure sine gives a leg that sweeps like a pendulum, which is exactly
            // what a mechanism does and exactly what a body does not.
            //
            // The warp is monotonic and fixes both ends, so sin(warped) crosses zero at
            // the same instants sin(phase) does. That matters: footsteps are dispatched
            // off the sign of the unwarped phase in AdvanceStride, and the feet would
            // otherwise land audibly before or after they land visibly.
            float warped = _stridePhase + Mathf.Sin(_stridePhase) * strideAsymmetry;

            float swing = Mathf.Sin(warped);
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
            float bob = -Mathf.Abs(Mathf.Cos(warped)) * bobHeight * moving;
            float limpDip = _limpSeverity * limpDrop * Mathf.Clamp01(swing * _limpSide) * moving;

            float roll = swing * lurchRollDegrees * moving
                         + Mathf.Sin(_stridePhase * 0.5f) * idleSwayDegrees * (1f - moving)
                         + _limpSeverity * 3.5f * _limpSide * moving;

            // Counter-rotation, and this is the one that matters most.
            //
            // A leg cannot swing forward without the hip on that side coming with it, and
            // the shoulders answer by turning the other way so the body does not corkscrew
            // off its heading. Without it a walker is a set of limbs hinged to a post that
            // never turns, which is what this rig was: every rotation in it was pitch or
            // roll, and Y was zero from the pelvis to the skull.
            //
            // The walker gives back less than a person does — 0.75 rather than 1 — because
            // the counter-turn is what a live spine does to stay economical, and economy is
            // the first thing to go.
            float pelvisYaw = swing * pelvisTwistDegrees * moving;
            float shoulderYaw = -pelvisYaw * shoulderCounterRotation;

            // Weight over the leg carrying it. Small — three centimetres — because it is
            // read as a shift of mass rather than seen as a sidestep, and past about five
            // it stops looking like walking and starts looking like staggering.
            float lateral = -swing * weightShift * moving;

            _bones.Rig.localPosition = new Vector3(lateral, bob - limpDip, lunge);
            _bones.Rig.localRotation = Quaternion.Euler(jolt, pelvisYaw, roll);

            // --- spine -------------------------------------------------------
            // The spine hangs off the rig, so its local yaw has to carry the whole
            // difference between where the hips are pointing and where the shoulders
            // should be. Writing shoulderYaw straight in here would only reduce the
            // pelvis's turn, never reverse it.
            if (_bones.Spine != null)
                _bones.Spine.localRotation = Quaternion.Euler(
                    _hunch + attackBlend * 10f,
                    shoulderYaw - pelvisYaw,
                    -roll * 0.3f);

            // --- head --------------------------------------------------------
            // The head trails what is underneath it. A skull rigidly bolted to a turning
            // spine moves in perfect lockstep with it, and perfect lockstep is the single
            // clearest signal that a thing has no mass: everything arrives at once.
            //
            // So a damped follower chases the yaw the neck inherits, and whatever it has
            // not caught up on yet is written back as counter-rotation. The head ends up
            // where the body was a moment ago, which is where a head actually is.
            if (_bones.Neck != null)
            {
                float carried = shoulderYaw;
                _headTrail = Mathf.Lerp(_headTrail, carried,
                                        1f - Mathf.Exp(-headFollowSpeed * dt));

                float lag = (_headTrail - carried) * headLagStrength;
                float loll = -swing * headLollDegrees * moving;

                _bones.Neck.localRotation = Quaternion.Euler(
                    -_hunch * 0.55f,
                    _headTurn + lag,
                    _headTilt + loll);
            }

            // --- arms --------------------------------------------------------
            float armBase = _reach + armStrike;
            float sway = swing * armSwayDegrees * moving;
            float elbow = Mathf.Lerp(elbowBaseBend, elbowReachBend, Mathf.InverseLerp(armHangDegrees, armReachDegrees, _reach));

            PoseArm(_bones.ShoulderLeft, _bones.ElbowLeft, armBase, -sway, elbow, _shoulderDroop);
            PoseArm(_bones.ShoulderRight, _bones.ElbowRight, armBase, sway, elbow, -_shoulderDroop * 0.4f);

            // Last, because it corrects what everything above just decided.
            PlaceFeet(dt);
        }

        // ------------------------------------------------------------ foot placement

        /// <summary>
        /// Puts the feet on the floor that is there rather than the floor the walk cycle
        /// assumes is there.
        ///
        /// The walk cycle is a pair of sine waves. On flat ground that is fine, and it is
        /// what every walker in this game has been doing; on the ship ladders, the
        /// pyramid ramps and the house stairs it means a foot passes through the tread and
        /// comes out below it. A body whose feet are inside the floor does not read as
        /// heavy, it reads as a decal - and the shadows turned on in the previous commit
        /// make that worse rather than better, because now there is a shadow sitting on
        /// the step with no foot on it.
        ///
        /// TWO PASSES, AND THE ORDER MATTERS. A foot that cannot reach its step is not
        /// fixed by stretching the leg; it is fixed by the hips coming down, which is what
        /// a person does. So the ground under both feet is measured first, the hips sink by
        /// however much the lower foot is short, and only then is each leg solved. Solving
        /// the legs first and dropping the hips afterwards moves both feet again and undoes
        /// the solve.
        /// </summary>
        private void PlaceFeet(float dt)
        {
            if (!footPlacement || _footLeft == null || _footRight == null) return;
            if (_thighLength <= 0f || _shinLength <= 0f) return;

            // Two raycasts a frame each is cheap; forty walkers' worth is not, and past
            // about twenty-five metres nobody can tell where a foot is anyway.
            if (_eye == null) _eye = Camera.main;
            if (_eye != null)
            {
                float far = footPlacementDistance * footPlacementDistance;
                if ((_eye.transform.position - transform.position).sqrMagnitude > far)
                {
                    // Unwind rather than freeze: a walker that leaves range mid-stride and
                    // keeps a 30 cm hip drop for the rest of its life is a crouching zombie
                    // with no explanation.
                    Relax(dt);
                    return;
                }
            }

            float leftGround, rightGround, leftPitch, rightPitch;
            bool leftFound = Probe(_footLeft, out leftGround, out leftPitch);
            bool rightFound = Probe(_footRight, out rightGround, out rightPitch);

            if (!leftFound && !rightFound) { Relax(dt); return; }

            float damp = 1f - Mathf.Exp(-footFollowSpeed * dt);

            // --- the hips ------------------------------------------------------
            // How far short is each foot of its own ground? Negative means the ground is
            // below the foot, which is the case that needs the hips.
            float leftShort = leftFound ? leftGround - FootSole(_footLeft) : 0f;
            float rightShort = rightFound ? rightGround - FootSole(_footRight) : 0f;

            float drop = Mathf.Clamp(Mathf.Min(leftShort, rightShort), -maxHipDrop, 0f);
            _hipDrop = Mathf.Lerp(_hipDrop, drop, damp);

            // The rig's Y was set by the walk cycle's bob a few lines ago; this rides on
            // top of it rather than replacing it, so the walker still dips as it steps.
            Vector3 rigLocal = _bones.Rig.localPosition;
            float scale = _bones.Rig.lossyScale.y;
            _bones.Rig.localPosition = new Vector3(
                rigLocal.x,
                rigLocal.y + (scale > 1e-4f ? _hipDrop / scale : 0f),
                rigLocal.z);

            // --- the legs ------------------------------------------------------
            // The leg first, then the ankle: levelling the foot needs the knee's final
            // rotation, and the knee is what the solve is about to change.
            if (leftFound)
            {
                SolveLeg(_bones.HipLeft, _bones.KneeLeft, _footLeft, leftGround);
                _footPitchLeft = Mathf.Lerp(_footPitchLeft, leftPitch, damp);
                LevelFoot(_bones.KneeLeft, _footLeft, _footPitchLeft);
            }

            if (rightFound)
            {
                SolveLeg(_bones.HipRight, _bones.KneeRight, _footRight, rightGround);
                _footPitchRight = Mathf.Lerp(_footPitchRight, rightPitch, damp);
                LevelFoot(_bones.KneeRight, _footRight, _footPitchRight);
            }
        }

        /// <summary>
        /// Finds the floor under one foot, and the pitch that would put the sole flat on
        /// it. The ray starts above the foot so that a foot already buried in a step still
        /// finds the surface it should have been standing on.
        /// </summary>
        private bool Probe(Transform foot, out float groundY, out float pitchDegrees)
        {
            groundY = 0f;
            pitchDegrees = 0f;

            Vector3 from = foot.position + Vector3.up * footRayAbove;
            RaycastHit hit;
            if (!Physics.Raycast(from, Vector3.down, out hit,
                                 footRayAbove + footRayBelow, _groundMask,
                                 QueryTriggerInteraction.Ignore))
                return false;

            groundY = hit.point.y;

            // The slope, in the walker's own facing: a ramp taken head-on pitches the
            // foot, the same ramp crossed sideways does not.
            Vector3 slope = transform.InverseTransformDirection(hit.normal);
            pitchDegrees = Mathf.Clamp(Mathf.Atan2(slope.z, slope.y) * Mathf.Rad2Deg,
                                       -maxFootPitchDegrees, maxFootPitchDegrees);
            return true;
        }

        /// <summary>
        /// Two-bone IK, solved in the plane the leg already swings in.
        ///
        /// This rig only ever rotates a hip and a knee about X, so the whole leg lives in
        /// the rig's own YZ plane, and the general three-dimensional solve - with its pole
        /// vector and its ambiguity about which way the knee should face - simply does not
        /// arise. What is left is the law of cosines twice, and a knee that can only bend
        /// the way a knee bends because that is the only direction the rig can express.
        /// </summary>
        private void SolveLeg(Transform hip, Transform knee, Transform foot, float groundY)
        {
            Transform rig = _bones.Rig;

            // Aim the foot pivot, not the sole. The gap between them is half the foot's
            // thickness and nothing else, now that the ankle keeps the foot level - which
            // matters because it makes the target a fixed point rather than one that moves
            // every time the knee does.
            Vector3 target = foot.position;
            target.y = groundY + _footSoleDrop * rig.lossyScale.y;

            Vector3 hipLocal = rig.InverseTransformPoint(hip.position);
            Vector3 targetLocal = rig.InverseTransformPoint(target);

            float forward = targetLocal.z - hipLocal.z;
            float up = targetLocal.y - hipLocal.y;

            float a = _thighLength;
            float b = _shinLength;

            // Clamped just inside full extension, because a leg that locks straight
            // reads as a stilt. Two millimetres rather than the ten this started with:
            // the margin is a floor under how accurately a foot can be placed, and at a
            // centimetre it was the largest single error left in the standing pose. The
            // acos calls below are guarded on their own, so this does not have to be
            // generous to be safe.
            float reach = Mathf.Clamp(Mathf.Sqrt(forward * forward + up * up),
                                      Mathf.Abs(a - b) + 0.002f, a + b - 0.002f);

            float kneeInterior = Mathf.Acos(
                Mathf.Clamp((a * a + b * b - reach * reach) / (2f * a * b), -1f, 1f));
            float hipOffset = Mathf.Acos(
                Mathf.Clamp((a * a + reach * reach - b * b) / (2f * a * reach), -1f, 1f));

            // Angle of the hip-to-target line away from straight down, positive forward.
            float toTarget = Mathf.Atan2(forward, -up);

            // The thigh leads the target line by the offset, which puts the knee in front
            // and lets it fold backwards - the only way a leg is allowed to bend.
            hip.localRotation = Quaternion.Euler(-(toTarget + hipOffset) * Mathf.Rad2Deg, 0f, 0f);

            // Plus the shin's own lean. The law of cosines above solves a chain of two
            // straight segments, and the lower one is not straight: the ankle sits 6 cm
            // forward of the knee as well as below it, because that is where an ankle is.
            // Ignoring that tilts the whole shin by about eight degrees, which over its
            // length put every sole six centimetres under the floor - a solve that looks
            // like it is working and is quietly wrong by the width of a step.
            knee.localRotation = Quaternion.Euler(
                180f - kneeInterior * Mathf.Rad2Deg + _shinLean * Mathf.Rad2Deg, 0f, 0f);
        }

        /// <summary>
        /// World Y of the underside of a foot, which is the part that has to touch.
        ///
        /// Computed from the foot's own half-height rather than read off its renderer
        /// bounds, and that is not a matter of taste. A bounding box is axis-aligned, so
        /// the moment the foot tilts it gets *taller* - a 24 cm foot pitched twenty degrees
        /// reports a box half again as deep as the foot is. Feeding that back in as "how
        /// far the sole is below the pivot" makes the target move whenever the pose moves,
        /// and the solve chases a number it is itself changing.
        /// </summary>
        private float FootSole(Transform foot)
        {
            return foot.position.y - _footSoleDrop * _bones.Rig.lossyScale.y;
        }

        /// <summary>
        /// The ankle the rig does not have.
        ///
        /// The foot is parented to the knee, so without this it rotates with the shin: a
        /// bent knee points the toe like a ballerina and drives the front of the foot
        /// through the floor. That is not a small effect - it was most of the residual
        /// error that made the first version of this solve look almost right.
        ///
        /// So the foot is given a world-space orientation instead of an inherited one:
        /// level with the walker's own facing, pitched to whatever it is standing on.
        /// </summary>
        private void LevelFoot(Transform knee, Transform foot, float pitchDegrees)
        {
            Quaternion wanted = transform.rotation * Quaternion.Euler(pitchDegrees, 0f, 0f);
            foot.localRotation = Quaternion.Inverse(knee.rotation) * wanted;
        }

        /// <summary>Bleeds the correction away, for a walker out of range or off the floor.</summary>
        private void Relax(float dt)
        {
            float damp = 1f - Mathf.Exp(-footFollowSpeed * dt);
            _hipDrop = Mathf.Lerp(_hipDrop, 0f, damp);
            _footPitchLeft = Mathf.Lerp(_footPitchLeft, 0f, damp);
            _footPitchRight = Mathf.Lerp(_footPitchRight, 0f, damp);
        }

        /// <summary>
        /// Measures the leg once, in the rig's own units.
        ///
        /// The lengths are read off the transforms rather than copied from the factory's
        /// constants, so the two cannot drift apart - and so this keeps working for the
        /// sailors, the kids and the mascots, which are the same rig at other sizes.
        /// </summary>
        private void CacheLegs()
        {
            _groundMask = LayerMask.GetMask("Default");

            if (_bones == null || _bones.KneeLeft == null || _bones.KneeRight == null) return;

            _footLeft = _bones.KneeLeft.Find("Foot_L");
            _footRight = _bones.KneeRight.Find("Foot_R");

            _thighLength = _bones.KneeLeft.localPosition.magnitude;
            _shinLength = _footLeft != null ? _footLeft.localPosition.magnitude : 0f;

            // How far the ankle leans forward of straight-down, in the knee's own frame.
            _shinLean = _footLeft != null
                ? Mathf.Atan2(_footLeft.localPosition.z, -_footLeft.localPosition.y)
                : 0f;

            // Half the foot's thickness: how far the sole sits below the foot's pivot once
            // the ankle is holding it level.
            _footSoleDrop = _footLeft != null ? _footLeft.localScale.y * 0.5f : 0f;
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
