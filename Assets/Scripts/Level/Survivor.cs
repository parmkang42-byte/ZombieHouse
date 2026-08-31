using UnityEngine;
using ZombieHouse.Audio;
using ZombieHouse.Core;
using ZombieHouse.Player;

namespace ZombieHouse.Level
{
    /// <summary>
    /// Someone still alive in here. Walk up to one and press E, and they are out.
    ///
    /// The door will not open until every one of them has been found, which is the point:
    /// the kill quota tells you to clear the level, and the survivors tell you to search
    /// it. They call out every few seconds so the search has something to follow, and
    /// they carry a lantern so it has something to see.
    /// </summary>
    public class Survivor : MonoBehaviour
    {
        [Tooltip("How close you have to be before E will help them up.")]
        [SerializeField] private float rescueRadius = 2.6f;

        [Tooltip("Seconds between calls for help. They call until they are found.")]
        [SerializeField] private float callInterval = 8f;
        [SerializeField] private float callIntervalJitter = 3f;

        [Tooltip("How long they take to get up and go once you reach them.")]
        [SerializeField] private float leaveSeconds = 1.6f;

        public bool Rescued { get; private set; }

        /// <summary>Tied to a post rather than hiding. Changes the prompt, nothing else.</summary>
        public bool IsBound { get; private set; }

        public void SetBound(bool bound)
        {
            IsBound = bound;
        }

        private Transform _player;
        private float _nextCallAt;
        private float _leaveAt = -1f;
        private Vector3 _restPosition;

        private void Start()
        {
            _restPosition = transform.position;
            _nextCallAt = Time.time + Random.Range(1f, callInterval);

            var playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) _player = playerObject.transform;

            if (GameManager.Instance != null) GameManager.Instance.RegisterSurvivor(this);
        }

        private void Update()
        {
            if (!GameManager.GameplayActive) return;

            if (_leaveAt > 0f)
            {
                LeaveOnFoot();
                return;
            }

            if (Rescued || _player == null) return;

            float distance = Vector3.Distance(_player.position, transform.position);

            if (distance <= rescueRadius)
            {
                InteractPrompt.Request(IsBound ? "CUT THEM LOOSE" : "HELP THEM UP");
                if (InputReader.InteractPressed) Rescue();
                return;
            }

            // Only call while nobody is standing over them; a survivor shouting for help
            // at someone already in the room reads as broken rather than as frightened.
            if (Time.time < _nextCallAt) return;

            _nextCallAt = Time.time + callInterval + Random.Range(0f, callIntervalJitter);
            GameAudio.PlayAt(Sfx.SurvivorCall, transform.position, 0.75f, 0.06f);
        }

        /// <summary>Found. They stand, and they see themselves out.</summary>
        public void Rescue()
        {
            if (Rescued) return;

            Rescued = true;
            _leaveAt = Time.time + leaveSeconds;

            GameAudio.PlayAt(Sfx.SurvivorSaved, transform.position, 0.9f);
            if (GameManager.Instance != null) GameManager.Instance.ReportSurvivorRescued(this);
        }

        /// <summary>
        /// Standing up and walking off, then gone. They are not simulated after this —
        /// there is nothing for them to do and nowhere they need to be.
        /// </summary>
        private void LeaveOnFoot()
        {
            float remaining = _leaveAt - Time.time;
            if (remaining <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            float progress = 1f - Mathf.Clamp01(remaining / leaveSeconds);

            // Someone cut free steps away from the post; someone huddled has to get up
            // first, so they rise as well as move.
            float rise = IsBound ? 0.06f : 0.42f;
            transform.position = _restPosition + Vector3.up * (rise * progress)
                                 + transform.forward * (0.7f * progress);
        }
    }
}
