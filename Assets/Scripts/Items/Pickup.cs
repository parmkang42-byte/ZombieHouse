using UnityEngine;
using ZombieHouse.Combat;
using ZombieHouse.Core;
using ZombieHouse.Player;

namespace ZombieHouse.Items
{
    public enum PickupKind { Ammo, Health, Battery }

    /// <summary>
    /// Floating pickup. Refuses to be consumed when the player is already full,
    /// so you can leave a medkit in a room and come back for it.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Pickup : MonoBehaviour
    {
        [SerializeField] private PickupKind kind = PickupKind.Ammo;
        [SerializeField] private int amount = 24;
        [SerializeField] private float bobHeight = 0.15f;
        [SerializeField] private float bobSpeed = 2f;
        [SerializeField] private float spinDegreesPerSecond = 60f;
        [SerializeField] private bool respawns = false;
        [SerializeField] private float respawnDelay = 45f;

        public PickupKind Kind => kind;
        public int Amount => amount;

        private Vector3 _basePosition;
        private float _phase;
        private float _hiddenUntil = -1f;
        private Renderer[] _renderers;
        private Collider _collider;

        public void Configure(PickupKind pickupKind, int pickupAmount)
        {
            kind = pickupKind;
            amount = pickupAmount;
        }

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            _collider.isTrigger = true;
            _renderers = GetComponentsInChildren<Renderer>();
            _basePosition = transform.position;
            _phase = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Update()
        {
            if (_hiddenUntil > 0f)
            {
                if (Time.time < _hiddenUntil) return;
                SetVisible(true);
                _hiddenUntil = -1f;
            }

            if (!GameManager.GameplayActive) return;

            float y = _basePosition.y + Mathf.Sin(Time.time * bobSpeed + _phase) * bobHeight;
            transform.position = new Vector3(_basePosition.x, y, _basePosition.z);
            transform.Rotate(Vector3.up, spinDegreesPerSecond * Time.deltaTime, Space.World);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_hiddenUntil > 0f) return;
            if (!other.CompareTag("Player")) return;

            bool consumed = false;

            switch (kind)
            {
                case PickupKind.Ammo:
                {
                    // Whatever is in your hands. GetComponentInChildren skips inactive
                    // objects, and a holstered weapon is inactive — so this resolves to
                    // the drawn one without having to ask the switcher.
                    var weapon = other.GetComponentInChildren<Weapon>();
                    if (weapon == null) weapon = other.GetComponentInParent<Weapon>();
                    if (weapon != null) consumed = weapon.AddAmmo(amount) > 0;
                    break;
                }
                case PickupKind.Health:
                {
                    var health = other.GetComponentInParent<PlayerHealth>();
                    if (health != null) consumed = health.Heal(amount) > 0f;
                    break;
                }
                case PickupKind.Battery:
                {
                    // Also on the camera, for the same reason as the launcher.
                    var torch = other.GetComponentInChildren<Flashlight>();
                    if (torch == null) torch = other.GetComponentInParent<Flashlight>();
                    if (torch != null) consumed = torch.AddBattery(amount) > 0;
                    break;
                }
            }

            if (!consumed) return;

            // Health pickups are announced by PlayerAudio when the heal lands.
            if (kind != PickupKind.Health)
                Audio.GameAudio.Play2D(Audio.Sfx.PickupAmmo, 0.55f);

            if (respawns)
            {
                SetVisible(false);
                _hiddenUntil = Time.time + respawnDelay;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void SetVisible(bool visible)
        {
            foreach (var r in _renderers) if (r != null) r.enabled = visible;
            if (_collider != null) _collider.enabled = visible;
        }
    }
}
