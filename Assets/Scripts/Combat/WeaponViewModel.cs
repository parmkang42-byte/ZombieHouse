using UnityEngine;
using ZombieHouse.Core;
using ZombieHouse.Player;

namespace ZombieHouse.Combat
{
    /// <summary>
    /// Animates the gun in the player's hands: sway that lags the mouse, a walk bob,
    /// a recoil kick that springs back, and a reload dip.
    ///
    /// Everything is an offset from the rest pose captured at Awake, so moving the
    /// weapon in the scene re-bases the whole animation with no code changes.
    /// </summary>
    public class WeaponViewModel : MonoBehaviour
    {
        [Header("Aim pose (local space, relative to the camera)")]
        [SerializeField] private Vector3 aimPosition = new Vector3(0f, -0.062f, 0.34f);
        [SerializeField] private float aimLerpSpeed = 14f;

        [Header("Sway — the gun lags the mouse")]
        [SerializeField] private float swayAmount = 0.014f;
        [SerializeField] private float swayRotationDegrees = 3.2f;
        [SerializeField] private float swayMaximum = 0.05f;
        [SerializeField] private float swaySmoothing = 7f;

        [Header("Walk bob")]
        [SerializeField] private float bobHorizontal = 0.022f;
        [SerializeField] private float bobVertical = 0.016f;
        [SerializeField] private float bobFrequency = 8.5f;
        [SerializeField] private float bobSmoothing = 8f;

        [Header("Recoil kick")]
        [SerializeField] private float kickBack = 0.055f;
        [SerializeField] private float kickUp = 0.014f;
        [SerializeField] private float kickPitchDegrees = 9f;
        [SerializeField] private float kickRollDegrees = 3.5f;
        [SerializeField] private float kickRecoverySpeed = 11f;

        [Header("Reload")]
        [SerializeField] private float reloadDip = 0.14f;
        [SerializeField] private float reloadRollDegrees = 42f;

        [Header("References")]
        [SerializeField] private Weapon weapon;
        [SerializeField] private PlayerController playerController;

        private Vector3 _restPosition;
        private Quaternion _restRotation;

        private Vector3 _swayOffset;
        private Vector3 _swayRotation;
        private Vector3 _bobOffset;
        private float _bobPhase;

        private Vector3 _kickOffset;
        private Vector3 _kickRotation;

        /// <summary>
        /// Where the weapon sits while aiming. The rifle uses a lower pose than the
        /// pistol: with the scope overlay filling the screen, the gun wants to sit under
        /// the sight picture rather than in the middle of it.
        /// </summary>
        public void ConfigureAimPose(Vector3 localPosition)
        {
            aimPosition = localPosition;
        }

        private void Awake()
        {
            _restPosition = transform.localPosition;
            _restRotation = transform.localRotation;

            if (weapon == null) weapon = GetComponent<Weapon>();
            if (playerController == null) playerController = GetComponentInParent<PlayerController>();
        }

        private void OnEnable()
        {
            if (weapon != null) weapon.Fired += OnFired;
        }

        private void OnDisable()
        {
            if (weapon != null) weapon.Fired -= OnFired;
        }

        private void OnFired()
        {
            _kickOffset += new Vector3(0f, kickUp, -kickBack);
            _kickRotation += new Vector3(-kickPitchDegrees,
                                          Random.Range(-kickRollDegrees, kickRollDegrees) * 0.4f,
                                          Random.Range(-kickRollDegrees, kickRollDegrees));
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            bool active = GameManager.GameplayActive;

            UpdateSway(active, dt);
            UpdateBob(active, dt);
            UpdateKick(dt);

            Vector3 basePosition = _restPosition;
            Quaternion baseRotation = _restRotation;

            // Aiming pulls the gun to the centre of the screen and kills the bob.
            if (weapon != null && weapon.IsAiming)
            {
                basePosition = Vector3.Lerp(transform.localPosition, aimPosition, aimLerpSpeed * dt);
                basePosition = Vector3.Lerp(basePosition, aimPosition, 0.5f);
            }

            Vector3 reloadOffset = Vector3.zero;
            Quaternion reloadRotation = Quaternion.identity;

            if (weapon != null && weapon.IsReloading)
            {
                // Dip out of view and roll the gun over, then come back up.
                float t = Mathf.Sin(weapon.ReloadProgress * Mathf.PI);
                reloadOffset = new Vector3(0f, -reloadDip * t, -0.03f * t);
                reloadRotation = Quaternion.Euler(reloadRollDegrees * t, 0f, -reloadRollDegrees * 0.4f * t);
            }

            float aimDamp = weapon != null && weapon.IsAiming ? 0.25f : 1f;

            transform.localPosition = basePosition
                                      + (_swayOffset + _bobOffset) * aimDamp
                                      + _kickOffset
                                      + reloadOffset;

            transform.localRotation = baseRotation
                                      * Quaternion.Euler(_swayRotation * aimDamp)
                                      * Quaternion.Euler(_kickRotation)
                                      * reloadRotation;
        }

        private void UpdateSway(bool active, float dt)
        {
            Vector2 look = active ? InputReader.Look : Vector2.zero;

            Vector3 targetOffset = new Vector3(
                Mathf.Clamp(-look.x * swayAmount, -swayMaximum, swayMaximum),
                Mathf.Clamp(-look.y * swayAmount, -swayMaximum, swayMaximum),
                0f);

            Vector3 targetRotation = new Vector3(
                Mathf.Clamp(look.y * swayRotationDegrees, -12f, 12f),
                Mathf.Clamp(look.x * swayRotationDegrees, -12f, 12f),
                Mathf.Clamp(-look.x * swayRotationDegrees * 0.6f, -12f, 12f));

            _swayOffset = Vector3.Lerp(_swayOffset, targetOffset, swaySmoothing * dt);
            _swayRotation = Vector3.Lerp(_swayRotation, targetRotation, swaySmoothing * dt);
        }

        private void UpdateBob(bool active, float dt)
        {
            float speed = 0f;
            bool grounded = true;

            if (active && playerController != null)
            {
                speed = playerController.CurrentSpeed;
                grounded = playerController.IsGrounded;
            }

            float intensity = grounded ? Mathf.Clamp01(speed / 5f) : 0f;

            if (intensity > 0.01f) _bobPhase += dt * bobFrequency * Mathf.Max(0.5f, intensity);
            if (_bobPhase > Mathf.PI * 2f) _bobPhase -= Mathf.PI * 2f;

            // Figure-eight: horizontal at half the vertical rate reads as a walk.
            Vector3 target = new Vector3(
                Mathf.Sin(_bobPhase) * bobHorizontal * intensity,
                -Mathf.Abs(Mathf.Cos(_bobPhase)) * bobVertical * intensity,
                0f);

            _bobOffset = Vector3.Lerp(_bobOffset, target, bobSmoothing * dt);
        }

        private void UpdateKick(float dt)
        {
            _kickOffset = Vector3.Lerp(_kickOffset, Vector3.zero, kickRecoverySpeed * dt);
            _kickRotation = Vector3.Lerp(_kickRotation, Vector3.zero, kickRecoverySpeed * dt);
        }
    }
}
