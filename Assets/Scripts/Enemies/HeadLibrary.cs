using UnityEngine;

namespace ZombieHouse.Enemies
{
    /// <summary>
    /// The sculpted heads Blender builds, and the fallback when none have been built.
    ///
    /// WHAT THEY ARE. Tools_Props/zombiehead.py sculpts each head as one solid -- cranium,
    /// face, the jaw already hanging open, the cheeks joining them -- carves the holes that
    /// make a dead face frightening, and writes it out cut into four meshes that are all one
    /// surface: the skull, the jaw, the wounds, and the hair grown off it. Every one of them
    /// is in the Skull part's own space, so each part that wears one simply takes the Skull
    /// part's transform and they cannot disagree about where the mouth is. The teeth are the
    /// same for every variant and live alongside.
    ///
    /// WHY IT IS A LIBRARY AND NOT A HARD DEPENDENCY. Same philosophy as PropLibrary: a missing
    /// mesh is a deliberate no-op. If Blender has not been run, <see cref="Available"/> is
    /// false and ZombieFactory builds the procedural head it always did, so a fresh clone plays
    /// before anyone has installed Blender.
    ///
    /// THE COLLIDER IS UNTOUCHED. The skull mesh is drawn inside the primitive sphere that
    /// carries the 2.5x critical -- the Blender script builds it that way and Test Heads
    /// measures it -- and the other three carry no collider at all.
    /// </summary>
    public static class HeadLibrary
    {
        public const string ResourceFolder = "Heads/";

        /// <summary>Upper bound on variants looked for. Not a count -- the files decide that.</summary>
        public const int MaxVariants = 8;

        /// <summary>One variant: four meshes that together are one continuous surface.</summary>
        public struct Head
        {
            public Mesh Skull;
            public Mesh Jaw;
            public Mesh Gore;
            public Mesh Hair;

            public bool Complete => Skull != null && Jaw != null && Gore != null && Hair != null;
        }

        private static Head[] _variants;
        private static Mesh _teethUpper;
        private static Mesh _teethLower;

        /// <summary>
        /// How many complete variants exist. A variant missing any of its four meshes is not
        /// counted, and neither is anything after it -- a skull with no jaw would hang its face
        /// over empty air.
        /// </summary>
        public static int Count
        {
            get
            {
                Load();
                return _variants.Length;
            }
        }

        /// <summary>Whether the sculpted heads can be used at all.</summary>
        public static bool Available => Count > 0 && TeethUpper != null && TeethLower != null;

        public static Mesh TeethUpper
        {
            get { Load(); return _teethUpper; }
        }

        public static Mesh TeethLower
        {
            get { Load(); return _teethLower; }
        }

        public static Head Variant(int index)
        {
            Load();
            if (_variants.Length == 0) return default;
            return _variants[Mathf.Clamp(index, 0, _variants.Length - 1)];
        }

        /// <summary>
        /// Which variant a given skull mesh belongs to, or -1. Lets a test confirm that the jaw,
        /// wounds and hair a walker is wearing came from the same head as its skull.
        /// </summary>
        public static int IndexOfSkull(Mesh skull)
        {
            Load();
            for (int i = 0; i < _variants.Length; i++)
                if (_variants[i].Skull == skull) return i;
            return -1;
        }

        /// <summary>Drops cached lookups so freshly exported meshes are picked up.</summary>
        public static void ClearCache()
        {
            _variants = null;
            _teethUpper = null;
            _teethLower = null;
        }

        private static void Load()
        {
            if (_variants != null) return;

            var found = new System.Collections.Generic.List<Head>();
            for (int i = 0; i < MaxVariants; i++)
            {
                string stem = ResourceFolder + "ZombieHead" + i;
                var head = new Head
                {
                    Skull = Resources.Load<Mesh>(stem),
                    Jaw = Resources.Load<Mesh>(stem + "Jaw"),
                    Gore = Resources.Load<Mesh>(stem + "Gore"),
                    Hair = Resources.Load<Mesh>(stem + "Hair")
                };

                if (!head.Complete) break;
                found.Add(head);
            }

            _variants = found.ToArray();
            _teethUpper = Resources.Load<Mesh>(ResourceFolder + "ZombieTeethUpper");
            _teethLower = Resources.Load<Mesh>(ResourceFolder + "ZombieTeethLower");
        }
    }
}
