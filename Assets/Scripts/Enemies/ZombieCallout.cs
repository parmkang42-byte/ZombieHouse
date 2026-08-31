using UnityEngine;
using ZombieHouse.Audio;
using ZombieHouse.Core;

namespace ZombieHouse.Enemies
{
    /// <summary>
    /// A zombie that spots you tells the others.
    ///
    /// The AI was already individually clever — it remembers where it last saw you, walks
    /// there, and searches several points nearby before giving up, so breaking line of sight
    /// and standing still in the next room does not work. What it could not do was
    /// *coordinate*. Forty zombies each hunting alone is forty separate small problems;
    /// forty that converge because one of them saw you is a horde, and the difference is
    /// entirely in whether detection propagates.
    ///
    /// **It reuses the hearing channel rather than inventing a telepathy one.** A callout is
    /// just <see cref="Noise.Emit"/> at the player's position, which every zombie already
    /// listens to and responds to by going to look. So the alarm obeys every rule the game
    /// has already established: it has a radius, deaf things ignore it, distance matters,
    /// and it draws them to *where you were* rather than handing them your live position.
    /// Nothing knows anything it could not have heard.
    ///
    /// **You hear it happen.** The call is an audible snarl, not a silent flag. That is what
    /// turns the mechanic from bad luck into information — the sound is your warning that
    /// one of them has seen you and that the room is about to get worse, and it gives you
    /// the half-second to decide between the doorway behind you and the fight in front.
    /// </summary>
    [RequireComponent(typeof(ZombieAI))]
    public class ZombieCallout : MonoBehaviour
    {
        [Tooltip("How far the call carries. Deliberately shorter than a gunshot: shooting "
                 + "should still be the loudest thing you can do.")]
        [SerializeField] private float callRadius = 22f;

        [Tooltip("Shortest gap between calls from this one. Without it a zombie flickering "
                 + "between chase and investigate at the edge of its sight range would "
                 + "hold the whole level on permanent alert.")]
        [SerializeField] private float cooldownSeconds = 6f;

        [Tooltip("Volume of the snarl. It has to carry over gunfire or it is not a warning.")]
        [SerializeField] private float callVolume = 0.85f;

        private ZombieAI _ai;
        private ZombieHealth _health;
        private float _nextCallTime;

        private void Awake()
        {
            _ai = GetComponent<ZombieAI>();
            _health = GetComponent<ZombieHealth>();
        }

        private void OnEnable()
        {
            if (_ai == null) _ai = GetComponent<ZombieAI>();
            if (_ai != null) _ai.StateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            if (_ai != null) _ai.StateChanged -= OnStateChanged;
        }

        private void OnStateChanged(ZombieState state)
        {
            // Only the moment of *acquiring* you. Attack does not call, because by then
            // everything nearby has already heard the chase.
            if (state != ZombieState.Chase) return;
            if (!GameManager.GameplayActive) return;
            if (_health != null && !_health.IsAlive) return;
            if (Time.time < _nextCallTime) return;

            _nextCallTime = Time.time + Mathf.Max(0.5f, cooldownSeconds);

            GameAudio.PlayAt(Sfx.ZombieAlert, transform.position, callVolume, 0.1f);

            // Emitted where the caller is, not where you are. It is shouting about a place
            // it can see, and the others converge on the shout — so slipping sideways as it
            // goes up sends the rest of them to the wrong spot, which is the counterplay.
            Noise.Emit(transform.position, callRadius);
        }
    }
}
