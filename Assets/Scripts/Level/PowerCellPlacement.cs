using System.Collections.Generic;
using UnityEngine;

namespace ZombieHouse.Level
{
    /// <summary>
    /// Draws this run's power cell position out of a level's candidate list.
    ///
    /// Deliberately uses UnityEngine.Random and not the level's seeded System.Random. The
    /// geometry of a level has to be reproducible — a given seed is always the same forest,
    /// which is what makes a bug reproducible and a route learnable — but the cell is the
    /// one thing that must not be learnable, so it is drawn from the unseeded generator
    /// every time the scene loads.
    /// </summary>
    public static class PowerCellPlacement
    {
        /// <summary>
        /// Picks a candidate, preferring the ones furthest from where the player starts.
        /// Ranking rather than a flat draw keeps the cell out of the first room you walk
        /// into, without ever making it the same room twice.
        /// </summary>
        public static Vector3 Draw(List<Vector3> candidates, Vector3 playerSpawn, Vector3 fallback)
        {
            if (candidates == null || candidates.Count == 0) return fallback;
            if (candidates.Count == 1) return candidates[0];

            // Take the far half of the list by distance from the start, then draw from it.
            var ranked = new List<Vector3>(candidates);
            ranked.Sort((a, b) =>
                (b - playerSpawn).sqrMagnitude.CompareTo((a - playerSpawn).sqrMagnitude));

            int pool = Mathf.Max(2, Mathf.CeilToInt(ranked.Count * 0.6f));
            return ranked[Random.Range(0, pool)];
        }
    }
}
