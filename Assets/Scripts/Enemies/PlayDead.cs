using UnityEngine;
using ZombieHouse.Audio;
using ZombieHouse.Core;

namespace ZombieHouse.Enemies
{
    /// <summary>
    /// A zombie lying among the bodies, which gets up when you are close enough to touch it.
    ///
    /// This is the best scare available in this project and it costs almost nothing, because
    /// the game has already spent hours teaching the player to ignore corpses. Every zombie
    /// killed leaves a ragdoll on the floor, so by the second level a body on the ground is
    /// furniture — the eye stops going to it. Turning a handful of those into something that
    /// is merely *waiting* does not add a new threat so much as retroactively make every
    /// corpse in the game worth a second look, including the hundreds that really are dead.
    ///
    /// **It does not cheat**, on exactly the same terms as <see cref="DoorAmbush"/>. The
    /// zombie is genuinely there the whole time. You can shoot it while it lies still, and
    /// if you do, it gets up — which is the correct answer to a player who has learned the
    /// trick and starts putting a round into every body they pass. That costs them
    /// ammunition and noise, which is the price of knowing, and it is a fair one.
    ///
    /// **It waits until you are very close.** Closer than the ordinary proximity wake, which
    /// is the entire point: you have to have walked past it, or better, over it. A body that
    /// sits up across the room is a monster. A body that sits up at your feet is a fright.
    /// </summary>
    [RequireComponent(typeof(ZombieAI))]
    public class PlayDead : MonoBehaviour
    {
        [Tooltip("How close you have to be before it moves. Deliberately shorter than the "
                 + "AI's own wake distance, so this always fires after you have committed "
                 + "to walking past it.")]
        [SerializeField] private float triggerRange = 3.4f;

        [Tooltip("Seconds to get up. Slower than the door ambush: a body pushing itself off "
                 + "the floor is a heavier movement than a crouched thing standing, and the "
                 + "extra beat is what you spend realising what is happening.")]
        [SerializeField] private float riseSeconds = 0.75f;

        [Tooltip("Small lift applied after the body is laid flat, so it rests ON the floor "
                 + "rather than with its centreline through it. Half a body's thickness.")]
        [SerializeField] private float floorClearance = 0.15f;

        private ZombieAI _ai;
        private ZombieHealth _health;
        private Transform _rig;

        private Vector3 _standingPosition;
        private Quaternion _standingRotation;

        private Transform _player;
        private bool _risen;
        private float _rise;

        public bool HasRisen => _risen;

        /// <summary>
        /// Lays it out, and returns false if it refused.
        ///
        /// Called by the spawner rather than from Awake, because the spawner runs in edit
        /// mode during a build and Awake does not — a zombie configured only in Awake would
        /// ship standing upright in the saved scene.
        ///
        /// **It refuses to share a zombie with a <see cref="DoorAmbush"/>**, and enforcing
        /// that here rather than in the caller is deliberate. The two scares cancel silently
        /// when combined: a body lying flat behind a closed door is hidden BY the door, so
        /// the door swings open onto an empty-looking floor and neither effect ever fires.
        /// Nothing errors, nothing looks broken, and the level simply contains fewer scares
        /// than the numbers claim. An invariant whose violation is invisible belongs with the
        /// thing it protects, not in whichever caller happens to remember it — and it makes
        /// the rule testable without having to reproduce a whole runtime spawn.
        /// </summary>
        public bool Lie()
        {
            if (GetComponent<DoorAmbush>() != null) return false;

            _ai = GetComponent<ZombieAI>();
            _health = GetComponent<ZombieHealth>();
            _rig = transform.Find("Rig");
            if (_rig == null) return false;

            _standingPosition = _rig.localPosition;
            _standingRotation = _rig.localRotation;

            // Face down, rolled a little off true. A body squared up to the floor reads as
            // placed; the roll is most of what makes it read as fallen.
            _rig.localRotation = _standingRotation
                               * Quaternion.Euler(90f, 0f, Random.Range(-24f, 24f));

            // A LIFT, not a drop, and the sign here was wrong at first in a way worth
            // recording: the Rig's origin sits at the zombie's feet, so rotating it ninety
            // degrees has already laid the whole body flat along the floor. There is no
            // sinking left to do. The original code borrowed DoorAmbush's downward offset —
            // which is correct for a crouch, where the body stays vertical and genuinely has
            // to come down — and buried the corpse a metre under the floorboards, where it
            // was invisible, unshootable, and still perfectly able to stand up at you.
            _rig.localPosition = _standingPosition + Vector3.up * floorClearance;

            // No collider bookkeeping is needed, and that is worth stating rather than
            // leaving as an absence. A zombie has no collider on its root — the things you
            // can shoot are the body parts, and they are children of the Rig — so laying the
            // Rig down lays the collision down with it. The body is hittable where it looks
            // hittable while prone, and stands back up complete.
            return true;
        }

        private void Start()
        {
            if (_ai == null) _ai = GetComponent<ZombieAI>();
            if (_health == null) _health = GetComponent<ZombieHealth>();
            if (_rig == null) _rig = transform.Find("Rig");

            // Shooting a body that was only pretending is supposed to work. Subscribed here
            // as well as nowhere else because an event handler is not serialisable — a scene
            // built in edit mode has to wire this at runtime or it silently never listens.
            if (_health != null) _health.Damaged += OnDamaged;
        }

        private void OnDestroy()
        {
            if (_health != null) _health.Damaged -= OnDamaged;
        }

        private void OnDamaged(DamageInfo info) => Rise();

        private void Update()
        {
            if (!GameManager.GameplayActive) return;

            if (!_risen)
            {
                // Nothing stirs during the opening grace, the same rule every other enemy
                // obeys. A level that opens with a body sitting up at your feet is not a
                // safe start by any reading of the phrase.
                if (!GameManager.CombatAllowed) return;

                if (_player == null)
                {
                    var playerObject = GameObject.FindGameObjectWithTag("Player");
                    if (playerObject != null) _player = playerObject.transform;
                    if (_player == null) return;
                }

                if (Vector3.Distance(transform.position, _player.position) <= triggerRange)
                    Rise();

                return;
            }

            if (_rig == null || _rise >= 1f) return;

            _rise = Mathf.MoveTowards(_rise, 1f, Time.deltaTime / Mathf.Max(0.05f, riseSeconds));

            float eased = 1f - Mathf.Pow(1f - _rise, 3f);

            _rig.localRotation = Quaternion.Slerp(
                _standingRotation * Quaternion.Euler(90f, 0f, 0f), _standingRotation, eased);

            _rig.localPosition = Vector3.Lerp(_standingPosition + Vector3.up * floorClearance,
                                              _standingPosition, eased);
        }

        /// <summary>Gets up. Public so a test can fire it without walking a player at it.</summary>
        public void Rise()
        {
            if (_risen) return;

            _risen = true;

            // The sound is doing at least half the work here. The rise itself is quiet and
            // happens behind you as often as in front, and a scare you only catch out of the
            // corner of your eye needs something to point at it.
            GameAudio.PlayAt(Sfx.ZombieRise, transform.position, 0.95f);
            Noise.Emit(transform.position, 6f);

            if (_ai != null) _ai.Wake();
        }
    }
}
