using UnityEngine;
using ZombieHouse.Core;

namespace ZombieHouse.Enemies
{
    /// <summary>
    /// Drives the scarab's six legs with a real insect gait, on the floor, up a wall and
    /// across the ceiling alike.
    ///
    /// Two things make this different from the quadruped animation in
    /// <see cref="ZombieVisuals"/>, and both were bugs you could see:
    ///
    /// **It measures displacement, not agent speed.** ZombieVisuals advances its stride
    /// from the NavMeshAgent's velocity — and the agent is switched *off* while the scarab
    /// is climbing, because the crawler is moving the transform by hand. So the legs froze
    /// solid the moment one started up a wall, and a rigid beetle slid up the stone. Taking
    /// the phase from how far the transform actually moved works no matter what is doing
    /// the moving.
    ///
    /// **It is an alternating tripod.** Insects do not walk in diagonal pairs like a horse:
    /// front-left, middle-right and rear-left swing together while the other three hold the
    /// ground, then they swap. Three legs down at all times is what makes a beetle look
    /// stable and busy rather than like a dog with too many legs.
    ///
    /// Runs in LateUpdate with a high execution order so it has the last word on the legs;
    /// ZombieVisuals still owns the body, the lurch and the attack.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class ScarabGait : MonoBehaviour
    {
        [Tooltip("Stride cycles per metre travelled. Higher is a busier, twitchier walk.")]
        [SerializeField] private float cyclesPerMetre = 1.15f;

        [Tooltip("Degrees each leg swings fore and aft.")]
        [SerializeField] private float swingDegrees = 26f;

        [Tooltip("Degrees the lower joint lifts as the leg comes forward.")]
        [SerializeField] private float liftDegrees = 18f;

        [Tooltip("Idle twitch, so a stationary scarab is never completely still.")]
        [SerializeField] private float idleTwitchDegrees = 2.5f;
        [SerializeField] private float idleTwitchSpeed = 1.8f;

        private ZombieRig _rig;
        private Transform _midLeft;
        private Transform _midRight;

        private Quaternion[] _rest;
        private Transform[] _upper;
        private Transform[] _lower;
        private float[] _phaseOffset;

        private Vector3 _lastPosition;
        private float _phase;
        private float _movingBlend;

        private void Awake()
        {
            _rig = GetComponent<ZombieRig>();
            if (_rig == null || _rig.Bones == null) return;

            ZombieBones bones = _rig.Bones;

            if (bones.Spine != null)
            {
                _midLeft = bones.Spine.Find("MidLeg_-1");
                _midRight = bones.Spine.Find("MidLeg_1");
            }

            // Six legs, in tripod order. The two tripods are (FL, MR, RL) and (FR, ML, RR),
            // so the offsets alternate 0, π, 0, π, 0, π around the body.
            _upper = new[]
            {
                bones.ShoulderLeft, _midRight, bones.HipLeft,
                bones.ShoulderRight, _midLeft, bones.HipRight
            };

            _lower = new[]
            {
                bones.ElbowLeft, null, bones.KneeLeft,
                bones.ElbowRight, null, bones.KneeRight
            };

            _phaseOffset = new[] { 0f, 0f, 0f, Mathf.PI, Mathf.PI, Mathf.PI };

            _rest = new Quaternion[_upper.Length * 2];
            for (int i = 0; i < _upper.Length; i++)
            {
                _rest[i] = _upper[i] != null ? _upper[i].localRotation : Quaternion.identity;
                _rest[i + _upper.Length] = _lower[i] != null ? _lower[i].localRotation : Quaternion.identity;
            }

            _lastPosition = transform.position;
            _phase = Random.Range(0f, Mathf.PI * 2f);
        }

        private void LateUpdate()
        {
            if (_rig == null || _upper == null || !GameManager.GameplayActive) return;

            // How far it actually moved this frame, whoever moved it — the agent on the
            // floor, or the ceiling crawler with the agent switched off.
            Vector3 position = transform.position;
            float travelled = Vector3.Distance(position, _lastPosition);
            _lastPosition = position;

            float dt = Mathf.Max(0.0001f, Time.deltaTime);
            float speed = travelled / dt;

            _phase += travelled * cyclesPerMetre * Mathf.PI * 2f;
            _movingBlend = Mathf.Lerp(_movingBlend, Mathf.Clamp01(speed / 2.5f), 8f * dt);

            for (int i = 0; i < _upper.Length; i++)
            {
                Transform upper = _upper[i];
                if (upper == null) continue;

                float legPhase = _phase + _phaseOffset[i];
                float swing = Mathf.Sin(legPhase);

                // The recovery half of the cycle lifts; the stance half stays planted.
                float lift = Mathf.Max(0f, Mathf.Cos(legPhase));

                float idle = Mathf.Sin(Time.time * idleTwitchSpeed + i * 1.7f)
                             * idleTwitchDegrees * (1f - _movingBlend);

                upper.localRotation = _rest[i]
                    * Quaternion.Euler(swing * swingDegrees * _movingBlend + idle, 0f, 0f);

                Transform lower = _lower[i];
                if (lower == null) continue;

                lower.localRotation = _rest[i + _upper.Length]
                    * Quaternion.Euler(-lift * liftDegrees * _movingBlend, 0f, 0f);
            }
        }
    }
}
