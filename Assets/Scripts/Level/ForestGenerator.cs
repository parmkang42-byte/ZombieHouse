using System.Collections.Generic;
using UnityEngine;

namespace ZombieHouse.Level
{
    /// <summary>
    /// Builds the forest: a dark stand of trees with a cliff wall around it, a clearing to
    /// start in and a trail out at the far end.
    ///
    /// Where the house is a grid of rooms, this is scattered geometry on open ground, so
    /// none of the wall/floor machinery applies. What it shares with the house is the
    /// <see cref="ILevelSource"/> contract, which is all the rest of the game needs.
    ///
    /// Everything is placed from a seeded random, so a given seed always produces the same
    /// forest — you can learn a route through it, and a bug is reproducible.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class ForestGenerator : MonoBehaviour, ILevelSource
    {
        [Header("Extent")]
        [SerializeField] private int seed = 20260815;
        [SerializeField] private float radius = 70f;
        [SerializeField] private float cliffHeight = 14f;

        [Header("Trees")]
        [Tooltip("Attempts, not trees: each one is dropped if it lands too close to "
                 + "another or inside a clearing, so the real count is lower.")]
        [SerializeField] private int treeAttempts = 1400;
        [Tooltip("Closest two trunks may stand. Do not go below about 3.5 — a zombie bear "
                 + "has a 0.85 m agent radius and needs a 1.7 m gap to path through.")]
        [SerializeField] private float minimumTreeSpacing = 4.2f;
        [SerializeField] private Vector2 trunkHeight = new Vector2(7f, 13f);
        [SerializeField] private Vector2 trunkRadius = new Vector2(0.28f, 0.55f);

        [Header("Undergrowth")]
        [SerializeField] private int rockCount = 66;
        [SerializeField] private int logCount = 44;
        [SerializeField] private int bushCount = 120;

        [Tooltip("Clumps of bush around a boulder. The best cover in the wood: solid in "
                 + "the middle, and wide enough to hide a bear standing still.")]
        [SerializeField] private int thicketCount = 26;

        [Header("Clearings")]
        [SerializeField] private float startClearing = 11f;
        [SerializeField] private float exitClearing = 13f;

        [Header("Contents")]
        [SerializeField] private int ammoCount = 7;
        [SerializeField] private int medkitCount = 9;
        [Tooltip("Spare torch cells out in the wood. More than the house: it is darker, "
                 + "wider, and there is nowhere to shelter while you swap one in.")]
        [SerializeField] private int batteryCount = 6;

        [Tooltip("People still alive out here. Every one of them has to be found before "
                 + "the trail head will open.")]
        [SerializeField] private int survivorCount = 3;
        [SerializeField] private int markedZombieSpawns = 10;

        [Header("Atmosphere")]
        [SerializeField] private bool placeMoonShafts = true;
        [SerializeField] private int moonShaftCount = 14;
        [SerializeField] private Color shaftColour = new Color(0.58f, 0.68f, 0.85f);
        [SerializeField] private float shaftIntensity = 0.85f;
        [SerializeField] private float shaftRange = 18f;

        [Header("Options")]
        [SerializeField] private bool generateOnAwake = true;

        // ---- ILevelSource ---------------------------------------------------

        public bool Generated { get; private set; }
        public Vector3 PlayerSpawn { get; private set; }
        public Vector3 ExitPosition { get; private set; }
        public bool HasExit { get; private set; }
        public Bounds LevelBounds { get; private set; }

        public List<Vector3> ZombieSpawns { get; } = new List<Vector3>();
        public List<Vector3> AmmoSpawns { get; } = new List<Vector3>();
        public List<Vector3> MedkitSpawns { get; } = new List<Vector3>();
        public List<Vector3> BatterySpawns { get; } = new List<Vector3>();
        public List<Vector3> SurvivorSpawns { get; } = new List<Vector3>();

        /// <summary>Hiding in the wood, not tied up.</summary>
        public bool SurvivorsAreBound => false;

        public List<Vector3> PowerUpSpawns { get; } = new List<Vector3>();
        public List<Vector3> BeltCrateSpawns { get; } = new List<Vector3>();
        public List<Vector3> PowerCellCandidates { get; } = new List<Vector3>();
        public Vector3 PowerCellSpawn { get; private set; }
        public Vector3 MotorPosition { get; private set; }
        public List<Pose> HidingSpots { get; } = new List<Pose>();

        private const string ContainerName = "ForestGeometry";
        private Transform _container;
        private System.Random _rng;
        private readonly List<Vector2> _treePositions = new List<Vector2>();

        private void Awake()
        {
            if (generateOnAwake) Generate();
        }

        [ContextMenu("Generate Forest")]
        public void Generate()
        {
            ClearGeometry();
            Reset();

            _rng = new System.Random(seed);
            _container = new GameObject(ContainerName).transform;
            _container.SetParent(transform, false);

            // Start at one edge, leave by the other: the forest has to be crossed.
            PlayerSpawn = transform.position + new Vector3(0f, 0.2f, -(radius - 9f));
            ExitPosition = transform.position + new Vector3(0f, 0f, radius - 9f);
            HasExit = true;

            LevelBounds = new Bounds(
                transform.position + Vector3.up * (cliffHeight * 0.5f),
                new Vector3(radius * 2.4f, cliffHeight + 24f, radius * 2.4f));

            BuildGround();
            BuildCliffs();
            BuildTrees();
            BuildUndergrowth();
            BuildTrail();
            ScatterContents();

            // The motor is at the way out. The cell could be at any of a dozen places
            // around the edge of the wood, none of them on the way to anywhere — the
            // walk to it is off the trail by design.
            MotorPosition = ExitPosition + new Vector3(4.5f, 0f, -2.5f);
            // Two Uzis: one about a third of the way across the wood, one three
            // quarters of the way — so the second is always further in than you want
            // to be when you go for it.
            PowerUpSpawns.Add(PickOpenSpot());
            PowerUpSpawns.Add(PickOpenSpot());

            // Belt crates, spread through the wood so the gatling gun has somewhere to go.
            for (int i = 0; i < 4; i++) BeltCrateSpawns.Add(PickOpenSpot());

            BuildPowerCellCandidates();
            PowerCellSpawn = PowerCellPlacement.Draw(PowerCellCandidates, PlayerSpawn,
                                                     transform.position + Vector3.back * (radius - 16f));

            if (placeMoonShafts) BuildMoonShafts();

            Generated = true;
        }

        private void Reset()
        {
            ZombieSpawns.Clear();
            AmmoSpawns.Clear();
            MedkitSpawns.Clear();
            BatterySpawns.Clear();
            SurvivorSpawns.Clear();
            PowerCellCandidates.Clear();
            PowerUpSpawns.Clear();
            BeltCrateSpawns.Clear();
            HidingSpots.Clear();
            _treePositions.Clear();
            Generated = false;
        }

        [ContextMenu("Clear Forest")]
        public void ClearGeometry()
        {
            var doomed = new List<GameObject>();
            foreach (Transform child in transform)
                if (child.name == ContainerName) doomed.Add(child.gameObject);

            foreach (GameObject go in doomed)
            {
                if (Application.isPlaying)
                {
                    go.transform.SetParent(null, false);
                    Destroy(go);
                }
                else
                {
                    DestroyImmediate(go);
                }
            }

            Generated = false;
        }

        // ---- terrain --------------------------------------------------------

        private void BuildGround()
        {
            CreateBox("Ground", new Vector3(0f, -0.5f, 0f),
                new Vector3(radius * 2.6f, 1f, radius * 2.6f), ProtoMaterials.ForestFloor, true);
        }

        /// <summary>
        /// A ring of cliff faces around the wood. It is the cheapest honest way to bound an
        /// outdoor level: the player cannot walk out, and the NavMesh stops where it does.
        /// </summary>
        private void BuildCliffs()
        {
            const int segments = 28;

            for (int i = 0; i < segments; i++)
            {
                float angle = i * (360f / segments);
                float jitter = Range(-2.5f, 2.5f);

                Vector3 outward = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Vector3 position = outward * (radius + 3f + jitter) + Vector3.up * (cliffHeight * 0.5f);

                var face = CreateBox($"Cliff_{i}", position,
                    new Vector3(radius * 0.42f, cliffHeight + Range(-2f, 4f), 6f),
                    ProtoMaterials.Rock, true);

                face.transform.rotation = Quaternion.Euler(Range(-6f, 6f), angle, Range(-4f, 4f));
            }
        }

        // ---- trees ----------------------------------------------------------

        private void BuildTrees()
        {
            for (int attempt = 0; attempt < treeAttempts; attempt++)
            {
                Vector2 point = RandomPointInWood();
                if (!IsClearOfLandmarks(point)) continue;
                if (TooCloseToATree(point)) continue;

                _treePositions.Add(point);
                BuildTree(_treePositions.Count - 1, point);
            }
        }

        private void BuildTree(int index, Vector2 point)
        {
            float height = Range(trunkHeight.x, trunkHeight.y);
            float thickness = Range(trunkRadius.x, trunkRadius.y);
            Vector3 basePosition = new Vector3(point.x, 0f, point.y);

            // Trunk: the only part with a collider, so the canopy overhead never carves
            // into the NavMesh below it.
            var trunk = CreateBox($"Trunk_{index}", basePosition + Vector3.up * (height * 0.5f),
                new Vector3(thickness, height, thickness), ProtoMaterials.Bark, true);
            trunk.transform.rotation = Quaternion.Euler(Range(-4f, 4f), Range(0f, 360f), Range(-4f, 4f));

            // A leaning, flared, out-of-round trunk instead of a post. Hundreds of these
            // are on screen at once, so it is the single largest visual change available —
            // and it is visual only: the box collider under it does not move.
            PropLibrary.Dress(trunk, "TreeTrunk");

            // Canopy: a few overlapping masses, decoration only.
            int masses = 2 + _rng.Next(0, 2);
            for (int i = 0; i < masses; i++)
            {
                float t = (float)i / masses;
                float spread = Mathf.Lerp(3.4f, 1.8f, t) * Range(0.8f, 1.2f);

                Vector3 offset = new Vector3(Range(-0.8f, 0.8f), height * (0.62f + t * 0.28f), Range(-0.8f, 0.8f));
                CreateDecoration($"Canopy_{index}_{i}", basePosition + offset,
                    new Vector3(spread, spread * 0.75f, spread), ProtoMaterials.Foliage);
            }

            // Somewhere to wait, tucked against the trunk and facing out into the wood.
            Vector3 facing = new Vector3(Range(-1f, 1f), 0f, Range(-1f, 1f));
            if (facing.sqrMagnitude < 0.01f) facing = Vector3.forward;
            facing.Normalize();

            HidingSpots.Add(new Pose(
                transform.position + basePosition + facing * (thickness * 0.5f + 0.5f),
                Quaternion.LookRotation(facing, Vector3.up)));
        }

        private void BuildUndergrowth()
        {
            for (int i = 0; i < rockCount; i++)
            {
                Vector2 point = RandomPointInWood();
                if (!IsClearOfLandmarks(point)) continue;

                float size = Range(0.9f, 2.6f);
                var rock = CreateBox($"Rock_{i}", new Vector3(point.x, size * 0.3f, point.y),
                    new Vector3(size, size * 0.7f, size * Range(0.7f, 1.3f)), ProtoMaterials.Rock, true);
                rock.transform.rotation = Quaternion.Euler(Range(-12f, 12f), Range(0f, 360f), Range(-12f, 12f));

                // Visual only: the box collider under it is untouched, so nothing about
                // pathing changes when this stops being a cube.
                PropLibrary.Dress(rock, "Boulder");

                HidingSpots.Add(new Pose(
                    transform.position + new Vector3(point.x, 0f, point.y) + Vector3.forward * (size * 0.8f),
                    Quaternion.LookRotation(Vector3.forward, Vector3.up)));
            }

            for (int i = 0; i < logCount; i++)
            {
                Vector2 point = RandomPointInWood();
                if (!IsClearOfLandmarks(point)) continue;

                float length = Range(3f, 7f);
                var log = CreateBox($"Log_{i}", new Vector3(point.x, 0.35f, point.y),
                    new Vector3(0.7f, 0.7f, length), ProtoMaterials.Bark, true);
                float lie = Range(0f, 360f);
                log.transform.rotation = Quaternion.Euler(0f, lie, Range(-5f, 5f));

                // Crouched along the far side of it, facing across rather than down it.
                Vector3 across = Quaternion.Euler(0f, lie, 0f) * Vector3.right;
                AddHidingSpot(new Vector3(point.x, 0f, point.y) + across * 0.9f, across);
            }

            for (int i = 0; i < bushCount; i++)
            {
                Vector2 point = RandomPointInWood();
                if (!IsClearOfLandmarks(point)) continue;

                float size = Range(1.1f, 2.4f);
                CreateDecoration($"Bush_{i}", new Vector3(point.x, size * 0.3f, point.y),
                    new Vector3(size, size * 0.65f, size), ProtoMaterials.Foliage);

                // A bush has no collider, so nothing pathing cares about it — but it
                // hides a body completely, which makes it the cheapest ambush in the
                // level: they are simply standing in it, and you cannot see them.
                Vector3 out2 = new Vector3(Range(-1f, 1f), 0f, Range(-1f, 1f));
                AddHidingSpot(new Vector3(point.x, 0f, point.y), out2);
            }

            BuildThickets();
        }

        /// <summary>
        /// A boulder with bush grown up around it. Solid in the middle so it breaks line
        /// of sight properly, soft and wide at the edges so the shape reads as scrub
        /// rather than as a wall — and two places to wait, on opposite sides, so rounding
        /// one is never safe.
        /// </summary>
        private void BuildThickets()
        {
            for (int i = 0; i < thicketCount; i++)
            {
                Vector2 point = RandomPointInWood();
                if (!IsClearOfLandmarks(point)) continue;

                float core = Range(1.2f, 2.2f);
                var boulder = CreateBox($"ThicketRock_{i}", new Vector3(point.x, core * 0.32f, point.y),
                    new Vector3(core, core * 0.8f, core * Range(0.8f, 1.2f)), ProtoMaterials.Rock, true);
                boulder.transform.rotation = Quaternion.Euler(Range(-10f, 10f), Range(0f, 360f), Range(-10f, 10f));

                PropLibrary.Dress(boulder, "Boulder");

                int clumps = 3 + _rng.Next(0, 3);
                for (int c = 0; c < clumps; c++)
                {
                    float angle = (float)c / clumps * Mathf.PI * 2f + Range(-0.4f, 0.4f);
                    float distance = core * Range(0.7f, 1.3f);
                    var offset = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);

                    float size = Range(1.3f, 2.6f);
                    CreateDecoration($"ThicketBush_{i}_{c}",
                        new Vector3(point.x, size * 0.3f, point.y) + offset,
                        new Vector3(size, size * 0.7f, size), ProtoMaterials.Foliage);
                }

                Vector3 centre = new Vector3(point.x, 0f, point.y);
                AddHidingSpot(centre + Vector3.forward * (core * 0.9f), Vector3.forward);
                AddHidingSpot(centre + Vector3.back * (core * 0.9f), Vector3.back);
            }
        }

        /// <summary>
        /// Records somewhere to wait, facing out from the cover. One place so every kind
        /// of obstacle agrees on what a hiding spot means.
        /// </summary>
        private void AddHidingSpot(Vector3 localPosition, Vector3 facing)
        {
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.01f) facing = Vector3.forward;
            facing.Normalize();

            HidingSpots.Add(new Pose(transform.position + localPosition,
                                     Quaternion.LookRotation(facing, Vector3.up)));
        }

        /// <summary>The way out: a lit gap in the cliff with a marked trail leading to it.</summary>
        private void BuildTrail()
        {
            Vector3 exitLocal = ExitPosition - transform.position;

            // Posts either side of the trail head, so the exit reads as a way through.
            for (int side = -1; side <= 1; side += 2)
            {
                CreateBox($"TrailPost_{side}", exitLocal + new Vector3(2.6f * side, 1.6f, 0f),
                    new Vector3(0.45f, 3.2f, 0.45f), ProtoMaterials.Bark, true);
            }

            CreateDecoration("TrailLintel", exitLocal + Vector3.up * 3.4f,
                new Vector3(6f, 0.4f, 0.4f), ProtoMaterials.Bark);

            // A worn path from the clearing to the trail head.
            int steps = 14;
            for (int i = 0; i < steps; i++)
            {
                float t = (float)i / steps;
                Vector3 along = Vector3.Lerp(PlayerSpawn - transform.position, exitLocal, t);
                along.y = 0.02f;

                CreateDecoration($"Path_{i}", along + new Vector3(Range(-1.5f, 1.5f), 0f, 0f),
                    new Vector3(Range(3f, 5f), 0.04f, Range(4f, 7f)), ProtoMaterials.Path);
            }
        }

        private void BuildMoonShafts()
        {
            for (int i = 0; i < moonShaftCount; i++)
            {
                Vector2 point = RandomPointInWood();

                var go = new GameObject($"MoonShaft_{i}");
                go.transform.SetParent(_container, false);
                go.transform.position = transform.position + new Vector3(point.x, 7f, point.y);

                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = shaftColour;
                light.intensity = shaftIntensity;
                light.range = shaftRange;
                LevelLighting.MakeRoomLight(light);
            }
        }

        // ---- contents -------------------------------------------------------

        private void ScatterContents()
        {
            for (int i = 0; i < ammoCount; i++) AmmoSpawns.Add(PickOpenSpot());
            for (int i = 0; i < medkitCount; i++) MedkitSpawns.Add(PickOpenSpot());
            for (int i = 0; i < batteryCount; i++) BatterySpawns.Add(PickOpenSpot());
            for (int i = 0; i < survivorCount; i++) SurvivorSpawns.Add(PickOpenSpot());
            for (int i = 0; i < markedZombieSpawns; i++) ZombieSpawns.Add(PickOpenSpot());
        }

        /// <summary>A spot out in the open, away from the start and the exit.</summary>
        /// <summary>
        /// Twelve places around the outer ring of the wood, evenly spaced by angle so they
        /// are spread right around it, and all of them a long way from the trail between
        /// the start and the exit. The set is seeded — the same wood always offers the
        /// same dozen — but which one holds the cell is not.
        /// </summary>
        private void BuildPowerCellCandidates()
        {
            const int count = 14;
            const float minimumCarryDistance = 40f;

            for (int i = 0; i < count; i++)
            {
                float angle = (i + 0.5f) * (Mathf.PI * 2f / count) + Range(-0.12f, 0.12f);
                float distance = radius * Range(0.62f, 0.84f);

                var point = new Vector2(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance);
                if (!IsClearOfLandmarks(point)) continue;

                Vector3 world = transform.position + new Vector3(point.x, 0f, point.y);

                // The ring passes the trail head, so a few of these would otherwise land
                // near the motor and turn the errand into a button. Verification enforces
                // 20 m; keep well clear of it rather than sitting on the limit.
                if (Vector3.Distance(world, MotorPosition) < minimumCarryDistance) continue;

                PowerCellCandidates.Add(world);
            }

            // A wood with no candidates at all would be unfinishable; fall back to the
            // old fixed spot rather than leaving the list empty.
            if (PowerCellCandidates.Count == 0)
                PowerCellCandidates.Add(transform.position + new Vector3(0f, 0f, -(radius - 16f)));
        }

        private Vector3 PickOpenSpot()
        {
            for (int attempt = 0; attempt < 40; attempt++)
            {
                Vector2 point = RandomPointInWood();
                if (!IsClearOfLandmarks(point)) continue;
                if (TooCloseToATree(point, minimumTreeSpacing * 0.55f)) continue;

                return transform.position + new Vector3(point.x, 0f, point.y);
            }

            return transform.position + new Vector3(Range(-radius * 0.5f, radius * 0.5f), 0f, 0f);
        }

        // ---- helpers --------------------------------------------------------

        private float Range(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

        private Vector2 RandomPointInWood()
        {
            // Rejection-free: sample an angle and a square-rooted radius for even coverage.
            float angle = Range(0f, Mathf.PI * 2f);
            float distance = Mathf.Sqrt((float)_rng.NextDouble()) * (radius - 4f);
            return new Vector2(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance);
        }

        /// <summary>Keeps the start clearing, the exit clearing and the trail open.</summary>
        private bool IsClearOfLandmarks(Vector2 point)
        {
            Vector2 start = new Vector2(PlayerSpawn.x - transform.position.x, PlayerSpawn.z - transform.position.z);
            Vector2 exit = new Vector2(ExitPosition.x - transform.position.x, ExitPosition.z - transform.position.z);

            if (Vector2.Distance(point, start) < startClearing) return false;
            if (Vector2.Distance(point, exit) < exitClearing) return false;

            return true;
        }

        private bool TooCloseToATree(Vector2 point, float spacing = -1f)
        {
            float limit = spacing > 0f ? spacing : minimumTreeSpacing;
            float limitSquared = limit * limit;

            foreach (Vector2 tree in _treePositions)
                if ((tree - point).sqrMagnitude < limitSquared) return true;

            return false;
        }

        private GameObject CreateBox(string name, Vector3 localCentre, Vector3 size, Material material, bool solid)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(_container, false);
            go.transform.position = transform.position + localCentre;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            go.isStatic = true;

            if (!solid)
            {
                var collider = go.GetComponent<Collider>();
                if (collider != null)
                {
                    if (Application.isPlaying) Destroy(collider);
                    else DestroyImmediate(collider);
                }
            }

            return go;
        }

        private GameObject CreateDecoration(string name, Vector3 localCentre, Vector3 size, Material material)
        {
            return CreateBox(name, localCentre, size, material, false);
        }
    }
}
