using System;
using UnityEngine;

namespace ZombieHouse.Enemies
{
    /// <summary>
    /// How the horde talks to itself. When one walker gets a clear look at you it cries
    /// out, and everything within earshot converges on where you were — which is why
    /// being seen once by one of them is far more dangerous than it sounds.
    ///
    /// Separate from <see cref="Core.Noise"/> on purpose: that channel is about sounds
    /// the player makes, this one is about zombies coordinating.
    /// </summary>
    public static class ZombieComms
    {
        /// <summary>(where the player was, who called it, how far the call carries)</summary>
        public static event Action<Vector3, Vector3, float> HordeCalled;

        public static void CallHorde(Vector3 playerPosition, Vector3 callerPosition, float radius)
        {
            HordeCalled?.Invoke(playerPosition, callerPosition, radius);
        }

        /// <summary>Listeners unsubscribe on destroy; this clears strays on scene reload.</summary>
        public static void Reset()
        {
            HordeCalled = null;
        }
    }
}
