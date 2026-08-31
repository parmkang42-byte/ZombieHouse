using UnityEngine;
using ZombieHouse.Audio;
using ZombieHouse.Combat;
using ZombieHouse.Core;

namespace ZombieHouse.Level
{
    /// <summary>
    /// A belt box for the gatling gun. Several per level, and each one is worth 150 rounds.
    ///
    /// The gatling gun cannot be reloaded from the world in general — ordinary ammunition
    /// boxes do nothing for it — but a crate of *linked* ammunition does. Since the gun is
    /// now part of the standing loadout rather than something you find, these are what
    /// keep it alive across a level: it starts with 400 and there are four to six crates,
    /// so how much of it you have left at the burial chamber is a routing decision.
    ///
    /// A crate you cannot use yet stays where it is — walk over one with a full gun and it
    /// is still there when you come back needing it.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BeltCrate : MonoBehaviour
    {
        [Tooltip("Rounds in the box. About eight seconds of fire.")]
        [SerializeField] private int rounds = 150;

        [SerializeField] private float bobHeight = 0.08f;
        [SerializeField] private float bobSpeed = 1.7f;

        private Vector3 _basePosition;
        private float _phase;

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            _basePosition = transform.position;
            _phase = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Update()
        {
            if (!GameManager.GameplayActive) return;

            float y = _basePosition.y + Mathf.Sin(Time.time * bobSpeed + _phase) * bobHeight;
            transform.position = new Vector3(_basePosition.x, y, _basePosition.z);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            var switcher = other.GetComponentInChildren<WeaponSwitcher>();
            if (switcher == null) switcher = other.GetComponentInParent<WeaponSwitcher>();
            if (switcher == null) return;

            // Full, or nothing to feed: leave the crate on the floor for later.
            if (switcher.FeedBeltFed(rounds) <= 0) return;

            GameAudio.Play2D(Sfx.BeltFeed, 0.75f);
            Destroy(gameObject);
        }
    }
}
