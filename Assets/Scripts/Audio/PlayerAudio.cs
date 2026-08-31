using UnityEngine;
using ZombieHouse.Core;
using ZombieHouse.Player;

namespace ZombieHouse.Audio
{
    /// <summary>
    /// The player's own sounds: footsteps matched to stance, hurt grunts, and a heartbeat
    /// that fades in as health drops. The heartbeat is the main "you are in trouble"
    /// signal, so it is driven straight off the health fraction rather than off events.
    /// </summary>
    public class PlayerAudio : MonoBehaviour
    {
        [Header("Footsteps")]
        [SerializeField] private float walkVolume = 0.2f;
        [SerializeField] private float sprintVolume = 0.32f;
        [SerializeField] private float crouchVolume = 0.08f;

        [Header("Heartbeat")]
        [SerializeField] private float heartbeatBelowHealth = 0.45f;
        [SerializeField] private float heartbeatMaxVolume = 0.7f;
        [SerializeField] private float heartbeatSlowestInterval = 1.15f;
        [SerializeField] private float heartbeatFastestInterval = 0.62f;

        private PlayerController _controller;
        private PlayerHealth _health;
        private Flashlight _flashlight;
        private float _nextHeartbeat;

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
            _health = GetComponent<PlayerHealth>();

            // The torch hangs off the camera, so it is a child rather than a sibling.
            _flashlight = GetComponentInChildren<Flashlight>();
        }

        private void OnEnable()
        {
            if (_controller != null)
            {
                _controller.Stepped += OnStepped;
                _controller.Jumped += OnJumped;
                _controller.Landed += OnLanded;
            }
            if (_health != null)
            {
                _health.Damaged += OnDamaged;
                _health.Healed += OnHealed;
                _health.Died += OnDied;
            }
            if (_flashlight != null)
            {
                _flashlight.Toggled += OnFlashlightToggled;
                _flashlight.Died += OnFlashlightDied;
                _flashlight.Swapped += OnBatterySwapped;
            }
        }

        private void OnDisable()
        {
            if (_controller != null)
            {
                _controller.Stepped -= OnStepped;
                _controller.Jumped -= OnJumped;
                _controller.Landed -= OnLanded;
            }
            if (_health != null)
            {
                _health.Damaged -= OnDamaged;
                _health.Healed -= OnHealed;
                _health.Died -= OnDied;
            }
            if (_flashlight != null)
            {
                _flashlight.Toggled -= OnFlashlightToggled;
                _flashlight.Died -= OnFlashlightDied;
                _flashlight.Swapped -= OnBatterySwapped;
            }
        }

        private void Update()
        {
            if (_health == null || !_health.IsAlive || !GameManager.GameplayActive) return;

            float health = _health.Normalized;
            if (health > heartbeatBelowHealth) return;

            // Closer to death: louder and faster.
            float severity = 1f - Mathf.Clamp01(health / heartbeatBelowHealth);

            if (Time.time < _nextHeartbeat) return;

            GameAudio.Play2D(Sfx.Heartbeat, heartbeatMaxVolume * severity);
            _nextHeartbeat = Time.time + Mathf.Lerp(heartbeatSlowestInterval, heartbeatFastestInterval, severity);
        }

        private void OnStepped()
        {
            float volume = walkVolume;
            if (_controller != null)
            {
                if (_controller.IsCrouching) volume = crouchVolume;
                else if (_controller.IsSprinting) volume = sprintVolume;
            }

            GameAudio.Play2D(Sfx.FootstepSand, volume, 0.14f);
        }

        private void OnJumped()
        {
            GameAudio.Play2D(Sfx.Jump, 0.4f, 0.1f);
        }

        private void OnLanded()
        {
            GameAudio.Play2D(Sfx.Land, 0.5f, 0.1f);
        }

        private void OnDamaged(float amount)
        {
            GameAudio.Play2D(Sfx.PlayerHurt, 0.8f, 0.1f);
        }

        private void OnHealed(float amount)
        {
            GameAudio.Play2D(Sfx.PickupHealth, 0.5f);
        }

        private void OnDied()
        {
            GameAudio.Play2D(Sfx.PlayerHurt, 1f);
        }

        private void OnFlashlightToggled()
        {
            GameAudio.Play2D(Sfx.FlashlightClick, 0.45f, 0.06f);
        }

        // Quiet on purpose: the light going out is the loud part, not the sound.
        private void OnFlashlightDied()
        {
            GameAudio.Play2D(Sfx.FlashlightDie, 0.4f);
        }

        private void OnBatterySwapped()
        {
            GameAudio.Play2D(Sfx.BatterySwap, 0.5f);
        }
    }
}
