using System.Collections.Generic;
using UnityEngine;

namespace ZombieHouse.Level
{
    /// <summary>
    /// What a level has to provide for the rest of the game to run on it: somewhere to
    /// start, somewhere to leave from, places to put pickups, and places for zombies to
    /// be waiting.
    ///
    /// The house and the forest are built completely differently — one is a grid of rooms
    /// with walls, the other is scattered trees on open ground — but both answer these
    /// questions, so LevelDirector, ZombieSpawner and the NavMesh baker work on either
    /// without knowing which they are standing in.
    /// </summary>
    public interface ILevelSource
    {
        bool Generated { get; }
        void Generate();

        Vector3 PlayerSpawn { get; }

        Vector3 ExitPosition { get; }
        bool HasExit { get; }

        /// <summary>Volume the NavMesh is baked over.</summary>
        Bounds LevelBounds { get; }

        List<Vector3> ZombieSpawns { get; }
        List<Vector3> AmmoSpawns { get; }
        List<Vector3> MedkitSpawns { get; }
        List<Vector3> BatterySpawns { get; }

        /// <summary>Where the people you came for are waiting.</summary>
        List<Vector3> SurvivorSpawns { get; }

        /// <summary>
        /// True when this level's survivors are tied up rather than hiding — hostages.
        /// Changes the pose and the prompt; the rescue and the exit gate are identical,
        /// because being tied to a post is a reason to be found, not a different rule.
        /// </summary>
        bool SurvivorsAreBound { get; }

        /// <summary>
        /// Where the power cell is lying this run, and where the motor that wants it is
        /// bolted. They are always a long way apart: the walk back with it is the point.
        ///
        /// The cell moves. <see cref="PowerCellCandidates"/> is every place it could be,
        /// and <see cref="PowerCellSpawn"/> is the one drawn for this run — drawn with
        /// UnityEngine.Random rather than the level's own seeded generator, so the
        /// geometry stays reproducible for a given seed while the cell does not. You
        /// cannot learn where it is; you have to search.
        ///
        /// Verification walks *every* candidate, because any of them could be the one.
        /// </summary>
        List<Vector3> PowerCellCandidates { get; }
        Vector3 PowerCellSpawn { get; }
        Vector3 MotorPosition { get; }

        /// <summary>
        /// Where the two scavenged weapons are lying. Exactly two per level, always — the
        /// Uzi is a power-up rather than a supply, and a third would make it a supply.
        /// </summary>
        List<Vector3> PowerUpSpawns { get; }

        /// <summary>
        /// Belt crates for the scavenged weapon. Several per level, scattered where the
        /// fighting is: the gatling gun cannot be reloaded from ordinary ammunition, so
        /// these are the only thing in the world that keeps it fed.
        /// </summary>
        List<Vector3> BeltCrateSpawns { get; }

        /// <summary>Positions worth lurking in, each facing out from its cover.</summary>
        List<Pose> HidingSpots { get; }
    }

    /// <summary>
    /// An optional extra a level may implement: creatures placed at exact positions the
    /// level chose, rather than drawn against the spawn markers like everything else.
    ///
    /// Separate from <see cref="ILevelSource"/> on purpose. Five levels do not have these
    /// and should not have to answer a question about them, and a level that grows some
    /// later only has to add an interface rather than a stub to every generator in the
    /// project. The jungle's snakes are the first: they are lying in particular fern beds
    /// and particular reeds, and one that wandered in from a marker would not be a snake.
    /// </summary>
    public interface ILurkerSource
    {
        List<Vector3> LurkerSpawns { get; }
    }
}
