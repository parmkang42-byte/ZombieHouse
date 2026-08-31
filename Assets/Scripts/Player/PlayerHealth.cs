using System;
using UnityEngine;
using ZombieHouse.Core;

namespace ZombieHouse.Player
{
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        [Header("Health")]
        [SerializeField] private float maxHealth = 100f;

        [Header("Regeneration")]
        [SerializeField] private bool regenerates = true;
        [SerializeField] private float regenDelay = 6f;
        [SerializeField] private float regenPerSecond = 4f;
        [SerializeField] private float regenCeiling = 100f;

        [Header("Feedback")]
        [SerializeField] private float hitFlashDuration = 0.35f;

        public float Current { get; private set; }
        public float Max => maxHealth;
        public float Normalized => maxHealth <= 0f ? 0f : Mathf.Clamp01(Current / maxHealth);
        public bool IsAlive => Current > 0f;

        /// <summary>0..1, decays after a hit. The HUD uses it for the red vignette.</summary>
        public float HitFlash { get; private set; }

        /// <summary>Direction the last hit came from, world space. Used for the damage indicator.</summary>
        public Vector3 LastHitDirection { get; private set; }

        public event Action<float> Damaged;
        public event Action<float> Healed;
        public event Action Died;

        private float _lastDamageTime = -999f;

        private void Awake()
        {
            Current = maxHealth;
        }

        private void Update()
        {
            if (HitFlash > 0f)
                HitFlash = Mathf.Max(0f, HitFlash - Time.deltaTime / Mathf.Max(0.01f, hitFlashDuration));

            if (!regenerates || !IsAlive) return;
            if (Time.time - _lastDamageTime < regenDelay) return;
            if (Current >= regenCeiling) return;

            Current = Mathf.Min(regenCeiling, Current + regenPerSecond * Time.deltaTime);
        }

        public void TakeDamage(DamageInfo info)
        {
            if (!IsAlive || info.Amount <= 0f) return;

            Current = Mathf.Max(0f, Current - info.Amount);
            _lastDamageTime = Time.time;
            HitFlash = 1f;
            LastHitDirection = info.Direction.sqrMagnitude > 0.001f
                ? info.Direction.normalized
                : (info.Source != null ? (info.Source.transform.position - transform.position).normalized : Vector3.zero);

            Damaged?.Invoke(info.Amount);

            if (!IsAlive)
            {
                Died?.Invoke();
                if (GameManager.Instance != null) GameManager.Instance.PlayerDied();
            }
        }

        /// <summary>Returns how much was actually restored, so pickups can refuse to be consumed at full health.</summary>
        public float Heal(float amount)
        {
            if (!IsAlive || amount <= 0f) return 0f;
            float before = Current;
            Current = Mathf.Min(maxHealth, Current + amount);
            float restored = Current - before;
            if (restored > 0f) Healed?.Invoke(restored);
            return restored;
        }
    }
}
