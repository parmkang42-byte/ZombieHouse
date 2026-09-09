using System.Collections.Generic;
using UnityEngine;

namespace ZombieHouse.Level
{
    /// <summary>
    /// Builds the jungle: a river valley under a closed canopy, with a ruined temple gate
    /// at the far end and almost no sky.
    ///
    /// The forest and the jungle are both scattered geometry on open ground, so the shape
    /// of this file follows <see cref="ForestGenerator"/> — but they are different places
    /// to fight in, and the differences are all deliberate:
    ///
    /// **The canopy is a ceiling.** The forest has moonlight coming down between the trees;
    /// the jungle has a roof of leaves twelve metres up with a handful of gaps in it. What
    /// light there is comes from those gaps and from your torch, and everything else is a
    /// silhouette.
    ///
    /// **The river cuts it in half.** A shallow channel runs corner to corner with three
    /// crossings. You can wade it — it is not a wall — but you are in the open for nine
    /// metres while you do, and the crossings are exactly where the level puts its best
    /// ambush cover.
    ///
    /// **Sightlines are short and the ground is busy.** Buttress roots, fallen trunks and
    /// fern beds mean something is always within eight metres of you and out of sight. The
    /// forest is about the dark; the jungle is about the clutter.
    ///
    /// Everything is placed from a seeded random, so a given seed always produces the same
    /// valley — you can learn a route through it, and a bug is reproducible.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class JungleGenerator : MonoBehaviour, ILevelSource, ILurkerSource
    {
        [Header("Extent")]
        [SerializeField] private int seed = 20260826;
        [SerializeField] private float radius = 74f;
        [SerializeField] private float wallHeight = 18f;

        [Tooltip("Height of the leaf ceiling. It is a real roof, not decoration: it is "
                 + "what makes the valley feel like a room rather than a wood.")]
        [SerializeField] private float canopyHeight = 12f;

        [Header("Trees")]
        [Tooltip("Attempts, not trees: each is dropped if it lands too close to another, "
                 + "in the river, or in a clearing.")]
        [SerializeField] private int treeAttempts = 1500;
        [Tooltip("Closest two trunks may stand. A jaguar has a 0.55 m agent radius, so "
                 + "this cannot go much below 3.2 without sealing lanes off.")]
        [SerializeField] private float minimumTreeSpacing = 4.6f;
        [SerializeField] private Vector2 trunkHeight = new Vector2(9f, 16f);
        [SerializeField] private Vector2 trunkRadius = new Vector2(0.34f, 0.72f);

        [Tooltip("Share of trees that get buttress roots flaring out from the base. Those "
                 + "roots are solid, and they are the best cover in the valley.")]
        [Range(0f, 1f)] [SerializeField] private float buttressShare = 0.22f;

        [Header("Undergrowth")]
        [SerializeField] private int fernCount = 190;
        [SerializeField] private int logCount = 46;
        [SerializeField] private int boulderCount = 40;
        [SerializeField] private int vineCount = 90;

        [Tooltip("Temple ruins: a wall stub and a toppled column. Solid, chest high, and "
                 + "the only cover you can shoot over rather than around.")]
        [SerializeField] private int ruinCount = 22;

        [Header("River")]
        [SerializeField] private bool cutRiver = true;
        [SerializeField] private float riverWidth = 9f;
        [Tooltip("How many places the river can be crossed. Fewer is a harder level: the "
                 + "crossings are where everything meets you.")]
        [SerializeField] private int crossingCount = 3;

        [Header("Clearings")]
        [SerializeField] private float startClearing = 12f;
        [SerializeField] private float exitClearing = 13f;

        [Header("Contents")]
        [SerializeField] private int ammoCount = 8;
        [SerializeField] private int medkitCount = 10;
        [Tooltip("Spare torch cells. The most of any level: under a closed canopy the "
                 + "torch is not an aid, it is the only way to see anything at all.")]
        [SerializeField] private int batteryCount = 7;

        [Tooltip("People still alive out here. Every one has to be found before the "
                 + "temple gate will open.")]
        [SerializeField] private int survivorCount = 3;
        [SerializeField] private int markedZombieSpawns = 12;

        [Header("Atmosphere")]
        [SerializeField] private bool placeCanopyGaps = true;
        [Tooltip("Holes in the leaf roof. Keep this low — every one is a light, and a "
                 + "hundred lights is what makes a scene look flat and cost frames.")]
        [SerializeField] private int canopyGapCount = 11;
        [SerializeField] private Color gapColour = new Color(0.52f, 0.62f, 0.55f);
        [SerializeField] private float gapIntensity = 0.7f;
        [SerializeField] private float gapRange = 17f;

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

        /// <summary>Hiding under cover, not tied up — the same as the wood.</summary>
        public bool SurvivorsAreBound => false;

        public List<Vector3> PowerUpSpawns { get; } = new List<Vector3>();
        public List<Vector3> BeltCrateSpawns { get; } = new List<Vector3>();
        public List<Vector3> PowerCellCandidates { get; } = new List<Vector3>();
        public Vector3 PowerCellSpawn { get; private set; }
        public Vector3 MotorPosition { get; private set; }
        public List<Pose> HidingSpots { get; } = new List<Pose>();

        /// <summary>
        /// Where the snakes are lying in wait. Not part of the spawner's population — the
        /// level places these itself, low in the fern beds and along the water, because a
        /// snake that wandered in from a marker like everything else would just be a
        /// short jaguar.
        /// </summary>
        public List<Vector3> SnakeSpawns { get; } = new List<Vector3>();

        /// <summary>Under the tallest trees, where the monkeys come down from.</summary>
        public List<Vector3> CanopySpawns { get; } = new List<Vector3>();

        /// <summary>The snakes, as the spawner asks for them.</summary>
        public List<Vector3> LurkerSpawns => SnakeSpawns;

        private const string ContainerName = "JungleGeometry";
        private Transform _container;
        private System.Random _rng;
        private readonly List<Vector2> _treePositions = new List<Vector2>();
        private readonly List<Vector2> _buttressed = new List<Vector2>();
        private readonly List<Vector2> _crossings = new List<Vector2>();

        /// The river runs corner to corner through the origin, so it splits the valley
        /// diagonally and neither half is the "back" of the level.
        private static Vector2 RiverDirection => new Vector2(0.7071f, 0.7071f);

        private void Awake()
        {
            if (generateOnAwake) Generate();
        }

        [ContextMenu("Generate Jungle")]
        public void Generate()
        {
            ClearGeometry();
            Reset();

            _rng = new System.Random(seed);
            _container = new GameObject(ContainerName).transform;
            _container.SetParent(transform, false);

            // The river runs bottom-left to top-right, so the two banks are the other
            // diagonal: start on the north-west bank, leave from the south-east one.
            //
            // Both ends are placed along that diagonal at a *radius*, not by setting x and
            // z to radius-14 each. The valley is a circle, and a corner at (-60, 60) is 85
            // metres from the middle of a 74-metre valley — outside the wall, on the
            // apron of ground beyond it, connected to nothing. That is exactly what the
            // first cut of this level did, and it read as "the river has sealed the
            // valley" because from out there everything inside really is unreachable.
            Vector2 bankAxis = new Vector2(-0.7071f, 0.7071f);   // across the river
            float endDistance = radius - 14f;

            PlayerSpawn = transform.position
                + new Vector3(bankAxis.x * endDistance, 0.2f, bankAxis.y * endDistance);
            ExitPosition = transform.position
                + new Vector3(-bankAxis.x * endDistance, 0f, -bankAxis.y * endDistance);
            HasExit = true;

            LevelBounds = new Bounds(
                transform.position + Vector3.up * (wallHeight * 0.5f),
                new Vector3(radius * 2.4f, wallHeight + 28f, radius * 2.4f));

            PlaceCrossings();

            BuildGround();
            BuildValleyWalls();
            if (cutRiver) BuildRiver();
            BuildTrees();
            BuildUndergrowth();
            BuildRuins();
            BuildTrail();
            ScatterContents();

            // The motor is at the gate, on the far bank. The cell is somewhere around the
            // rim — which means at least one crossing made carrying it.
            MotorPosition = ExitPosition + new Vector3(-4.5f, 0f, -2.5f);

            PowerUpSpawns.Add(PickOpenSpot());
            PowerUpSpawns.Add(PickOpenSpot());

            for (int i = 0; i < 5; i++) BeltCrateSpawns.Add(PickOpenSpot());

            BuildPowerCellCandidates();
            PowerCellSpawn = PowerCellPlacement.Draw(PowerCellCandidates, PlayerSpawn,
                                                     transform.position + Vector3.left * (radius - 18f));

            if (placeCanopyGaps) BuildCanopyGaps();

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
            SnakeSpawns.Clear();
            CanopySpawns.Clear();
            _treePositions.Clear();
            _buttressed.Clear();
            _crossings.Clear();
            Generated = false;
        }

        [ContextMenu("Clear Jungle")]
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
                new Vector3(radius * 2.6f, 1f, radius * 2.6f), ProtoMaterials.JungleFloor, true);

            // Patches of bare mud where the leaf litter has been walked off. Flat
            // decoration, but it stops the floor reading as one enormous brown square.
            for (int i = 0; i < 34; i++)
            {
                Vector2 point = RandomPointInValley();
                CreateDecoration($"Mud_{i}", new Vector3(point.x, 0.02f, point.y),
                    new Vector3(Range(3f, 8f), 0.04f, Range(3f, 8f)), ProtoMaterials.Mud);
            }
        }

        /// <summary>
        /// The valley sides, and the leaf roof over the top. Together they close the level
        /// in on all six faces — the cliffs do the same job for the wood, except that here
        /// the lid matters as much as the walls.
        /// </summary>
        private void BuildValleyWalls()
        {
            const int segments = 30;

            for (int i = 0; i < segments; i++)
            {
                float angle = i * (360f / segments);
                float jitter = Range(-2.5f, 2.5f);

                Vector3 outward = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Vector3 position = outward * (radius + 3f + jitter) + Vector3.up * (wallHeight * 0.5f);

                var face = CreateBox($"Valley_{i}", position,
                    new Vector3(radius * 0.42f, wallHeight + Range(-2f, 5f), 6f),
                    ProtoMaterials.Rock, true);

                face.transform.rotation = Quaternion.Euler(Range(-6f, 6f), angle, Range(-4f, 4f));

                // Growth down the rock face, so the wall is not a bare grey band at the
                // edge of every screenshot.
                var moss = CreateDecoration($"ValleyMoss_{i}",
                    outward * (radius + 0.4f) + Vector3.up * Range(2f, wallHeight * 0.7f),
                    new Vector3(radius * 0.34f, Range(2f, 5f), 1.4f), ProtoMaterials.Moss);
                moss.transform.rotation = Quaternion.Euler(0f, angle, 0f);
            }

            // The lid. Overlapping slabs rather than one plane, so the gaps the light
            // comes through are real holes rather than a texture.
            for (int x = -4; x <= 4; x++)
            {
                for (int z = -4; z <= 4; z++)
                {
                    if (Chance(0.13f)) continue;   // this one is a gap

                    var at = new Vector3(x * (radius * 0.44f), canopyHeight + Range(-0.8f, 1.6f),
                                         z * (radius * 0.44f));

                    CreateDecoration($"CanopySlab_{x}_{z}", at,
                        new Vector3(radius * 0.5f, 1.6f, radius * 0.5f), ProtoMaterials.Canopy);
                }
            }
        }

        /// <summary>
        /// Picks where the river can be crossed, evenly spread along its length. Everything
        /// the level does with the river — the banks, the cover, the snakes — hangs off
        /// these, so they are chosen before any geometry exists.
        /// </summary>
        private void PlaceCrossings()
        {
            int count = Mathf.Max(1, crossingCount);

            for (int i = 0; i < count; i++)
            {
                float t = (i + 0.5f) / count;
                float along = Mathf.Lerp(-(radius - 14f), radius - 14f, t) + Range(-5f, 5f);
                _crossings.Add(RiverDirection * along);
            }
        }

        /// <summary>
        /// A shallow channel corner to corner, with muddy banks and a felled trunk laid
        /// across at each crossing.
        ///
        /// The water has no collider: you wade it. What it costs you is that it is open —
        /// nine metres with nothing to stand behind — and that the crossings are exactly
        /// where the level has put the best ambush cover.
        /// </summary>
        private void BuildRiver()
        {
            Vector2 direction = RiverDirection;
            Vector2 across = new Vector2(-direction.y, direction.x);
            float angle = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;

            const int steps = 30;
            for (int i = 0; i < steps; i++)
            {
                float t = (float)i / (steps - 1);
                Vector2 point = direction * Mathf.Lerp(-(radius + 4f), radius + 4f, t);
                float width = riverWidth + Range(-1.2f, 1.8f);

                var water = CreateDecoration($"Water_{i}", new Vector3(point.x, 0.03f, point.y),
                    new Vector3(width, 0.06f, radius * 0.16f), ProtoMaterials.Water);
                water.transform.rotation = Quaternion.Euler(0f, angle, 0f);

                // Banks either side: mud, and now and then a lip of stone that breaks the
                // sightline along the channel so it is not a firing lane end to end.
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector2 bank = point + across * ((width * 0.5f + 1.2f) * s);

                    var mud = CreateDecoration($"Bank_{i}_{s}", new Vector3(bank.x, 0.04f, bank.y),
                        new Vector3(3.2f, 0.08f, radius * 0.15f), ProtoMaterials.Mud);
                    mud.transform.rotation = Quaternion.Euler(0f, angle, 0f);

                    // Never at a crossing, and never in a clearing. The stones are there
                    // to stop the channel being a firing lane end to end; at a crossing
                    // they would instead be a wall across the only way over, which is the
                    // same silent mistake the school made with a locker bank in a doorway.
                    if (IsNearACrossing(point)) continue;
                    if (!IsClearOfLandmarks(bank)) continue;
                    if (!Chance(0.4f)) continue;

                    var lip = CreateBox($"BankStone_{i}_{s}", new Vector3(bank.x, 0.4f, bank.y),
                        new Vector3(Range(1.4f, 3f), Range(0.7f, 1.3f), Range(1.4f, 3f)),
                        ProtoMaterials.Rock, true);
                    lip.transform.rotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);

                    var outwards = new Vector3(-across.x * s, 0f, -across.y * s);
                    AddHidingSpot(new Vector3(bank.x, 0f, bank.y) + outwards, outwards);
                }
            }

            for (int c = 0; c < _crossings.Count; c++)
            {
                Vector2 at = _crossings[c];

                var span = CreateBox($"Crossing_{c}", new Vector3(at.x, 0.25f, at.y),
                    new Vector3(riverWidth + 7f, 0.5f, 3.4f), ProtoMaterials.Bark, true);
                span.transform.rotation = Quaternion.Euler(0f, angle + 90f, 0f);

                for (int s = -1; s <= 1; s += 2)
                {
                    Vector2 bank = at + across * ((riverWidth * 0.5f + 3.4f) * s);
                    if (!IsClearOfLandmarks(bank)) continue;

                    var stump = CreateBox($"CrossingStump_{c}_{s}", new Vector3(bank.x, 0.8f, bank.y),
                        new Vector3(2.2f, 1.6f, 2.2f), ProtoMaterials.Bark, true);
                    stump.transform.rotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);

                    // Facing the water: whatever waits here is waiting for you to step
                    // onto the log.
                    var towardsWater = new Vector3(-across.x * s, 0f, -across.y * s);
                    AddHidingSpot(new Vector3(bank.x, 0f, bank.y) + towardsWater * 1.6f, towardsWater);
                }

                // A snake in the reeds at every crossing. This is the one place in the
                // level where you have to be in the open, so it is the one place the
                // thing you cannot see until it moves is guaranteed to be.
                SnakeSpawns.Add(transform.position + new Vector3(
                    at.x + across.x * (riverWidth * 0.5f + 2f), 0f,
                    at.y + across.y * (riverWidth * 0.5f + 2f)));
            }
        }

        // ---- trees ----------------------------------------------------------

        private void BuildTrees()
        {
            for (int attempt = 0; attempt < treeAttempts; attempt++)
            {
                Vector2 point = RandomPointInValley();
                if (!IsClearOfLandmarks(point)) continue;
                if (IsInTheRiver(point)) continue;
                if (TooCloseToATree(point)) continue;
                if (TooCloseToRoots(point)) continue;

                _treePositions.Add(point);
                BuildTree(_treePositions.Count - 1, point);
            }
        }

        private void BuildTree(int index, Vector2 point)
        {
            float height = Range(trunkHeight.x, trunkHeight.y);
            float thickness = Range(trunkRadius.x, trunkRadius.y);
            Vector3 basePosition = new Vector3(point.x, 0f, point.y);

            // Trunk: the only part of a tree with a collider, so the crown overhead never
            // carves into the NavMesh below it.
            var trunk = CreateBox($"Trunk_{index}", basePosition + Vector3.up * (height * 0.5f),
                new Vector3(thickness, height, thickness),
                Chance(0.3f) ? ProtoMaterials.BarkPale : ProtoMaterials.Bark, true);
            trunk.transform.rotation = Quaternion.Euler(Range(-3f, 3f), Range(0f, 360f), Range(-3f, 3f));

            // The valley's trunks carry their own buttress roots in the mesh. The separate
            // Buttress boxes below stay: they are the *collision* for that cover, and the
            // rule here is that a prop mesh never changes what you can walk through.
            PropLibrary.Dress(trunk, "JungleTrunk");

            // Buttress roots: three or four solid fins flaring out from the base. This is
            // the jungle's signature cover — waist high, and it wraps a corner.
            //
            // They are also the reason the valley can seal itself. A buttressed trunk is
            // about 2.6 m across rather than 0.7, so two of them at the bare minimum
            // spacing leave no gap at all — and a hundred of those turns the level into a
            // maze that nothing can path across. Anything with roots is recorded so the
            // next tree keeps its distance.
            if (Chance(buttressShare))
            {
                _buttressed.Add(point);

                int fins = 3 + _rng.Next(0, 2);
                for (int f = 0; f < fins; f++)
                {
                    float a = (float)f / fins * Mathf.PI * 2f + Range(-0.3f, 0.3f);
                    var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));

                    var fin = CreateBox($"Buttress_{index}_{f}",
                        basePosition + outward * 0.65f + Vector3.up * 0.7f,
                        new Vector3(0.35f, 1.4f, 1.3f), ProtoMaterials.Bark, true);
                    fin.transform.rotation = Quaternion.LookRotation(outward, Vector3.up);

                    AddHidingSpot(basePosition + outward * 1.6f, outward);
                }
            }

            int masses = 2 + _rng.Next(0, 2);
            for (int i = 0; i < masses; i++)
            {
                float t = (float)i / masses;
                float spread = Mathf.Lerp(4.2f, 2.2f, t) * Range(0.8f, 1.2f);

                Vector3 offset = new Vector3(Range(-1f, 1f), height * (0.66f + t * 0.26f), Range(-1f, 1f));
                CreateDecoration($"Crown_{index}_{i}", basePosition + offset,
                    new Vector3(spread, spread * 0.7f, spread),
                    t > 0.4f ? ProtoMaterials.Canopy : ProtoMaterials.FrondDark);
            }

            // The tallest trees are where the monkeys come down from.
            if (height > trunkHeight.y * 0.8f)
                CanopySpawns.Add(transform.position + basePosition);

            var facing = new Vector3(Range(-1f, 1f), 0f, Range(-1f, 1f));
            AddHidingSpot(basePosition + facing.normalized * (thickness * 0.5f + 0.5f), facing);
        }

        private void BuildUndergrowth()
        {
            // Fern beds. No collider, so nothing paths around them — and a jaguar lying in
            // one is invisible until it is four metres away and moving.
            for (int i = 0; i < fernCount; i++)
            {
                Vector2 point = RandomPointInValley();
                if (!IsClearOfLandmarks(point) || IsInTheRiver(point)) continue;

                float size = Range(1.4f, 3.2f);
                CreateDecoration($"Fern_{i}", new Vector3(point.x, size * 0.28f, point.y),
                    new Vector3(size, size * 0.6f, size),
                    Chance(0.5f) ? ProtoMaterials.Frond : ProtoMaterials.Undergrowth);

                AddHidingSpot(new Vector3(point.x, 0f, point.y),
                              new Vector3(Range(-1f, 1f), 0f, Range(-1f, 1f)));

                if (Chance(0.16f))
                    SnakeSpawns.Add(transform.position + new Vector3(point.x, 0f, point.y));
            }

            for (int i = 0; i < logCount; i++)
            {
                Vector2 point = RandomPointInValley();
                if (!IsClearOfLandmarks(point) || IsInTheRiver(point)) continue;

                float length = Range(4f, 9f);
                var log = CreateBox($"FallenTrunk_{i}", new Vector3(point.x, 0.45f, point.y),
                    new Vector3(0.9f, 0.9f, length), ProtoMaterials.Bark, true);
                float lie = Range(0f, 360f);
                log.transform.rotation = Quaternion.Euler(0f, lie, Range(-4f, 4f));

                // Moss along the top, which is most of what makes a fallen trunk look old.
                var moss = CreateDecoration($"TrunkMoss_{i}", new Vector3(point.x, 0.9f, point.y),
                    new Vector3(0.7f, 0.12f, length * 0.85f), ProtoMaterials.Moss);
                moss.transform.rotation = Quaternion.Euler(0f, lie, 0f);

                Vector3 sideways = Quaternion.Euler(0f, lie, 0f) * Vector3.right;
                AddHidingSpot(new Vector3(point.x, 0f, point.y) + sideways * 1.1f, sideways);
            }

            for (int i = 0; i < boulderCount; i++)
            {
                Vector2 point = RandomPointInValley();
                if (!IsClearOfLandmarks(point) || IsInTheRiver(point)) continue;

                float size = Range(1.1f, 2.8f);
                var rock = CreateBox($"Boulder_{i}", new Vector3(point.x, size * 0.3f, point.y),
                    new Vector3(size, size * 0.7f, size * Range(0.7f, 1.3f)), ProtoMaterials.Rock, true);
                rock.transform.rotation = Quaternion.Euler(Range(-12f, 12f), Range(0f, 360f), Range(-12f, 12f));

                PropLibrary.Dress(rock, "Boulder");

                CreateDecoration($"BoulderMoss_{i}", new Vector3(point.x, size * 0.62f, point.y),
                    new Vector3(size * 0.9f, 0.14f, size * 0.9f), ProtoMaterials.Moss);

                AddHidingSpot(new Vector3(point.x, 0f, point.y) + Vector3.forward * (size * 0.9f),
                              Vector3.forward);
            }

            // Hanging vines. Pure decoration, but they break up the middle distance, which
            // is exactly the band a jaguar closes through.
            for (int i = 0; i < vineCount; i++)
            {
                Vector2 point = RandomPointInValley();
                float drop = Range(3f, 7f);

                CreateDecoration($"Vine_{i}",
                    new Vector3(point.x, canopyHeight - drop * 0.5f, point.y),
                    new Vector3(0.18f, drop, 0.18f), ProtoMaterials.Vine);
            }
        }

        /// <summary>
        /// Temple ruins: a stub of wall, a toppled column and moss over both. Chest high
        /// and solid, so these are the only cover in the valley you can shoot over — which
        /// makes them the places worth fighting from rather than running through.
        /// </summary>
        private void BuildRuins()
        {
            for (int i = 0; i < ruinCount; i++)
            {
                Vector2 point = RandomPointInValley();
                if (!IsClearOfLandmarks(point) || IsInTheRiver(point)) continue;

                float lie = Range(0f, 360f);
                Quaternion rotation = Quaternion.Euler(0f, lie, 0f);
                var centre = new Vector3(point.x, 0f, point.y);

                var wall = CreateBox($"RuinWall_{i}", centre + Vector3.up * 0.6f,
                    new Vector3(Range(3.5f, 6.5f), 1.2f, 0.7f), ProtoMaterials.RuinStone, true);
                wall.transform.rotation = rotation;

                var moss = CreateDecoration($"RuinMoss_{i}", centre + Vector3.up * 1.22f,
                    new Vector3(Range(2.5f, 5f), 0.1f, 0.6f), ProtoMaterials.Moss);
                moss.transform.rotation = rotation;

                if (Chance(0.6f))
                {
                    var column = CreateBox($"RuinColumn_{i}",
                        centre + rotation * new Vector3(Range(-3f, 3f), 0.5f, Range(2f, 3.5f)),
                        new Vector3(0.9f, 0.9f, Range(3f, 5.5f)), ProtoMaterials.RuinStone, true);
                    column.transform.rotation = Quaternion.Euler(0f, lie + Range(-40f, 40f), 90f);

                    // Fluted and broken at one end, if the mesh has been built. The box is
                    // long in Z, which is why RuinColumn is exported lying along Z — and no
                    // extra yaw here, because the generator has already chosen how it fell.
                    PropLibrary.Dress(column, "RuinColumn");
                }

                // Behind the wall, facing back over it.
                Vector3 behind = rotation * Vector3.back;
                AddHidingSpot(centre + behind * 1.2f, -behind);
            }
        }

        /// <summary>The way out: a temple gate in the valley wall, with a path up to it.</summary>
        private void BuildTrail()
        {
            Vector3 exitLocal = ExitPosition - transform.position;

            for (int side = -1; side <= 1; side += 2)
            {
                var post = CreateBox($"GatePost_{side}", exitLocal + new Vector3(3f * side, 2f, 0f),
                    new Vector3(1.1f, 4f, 1.1f), ProtoMaterials.RuinStone, true);

                // Coursed stone with a glyph band, if it has been built. This is the last
                // thing you see before the level ends and the only prop in the valley you
                // walk right up to, so it is where a box costs the most.
                PropLibrary.Dress(post, "GatePost");
            }

            CreateDecoration("GateLintel", exitLocal + Vector3.up * 4.4f,
                new Vector3(7.2f, 0.8f, 1.2f), ProtoMaterials.RuinStone);
            CreateDecoration("GateGlyphs", exitLocal + Vector3.up * 4.4f + Vector3.forward * 0.7f,
                new Vector3(6f, 0.5f, 0.1f), ProtoMaterials.Moss);

            // A trodden path from the start clearing to the gate. It runs straight at the
            // river, which is the point: the obvious line and the crossings are not the
            // same line, and working that out is the first thing the level asks of you.
            const int steps = 18;
            for (int i = 0; i < steps; i++)
            {
                float t = (float)i / steps;
                Vector3 along = Vector3.Lerp(PlayerSpawn - transform.position, exitLocal, t);
                along.y = 0.03f;

                CreateDecoration($"Path_{i}", along + new Vector3(Range(-1.8f, 1.8f), 0f, 0f),
                    new Vector3(Range(3f, 5f), 0.05f, Range(4f, 7f)), ProtoMaterials.Mud);
            }
        }

        private void BuildCanopyGaps()
        {
            for (int i = 0; i < canopyGapCount; i++)
            {
                Vector2 point = RandomPointInValley();

                var go = new GameObject($"CanopyGap_{i}");
                go.transform.SetParent(_container, false);
                go.transform.position = transform.position + new Vector3(point.x, canopyHeight - 2f, point.y);

                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = gapColour;
                light.intensity = gapIntensity;
                light.range = gapRange;
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

        /// <summary>
        /// Fourteen places around the rim of the valley, spread by angle, all of them a
        /// long walk from the gate. The set is seeded — the same valley always offers the
        /// same fourteen — but which one holds the cell is not.
        /// </summary>
        private void BuildPowerCellCandidates()
        {
            const int count = 14;
            const float minimumCarryDistance = 42f;

            for (int i = 0; i < count; i++)
            {
                float angle = (i + 0.5f) * (Mathf.PI * 2f / count) + Range(-0.12f, 0.12f);
                float distance = radius * Range(0.62f, 0.84f);

                var point = new Vector2(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance);
                if (!IsClearOfLandmarks(point)) continue;
                if (IsInTheRiver(point)) continue;

                Vector3 world = transform.position + new Vector3(point.x, 0f, point.y);
                if (Vector3.Distance(world, MotorPosition) < minimumCarryDistance) continue;

                PowerCellCandidates.Add(world);
            }

            // A valley with no candidates at all would be unfinishable; fall back to a
            // fixed spot rather than leaving the list empty.
            if (PowerCellCandidates.Count == 0)
                PowerCellCandidates.Add(transform.position + new Vector3(-(radius - 18f), 0f, 0f));
        }

        private Vector3 PickOpenSpot()
        {
            for (int attempt = 0; attempt < 40; attempt++)
            {
                Vector2 point = RandomPointInValley();
                if (!IsClearOfLandmarks(point)) continue;
                if (IsInTheRiver(point)) continue;
                if (TooCloseToATree(point, minimumTreeSpacing * 0.55f)) continue;

                return transform.position + new Vector3(point.x, 0f, point.y);
            }

            return transform.position + new Vector3(Range(-radius * 0.5f, radius * 0.5f), 0f, radius * 0.3f);
        }

        // ---- helpers --------------------------------------------------------

        private float Range(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

        private bool Chance(float probability) => _rng.NextDouble() < probability;

        private Vector2 RandomPointInValley()
        {
            // Sample an angle and a square-rooted radius, for even coverage without
            // rejection sampling.
            float angle = Range(0f, Mathf.PI * 2f);
            float distance = Mathf.Sqrt((float)_rng.NextDouble()) * (radius - 4f);
            return new Vector2(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance);
        }

        /// <summary>True within a few metres of somewhere the river can be crossed.</summary>
        private bool IsNearACrossing(Vector2 point)
        {
            float alongLine = Vector2.Dot(point, RiverDirection);

            foreach (Vector2 crossing in _crossings)
                if (Mathf.Abs(alongLine - Vector2.Dot(crossing, RiverDirection)) < 8f) return true;

            return false;
        }

        /// <summary>
        /// True inside the channel, and false within a few metres of a crossing — the
        /// crossings are solid ground, so trees and cover are allowed to stand on them.
        /// </summary>
        private bool IsInTheRiver(Vector2 point)
        {
            if (!cutRiver) return false;

            Vector2 direction = RiverDirection;
            float alongLine = Vector2.Dot(point, direction);
            float fromLine = Mathf.Abs(point.x * direction.y - point.y * direction.x);

            if (fromLine > riverWidth * 0.5f + 2.5f) return false;

            foreach (Vector2 crossing in _crossings)
            {
                float crossingAlong = Vector2.Dot(crossing, direction);
                if (Mathf.Abs(alongLine - crossingAlong) < 5f) return false;
            }

            return true;
        }

        /// <summary>Keeps the start clearing and the gate clearing open.</summary>
        private bool IsClearOfLandmarks(Vector2 point)
        {
            Vector2 start = new Vector2(PlayerSpawn.x - transform.position.x,
                                        PlayerSpawn.z - transform.position.z);
            Vector2 exit = new Vector2(ExitPosition.x - transform.position.x,
                                       ExitPosition.z - transform.position.z);

            if (Vector2.Distance(point, start) < startClearing) return false;
            if (Vector2.Distance(point, exit) < exitClearing) return false;

            return true;
        }

        /// <summary>
        /// Keeps clear of the trees that have roots. 6.4 m rather than the usual 4: a
        /// buttressed trunk is a 2.6 m obstacle, and two of them any closer than this
        /// leave a gap narrower than a jaguar.
        /// </summary>
        private bool TooCloseToRoots(Vector2 point)
        {
            const float limitSquared = 6.4f * 6.4f;

            foreach (Vector2 tree in _buttressed)
                if ((tree - point).sqrMagnitude < limitSquared) return true;

            return false;
        }

        private bool TooCloseToATree(Vector2 point, float spacing = -1f)
        {
            float limit = spacing > 0f ? spacing : minimumTreeSpacing;
            float limitSquared = limit * limit;

            foreach (Vector2 tree in _treePositions)
                if ((tree - point).sqrMagnitude < limitSquared) return true;

            return false;
        }

        /// <summary>
        /// Records somewhere to wait, facing out from the cover. One place, so every kind
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

        private GameObject CreateBox(string name, Vector3 localCentre, Vector3 size,
                                     Material material, bool solid)
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
