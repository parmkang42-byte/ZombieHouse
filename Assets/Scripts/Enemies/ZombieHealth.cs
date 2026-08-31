using System;
using UnityEngine;
using UnityEngine.AI;
using ZombieHouse.Core;

namespace ZombieHouse.Enemies
{
    public class ZombieHealth : MonoBehaviour, IDamageable
    {
        [Header("Health")]
        [SerializeField] private float maxHealth = 100f;

        [Header("Reaction")]
        [SerializeField] private float flinchDuration = 0.18f;

        [Header("Death")]
        [Tooltip("Corpses are permanent. Set a positive value to remove bodies after N seconds instead.")]
        [SerializeField] private float removeCorpseAfterSeconds = 0f;

        public float Current { get; private set; }
        public float Max => maxHealth;
        public bool IsAlive => Current > 0f;

        public event Action<DamageInfo> Damaged;
        public event Action<DamageInfo> Died;

        private ZombieAI _ai;
        private NavMeshAgent _agent;
        private ZombieAppearance _appearance;
        private ZombieRagdoll _ragdoll;
        private bool _dead;

        /// <summary>Called by ZombieProfile before Awake, so Awake copies the right value across.</summary>
        public void ApplyArchetype(ZombieArchetype archetype)
        {
            if (archetype == null) return;

            _archetype = archetype;
            maxHealth = archetype.Health;
            Current = maxHealth;
        }

        private ZombieArchetype _archetype;

        private void Awake()
        {
            Current = maxHealth;
            _ai = GetComponent<ZombieAI>();
            _agent = GetComponent<NavMeshAgent>();
            _appearance = GetComponent<ZombieAppearance>();
            _ragdoll = GetComponent<ZombieRagdoll>();
        }

        private void Start()
        {
            if (GameManager.Instance != null) GameManager.Instance.RegisterZombie(gameObject);
        }

        public void TakeDamage(DamageInfo info)
        {
            if (_dead || info.Amount <= 0f) return;

            // Per-type vulnerability, applied after the hitbox's own multiplier so a
            // rifle round to the skull gets both.
            if (info.Kind == DamageKind.RifleRound && _archetype != null)
                info.Amount *= _archetype.RifleDamageMultiplier;

            Current -= info.Amount;
            Damaged?.Invoke(info);

            if (_appearance != null) _appearance.FlashHit();
            if (_ai != null) _ai.OnDamaged(info, flinchDuration);

            if (Current <= 0f) Die(info);
        }

        /// <summary>
        /// Kills outright, whatever health remains. Used for wounds no amount of health
        /// survives — losing a head, or a leg it can no longer stand on.
        /// </summary>
        public void Kill(DamageInfo info)
        {
            if (_dead) return;

            Current = 0f;
            Damaged?.Invoke(info);
            Die(info);
        }

        private void Die(DamageInfo info)
        {
            _dead = true;
            Current = 0f;

            if (_ai != null) _ai.OnDied();

            Died?.Invoke(info);
            if (GameManager.Instance != null) GameManager.Instance.ReportZombieKilled(gameObject);

            // Physics takes it from here: the body collapses under its own weight and
            // stays where it lands. Colliders stay enabled so the corpse is solid.
            if (_ragdoll != null)
            {
                _ragdoll.Collapse(info);
            }
            else
            {
                // No ragdoll component — fall back to simply stopping dead.
                if (_agent != null && _agent.isActiveAndEnabled) _agent.enabled = false;
            }

            if (removeCorpseAfterSeconds > 0f) Destroy(gameObject, removeCorpseAfterSeconds);
        }
    }
}
