using System.Collections.Generic;
using UnityEngine;

namespace ZombieHouse.Audio
{
    /// <summary>What is under a foot, for the purpose of the noise it makes.</summary>
    public enum StepSurface
    {
        Boards,   // the mansion's floorboards, the school's sprung gym floor
        Tile,     // linoleum over concrete
        Stone,    // the pyramid's sandstone, granite, anything cut and laid
        Sand,
        Metal,    // the Cormorant's deck plate
        Dirt,     // beaten earth, dust, a path
        Grass,
        Leaves,   // forest and jungle litter
    }

    /// <summary>
    /// Works out what the ground under a foot is and which step sound it makes.
    ///
    /// READ OFF THE MATERIAL, NOT OFF A TAG. Every floor in the game is already built with its
    /// own material -- the house's boards, the school's lino, the ship's deck plate -- so the
    /// name of the material a downward ray lands on is the surface, and nothing in the eight
    /// level generators had to change to say so. A tag or a component would have meant touching
    /// all eight and rebuilding all eight scenes to store it.
    ///
    /// The cost is that a floor built from a *new* material is silently unmapped and falls back to
    /// the generic step. That is exactly what Test Footsteps looks for: it samples the walkable
    /// ground of all eight levels and fails on anything it cannot name. Nine of the entries below
    /// are here because it found them -- the forest's and the jungle's boulders, the town's
    /// boardwalk and adobe and mesa, the ship's hull plating and catwalk gratings, Merryland's
    /// stall decks and castle walkway -- all walkable, none of them a thing anyone would have
    /// thought to list, and all of them silent-by-fallback until the test said so.
    ///
    /// Before this, every step in the game -- eight levels, player and walkers -- played one of two
    /// clips, and the player's was the sand one everywhere, including on floorboards.
    /// </summary>
    public static class StepSurfaces
    {
        /// <summary>How far below the foot to look. A step is taken standing on something.</summary>
        public const float Reach = 1.4f;

        private static readonly Dictionary<string, StepSurface> ByMaterial =
            new Dictionary<string, StepSurface>
            {
                // house
                { "floor", StepSurface.Boards },
                { "flooralt", StepSurface.Boards },
                { "ground", StepSurface.Dirt },
                { "wood", StepSurface.Boards },

                // school
                { "linoleum", StepSurface.Tile },
                { "linoleumalt", StepSurface.Tile },
                { "gymfloor", StepSurface.Boards },

                // pyramid
                { "sandstone", StepSurface.Stone },
                { "sandstonealt", StepSurface.Stone },
                { "sand", StepSurface.Sand },
                { "granite", StepSurface.Stone },

                // the Cormorant. Grating rings differently from plate in life, but it is steel
                // underfoot and a ninth recipe for the catwalks would be a detail nobody asked for.
                { "deckplate", StepSurface.Metal },
                { "deckplatealt", StepSurface.Metal },
                { "hullplate", StepSurface.Metal },
                { "grating", StepSurface.Metal },

                // outdoors
                { "forestfloor", StepSurface.Leaves },
                { "junglefloor", StepSurface.Leaves },
                { "parkgrass", StepSurface.Grass },
                { "dust", StepSurface.Dirt },
                { "path", StepSurface.Dirt },
                { "rock", StepSurface.Stone },

                // the town: a boardwalk over dust, adobe steps, and the mesa itself
                { "plank", StepSurface.Boards },
                { "plankpale", StepSurface.Boards },
                { "adobe", StepSurface.Stone },
                { "mesa", StepSurface.Stone },

                // Merryland: the stall decks, the castle's walkway, the wheel's frame
                { "stallwood", StepSurface.Boards },
                { "castlestone", StepSurface.Stone },
                { "ferrissteel", StepSurface.Metal },
            };

        /// <summary>The surface a material is, or null when it is not a floor we know.</summary>
        public static StepSurface? ForMaterial(string materialName)
        {
            string key = Key(materialName);
            if (key == null) return null;
            return ByMaterial.TryGetValue(key, out StepSurface surface) ? surface : (StepSurface?)null;
        }

        /// <summary>
        /// Material names arrive in three shapes: the asset's own name ("floor"), the in-memory
        /// fallback ProtoMaterials makes when the asset is missing ("Proto_floor"), and Unity's
        /// runtime copy of either ("floor (Instance)").
        /// </summary>
        public static string Key(string materialName)
        {
            if (string.IsNullOrEmpty(materialName)) return null;

            string key = materialName;
            int instance = key.IndexOf(" (Instance)", System.StringComparison.Ordinal);
            if (instance >= 0) key = key.Substring(0, instance);
            if (key.StartsWith("Proto_", System.StringComparison.Ordinal)) key = key.Substring(6);
            return key.Trim().ToLowerInvariant();
        }

        /// <summary>
        /// What is under this foot. Looks down from just above it, so a foot resting exactly on
        /// the floor still finds the floor, and ignores anything without a renderer to read.
        /// </summary>
        public static StepSurface? Under(Vector3 foot)
        {
            var hits = Physics.RaycastAll(foot + Vector3.up * 0.2f, Vector3.down, Reach + 0.2f,
                                          ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            // The nearest thing whose material we recognise. Walking over a prop -- a rug's box, a
            // crate -- should not silence the step; the floor is under it either way.
            foreach (RaycastHit hit in hits)
            {
                var renderer = hit.collider.GetComponent<Renderer>();
                if (renderer == null) continue;

                StepSurface? surface = ForMaterial(renderer.sharedMaterial != null
                                                       ? renderer.sharedMaterial.name : null);
                if (surface.HasValue) return surface;
            }
            return null;
        }

        /// <summary>The step sound for a surface; the generic one when it is not known.</summary>
        public static Sfx Step(StepSurface? surface)
        {
            switch (surface)
            {
                case StepSurface.Boards: return Sfx.FootstepBoards;
                case StepSurface.Tile:   return Sfx.FootstepTile;
                case StepSurface.Stone:  return Sfx.FootstepStone;
                case StepSurface.Sand:   return Sfx.FootstepSand;
                case StepSurface.Metal:  return Sfx.FootstepMetal;
                case StepSurface.Dirt:   return Sfx.FootstepDirt;
                case StepSurface.Grass:  return Sfx.FootstepGrass;
                case StepSurface.Leaves: return Sfx.FootstepLeaves;
                default:                 return Sfx.Footstep;
            }
        }

        /// <summary>
        /// How loud this surface is relative to the others. A boot on steel plate is a different
        /// event from the same boot in sand, and levelling them all to the same volume is most of
        /// why one clip for everything sounded wrong even before it sounded repetitive.
        /// </summary>
        public static float Loudness(StepSurface? surface)
        {
            switch (surface)
            {
                case StepSurface.Metal:  return 1.15f;
                case StepSurface.Tile:   return 1.0f;
                case StepSurface.Boards: return 0.95f;
                case StepSurface.Stone:  return 0.9f;
                case StepSurface.Leaves: return 0.8f;
                case StepSurface.Dirt:   return 0.7f;
                case StepSurface.Sand:   return 0.65f;
                case StepSurface.Grass:  return 0.6f;
                default:                 return 0.85f;
            }
        }
    }
}
