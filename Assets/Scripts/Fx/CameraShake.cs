using UnityEngine;

namespace ZombieHouse.Fx
{
    /// <summary>
    /// Shakes the view. Runs after MouseLook (execution order 100) so it layers a shake
    /// on top of the aim rotation instead of being overwritten by it, and only touches
    /// rotation — nudging the camera's position would fight the crouch and head bob that
    /// PlayerController drives every frame.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class CameraShake : MonoBehaviour
    {
        [SerializeField] private float frequency = 22f;

        private static CameraShake _instance;

        private float _amplitude;
        private float _duration;
        private float _elapsed;
        private float _seed;

        private void Awake()
        {
            _instance = this;
            _seed = Random.Range(0f, 100f);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>Shakes the camera. Amplitude is in degrees; a nearby blast is around 0.5.</summary>
        public static void Shake(float amplitudeDegrees, float seconds)
        {
            if (_instance == null) return;

            // A bigger shake overrides a smaller one still running, rather than adding to
            // it — two explosions should not stack into an unplayable mess.
            if (amplitudeDegrees * seconds < _instance._amplitude * (_instance._duration - _instance._elapsed))
                return;

            _instance._amplitude = amplitudeDegrees;
            _instance._duration = Mathf.Max(0.01f, seconds);
            _instance._elapsed = 0f;
        }

        private void LateUpdate()
        {
            if (_elapsed >= _duration || _amplitude <= 0f) return;

            _elapsed += Time.deltaTime;
            float remaining = 1f - Mathf.Clamp01(_elapsed / _duration);
            float strength = _amplitude * remaining * remaining * 12f;

            // Perlin rather than white noise: a shove that settles, not a vibration.
            float t = Time.time * frequency;
            float pitch = (Mathf.PerlinNoise(_seed, t) - 0.5f) * 2f * strength;
            float yaw = (Mathf.PerlinNoise(_seed + 13f, t) - 0.5f) * 2f * strength;
            float roll = (Mathf.PerlinNoise(_seed + 29f, t) - 0.5f) * 2f * strength * 0.6f;

            transform.localRotation *= Quaternion.Euler(pitch, yaw, roll);
        }
    }
}
