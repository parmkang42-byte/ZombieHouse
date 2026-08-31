using UnityEngine;
using ZombieHouse.Core;

namespace ZombieHouse.Level
{
    /// <summary>
    /// The way out. Stays sealed until every zombie in the house is down, then lights up
    /// and ends the level when the player steps in.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ExitZone : MonoBehaviour
    {
        [SerializeField] private float sealedIntensity = 0.15f;
        [SerializeField] private float openIntensity = 2.2f;
        [SerializeField] private Color sealedColor = new Color(0.6f, 0.1f, 0.1f);
        [SerializeField] private Color openColor = new Color(0.2f, 0.9f, 0.45f);
        [SerializeField] private float pulseSpeed = 2.5f;

        private Collider _collider;
        private Renderer _renderer;
        private Light _light;
        private MaterialPropertyBlock _mpb;
        private bool _playerInside;

        [Tooltip("Off opens the door from the start. On holds it shut until enough of the " +
                 "house is dead — the share is set on GameManager.")]
        [SerializeField] private bool requiresKillQuota = true;

        public bool IsOpen => !requiresKillQuota
                              || (GameManager.Instance != null && GameManager.Instance.ExitUnlocked);

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            _collider.isTrigger = true;
            _renderer = GetComponentInChildren<Renderer>();
            _light = GetComponentInChildren<Light>();
            _mpb = new MaterialPropertyBlock();
        }

        private void Update()
        {
            bool open = IsOpen;
            float pulse = 0.6f + 0.4f * Mathf.Sin(Time.time * pulseSpeed);
            Color color = open ? openColor : sealedColor;

            if (_renderer != null)
            {
                _renderer.GetPropertyBlock(_mpb);
                Color emissive = color * (open ? pulse * 1.6f : 0.4f);
                _mpb.SetColor("_BaseColor", color);
                _mpb.SetColor("_Color", color);
                _mpb.SetColor("_EmissionColor", emissive);
                _renderer.SetPropertyBlock(_mpb);
            }

            if (_light != null)
            {
                _light.color = color;
                _light.intensity = open ? openIntensity * pulse : sealedIntensity;
            }

            if (_playerInside && open) Escape();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _playerInside = true;
            if (IsOpen) Escape();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _playerInside = false;
        }

        private void Escape()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.PlayerEscaped();
        }
    }
}
