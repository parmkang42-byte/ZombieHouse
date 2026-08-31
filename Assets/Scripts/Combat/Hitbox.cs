using UnityEngine;
using ZombieHouse.Core;

namespace ZombieHouse.Combat
{
    /// <summary>
    /// Sits on a collider that belongs to a damageable body and forwards hits to it
    /// with a multiplier. Head colliders get a big multiplier; limbs get a small one.
    /// </summary>
    public class Hitbox : MonoBehaviour, IDamageable
    {
        [SerializeField] private MonoBehaviour ownerBehaviour;
        [SerializeField] private float damageMultiplier = 1f;
        [SerializeField] private bool countsAsCritical = false;

        private IDamageable _owner;

        public float Multiplier => damageMultiplier;
        public bool IsCritical => countsAsCritical;
        public bool IsAlive => Owner != null && Owner.IsAlive;

        /// <summary>The body this hitbox belongs to — used to avoid hitting one target twice per swing.</summary>
        public IDamageable OwnerDamageable => Owner;

        private IDamageable Owner
        {
            get
            {
                if (_owner == null)
                {
                    _owner = ownerBehaviour as IDamageable;

                    // Search from the parent up: searching from here would find this Hitbox.
                    if (_owner == null && transform.parent != null)
                        _owner = transform.parent.GetComponentInParent<IDamageable>();
                }
                return _owner;
            }
        }

        public void Configure(MonoBehaviour owner, float multiplier, bool critical)
        {
            ownerBehaviour = owner;
            _owner = owner as IDamageable;
            damageMultiplier = multiplier;
            countsAsCritical = critical;
        }

        public void TakeDamage(DamageInfo info)
        {
            IDamageable owner = Owner;
            if (owner == null || ReferenceEquals(owner, this)) return;

            info.Amount *= damageMultiplier;
            if (countsAsCritical) info.IsCritical = true;
            owner.TakeDamage(info);
        }
    }
}
