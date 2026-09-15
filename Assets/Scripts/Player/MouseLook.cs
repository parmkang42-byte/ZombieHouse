using UnityEngine;
using ZombieHouse.Core;

namespace ZombieHouse.Player
{
    /// <summary>
    /// Yaw goes on the body, pitch goes on the camera. Weapon recoil is added as a
    /// separate offset that decays back to centre, so it never corrupts the aim angles.
    /// </summary>
    public class MouseLook : MonoBehaviour
    {
        [Header("Sensitivity")]
        [SerializeField] private float sensitivity = 2.2f;
        [SerializeField] private bool invertY = false;

        [Header("Limits")]
        [SerializeField] private float minPitch = -85f;
        [SerializeField] private float maxPitch = 85f;

        [Header("References")]
        [SerializeField] private Transform body;
        [SerializeField] private Transform cameraTransform;

        [Header("Recoil")]
        [SerializeField] private float recoilRecoverySpeed = 9f;
        [SerializeField] private float recoilSnappiness = 18f;

        private float _yaw;
        private float _pitch;
        private Vector2 _recoilTarget;
        private Vector2 _recoilCurrent;

        /// <summary>Current aim pitch in degrees, negative upward. Read by the recoil test.</summary>
        public float PitchDegrees => _pitch;

        public float Sensitivity
        {
            get { return sensitivity; }
            set { sensitivity = Mathf.Max(0.05f, value); }
        }

        /// <summary>
        /// Scales sensitivity without changing the setting. Used by the scope: a magnified
        /// view needs a slower mouse or the aim skates across the target.
        /// </summary>
        public float SensitivityMultiplier { get; set; } = 1f;

        /// <summary>
        /// Degrees added to the camera on top of the aim -- x pitch, y yaw -- by whatever is
        /// making the view drift, which today is a scope held by hand. Added to what is drawn,
        /// never to _pitch or _yaw, so it cannot build up into a drift of the aim itself, and
        /// clearing it puts the view back exactly where the player was pointing. Shots leave
        /// along the camera, so they follow the sway the player sees.
        /// </summary>
        public Vector2 SwayDegrees { get; set; }

        private void Awake()
        {
            if (body == null) body = transform.parent != null ? transform.parent : transform;
            if (cameraTransform == null) cameraTransform = transform;

            _yaw = body.eulerAngles.y;
            _pitch = 0f;
        }

        private void LateUpdate()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>
        /// One frame of look. Public so a test can compose the camera without play mode, which
        /// is the only way to check that a sway actually reaches the view.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (body == null) body = transform.parent != null ? transform.parent : transform;
            if (cameraTransform == null) cameraTransform = transform;

            if (GameManager.GameplayActive)
            {
                Vector2 look = InputReader.Look * (sensitivity * SensitivityMultiplier);
                _yaw += look.x;
                _pitch += invertY ? look.y : -look.y;
                _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
            }

            // Recoil eases in fast and recovers slowly.
            _recoilTarget = Vector2.Lerp(_recoilTarget, Vector2.zero, recoilRecoverySpeed * deltaTime);
            _recoilCurrent = Vector2.Lerp(_recoilCurrent, _recoilTarget, recoilSnappiness * deltaTime);

            body.rotation = Quaternion.Euler(0f, _yaw + _recoilCurrent.y, 0f);
            cameraTransform.localRotation = Quaternion.Euler(_pitch - _recoilCurrent.x + SwayDegrees.x,
                                                            SwayDegrees.y, 0f);
        }

        /// <summary>
        /// Kick the view. <paramref name="pitchUp"/> is degrees upward and
        /// <paramref name="yawOffset"/> is degrees sideways — signed, because the weapon
        /// decides which way this shot walks rather than having it resampled here.
        ///
        /// <paramref name="uncorrectedShare"/> is the part that does not come back: it is
        /// added straight to the aim angles, so the muzzle really has climbed and you
        /// really do have to pull down. The rest decays to nothing as before.
        /// </summary>
        public void AddRecoil(float pitchUp, float yawOffset, float uncorrectedShare = 0f)
        {
            float share = Mathf.Clamp01(uncorrectedShare);

            if (share > 0f)
            {
                // Negative pitch is upward; see LateUpdate.
                _pitch = Mathf.Clamp(_pitch - pitchUp * share, minPitch, maxPitch);
                _yaw += yawOffset * share;
            }

            float recovering = 1f - share;
            _recoilTarget += new Vector2(pitchUp * recovering, yawOffset * recovering);
        }
    }
}
