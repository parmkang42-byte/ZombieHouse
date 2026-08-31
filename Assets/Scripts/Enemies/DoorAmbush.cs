using UnityEngine;
using ZombieHouse.Core;

namespace ZombieHouse.Enemies
{
    /// <summary>
    /// A zombie crouched behind a door, waiting for it to open.
    ///
    /// This is the cheapest genuinely frightening thing in the game, and it works because it
    /// inverts what a door is *for*. A door is cover — the one thing in a level you can put
    /// between yourself and everything else. Making a handful of them hold something that
    /// stands up as the gap widens means every door in every level is now a question, and
    /// the ones that are empty are doing work simply by being doors.
    ///
    /// **It does not cheat.** The zombie is really there, standing where you could shoot it
    /// through the doorway if you had another angle, audible if you listen — it is simply
    /// dormant and low. Nothing is spawned when the door opens. A jump scare that
    /// materialises an enemy is a trick you can only play once, because after the first time
    /// the player knows the room was empty until they touched the handle.
    ///
    /// **It stays crouched until it springs.** Crouching is what makes it work at all: the
    /// silhouette does not break the line of the doorway, so an open sightline from across
    /// the room shows nothing.
    /// </summary>
    [RequireComponent(typeof(ZombieAI))]
    public class DoorAmbush : MonoBehaviour
    {
        [Tooltip("How far it sinks while it waits, in metres. Enough that the head drops "
                 + "below the level of the door head and out of the sightline.")]
        [SerializeField] private float crouchDepth = 0.55f;

        [Tooltip("Seconds to rise. Fast, but not instant — the point is that you watch it "
                 + "happen and have almost, but not quite, enough time to react.")]
        [SerializeField] private float riseSeconds = 0.35f;

        private ZombieAI _ai;
        private Transform _rig;
        private Vector3 _standing;
        private bool _sprung;
        private float _rise;

        public bool HasSprung => _sprung;

        /// <summary>
        /// Sinks it into its waiting pose. Called by the generator, so it happens in edit
        /// mode too — Awake would never run there and the zombie would ship standing up.
        /// </summary>
        public void Crouch()
        {
            _ai = GetComponent<ZombieAI>();
            _rig = transform.Find("Rig");
            if (_rig == null) return;

            _standing = _rig.localPosition;
            _rig.localPosition = _standing - Vector3.up * crouchDepth;
        }

        private void Start()
        {
            if (_ai == null) _ai = GetComponent<ZombieAI>();

            if (_rig == null)
            {
                _rig = transform.Find("Rig");
                if (_rig != null) _standing = _rig.localPosition + Vector3.up * crouchDepth;
            }
        }

        /// <summary>Gets up and comes for you. Called by the door as it starts to swing.</summary>
        public void Spring()
        {
            if (_sprung) return;

            _sprung = true;
            if (_ai != null) _ai.Wake();
        }

        private void Update()
        {
            if (!GameManager.GameplayActive) return;
            if (!_sprung || _rig == null || _rise >= 1f) return;

            _rise = Mathf.MoveTowards(_rise, 1f, Time.deltaTime / Mathf.Max(0.05f, riseSeconds));

            // Overshoot slightly and settle. A body that rises and stops dead reads as a
            // lift; one that overshoots reads as something pushing itself up.
            float eased = 1f - Mathf.Pow(1f - _rise, 3f);
            float overshoot = Mathf.Sin(_rise * Mathf.PI) * 0.08f;

            _rig.localPosition = Vector3.Lerp(_standing - Vector3.up * crouchDepth,
                                              _standing, eased) + Vector3.up * overshoot;
        }
    }
}
