using UnityEngine;

namespace ZombieHouse.Core
{
    /// <summary>
    /// What delivered the hit. Bullet is the default, so every existing construction
    /// site keeps its old meaning; only things that want to be told apart set it.
    /// A target can then care about the difference — a zombie bear takes far more from
    /// a rifle round than from anything else.
    /// </summary>
    public enum DamageKind { Bullet, RifleRound, Blade, Blast }

    /// <summary>Everything a hit needs to know about itself. Passed by value.</summary>
    public struct DamageInfo
    {
        public float Amount;
        public DamageKind Kind;
        public Vector3 Point;
        public Vector3 Normal;
        public Vector3 Direction;
        public GameObject Source;
        public bool IsCritical;

        public DamageInfo(float amount, Vector3 point, Vector3 normal, Vector3 direction, GameObject source,
                          bool isCritical = false, DamageKind kind = DamageKind.Bullet)
        {
            Amount = amount;
            Kind = kind;
            Point = point;
            Normal = normal;
            Direction = direction;
            Source = source;
            IsCritical = isCritical;
        }

        /// <summary>Shorthand for damage with no meaningful world position (falls, scripted events).</summary>
        public static DamageInfo Simple(float amount, GameObject source = null)
        {
            return new DamageInfo(amount, Vector3.zero, Vector3.up, Vector3.zero, source);
        }
    }

    public interface IDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(DamageInfo info);
    }
}
