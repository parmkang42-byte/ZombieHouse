using UnityEngine;
using ZombieHouse.Audio;
using ZombieHouse.Combat;
using ZombieHouse.Core;

namespace ZombieHouse.Level
{
    /// <summary>
    /// A weapon lying on the floor. Two per level, and there are never any more.
    ///
    /// Walk over it and it is yours — no key press, unlike the power cell, because a
    /// power-up should be instant. What you get is an **Uzi with 200 rounds** and no way
    /// on earth to find another one: when the last round leaves the barrel the gun goes
    /// with it. The gatling gun is yours from the start and belt crates keep it fed; this
    /// is the one weapon in the game that is genuinely finite.
    ///
    /// That is the whole design. A weapon with a fixed, visible, dwindling number attached
    /// to it plays completely differently from one with a reserve you can top up: every
    /// burst is spending something you cannot replace, and the decision of *when* to spend
    /// it is the interesting part.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class WeaponPickup : MonoBehaviour
    {
        [Tooltip("Rounds the weapon arrives with. There is no resupply for it anywhere.")]
        [SerializeField] private int rounds = 200;

        [SerializeField] private float bobHeight = 0.14f;
        [SerializeField] private float bobSpeed = 2.2f;
        [SerializeField] private float spinDegreesPerSecond = 55f;

        private Vector3 _basePosition;
        private float _phase;

        private void Awake()
        {
            var trigger = GetComponent<Collider>();
            trigger.isTrigger = true;

            _basePosition = transform.position;
            _phase = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Update()
        {
            if (!GameManager.GameplayActive) return;

            float y = _basePosition.y + Mathf.Sin(Time.time * bobSpeed + _phase) * bobHeight;
            transform.position = new Vector3(_basePosition.x, y, _basePosition.z);
            transform.Rotate(Vector3.up, spinDegreesPerSecond * Time.deltaTime, Space.World);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            // The switcher lives on the camera, so search the whole rig from either end.
            var switcher = other.GetComponentInChildren<WeaponSwitcher>();
            if (switcher == null) switcher = other.GetComponentInParent<WeaponSwitcher>();
            if (switcher == null) return;

            if (!switcher.GrantPowerUp(rounds)) return;

            GameAudio.Play2D(Sfx.PickupAmmo, 0.7f);
            GameAudio.Play2D(Sfx.ReloadIn, 0.6f, 0.05f);
            Destroy(gameObject);
        }
    }
}
