using UnityEngine;
using ZombieHouse.Core;

namespace ZombieHouse.Enemies
{
    /// <summary>
    /// Animates the janitor's mop.
    ///
    /// The mop already rides the forearm, so it moves when the arm does — but a two and a
    /// half metre weapon that only ever follows the hand reads as a stick taped to a
    /// zombie. What makes it a weapon is its own motion: it lags behind the arm going up,
    /// whips past it coming down, and the head keeps travelling after the handle has
    /// stopped, because a wet mop head is heavy and on the end of a lever.
    ///
    /// Three layers, in order of how much they matter:
    ///
    /// **The swing.** Driven off ZombieAI.AttackStarted and the archetype's own windup, so
    /// the sweep is in time with the arm the AI is already animating rather than on a
    /// timer of its own that would drift out of sync with it.
    ///
    /// **The trail.** The head lags the handle by a frame or two of rotation, which is the
    /// single cheapest trick for making a swung object look like it has mass.
    ///
    /// **The carry.** Between swings it sways gently with the walk, so the thing is never
    /// perfectly still — a static prop on a moving body is what reads as "taped on".
    /// </summary>
    public class MopAnimator : MonoBehaviour
    {
        [Header("Swing")]
        [Tooltip("Degrees the mop rocks back during the windup, before the strike.")]
        [SerializeField] private float windupDegrees = 46f;

        [Tooltip("Degrees it sweeps through on the strike itself.")]
        [SerializeField] private float strikeDegrees = 128f;

        [Tooltip("Seconds the follow-through takes to settle after the strike lands.")]
        [SerializeField] private float recoverySeconds = 0.42f;

        [Header("Carry")]
        [SerializeField] private float swayDegrees = 7f;
        [SerializeField] private float swaySpeed = 2.1f;

        [Header("Head")]
        [Tooltip("How far the head lags the handle. This is what gives it weight.")]
        [Range(0f, 0.9f)] [SerializeField] private float headLag = 0.55f;
        [SerializeField] private float headFlopDegrees = 34f;

        private ZombieAI _ai;
        private Transform _mop;
        private Transform[] _strands;
        private Quaternion _restRotation;
        private Quaternion[] _strandRest;

        private float _swingTime = -1f;
        private float _swingLength = 1f;
        private float _phase;
        private float _laggedSwing;

        private void Awake()
        {
            _ai = GetComponent<ZombieAI>();
            _mop = FindMop(transform);

            if (_mop == null) return;

            _restRotation = _mop.localRotation;
            _phase = Random.Range(0f, 10f);

            // The strands flop independently of the handle.
            var found = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in _mop)
                if (child.name.StartsWith("MopStrand")) found.Add(child);

            _strands = found.ToArray();
            _strandRest = new Quaternion[_strands.Length];
            for (int i = 0; i < _strands.Length; i++) _strandRest[i] = _strands[i].localRotation;
        }

        private static Transform FindMop(Transform root)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == "Mop") return t;

            return null;
        }

        private void OnEnable()
        {
            if (_ai != null) _ai.AttackStarted += OnAttackStarted;
        }

        private void OnDisable()
        {
            if (_ai != null) _ai.AttackStarted -= OnAttackStarted;
        }

        private void OnAttackStarted()
        {
            _swingTime = 0f;

            // Match the AI's own windup so the sweep and the arm move together. A mop on
            // its own timer would drift out of step with the animation driving the arm.
            _swingLength = (_ai != null ? _ai.AttackWindup : 0.5f) + recoverySeconds;
        }

        private void LateUpdate()
        {
            if (_mop == null || !GameManager.GameplayActive) return;

            float dt = Time.deltaTime;
            _phase += dt;
            if (_swingTime >= 0f) _swingTime += dt;

            float swing = ResolveSwing();

            // The handle: rocks back, sweeps down, and sways when it is doing neither.
            float sway = Mathf.Sin(_phase * swaySpeed) * swayDegrees * (1f - Mathf.Abs(swing));
            _mop.localRotation = _restRotation * Quaternion.Euler(swing * strikeDegrees, 0f, sway);

            // The head follows late. One exponential lag is enough — it does not need to
            // be a physical simulation to read as heavy.
            _laggedSwing = Mathf.Lerp(_laggedSwing, swing, (1f - headLag) * 18f * dt);
            float flop = (swing - _laggedSwing) * headFlopDegrees;

            for (int i = 0; i < _strands.Length; i++)
            {
                if (_strands[i] == null) continue;
                _strands[i].localRotation = _strandRest[i] * Quaternion.Euler(flop, 0f, flop * 0.4f);
            }
        }

        /// <summary>
        /// Where the swing is, from -1 (fully wound back) through 0 to +1 (fully struck).
        /// Returns 0 when there is no swing in progress.
        /// </summary>
        private float ResolveSwing()
        {
            if (_swingTime < 0f) return 0f;

            if (_swingTime >= _swingLength)
            {
                _swingTime = -1f;
                return 0f;
            }

            float windup = Mathf.Max(0.05f, _swingLength - recoverySeconds);

            if (_swingTime < windup)
            {
                // Rocking back: slow, and it eases out so the top of the swing hangs for
                // a moment. That hang is the tell that something is about to happen.
                float t = Mathf.SmoothStep(0f, 1f, _swingTime / windup);
                return -t * (windupDegrees / strikeDegrees);
            }

            // The strike: fast in, then a settle back towards rest.
            float strikeT = (_swingTime - windup) / recoverySeconds;
            float driven = Mathf.Sin(strikeT * Mathf.PI * 0.85f);
            return Mathf.Lerp(-windupDegrees / strikeDegrees, 1f, driven);
        }
    }
}
