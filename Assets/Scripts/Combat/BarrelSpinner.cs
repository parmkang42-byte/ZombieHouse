using UnityEngine;
using ZombieHouse.Audio;
using ZombieHouse.Core;

namespace ZombieHouse.Combat
{
    /// <summary>
    /// Turns the gatling gun's barrel cluster and plays its whine.
    ///
    /// Everything here follows <see cref="Weapon.SpinFraction"/> rather than deciding
    /// anything: the weapon owns the rule — barrels have to reach speed before a round
    /// comes out — and this makes that rule visible and audible. Splitting it that way
    /// matters because the rule is testable and the presentation is not.
    ///
    /// The whine is the part players actually read. Pitch and volume both ride the spin,
    /// so the second between pulling the trigger and the gun firing is filled with a sound
    /// that is obviously *winding up* — without it the delay just feels like the weapon is
    /// broken.
    /// </summary>
    [RequireComponent(typeof(Weapon))]
    public class BarrelSpinner : MonoBehaviour
    {
        [Tooltip("The barrel cluster. Found by name if not assigned.")]
        [SerializeField] private Transform barrels;

        [Tooltip("Revolutions a second at full speed.")]
        [SerializeField] private float maximumRevolutionsPerSecond = 7.5f;

        [Header("Whine")]
        [SerializeField] private float whineVolume = 0.35f;
        [SerializeField] private float lowestPitch = 0.55f;
        [SerializeField] private float highestPitch = 1.5f;

        private Weapon _weapon;
        private AudioSource _whine;
        private ZombieHouse.Player.PlayerController _controller;

        private void Awake()
        {
            _weapon = GetComponent<Weapon>();
            if (barrels == null) barrels = transform.Find("Slide/BarrelCluster");
            _controller = GetComponentInParent<ZombieHouse.Player.PlayerController>();
        }

        private void Start()
        {
            _whine = GameAudio.AttachSource(gameObject, 0f);
            if (_whine == null) return;

            _whine.clip = GameAudio.Get(Sfx.GatlingSpin);
            if (_whine.clip == null) return;

            _whine.loop = true;
            _whine.spatialBlend = 0f;      // it is in your hands, not out in the world
            _whine.Play();
        }

        private void OnEnable()
        {
            // Drawn: you are now carrying something that weighs as much as you do.
            if (_controller != null) _controller.SetHeavilyArmed(true);
        }

        private void OnDisable()
        {
            // Holstered mid-spin, the whine must not follow you around on your back —
            // and you get your walking speed back.
            if (_whine != null) _whine.volume = 0f;
            if (_controller != null) _controller.SetHeavilyArmed(false);
        }

        private void Update()
        {
            if (_weapon == null || !GameManager.GameplayActive) return;

            float spin = _weapon.SpinFraction;

            if (barrels != null)
            {
                // Rotation follows the spin fraction rather than a fixed rate, so the
                // barrels visibly wind up and coast down instead of snapping between
                // stopped and blurring.
                barrels.Rotate(Vector3.forward,
                               spin * maximumRevolutionsPerSecond * 360f * Time.deltaTime,
                               Space.Self);
            }

            if (_whine == null || _whine.clip == null) return;

            _whine.volume = whineVolume * spin;
            _whine.pitch = Mathf.Lerp(lowestPitch, highestPitch, spin);
        }
    }
}
