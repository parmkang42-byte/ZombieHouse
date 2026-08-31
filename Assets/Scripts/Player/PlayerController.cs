using System;
using UnityEngine;
using ZombieHouse.Core;

namespace ZombieHouse.Player
{
    /// <summary>
    /// CharacterController-based FPS movement: walk / sprint / crouch / jump,
    /// with head bob and footstep noise that the zombies can hear.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Carrying the power cell")]
        [Tooltip("Speed multiplier while shouldering the cell. Sprinting is off entirely.")]
        [SerializeField] private float encumberedSpeedMultiplier = 0.72f;

        [Tooltip("Speed multiplier while holding the gatling gun. Sprinting still works.")]
        [SerializeField] private float heavyWeaponSpeedMultiplier = 0.85f;

        [Header("Speeds (m/s)")]
        [SerializeField] private float walkSpeed = 4.2f;
        [SerializeField] private float sprintSpeed = 6.8f;
        [SerializeField] private float crouchSpeed = 2.0f;
        [SerializeField] private float acceleration = 14f;
        [SerializeField] private float airControl = 0.35f;

        [Header("Scroll wheel throttle")]
        [Tooltip("Forward throttle added per wheel notch. Scroll up to walk forward, down to back up.")]
        [SerializeField] private float scrollPerNotch = 0.6f;
        [Tooltip("How fast the throttle bleeds back to zero. Set to 0 for a sustained cruise control.")]
        [SerializeField] private float scrollDecayPerSecond = 1.6f;

        [Header("Jump / gravity")]
        [SerializeField] private float jumpHeight = 1.1f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float groundStick = -2f;

        [Header("Stance")]
        [SerializeField] private float standHeight = 1.8f;
        [SerializeField] private float crouchHeight = 1.15f;
        [SerializeField] private float stanceLerpSpeed = 10f;
        [SerializeField] private LayerMask ceilingMask = ~0;

        [Header("Camera")]
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private float eyeOffsetFromTop = 0.15f;
        [SerializeField] private float bobAmplitude = 0.045f;
        [SerializeField] private float bobFrequency = 9f;

        [Header("Noise heard by zombies (metres)")]
        [SerializeField] private float walkNoiseRadius = 6f;
        [SerializeField] private float sprintNoiseRadius = 14f;
        [SerializeField] private float crouchNoiseRadius = 1.5f;
        [SerializeField] private float stepDistance = 2.2f;

        private CharacterController _cc;
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;
        private float _targetHeight;
        private float _bobTimer;
        private float _distanceSinceStep;
        private float _cameraBaseY;

        public bool IsCrouching { get; private set; }
        public bool IsSprinting { get; private set; }

        /// <summary>Carrying the power cell: slower, and no sprinting.</summary>
        public bool IsEncumbered { get; private set; }

        /// <summary>Holding the gatling gun: slower, but you can still run with it.</summary>
        public bool IsHeavilyArmed { get; private set; }

        public void SetEncumbered(bool value)
        {
            IsEncumbered = value;
        }

        public void SetHeavilyArmed(bool value)
        {
            IsHeavilyArmed = value;
        }
        public float CurrentSpeed => _horizontalVelocity.magnitude;

        /// <summary>Horizontal velocity, used by the AI to aim where you are going.</summary>
        public Vector3 Velocity => _horizontalVelocity;
        public bool IsGrounded => _cc != null && _cc.isGrounded;

        /// <summary>Raised on each footfall, so audio and the AI's hearing stay in step.</summary>
        public event Action Stepped;
        public event Action Jumped;
        public event Action Landed;

        private bool _wasGrounded = true;
        private float _scrollThrottle;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();

            // Never let the player's own collider count as a low ceiling.
            ceilingMask &= ~(1 << gameObject.layer);

            _targetHeight = standHeight;
            _cc.height = standHeight;
            _cc.center = new Vector3(0f, standHeight * 0.5f, 0f);

            if (cameraPivot == null && Camera.main != null && Camera.main.transform.IsChildOf(transform))
                cameraPivot = Camera.main.transform;
        }

        private void Update()
        {
            if (!GameManager.GameplayActive)
            {
                // Otherwise a throttle banked before pausing fires you forward on resume.
                _scrollThrottle = 0f;
                return;
            }

            UpdateStance();
            UpdateMovement();
            UpdateCameraHeightAndBob();
        }

        private void UpdateStance()
        {
            bool wantsCrouch = InputReader.CrouchHeld;

            // Refuse to stand up under a low ceiling.
            if (!wantsCrouch && IsCrouching && BlockedAbove())
                wantsCrouch = true;

            IsCrouching = wantsCrouch;
            _targetHeight = IsCrouching ? crouchHeight : standHeight;

            float h = Mathf.Lerp(_cc.height, _targetHeight, stanceLerpSpeed * Time.deltaTime);
            if (Mathf.Abs(h - _targetHeight) < 0.005f) h = _targetHeight;
            _cc.height = h;
            _cc.center = new Vector3(0f, h * 0.5f, 0f);
        }

        private bool BlockedAbove()
        {
            float radius = _cc.radius * 0.95f;
            Vector3 start = transform.position + Vector3.up * (_cc.height - radius);
            float distance = standHeight - _cc.height + 0.05f;
            return Physics.SphereCast(start, radius, Vector3.up, out _, distance, ceilingMask, QueryTriggerInteraction.Ignore);
        }

        private void UpdateMovement()
        {
            Vector2 input = InputReader.MoveNormalized;

            // The wheel contributes to the same forward axis as W and S, so scrolling
            // and the keyboard add together rather than fighting for control.
            float forward = Mathf.Clamp(input.y + UpdateScrollThrottle(), -1f, 1f);

            Vector3 wish = transform.right * input.x + transform.forward * forward;
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            // You cannot run with it on your shoulder, which is the whole cost of the
            // detour: the cell buys the door and spends your legs to do it.
            IsSprinting = InputReader.SprintHeld && !IsCrouching && !IsEncumbered && forward > 0.1f;

            float targetSpeed = IsCrouching ? crouchSpeed : (IsSprinting ? sprintSpeed : walkSpeed);
            if (IsEncumbered) targetSpeed *= encumberedSpeedMultiplier;

            // A gatling gun is heavy but it is not a crate: you keep your sprint, you
            // just do everything about fifteen per cent slower while it is in your hands.
            if (IsHeavilyArmed) targetSpeed *= heavyWeaponSpeedMultiplier;

            Vector3 targetVelocity = wish * targetSpeed;

            float rate = acceleration * (_cc.isGrounded ? 1f : airControl);
            _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, targetVelocity, rate * Time.deltaTime);

            if (_cc.isGrounded)
            {
                if (_verticalVelocity < 0f) _verticalVelocity = groundStick;
                if (InputReader.JumpPressed)
                {
                    _verticalVelocity = Mathf.Sqrt(-2f * gravity * jumpHeight);
                    Noise.Emit(transform.position, walkNoiseRadius);
                    Jumped?.Invoke();
                }
            }

            _verticalVelocity += gravity * Time.deltaTime;

            Vector3 motion = (_horizontalVelocity + Vector3.up * _verticalVelocity) * Time.deltaTime;
            _cc.Move(motion);

            // Landing is the frame grounded flips back on after time in the air.
            if (_cc.isGrounded && !_wasGrounded)
            {
                Landed?.Invoke();
                Noise.Emit(transform.position, walkNoiseRadius);
            }
            _wasGrounded = _cc.isGrounded;

            EmitFootsteps();
        }

        /// <summary>
        /// Turns wheel notches into a forward/back throttle. Each notch adds a push that
        /// bleeds away, so scrolling steadily keeps you walking and stopping lets you
        /// coast to a halt — rather than one stray notch committing you to a direction.
        /// Set scrollDecayPerSecond to 0 to make it a sustained cruise control instead.
        /// </summary>
        private float UpdateScrollThrottle()
        {
            float notches = InputReader.ScrollDelta;

            if (Mathf.Abs(notches) > 0.001f)
                _scrollThrottle = Mathf.Clamp(_scrollThrottle + notches * scrollPerNotch, -1f, 1f);

            if (scrollDecayPerSecond > 0f)
                _scrollThrottle = Mathf.MoveTowards(_scrollThrottle, 0f, scrollDecayPerSecond * Time.deltaTime);

            return _scrollThrottle;
        }

        /// <summary>Cancels any wheel throttle — used when control is taken away.</summary>
        public void ClearScrollThrottle()
        {
            _scrollThrottle = 0f;
        }

        private void EmitFootsteps()
        {
            if (!_cc.isGrounded) return;

            float planarSpeed = new Vector3(_horizontalVelocity.x, 0f, _horizontalVelocity.z).magnitude;
            if (planarSpeed < 0.4f)
            {
                _distanceSinceStep = 0f;
                return;
            }

            _distanceSinceStep += planarSpeed * Time.deltaTime;
            if (_distanceSinceStep < stepDistance) return;

            _distanceSinceStep = 0f;
            float radius = IsCrouching ? crouchNoiseRadius : (IsSprinting ? sprintNoiseRadius : walkNoiseRadius);
            Noise.Emit(transform.position, radius);
            Stepped?.Invoke();
        }

        private void UpdateCameraHeightAndBob()
        {
            if (cameraPivot == null) return;

            float eyeY = _cc.height - eyeOffsetFromTop;

            float planarSpeed = new Vector3(_horizontalVelocity.x, 0f, _horizontalVelocity.z).magnitude;
            if (_cc.isGrounded && planarSpeed > 0.4f)
                _bobTimer += Time.deltaTime * bobFrequency * (planarSpeed / walkSpeed);
            else
                _bobTimer = Mathf.MoveTowards(_bobTimer, 0f, Time.deltaTime * 6f);

            float bob = Mathf.Sin(_bobTimer) * bobAmplitude * Mathf.Clamp01(planarSpeed / walkSpeed);
            Vector3 local = cameraPivot.localPosition;
            local.y = Mathf.Lerp(local.y, eyeY + bob, stanceLerpSpeed * Time.deltaTime);
            cameraPivot.localPosition = local;
            _cameraBaseY = eyeY;
        }

        /// <summary>Used by the weapon to place muzzle/recoil relative to eye level.</summary>
        public float EyeHeight => _cameraBaseY;
    }
}
