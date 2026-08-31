using UnityEngine;
using ZombieHouse.Core;

namespace ZombieHouse.Level
{
    /// <summary>
    /// A fluorescent tube on its way out.
    ///
    /// Not a sine wave: a dying tube sits at full brightness for a while, stutters
    /// several times in a fraction of a second, drops out entirely for a beat, and comes
    /// back. The irregularity is the whole effect — anything periodic reads as a machine
    /// rather than as a fault, and stops being unsettling within about ten seconds.
    ///
    /// The intensity it was created with is treated as the maximum, so this can be
    /// dropped onto any light without configuring it.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class FlickeringLight : MonoBehaviour
    {
        [Tooltip("Longest the tube holds steady between fits, in seconds.")]
        [SerializeField] private Vector2 steadySeconds = new Vector2(1.2f, 6f);

        [Tooltip("How long a fit of stuttering lasts.")]
        [SerializeField] private Vector2 fitSeconds = new Vector2(0.12f, 0.7f);

        [Tooltip("How dim it goes during a stutter, as a share of full.")]
        [Range(0f, 1f)] [SerializeField] private float floorLevel = 0.05f;

        private Light _light;
        private float _fullIntensity;
        private float _nextChange;
        private bool _inFit;
        private float _fitEndsAt;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _fullIntensity = _light.intensity;

            // Stagger the start, or every tube in the corridor fires together.
            _nextChange = Time.time + Random.Range(0f, steadySeconds.y);
        }

        private void Update()
        {
            if (!GameManager.GameplayActive) return;
            if (Time.time < _nextChange) return;

            if (!_inFit)
            {
                _inFit = true;
                _fitEndsAt = Time.time + Random.Range(fitSeconds.x, fitSeconds.y);
            }

            if (Time.time >= _fitEndsAt)
            {
                // Back to steady, at full brightness, for an unpredictable while.
                _inFit = false;
                _light.intensity = _fullIntensity;
                _nextChange = Time.time + Random.Range(steadySeconds.x, steadySeconds.y);
                return;
            }

            // Mid-fit: jump between almost-off and almost-full at an irregular rate.
            _light.intensity = _fullIntensity * (Random.value < 0.45f
                ? Random.Range(floorLevel, 0.25f)
                : Random.Range(0.7f, 1f));

            _nextChange = Time.time + Random.Range(0.02f, 0.09f);
        }
    }
}
