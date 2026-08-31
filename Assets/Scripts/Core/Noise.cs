using System;
using UnityEngine;

namespace ZombieHouse.Core
{
    /// <summary>
    /// World-space sound events that AI can hear. Nothing plays audio here — this is
    /// the gameplay-relevant "something made a noise at X, audible within R metres" channel.
    /// Gunshots pull zombies from across the house; footsteps only give you away up close.
    /// </summary>
    public static class Noise
    {
        public static event Action<Vector3, float> Emitted;

        public static void Emit(Vector3 position, float radius)
        {
            Emitted?.Invoke(position, radius);
        }

        /// <summary>Listeners must unsubscribe on destroy; this clears strays on scene reload.</summary>
        public static void Reset()
        {
            Emitted = null;
        }
    }
}
