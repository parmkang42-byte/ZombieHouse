using System.Collections.Generic;
using UnityEngine;

namespace ZombieHouse.Level
{
    /// <summary>One storey of the house: a grid of characters, same size as every other storey.</summary>
    [System.Serializable]
    public class HouseFloor
    {
        public string name = "Floor";

        [Tooltip("# wall  . floor  D door  P player  Z zombie  A ammo  M medkit  V battery  R survivor  " +
                 "W power cell  G door motor  U Uzi  E exit  " +
                 "T table  S sofa  B bed  C cabinet  F bookshelf  " +
                 "> < ^ v stairs up (arrow points uphill)  space = outside")]
        public string[] rows;
    }

    /// <summary>
    /// Builds a multi-storey house from ASCII floor plans — one plan per storey, stacked,
    /// joined by staircases, and closed in by a roof.
    ///
    /// Legend:  '#' wall   '.' floor   'D' doorway (floor, no wall)
    ///          'P' player start   'Z' zombie spawn
    ///          'A' ammo pickup    'M' medkit    'V' torch battery    'R' survivor
    ///          'W' power cell     'G' door motor     'E' exit / extraction point
    ///          'T' table   'S' sofa   'B' bed   'C' cabinet   'F' bookshelf
    ///          '>' '<' '^' 'v' stairs climbing to the storey above, arrow pointing uphill
    ///          ' ' outside the building
    ///
    /// Furniture cells are still floor — the prop stands on them and blocks the NavMesh.
    ///
    /// A run of stair characters becomes one flight. The storey above automatically loses
    /// its floor over that run, so the stairwell is open and you can walk up through it —
    /// there is no second thing to keep in sync when you move a staircase.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class HouseGenerator : MonoBehaviour, ILevelSource
    {
        [Header("Scale")]
        [SerializeField] private float cellSize = 2f;
        [Tooltip("Ceiling height. Stairs and storey spacing follow from this automatically.")]
        [SerializeField] private float wallHeight = 4.2f;

        [Tooltip("How tall the door leaves are. Shorter than the wall so the frame reads.")]
        [SerializeField] private float doorHeight = 2.7f;

        [Tooltip("Layer the doors are built on. It must be one the NavMesh baker ignores, "
                 + "or a closed door bakes as a wall and seals the room behind it.")]
        [SerializeField] private int doorLayer;
        [SerializeField] private float wallThickness = 0.25f;
        [SerializeField] private float floorThickness = 0.4f;

        [Header("Options")]
        [SerializeField] private bool generateOnAwake = true;
        [SerializeField] private bool buildProps = true;
        [SerializeField] private bool buildRoof = true;
        [SerializeField] private bool buildWallTrim = true;
        [Tooltip("Height of the dado rail, and of the panelling below it.")]
        [SerializeField] private float dadoHeight = 1.05f;
        [SerializeField] private bool placeInteriorLights = true;
        [SerializeField] private bool buildChandeliers = true;
        [SerializeField] private float chandelierDrop = 1.1f;
        [SerializeField] private float lightSpacingCells = 5f;
        [SerializeField] private Color interiorLightColor = new Color(1f, 0.86f, 0.68f);
        // Sixteen metres was eight cells in every direction, through walls, and it was
        // only ever survivable because these lights cast no shadow — an unshadowed point
        // light ignores geometry, so every room was being lit by seven other rooms' lamps
        // as well as its own. With shadows on that stops, and a 16 m radius stops being
        // generous and starts being 8.4 overlapping shadow maps at the average standing
        // spot, against the four per-pixel lights the renderer will actually promote.
        //
        // Ten covers the 10 m light grid with overlap to spare and brings that to 3.5.
        // Eleven was tried first and measured 4.1 — over, because the house has two
        // floors and a lamp on the storey above is inside an 11 m sphere as surely as one
        // in the next room is. Plan area alone would have said 3.8 and been wrong; this
        // is why the number is measured rather than derived.
        //
        // The intensity goes up because the room genuinely does lose the borrowed light
        // it was getting through the plaster, not to compensate for the shorter range.
        // This pair is the house's brightness, and it is the one thing in this change
        // that wants a human to look at it.
        [SerializeField] private float interiorLightIntensity = 3.4f;
        [SerializeField] private float interiorLightRange = 10f;

        [Header("Stairs")]
        [SerializeField] private int stepsPerFlight = 24;
        [SerializeField] private float stairWidthFraction = 0.86f;
        [SerializeField] private bool buildStairRailings = true;
        [SerializeField] private float railingHeight = 1.05f;
        [SerializeField] private float railingThickness = 0.07f;

        [Header("Floor plans — every row of every storey must be the same length")]
        [SerializeField]
        private HouseFloor[] floors =
        {
            new HouseFloor
            {
                name = "Ground",
                rows = new[]
                {
                    "##############################",
                    "#.C...R..#...S......#...T..W.#",
                    "#.......F#....Z.....#...M....#",
                    "#.Z.N....D..........#....A...#",
                    "#........#.....V....D........#",
                    "#...P....#..........#....Z.C.#",
                    "#......Z.#........Z.#..Z.....#",
                    "####D#########D#########D#####",
                    "#.....V........A........U....#",
                    "#.................>>>>>......#",
                    "#####D########D#########D#####",
                    "#....X....#...T....#....M....#",
                    "#....A..C.#........#....Z.N..#",
                    "#.........#...Z.T..D....V....#",
                    "#W.Z......#...Z....#....Z....#",
                    "#.........D........#####D#####",
                    "####D######........#.........#",
                    "#..X......#...M....#..B..R...#",
                    "#....Z.V..#...Z....#.F....M..#",
                    "#....Z....#.W.M....#....Z....#",
                    "#....Z....#...Z....#.C....G..#",
                    "##########################E###"
                }
            },
            new HouseFloor
            {
                name = "Upper",
                rows = new[]
                {
                    "##############################",
                    "#...W....#..........#........#",
                    "#...Z....#....B.....#....B...#",
                    "#..Z.....D.....V...Z#....Z...#",
                    "#..F.....#....C.....D........#",
                    "#...A.N..#....M.....#W...Z...#",
                    "#.....Z..#....Z.....#..Z.....#",
                    "####D#########D#########D#####",
                    "#........U.....A.............#",
                    "#.....................V......#",
                    "#####D########D#########D#####",
                    "#....M....#...M....#....M....#",
                    "#....B....#...T....#....Z.R..#",
                    "#.........#........D....F....#",
                    "#....Z....#...C....#..N.B..W.#",
                    "#.........D........#####D#####",
                    "####D######........#.........#",
                    "#..XV..Z..#....Z...#..M......#",
                    "#....F....#........#....X....#",
                    "#....M....#...Z....#....A....#",
                    "#W...Z....#...Z....#....B....#",
                    "##############################"
                }
            }
        };

        // ---- results, read by LevelDirector --------------------------------

        public Vector3 PlayerSpawn { get; private set; }
        public List<Vector3> ZombieSpawns { get; } = new List<Vector3>();
        public List<Vector3> AmmoSpawns { get; } = new List<Vector3>();
        public List<Vector3> MedkitSpawns { get; } = new List<Vector3>();
        public List<Vector3> BatterySpawns { get; } = new List<Vector3>();
        public List<Vector3> SurvivorSpawns { get; } = new List<Vector3>();

        /// <summary>They are hiding in the house, not tied up.</summary>
        public bool SurvivorsAreBound => false;

        public List<Vector3> PowerUpSpawns { get; } = new List<Vector3>();
        public List<Vector3> BeltCrateSpawns { get; } = new List<Vector3>();
        public List<Vector3> PowerCellCandidates { get; } = new List<Vector3>();
        public Vector3 PowerCellSpawn { get; private set; }
        public Vector3 MotorPosition { get; private set; }

        /// <summary>
        /// Places worth lurking in — tucked behind a desk or a bed, or standing in a
        /// corner out of the line of sight down a room. Derived from the layout rather
        /// than hand-placed, so moving the furniture moves the ambushes with it.
        ///
        /// Each pose faces away from whatever it is hiding behind, so a walker is looking
        /// into the room when you come through the door.
        /// </summary>
        public List<Pose> HidingSpots { get; } = new List<Pose>();
        public Vector3 ExitPosition { get; private set; }
        public bool HasExit { get; private set; }
        public Bounds HouseBounds { get; private set; }
        public bool Generated { get; private set; }

        /// <summary>ILevelSource: the house's bounds are the level's bounds.</summary>
        public Bounds LevelBounds => HouseBounds;

        public int FloorCount => floors != null ? floors.Length : 0;

        /// <summary>Vertical distance from one storey's floor to the next.</summary>
        public float FloorSpacing => wallHeight + floorThickness;

        private const string ContainerName = "HouseGeometry";
        private Transform _container;

        /// <summary>Cells to leave out of each storey's floor, so stairwells are open.</summary>
        private List<HashSet<long>> _holes;

        private int Rows => (floors != null && floors.Length > 0 && floors[0].rows != null)
            ? floors[0].rows.Length : 0;

        private int Columns => (Rows > 0 && floors[0].rows[0] != null)
            ? floors[0].rows[0].Length : 0;

        private void Awake()
        {
            if (generateOnAwake) Generate();
        }

        [ContextMenu("Generate House")]
        public void Generate()
        {
            if (!ValidateLayout()) return;

            ClearGeometry();
            ResetMarkers();

            _container = new GameObject(ContainerName).transform;
            _container.SetParent(transform, false);

            float width = Columns * cellSize;
            float depth = Rows * cellSize;
            float totalHeight = FloorCount * FloorSpacing + wallHeight;

            HouseBounds = new Bounds(
                transform.position + new Vector3(0f, totalHeight * 0.5f, 0f),
                new Vector3(width + 12f, totalHeight + 10f, depth + 12f));

            CutStairwells();
            BuildGround(width, depth);

            for (int f = 0; f < FloorCount; f++)
            {
                BuildFloorSlab(f);
                BuildWalls(f);
                BuildDoors(f);
                BuildStairs(f);
                if (buildStairRailings) BuildStairwellRailings(f);
                if (buildProps) BuildProps(f);
                if (placeInteriorLights) BuildInteriorLights(f);
                ScanMarkers(f);
                ScanHidingSpots(f);
            }

            if (buildRoof) BuildRoof(width, depth);

            // Every 'W' in the plans is somewhere the cell could be; which one holds it
            // is drawn fresh each time the scene loads, so it cannot be memorised.
            PowerCellSpawn = PowerCellPlacement.Draw(PowerCellCandidates, PlayerSpawn, PlayerSpawn);

            Generated = true;
        }

        private bool ValidateLayout()
        {
            if (floors == null || floors.Length == 0)
            {
                Debug.LogError("[HouseGenerator] No floors defined.");
                return false;
            }

            int expectedRows = -1;
            int expectedColumns = -1;

            for (int f = 0; f < floors.Length; f++)
            {
                string[] rows = floors[f].rows;
                if (rows == null || rows.Length == 0)
                {
                    Debug.LogError($"[HouseGenerator] Floor {f} ('{floors[f].name}') has no rows.");
                    return false;
                }

                if (expectedRows < 0) { expectedRows = rows.Length; expectedColumns = rows[0].Length; }

                if (rows.Length != expectedRows)
                {
                    Debug.LogError($"[HouseGenerator] Floor {f} ('{floors[f].name}') has {rows.Length} rows; " +
                                   $"every storey must have {expectedRows}. Storeys have to stack.");
                    return false;
                }

                for (int r = 0; r < rows.Length; r++)
                {
                    if (rows[r] != null && rows[r].Length == expectedColumns) continue;

                    Debug.LogError($"[HouseGenerator] Floor {f} ('{floors[f].name}') row {r} is " +
                                   $"{(rows[r] == null ? 0 : rows[r].Length)} characters; every row must be {expectedColumns}.");
                    return false;
                }
            }

            return true;
        }

        private void ResetMarkers()
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
            HasExit = false;
            PlayerSpawn = transform.position + Vector3.up * 0.1f;
        }

        [ContextMenu("Clear House")]
        public void ClearGeometry()
        {
            var doomed = new List<GameObject>();
            foreach (Transform child in transform)
                if (child.name == ContainerName) doomed.Add(child.gameObject);

            foreach (GameObject go in doomed)
            {
                if (Application.isPlaying)
                {
                    // Destroy() is deferred to end of frame, so detach first — otherwise the
                    // old container is still a child when we go looking for it again.
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

        // ---- stairwells -----------------------------------------------------

        private static long CellKey(int row, int column) => ((long)row << 32) | (uint)column;

        /// <summary>
        /// Works out where each storey's floor has to be left open. A flight on storey f
        /// needs the ceiling above it removed, which is the floor of storey f + 1.
        /// </summary>
        private void CutStairwells()
        {
            _holes = new List<HashSet<long>>(FloorCount);
            for (int f = 0; f < FloorCount; f++) _holes.Add(new HashSet<long>());

            for (int f = 0; f < FloorCount - 1; f++)
            {
                foreach (StairFlight flight in FindFlights(f))
                {
                    // The run itself is open overhead; the cell you step off onto at the
                    // top keeps its floor, because that is the landing.
                    for (int i = 0; i < flight.Length; i++)
                    {
                        int row = flight.StartRow + flight.RowStep * i;
                        int column = flight.StartColumn + flight.ColumnStep * i;
                        _holes[f + 1].Add(CellKey(row, column));
                    }
                }
            }
        }

        private struct StairFlight
        {
            public int StartRow, StartColumn;   // bottom of the flight
            public int RowStep, ColumnStep;     // direction of ascent
            public int Length;                  // cells
        }

        private static bool IsStair(char c) => c == '>' || c == '<' || c == '^' || c == 'v' || c == 'V';

        private static void StairDirection(char c, out int rowStep, out int columnStep)
        {
            switch (c)
            {
                case '>': rowStep = 0; columnStep = 1; break;
                case '<': rowStep = 0; columnStep = -1; break;
                case '^': rowStep = -1; columnStep = 0; break;
                default: rowStep = 1; columnStep = 0; break;   // 'v'
            }
        }

        /// <summary>Finds each unbroken run of identical stair characters on a storey.</summary>
        private List<StairFlight> FindFlights(int floor)
        {
            var flights = new List<StairFlight>();
            var visited = new HashSet<long>();
            string[] rows = floors[floor].rows;

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    char cell = rows[r][c];
                    if (!IsStair(cell) || visited.Contains(CellKey(r, c))) continue;

                    int rowStep, columnStep;
                    StairDirection(cell, out rowStep, out columnStep);

                    // Walk backwards to the bottom of the run, then forwards to measure it.
                    int startRow = r, startColumn = c;
                    while (InBounds(startRow - rowStep, startColumn - columnStep) &&
                           rows[startRow - rowStep][startColumn - columnStep] == cell)
                    {
                        startRow -= rowStep;
                        startColumn -= columnStep;
                    }

                    int length = 0;
                    int row = startRow, column = startColumn;
                    while (InBounds(row, column) && rows[row][column] == cell)
                    {
                        visited.Add(CellKey(row, column));
                        length++;
                        row += rowStep;
                        column += columnStep;
                    }

                    flights.Add(new StairFlight
                    {
                        StartRow = startRow,
                        StartColumn = startColumn,
                        RowStep = rowStep,
                        ColumnStep = columnStep,
                        Length = length
                    });
                }
            }

            return flights;
        }

        /// <summary>
        /// Builds each flight as solid steps rising exactly one storey. The risers are
        /// well under the NavMesh agent's step height and the player's step offset, so
        /// both walk up without any special handling.
        /// </summary>
        private void BuildStairs(int floor)
        {
            if (floor >= FloorCount - 1) return;   // nothing above to climb to

            float baseY = FloorBaseY(floor);

            foreach (StairFlight flight in FindFlights(floor))
            {
                float runDistance = flight.Length * cellSize;
                int steps = Mathf.Max(4, stepsPerFlight);
                float riser = FloorSpacing / steps;
                float tread = runDistance / steps;

                // The flight starts at the near edge of its first cell.
                Vector3 bottom = CellToLocal(flight.StartRow, flight.StartColumn);
                Vector3 direction = new Vector3(flight.ColumnStep, 0f, -flight.RowStep);
                Vector3 start = bottom - direction * (cellSize * 0.5f);

                bool alongX = flight.ColumnStep != 0;
                float width = cellSize * stairWidthFraction;

                var stepBoxes = new List<GameObject>(steps);

                for (int i = 0; i < steps; i++)
                {
                    float topHeight = (i + 1) * riser;
                    Vector3 centre = start + direction * (tread * (i + 0.5f));

                    // Each step is solid down to the floor, so the staircase has a closed
                    // underside instead of floating treads.
                    Vector3 size = alongX
                        ? new Vector3(tread, topHeight, width)
                        : new Vector3(width, topHeight, tread);

                    stepBoxes.Add(CreateBox(
                        $"Step_{floor}_{flight.StartRow}_{flight.StartColumn}_{i}",
                        new Vector3(centre.x, baseY + topHeight * 0.5f, centre.z),
                        size, ProtoMaterials.Wood));
                }

                DressStairFlight(flight, floor, start, direction, runDistance, width, baseY,
                                 stepBoxes);
            }
        }

        /// <summary>
        /// Fences the stairwell opening so you cannot walk off the landing into the hole.
        ///
        /// A railing goes on every edge of the opening that borders standing floor, with
        /// one deliberate exception: the edge the staircase arrives at is left open, or
        /// the railing would seal the stairs off from the storey they lead to. That gap
        /// is derived from the flight itself, so moving a staircase moves the gap too.
        /// </summary>
        private void BuildStairwellRailings(int floor)
        {
            if (floor == 0) return;

            HashSet<long> holes = _holes[floor];
            if (holes.Count == 0) return;

            // Work out which edge each flight arrives at, so it can be skipped.
            var openEdges = new HashSet<long>();
            foreach (StairFlight flight in FindFlights(floor - 1))
            {
                int topRow = flight.StartRow + flight.RowStep * (flight.Length - 1);
                int topColumn = flight.StartColumn + flight.ColumnStep * (flight.Length - 1);
                openEdges.Add(EdgeKey(topRow, topColumn, flight.RowStep, flight.ColumnStep));
            }

            // Four neighbours: north, south, east, west in (rowStep, columnStep) terms.
            int[] rowSteps = { -1, 1, 0, 0 };
            int[] columnSteps = { 0, 0, 1, -1 };

            float baseY = FloorBaseY(floor);

            foreach (long cell in holes)
            {
                int row = (int)(cell >> 32);
                int column = (int)(cell & 0xFFFFFFFF);

                for (int d = 0; d < 4; d++)
                {
                    int nr = row + rowSteps[d];
                    int nc = column + columnSteps[d];

                    // No railing against another part of the same opening.
                    if (holes.Contains(CellKey(nr, nc))) continue;

                    // No railing where there is already a wall, or nothing at all.
                    if (!IsWalkable(floor, nr, nc)) continue;

                    // Leave the way onto the landing clear.
                    if (openEdges.Contains(EdgeKey(row, column, rowSteps[d], columnSteps[d]))) continue;

                    BuildRailingSegment(floor, baseY, row, column, rowSteps[d], columnSteps[d]);
                }
            }
        }

        private static long EdgeKey(int row, int column, int rowStep, int columnStep)
        {
            int direction = rowStep == -1 ? 0 : rowStep == 1 ? 1 : columnStep == 1 ? 2 : 3;
            return ((long)row << 40) | ((long)column << 8) | (uint)direction;
        }

        /// <summary>One span of banister: a top rail, a mid rail, and a post at each end.</summary>
        private void BuildRailingSegment(int floor, float baseY, int row, int column, int rowStep, int columnStep)
        {
            Vector3 centre = CellToLocal(row, column);
            Vector3 outward = new Vector3(columnStep, 0f, -rowStep);
            Vector3 edge = centre + outward * (cellSize * 0.5f - railingThickness * 0.5f);

            bool alongX = columnStep == 0;   // a north/south edge runs east-west
            Vector3 railSize = alongX
                ? new Vector3(cellSize, railingThickness, railingThickness)
                : new Vector3(railingThickness, railingThickness, cellSize);

            string id = $"{floor}_{row}_{column}_{rowStep}_{columnStep}";

            CreateBox($"RailTop_{id}",
                new Vector3(edge.x, baseY + railingHeight, edge.z), railSize, ProtoMaterials.Wood);
            CreateBox($"RailMid_{id}",
                new Vector3(edge.x, baseY + railingHeight * 0.52f, edge.z), railSize, ProtoMaterials.Wood);

            // Posts at the ends of the span.
            Vector3 along = alongX ? Vector3.right : Vector3.forward;
            for (int end = -1; end <= 1; end += 2)
            {
                Vector3 post = edge + along * (cellSize * 0.5f * end);
                CreateBox($"RailPost_{id}_{end}",
                    new Vector3(post.x, baseY + railingHeight * 0.5f, post.z),
                    new Vector3(railingThickness * 1.4f, railingHeight, railingThickness * 1.4f),
                    ProtoMaterials.Wood);
            }
        }

        // ---- geometry -------------------------------------------------------

        private float FloorBaseY(int floor) => floor * FloorSpacing;

        private void BuildGround(float width, float depth)
        {
            var ground = CreateBox("Ground",
                new Vector3(0f, -floorThickness - 0.25f, 0f),
                new Vector3(width + 60f, 0.5f, depth + 60f),
                ProtoMaterials.Ground);
            ground.isStatic = true;
        }

        /// <summary>
        /// Lays the floor as merged runs along each row, skipping cells that are outside
        /// the building or open to the storey below. Runs keep the object count sane while
        /// still allowing a stairwell-shaped hole, which a single slab could not.
        /// </summary>
        private void BuildFloorSlab(int floor)
        {
            float y = FloorBaseY(floor);
            string[] rows = floors[floor].rows;
            HashSet<long> holes = _holes[floor];

            for (int r = 0; r < Rows; r++)
            {
                int c = 0;
                while (c < Columns)
                {
                    bool needsFloor = rows[r][c] != ' ' && !holes.Contains(CellKey(r, c));
                    if (!needsFloor) { c++; continue; }

                    int start = c;
                    while (c < Columns && rows[r][c] != ' ' && !holes.Contains(CellKey(r, c))) c++;
                    int length = c - start;

                    Vector3 centre = CellToLocal(r, start + (length - 1) * 0.5f);

                    // Alternate the tone row by row so the floor reads as boards rather
                    // than one flat expanse.
                    Material boards = (r & 1) == 0 ? ProtoMaterials.Floor : ProtoMaterials.FloorAlt;

                    CreateBox($"Floor_{floor}_{r}_{start}",
                        new Vector3(centre.x, y - floorThickness * 0.5f, centre.z),
                        new Vector3(length * cellSize, floorThickness, cellSize),
                        boards);
                }
            }
        }

        private void BuildRoof(float width, float depth)
        {
            float y = FloorBaseY(FloorCount - 1) + wallHeight;

            CreateBox("Roof",
                new Vector3(0f, y + 0.15f, 0f),
                new Vector3(width, 0.3f, depth),
                ProtoMaterials.Wall);

            // A low parapet around the edge so the roofline reads as a building.
            const float parapet = 0.55f;
            CreateBox("ParapetN", new Vector3(0f, y + parapet * 0.5f + 0.3f, depth * 0.5f - 0.15f),
                new Vector3(width, parapet, 0.3f), ProtoMaterials.Wall);
            CreateBox("ParapetS", new Vector3(0f, y + parapet * 0.5f + 0.3f, -depth * 0.5f + 0.15f),
                new Vector3(width, parapet, 0.3f), ProtoMaterials.Wall);
            CreateBox("ParapetE", new Vector3(width * 0.5f - 0.15f, y + parapet * 0.5f + 0.3f, 0f),
                new Vector3(0.3f, parapet, depth), ProtoMaterials.Wall);
            CreateBox("ParapetW", new Vector3(-width * 0.5f + 0.15f, y + parapet * 0.5f + 0.3f, 0f),
                new Vector3(0.3f, parapet, depth), ProtoMaterials.Wall);
        }

        /// <summary>
        /// A hinged door in every 'D' on the plan.
        ///
        /// Built on the door layer, which <see cref="RuntimeNavMeshBaker"/> does not bake —
        /// so a house full of doors changes navigation by nothing and Verify Level stays
        /// valid. Zombies shove them open on contact, which is what the unchanged pathing
        /// already assumes; you have to open them yourself with Space, which is what makes
        /// each one a decision rather than a formality.
        /// </summary>
        private void BuildDoors(int floor)
        {
            float baseY = FloorBaseY(floor);

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    if (char.ToUpperInvariant(floors[floor].rows[r][c]) != 'D') continue;

                    // Which way the opening runs: walls to left and right mean the gap is
                    // spanned across the columns.
                    bool acrossColumns = IsWall(floor, r, c - 1) && IsWall(floor, r, c + 1);

                    float half = cellSize * 0.5f;
                    Vector3 hinge = acrossColumns ? new Vector3(-half, 0f, 0f)
                                                  : new Vector3(0f, 0f, -half);

                    var pivot = new GameObject($"Door_{floor}_{r}_{c}");
                    pivot.layer = doorLayer;
                    pivot.transform.SetParent(_container, false);
                    pivot.transform.position = transform.position + CellToLocal(r, c)
                                             + hinge + Vector3.up * baseY;

                    if (!acrossColumns) pivot.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

                    // Pivot -> hinge -> leaf. The hinge sits at the hanging edge and is
                    // what turns; the leaf hangs off it. Rotating the leaf directly would
                    // spin it about its own centre, which is a turnstile, not a door.
                    var hingeObject = new GameObject("Hinge");
                    hingeObject.layer = doorLayer;
                    hingeObject.transform.SetParent(pivot.transform, false);

                    var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    leaf.name = "Leaf";
                    leaf.layer = doorLayer;
                    leaf.transform.SetParent(hingeObject.transform, false);
                    leaf.transform.localPosition = new Vector3(half, doorHeight * 0.5f, 0f);
                    leaf.transform.localScale = new Vector3(cellSize, doorHeight, 0.09f);
                    leaf.GetComponent<MeshRenderer>().sharedMaterial = ProtoMaterials.Trim;

                    // A handle on the swinging edge, which is also the clearest way to see
                    // at a glance which side a door is hung on.
                    var handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    handle.name = "Handle";
                    handle.layer = doorLayer;
                    handle.transform.SetParent(hingeObject.transform, false);
                    handle.transform.localPosition =
                        new Vector3(cellSize * 0.86f, doorHeight * 0.47f, 0.075f);
                    handle.transform.localScale = new Vector3(0.14f, 0.05f, 0.06f);
                    handle.GetComponent<MeshRenderer>().sharedMaterial = ProtoMaterials.GunEdge;

                    var handleCollider = handle.GetComponent<Collider>();
                    if (Application.isPlaying) Destroy(handleCollider);
                    else DestroyImmediate(handleCollider);

                    var door = pivot.AddComponent<Door>();

                    var trigger = pivot.GetComponent<BoxCollider>();
                    trigger.isTrigger = true;
                    trigger.size = new Vector3(cellSize * 1.4f, doorHeight, cellSize * 1.1f);
                    trigger.center = new Vector3(half, doorHeight * 0.5f, 0f);

                    // Alternate the swing so a landing of them does not look machine-hung.
                    door.Initialise(hingeObject.transform, ((r + c) % 2 == 0) ? 1f : -1f);
                }
            }
        }

        private void BuildWalls(int floor)
        {
            float baseY = FloorBaseY(floor);
            var covered = new bool[Rows, Columns];

            // Horizontal runs first — they produce the fewest, longest boxes.
            for (int r = 0; r < Rows; r++)
            {
                int c = 0;
                while (c < Columns)
                {
                    if (!IsWall(floor, r, c)) { c++; continue; }

                    int start = c;
                    while (c < Columns && IsWall(floor, r, c)) c++;
                    int length = c - start;
                    if (length < 2) continue;

                    for (int i = start; i < start + length; i++) covered[r, i] = true;

                    Vector3 centre = CellToLocal(r, start + (length - 1) * 0.5f);
                    CreateBox($"Wall_{floor}_H_{r}_{start}",
                        new Vector3(centre.x, baseY + wallHeight * 0.5f, centre.z),
                        new Vector3(length * cellSize, wallHeight, wallThickness),
                        ProtoMaterials.Wall);

                    AddWallTrim($"{floor}_H_{r}_{start}", centre, baseY, length * cellSize, true);
                }
            }

            for (int c = 0; c < Columns; c++)
            {
                int r = 0;
                while (r < Rows)
                {
                    if (!IsWall(floor, r, c)) { r++; continue; }

                    int start = r;
                    while (r < Rows && IsWall(floor, r, c)) r++;
                    int length = r - start;
                    if (length < 2) continue;

                    for (int i = start; i < start + length; i++) covered[i, c] = true;

                    Vector3 centre = CellToLocal(start + (length - 1) * 0.5f, c);
                    CreateBox($"Wall_{floor}_V_{c}_{start}",
                        new Vector3(centre.x, baseY + wallHeight * 0.5f, centre.z),
                        new Vector3(wallThickness, wallHeight, length * cellSize),
                        ProtoMaterials.Wall);

                    AddWallTrim($"{floor}_V_{c}_{start}", centre, baseY, length * cellSize, false);
                }
            }

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    if (!IsWall(floor, r, c) || covered[r, c]) continue;

                    Vector3 centre = CellToLocal(r, c);
                    CreateBox($"Pillar_{floor}_{r}_{c}",
                        new Vector3(centre.x, baseY + wallHeight * 0.5f, centre.z),
                        new Vector3(wallThickness * 2f, wallHeight, wallThickness * 2f),
                        ProtoMaterials.Wall);
                }
            }
        }

        /// <summary>
        /// Skirting, panelling, a dado rail and a picture rail along a wall run.
        ///
        /// All of it is collider-free decoration: trim standing proud of the wall would
        /// otherwise eat a few centimetres off every doorway in the NavMesh, and a rail at
        /// waist height would read as an obstacle to the agents.
        /// </summary>
        private void AddWallTrim(string id, Vector3 centre, float baseY, float runLength, bool horizontal)
        {
            if (!buildWallTrim) return;

            float t = wallThickness;

            // Panelling sits just proud of the plaster, up to the dado rail.
            Vector3 panelSize = horizontal
                ? new Vector3(runLength, dadoHeight, t * 1.35f)
                : new Vector3(t * 1.35f, dadoHeight, runLength);

            CreateDecoration($"Wainscot_{id}",
                transform.position + new Vector3(centre.x, baseY + dadoHeight * 0.5f, centre.z),
                panelSize, ProtoMaterials.Wainscot);

            // Skirting board at the foot.
            Vector3 skirtSize = horizontal
                ? new Vector3(runLength, 0.16f, t * 1.7f)
                : new Vector3(t * 1.7f, 0.16f, runLength);

            CreateDecoration($"Skirting_{id}",
                transform.position + new Vector3(centre.x, baseY + 0.08f, centre.z),
                skirtSize, ProtoMaterials.Trim);

            // Dado rail capping the panelling.
            Vector3 railSize = horizontal
                ? new Vector3(runLength, 0.075f, t * 1.6f)
                : new Vector3(t * 1.6f, 0.075f, runLength);

            CreateDecoration($"DadoRail_{id}",
                transform.position + new Vector3(centre.x, baseY + dadoHeight, centre.z),
                railSize, ProtoMaterials.Trim);

            // Picture rail near the ceiling.
            Vector3 pictureSize = horizontal
                ? new Vector3(runLength, 0.055f, t * 1.45f)
                : new Vector3(t * 1.45f, 0.055f, runLength);

            CreateDecoration($"PictureRail_{id}",
                transform.position + new Vector3(centre.x, baseY + wallHeight - 0.5f, centre.z),
                pictureSize, ProtoMaterials.Trim);
        }

        /// <summary>
        /// Furniture. Everything is a collider on the Default layer, so props are baked
        /// into the NavMesh as obstacles — zombies path around the sofa instead of
        /// through it, and the rooms give the player something to break line of sight on.
        /// </summary>
        private void BuildProps(int floor)
        {
            float baseY = FloorBaseY(floor);

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    char cell = char.ToUpperInvariant(floors[floor].rows[r][c]);
                    if (!IsProp(cell)) continue;

                    Vector3 centre = CellToLocal(r, c) + Vector3.up * baseY;
                    switch (cell)
                    {
                        case 'T': BuildTable(centre, floor, r, c); break;
                        case 'S': BuildSofa(centre, floor, r, c); break;
                        case 'B': BuildBed(centre, floor, r, c); break;
                        case 'C': BuildCabinet(centre, floor, r, c); break;
                        case 'F': BuildBookshelf(centre, floor, r, c); break;
                    }
                }
            }
        }

        private void BuildTable(Vector3 centre, int f, int r, int c)
        {
            CreateBox($"Table_{f}_{r}_{c}", centre + new Vector3(0f, 0.74f, 0f),
                new Vector3(1.4f, 0.08f, 0.85f), ProtoMaterials.Wood);

            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1f : 1f) * 0.6f;
                float z = (i < 2 ? -1f : 1f) * 0.33f;
                CreateBox($"TableLeg_{f}_{r}_{c}_{i}", centre + new Vector3(x, 0.35f, z),
                    new Vector3(0.08f, 0.7f, 0.08f), ProtoMaterials.Wood);
            }
        }

        private void BuildSofa(Vector3 centre, int f, int r, int c)
        {
            CreateBox($"SofaSeat_{f}_{r}_{c}", centre + new Vector3(0f, 0.22f, 0f),
                new Vector3(1.7f, 0.44f, 0.75f), ProtoMaterials.Fabric);
            CreateBox($"SofaBack_{f}_{r}_{c}", centre + new Vector3(0f, 0.6f, -0.32f),
                new Vector3(1.7f, 0.55f, 0.18f), ProtoMaterials.Fabric);
            CreateBox($"SofaArmL_{f}_{r}_{c}", centre + new Vector3(-0.82f, 0.42f, 0f),
                new Vector3(0.16f, 0.42f, 0.75f), ProtoMaterials.Fabric);
            CreateBox($"SofaArmR_{f}_{r}_{c}", centre + new Vector3(0.82f, 0.42f, 0f),
                new Vector3(0.16f, 0.42f, 0.75f), ProtoMaterials.Fabric);
        }

        private void BuildBed(Vector3 centre, int f, int r, int c)
        {
            var frame = CreateBox($"BedFrame_{f}_{r}_{c}", centre + new Vector3(0f, 0.18f, 0f),
                new Vector3(1.3f, 0.36f, 1.8f), ProtoMaterials.Wood);
            var mattress = CreateBox($"BedMattress_{f}_{r}_{c}", centre + new Vector3(0f, 0.46f, 0.05f),
                new Vector3(1.22f, 0.22f, 1.65f), ProtoMaterials.Linen);
            var pillow = CreateBox($"BedPillow_{f}_{r}_{c}", centre + new Vector3(0f, 0.62f, -0.68f),
                new Vector3(0.9f, 0.14f, 0.3f), ProtoMaterials.Linen);
            var head = CreateBox($"BedHead_{f}_{r}_{c}", centre + new Vector3(0f, 0.7f, -0.9f),
                new Vector3(1.3f, 0.8f, 0.1f), ProtoMaterials.Wood);

            // One mesh over all four boxes — bedstead, headboard, a mattress that sags in
            // the middle and a pillow. The four boxes keep their colliders, so the bed
            // blocks and is climbed on exactly as it was.
            //
            // Anchored on the frame, whose scale is (1.3, 0.36, 1.8); the 0.36 is why the
            // vertical numbers below look so large in local units. The mesh spans the whole
            // bed including the headboard, which the frame box does not.
            PropLibrary.Overlay(frame, "Bed",
                localCentre: new Vector3(0f, 0.395f / 0.36f, -0.02f / 1.8f),
                size: new Vector3(1.30f / 1.3f, 1.15f / 0.36f, 1.90f / 1.8f),
                material: ProtoMaterials.Linen,
                hide: new[] { frame, mattress, pillow, head });
        }

        private void BuildCabinet(Vector3 centre, int f, int r, int c)
        {
            CreateBox($"Cabinet_{f}_{r}_{c}", centre + new Vector3(0f, 0.55f, 0f),
                new Vector3(0.95f, 1.1f, 0.5f), ProtoMaterials.Wood);
            CreateBox($"CabinetTop_{f}_{r}_{c}", centre + new Vector3(0f, 1.13f, 0f),
                new Vector3(1.02f, 0.06f, 0.56f), ProtoMaterials.Metal);
        }

        private void BuildBookshelf(Vector3 centre, int f, int r, int c)
        {
            CreateBox($"Shelf_{f}_{r}_{c}", centre + new Vector3(0f, 0.9f, -0.15f),
                new Vector3(1.1f, 1.8f, 0.32f), ProtoMaterials.Wood);

            for (int i = 0; i < 2; i++)
            {
                CreateBox($"Books_{f}_{r}_{c}_{i}", centre + new Vector3(0f, 0.62f + i * 0.62f, 0.02f),
                    new Vector3(0.92f, 0.34f, 0.12f), ProtoMaterials.Fabric);
            }
        }

        private void BuildInteriorLights(int floor)
        {
            int step = Mathf.Max(2, Mathf.RoundToInt(lightSpacingCells));
            float baseY = FloorBaseY(floor);

            for (int r = step / 2; r < Rows; r += step)
            {
                for (int c = step / 2; c < Columns; c += step)
                {
                    if (!IsWalkable(floor, r, c)) continue;

                    Vector3 position = CellToLocal(r, c) + transform.position;
                    var go = new GameObject($"Light_{floor}_{r}_{c}");
                    go.transform.SetParent(_container, false);
                    go.transform.position = new Vector3(position.x, baseY + wallHeight - 0.4f, position.z);

                    var light = go.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = interiorLightColor;
                    light.intensity = interiorLightIntensity;
                    light.range = interiorLightRange;
                    LevelLighting.MakeRoomLight(light);

                    if (buildChandeliers) BuildChandelier(go.transform, floor, r, c);
                }
            }
        }

        /// <summary>
        /// A chandelier hanging where each interior light sits: a chain up to the ceiling,
        /// a ring, and a crown of candles.
        ///
        /// Every part is collider-free on purpose. A chandelier hangs well above head
        /// height and has no business in the physics or navigation world — giving it
        /// colliders would only risk carving holes in the NavMesh below it.
        /// </summary>
        private void BuildChandelier(Transform lightTransform, int floor, int r, int c)
        {
            // The light is a drop below the ceiling; the chain runs back up to it.
            lightTransform.position -= Vector3.up * chandelierDrop;

            Vector3 centre = lightTransform.position;
            string id = $"{floor}_{r}_{c}";

            CreateDecoration($"ChandelierChain_{id}", centre + Vector3.up * (chandelierDrop * 0.5f + 0.1f),
                new Vector3(0.035f, chandelierDrop, 0.035f), ProtoMaterials.Metal);

            var body = CreateDecoration($"ChandelierBody_{id}", centre + Vector3.up * 0.06f,
                new Vector3(0.16f, 0.18f, 0.16f), ProtoMaterials.Metal);

            // The ring, approximated by eight bars around a circle.
            const int arms = 8;
            const float ringRadius = 0.44f;

            var boxes = new List<GameObject> { body };

            for (int i = 0; i < arms; i++)
            {
                float angle = i * (360f / arms);
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * (Vector3.forward * ringRadius);

                var segment = CreateDecoration($"ChandelierRing_{id}_{i}", centre + offset,
                    new Vector3(0.05f, 0.045f, 0.36f), ProtoMaterials.Metal);
                segment.transform.localRotation = Quaternion.Euler(0f, angle + 90f, 0f);
                boxes.Add(segment);

                var arm = CreateDecoration($"ChandelierArm_{id}_{i}", centre + offset * 0.5f + Vector3.up * 0.02f,
                    new Vector3(0.03f, 0.03f, ringRadius), ProtoMaterials.Metal);
                arm.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
                boxes.Add(arm);

                boxes.Add(CreateDecoration($"ChandelierCandle_{id}_{i}", centre + offset + Vector3.up * 0.14f,
                    new Vector3(0.035f, 0.16f, 0.035f), ProtoMaterials.Linen));
            }

            // One mesh in place of twenty-five boxes. The chain stays a box: it is a
            // straight vertical bar, which is the one shape a primitive already gets right.
            //
            // Twenty-five hidden renderers per chandelier sounds wasteful, and it is the
            // cheap half of a trade worth making -- building them anyway is what lets a
            // missing mesh degrade into exactly the old chandelier instead of into nothing
            // hanging from the ceiling.
            var anchor = new GameObject($"ChandelierMesh_{id}");
            anchor.transform.SetParent(_container, false);
            anchor.transform.position = centre + Vector3.up * 0.06f;

            // A little rotation so a row of them down a hallway does not read as one object
            // stamped repeatedly. Visual only -- nothing here has a collider.
            anchor.transform.rotation = Quaternion.Euler(0f, (r * 37 + c * 61) % 360, 0f);

            PropLibrary.Overlay(anchor, "Chandelier", Vector3.zero,
                                new Vector3(ringRadius * 2.15f, 0.46f, ringRadius * 2.15f),
                                ProtoMaterials.Metal, boxes.ToArray());
        }

        /// <summary>
        /// Draws the modelled staircase over a flight of step boxes.
        ///
        /// The mesh has real treads with nosings -- the couple of centimetres a tread
        /// overhangs its riser by -- which is the detail that makes a staircase read as a
        /// staircase rather than as a ramp with lines on it. The boxes underneath keep their
        /// colliders and stop being drawn, so what the player walks on is unchanged to the
        /// millimetre and the NavMesh bake is untouched.
        ///
        /// The rotation lives on an empty anchor rather than on the visual, because Overlay
        /// sets position and scale but not rotation, and scaling a rotated child is how a
        /// staircase ends up sheared. Parenting to an already-rotated empty means the scale
        /// below is applied in the flight's own frame: X across it, Y up it, Z along it.
        /// </summary>
        private void DressStairFlight(StairFlight flight, int floor, Vector3 start,
                                      Vector3 direction, float runDistance, float width,
                                      float baseY, List<GameObject> stepBoxes)
        {
            Vector3 centre = start + direction * (runDistance * 0.5f);

            var anchor = new GameObject(
                $"StairFlight_{floor}_{flight.StartRow}_{flight.StartColumn}");
            anchor.transform.SetParent(_container, false);
            anchor.transform.position = new Vector3(centre.x, baseY + FloorSpacing * 0.5f, centre.z);

            // The mesh climbs towards +Z and rises towards +Y (Blender Y and Z respectively,
            // mapped through the exporter). Pointing the anchor's +Z down the flight is all
            // the orientation it needs, and it works for flights running along either axis.
            anchor.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            PropLibrary.Overlay(anchor, "Stairs", Vector3.zero,
                                new Vector3(width, FloorSpacing, runDistance),
                                ProtoMaterials.Wood, stepBoxes.ToArray());
        }

        /// <summary>A purely visual box: no collider, so it never touches physics or the NavMesh.</summary>
        private GameObject CreateDecoration(string name, Vector3 worldCentre, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(_container, false);
            go.transform.position = worldCentre;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;

            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying) Destroy(collider);
                else DestroyImmediate(collider);
            }

            go.isStatic = true;
            return go;
        }

        private void ScanMarkers(int floor)
        {
            float baseY = FloorBaseY(floor);

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    Vector3 position = CellToLocal(r, c) + transform.position + Vector3.up * baseY;

                    switch (char.ToUpperInvariant(floors[floor].rows[r][c]))
                    {
                        case 'P': PlayerSpawn = position + Vector3.up * 0.2f; break;
                        case 'Z': ZombieSpawns.Add(position); break;
                        case 'A': AmmoSpawns.Add(position); break;
                        case 'M': MedkitSpawns.Add(position); break;
                        // 'X' was the grenade crate. The launcher is gone, so any X still left
                        // in a floor plan now reads as plain floor.
                        case 'V': BatterySpawns.Add(position); break;
                        case 'R': SurvivorSpawns.Add(position); break;
                        case 'W': PowerCellCandidates.Add(position); break;
                        case 'U': PowerUpSpawns.Add(position); break;
                        case 'N': BeltCrateSpawns.Add(position); break;
                        case 'G': MotorPosition = position; break;
                        case 'E': ExitPosition = position; HasExit = true; break;
                    }
                }
            }
        }

        /// <summary>
        /// Works out where a walker could plausibly be waiting: pressed up against a desk,
        /// a bed or a cabinet, or standing in the corner of a room.
        ///
        /// Both cases face the same way — out, away from the cover — so the ambush reads
        /// as something that was standing there rather than something dropped in.
        /// </summary>
        private void ScanHidingSpots(int floor)
        {
            float baseY = FloorBaseY(floor);

            int[] rowSteps = { -1, 1, 0, 0 };
            int[] columnSteps = { 0, 0, 1, -1 };

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    if (!IsWalkable(floor, r, c)) continue;

                    char cell = char.ToUpperInvariant(floors[floor].rows[r][c]);

                    // Never lurk on the player's start, the exit, a staircase or on top of
                    // a prop — the first two would be unfair and the rest do not fit.
                    if (cell == 'P' || cell == 'E' || IsProp(cell) || IsStair(floors[floor].rows[r][c]))
                        continue;

                    Vector3 away = Vector3.zero;
                    int props = 0;
                    int walls = 0;

                    for (int d = 0; d < 4; d++)
                    {
                        int nr = r + rowSteps[d];
                        int nc = c + columnSteps[d];
                        if (!InBounds(nr, nc)) continue;

                        // Outward direction for this neighbour, in world axes.
                        Vector3 outward = new Vector3(-columnSteps[d], 0f, rowSteps[d]);

                        if (IsProp(floors[floor].rows[nr][nc])) { props++; away += outward; }
                        else if (IsWall(floor, nr, nc)) { walls++; away += outward; }
                    }

                    // Behind furniture, or wedged into a corner where two walls meet.
                    bool behindFurniture = props > 0;
                    bool inCorner = props == 0 && walls >= 2;
                    if (!behindFurniture && !inCorner) continue;

                    if (away.sqrMagnitude < 0.01f) continue;

                    Vector3 position = CellToLocal(r, c) + transform.position + Vector3.up * baseY;

                    // Tuck it towards the cover rather than standing in the middle of the cell.
                    Vector3 facing = away.normalized;
                    position -= facing * (cellSize * 0.22f);

                    HidingSpots.Add(new Pose(position, Quaternion.LookRotation(facing, Vector3.up)));
                }
            }
        }

        // ---- helpers --------------------------------------------------------

        private bool InBounds(int r, int c) => r >= 0 && r < Rows && c >= 0 && c < Columns;

        private bool IsWall(int floor, int r, int c) =>
            InBounds(r, c) && floors[floor].rows[r][c] == '#';

        private bool IsWalkable(int floor, int r, int c)
        {
            if (!InBounds(r, c)) return false;
            char cell = floors[floor].rows[r][c];
            return cell != '#' && cell != ' ';
        }

        /// <summary>Furniture characters: floor cells that also carry a prop.</summary>
        private static bool IsProp(char c)
        {
            switch (char.ToUpperInvariant(c))
            {
                case 'T': case 'S': case 'B': case 'C': case 'F': return true;
                default: return false;
            }
        }

        /// <summary>
        /// Cell centre relative to this transform, at ground level. Row 0 is the north
        /// (+Z) edge, so the plan reads the same way in the scene view from above as it
        /// does in the inspector.
        /// </summary>
        private Vector3 CellToLocal(float row, float column)
        {
            float x = (column + 0.5f) * cellSize - Columns * cellSize * 0.5f;
            float z = (Rows - 1f - row + 0.5f) * cellSize - Rows * cellSize * 0.5f;
            return new Vector3(x, 0f, z);
        }

        private GameObject CreateBox(string name, Vector3 localCenter, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(_container, false);
            go.transform.position = transform.position + localCenter;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            go.isStatic = true;
            return go;
        }
    }
}
