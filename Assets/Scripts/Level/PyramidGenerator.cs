using System.Collections.Generic;
using UnityEngine;

namespace ZombieHouse.Level
{
    /// <summary>
    /// Builds the pyramid: a long entrance passage, a spine running east with chambers
    /// off it, and the burial chamber at the far end.
    ///
    /// The shape is deliberately the opposite of the school's. A school is a corridor with
    /// rooms you can see into; a tomb is a corridor with rooms you cannot, joined by
    /// doorways barely wider than you are, and lit only by whatever is still burning. You
    /// never see a chamber before you are standing in it.
    ///
    /// Its floor plan is generated and proved by `Tools_PyramidPlan.py` rather than
    /// authored by hand — it is flood-filled with every solid prop treated as solid, and
    /// refuses to be written out if a single room is cut off. That script exists because
    /// the school shipped three separately sealed rooms that each cost a NavMesh bake to
    /// find.
    ///
    /// Legend:  '#' stone   '.' floor   'D' doorway
    ///          'P' player start   'Z' enemy spawn   'R' survivor
    ///          'A' ammo   'M' medkit   'V' torch battery
    ///          'W' possible power cell position   'U' Uzi   'G' door motor   'E' exit
    ///          'C' column   'Q' sarcophagus   'O' rubble   'T' wall torch
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PyramidGenerator : MonoBehaviour, ILevelSource
    {
        [Header("Scale")]
        [SerializeField] private float cellSize = 2f;
        [SerializeField] private float wallHeight = 4.6f;
        [SerializeField] private float wallThickness = 0.3f;

        [Header("Options")]
        [SerializeField] private bool generateOnAwake = true;
        [SerializeField] private bool buildProps = true;
        [SerializeField] private bool buildCeiling = true;

        [Header("Torches")]
        [Tooltip("Share of the wall torches that are still burning. The rest are dead "
                 + "sconces — the tomb has been dark a long time.")]
        [Range(0f, 1f)] [SerializeField] private float litTorchShare = 0.62f;
        [SerializeField] private Color torchColour = new Color(1f, 0.63f, 0.26f);
        [SerializeField] private float torchIntensity = 1.6f;
        [SerializeField] private float torchRange = 10f;

        [Header("Floor plan — every row must be the same length")]
        [SerializeField]
        private string[] rows =
        {
            "#############################################",
            "#...#...#....T..#.....#.......#.....#.....#.#",
            "#.#..X..A...#.........#.####..#.#.###...#.#.#",
            "#.#..N.W.O......M...#Z#.#.#...#.#.....#...#.#",
            "#.#.WQ..Z...#..Z.W..###...#.#.#.#.#####.#.#.#",
            "#...MZ..RT..#.U.OT.......T#Z......#.....#...#",
            "#...A.V..N.......Z.###.##.###.#.#...#.#.#####",
            "#.....#.....#...#......Z.....T...........T..#",
            "#...###.###...#.#.######.####..TZ.O...RT.Z#.#",
            "#...#...#...#............Z#.....C.Z.CM..C.#.#",
            "#.#.#.########..#######.#.#.##T..V.....Q..#GE",
            "#P................#..Z#......T.N..Q..Z......#",
            "##........#.##.####.#.###.###...C..AC.O.CZ#.#",
            "#..VC..C.....T......#...#.#Z...T..Z....TNT..#",
            "#..U.Z...##.#....A..###.#.#.#...#.#...#.#..##",
            "#.........#...N.QZV...#Z#.....#...#.#.#.#...#",
            "#...CW.C..###.M..NO.#.#.#####.#.###.#.#.#.#.#",
            "#T..ZT.......A.QRTW.#.#....Z....#.....#...#.#",
            "#...#.#...#....X.Z..#.########..#####...###.#",
            "#...#....T..#...#...#................T......#",
            "#############################################"
        };

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
        public List<Pose> HidingSpots { get; } = new List<Pose>();

        /// <summary>Whoever is still alive down here is hiding, not tied up.</summary>
        public bool SurvivorsAreBound => false;

        public List<Vector3> PowerUpSpawns { get; } = new List<Vector3>();
        public List<Vector3> BeltCrateSpawns { get; } = new List<Vector3>();
        public List<Vector3> PowerCellCandidates { get; } = new List<Vector3>();
        public Vector3 PowerCellSpawn { get; private set; }
        public Vector3 MotorPosition { get; private set; }

        private const string ContainerName = "PyramidGeometry";
        private Transform _container;
        private System.Random _rng;

        private int Rows => rows.Length;
        private int Columns => rows.Length == 0 ? 0 : rows[0].Length;

        private void Awake()
        {
            if (generateOnAwake) Generate();
        }

        [ContextMenu("Generate Pyramid")]
        public void Generate()
        {
            ClearGeometry();
            Reset();

            if (!ValidateLayout()) return;

            _rng = new System.Random(20260820);
            _container = new GameObject(ContainerName).transform;
            _container.SetParent(transform, false);

            float width = Columns * cellSize;
            float depth = Rows * cellSize;

            LevelBounds = new Bounds(
                transform.position + new Vector3(0f, wallHeight * 0.5f, 0f),
                new Vector3(width + 6f, wallHeight + 8f, depth + 6f));

            BuildFloor();
            BuildWalls();
            if (buildProps) BuildProps();
            BuildTorches();
            if (buildCeiling) BuildCeiling(width, depth);

            ScanMarkers();
            ScanHidingSpots();

            PowerCellSpawn = PowerCellPlacement.Draw(PowerCellCandidates, PlayerSpawn, PlayerSpawn);

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
            Generated = false;
        }

        [ContextMenu("Clear Pyramid")]
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

        private bool ValidateLayout()
        {
            if (rows == null || rows.Length < 3)
            {
                Debug.LogError("[Pyramid] The floor plan needs at least three rows.");
                return false;
            }

            int length = rows[0].Length;
            for (int r = 0; r < rows.Length; r++)
            {
                if (rows[r].Length == length) continue;
                Debug.LogError($"[Pyramid] Row {r} is {rows[r].Length} characters; row 0 is {length}.");
                return false;
            }

            return true;
        }

        // ---- shell -----------------------------------------------------------

        private void BuildFloor()
        {
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    if (rows[r][c] == '#') continue;

                    // Sand drifted over dressed stone, in an irregular pattern rather
                    // than a chequerboard — a tomb floor is not tiled.
                    Material floor = _rng.NextDouble() < 0.3 ? ProtoMaterials.Sand
                                   : ((r + c) % 3 == 0 ? ProtoMaterials.SandstoneAlt
                                                       : ProtoMaterials.Sandstone);

                    CreateBox($"Floor_{r}_{c}", CellToLocal(r, c) + Vector3.down * 0.05f,
                        new Vector3(cellSize, 0.1f, cellSize), floor, true);
                }
            }
        }

        private void BuildWalls()
        {
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    if (rows[r][c] != '#') continue;
                    if (!TouchesFloor(r, c)) continue;   // no need to build the solid mass

                    CreateBox($"Stone_{r}_{c}", CellToLocal(r, c) + Vector3.up * (wallHeight * 0.5f),
                        new Vector3(cellSize + wallThickness, wallHeight, cellSize + wallThickness),
                        ProtoMaterials.Sandstone, true);

                    // Hieroglyph bands at eye height on the walls that face a room.
                    if (_rng.NextDouble() < 0.35)
                    {
                        CreateDecoration($"Glyphs_{r}_{c}",
                            CellToLocal(r, c) + Vector3.up * 1.8f,
                            new Vector3(cellSize + wallThickness + 0.04f, 0.9f, cellSize + wallThickness + 0.04f),
                            ProtoMaterials.Hieroglyph);
                    }
                }
            }
        }

        /// <summary>
        /// True when a stone cell borders open floor. The plan is mostly solid rock, and
        /// building a cube for every buried cell would be tens of thousands of objects
        /// that nobody will ever see.
        /// </summary>
        private bool TouchesFloor(int r, int c)
        {
            for (int dr = -1; dr <= 1; dr++)
            {
                for (int dc = -1; dc <= 1; dc++)
                {
                    int rr = r + dr, cc = c + dc;
                    if (rr < 0 || rr >= Rows || cc < 0 || cc >= Columns) continue;
                    if (rows[rr][cc] != '#') return true;
                }
            }

            return false;
        }

        private void BuildCeiling(float width, float depth)
        {
            CreateBox("Ceiling", new Vector3(0f, wallHeight + 0.15f, 0f),
                new Vector3(width, 0.3f, depth), ProtoMaterials.SandstoneAlt, true);
        }

        // ---- props -------------------------------------------------------------

        private void BuildProps()
        {
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    Vector3 at = CellToLocal(r, c);

                    switch (char.ToUpperInvariant(rows[r][c]))
                    {
                        case 'C': BuildColumn(r, c, at); break;
                        case 'Q': BuildSarcophagus(r, c, at); break;
                        case 'O': BuildRubble(r, c, at); break;
                    }
                }
            }
        }

        /// <summary>A fluted column with a papyrus capital, floor to ceiling.</summary>
        private void BuildColumn(int r, int c, Vector3 at)
        {
            CreateBox($"ColumnBase_{r}_{c}", at + Vector3.up * 0.18f,
                new Vector3(1.35f, 0.36f, 1.35f), ProtoMaterials.Granite, true);

            var shaft = CreateCylinder($"Column_{r}_{c}", at + Vector3.up * (wallHeight * 0.5f),
                new Vector3(1.05f, wallHeight * 0.5f, 1.05f), ProtoMaterials.Sandstone, true);
            shaft.transform.rotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);

            CreateDecoration($"ColumnCapital_{r}_{c}", at + Vector3.up * (wallHeight - 0.35f),
                new Vector3(1.4f, 0.7f, 1.4f), ProtoMaterials.SandstoneAlt);

            // A column is a corner: something can stand behind one in a lit hall.
            AddHidingSpot(at + new Vector3(0f, 0f, cellSize * 0.55f), Vector3.back);
        }

        /// <summary>
        /// A stone coffin with a gilded lid. Solid, waist high, and the best cover in a
        /// chamber that otherwise has none.
        /// </summary>
        private void BuildSarcophagus(int r, int c, Vector3 at)
        {
            var box = CreateBox($"Sarcophagus_{r}_{c}", at + Vector3.up * 0.55f,
                new Vector3(1.1f, 1.1f, 2.4f), ProtoMaterials.Granite, true);
            box.transform.rotation = Quaternion.Euler(0f, Range(-8f, 8f), 0f);

            var lid = CreateDecoration($"SarcLid_{r}_{c}", at + Vector3.up * 1.16f,
                new Vector3(1.2f, 0.18f, 2.5f), ProtoMaterials.Gold);
            lid.transform.rotation = box.transform.rotation;

            // The mask on the lid — one gold face in a dark room does a lot of work.
            var mask = CreateDecoration($"SarcMask_{r}_{c}", at + new Vector3(0f, 1.28f, 0.7f),
                new Vector3(0.5f, 0.14f, 0.66f), ProtoMaterials.Gold);

            // The mesh carries chest, lid and mask together, so all three boxes go dark and
            // one object replaces them. Overlay rather than Dress because it stands taller
            // than the chest box — the lid is proud of it, which is most of the silhouette.
            //
            // Local units: Overlay works in the anchor's own space and the anchor is a cube
            // scaled to (1.1, 1.1, 2.4), so every world measurement below is divided by the
            // axis it sits on. Forgetting that division is how a prop arrives at the right
            // place in the wrong size.
            PropLibrary.Overlay(box, "Sarcophagus",
                localCentre: new Vector3(0f, 0.10f / 1.1f, 0f),
                size: new Vector3(1.20f / 1.1f, 1.30f / 1.1f, 2.50f / 2.4f),
                material: ProtoMaterials.Granite,
                hide: new[] { box, lid, mask });

            AddHidingSpot(at + new Vector3(cellSize * 0.6f, 0f, 0f), Vector3.right);
        }

        /// <summary>Fallen ceiling: a heap of blocks you have to walk around.</summary>
        private void BuildRubble(int r, int c, Vector3 at)
        {
            for (int i = 0; i < 5; i++)
            {
                float size = Range(0.5f, 1.1f);
                var block = CreateBox($"Rubble_{r}_{c}_{i}",
                    at + new Vector3(Range(-0.5f, 0.5f), size * 0.4f, Range(-0.5f, 0.5f)),
                    new Vector3(size, size * 0.8f, size), ProtoMaterials.Sandstone, true);

                block.transform.rotation = Quaternion.Euler(Range(-18f, 18f), Range(0f, 360f), Range(-18f, 18f));
            }

            AddHidingSpot(at + new Vector3(0f, 0f, cellSize * 0.6f), Vector3.back);
        }

        // ---- torches -------------------------------------------------------------

        /// <summary>
        /// Wall sconces, most still burning. The pyramid is lit warm and low and in
        /// pools — the opposite of the school's cold overhead strips, and the reason a
        /// chamber reads as a chamber rather than as a dark box with things in it.
        /// </summary>
        private void BuildTorches()
        {
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    if (char.ToUpperInvariant(rows[r][c]) != 'T') continue;

                    Vector3 at = CellToLocal(r, c) + Vector3.up * 2.3f;
                    bool lit = _rng.NextDouble() < litTorchShare;

                    CreateDecoration($"Sconce_{r}_{c}", at,
                        new Vector3(0.22f, 0.5f, 0.22f), ProtoMaterials.Granite);

                    CreateDecoration($"TorchHead_{r}_{c}", at + Vector3.up * 0.34f,
                        new Vector3(0.3f, 0.34f, 0.3f),
                        lit ? ProtoMaterials.Flame : ProtoMaterials.Granite);

                    if (!lit) continue;

                    var lightObject = new GameObject($"Torch_{r}_{c}");
                    lightObject.transform.SetParent(_container, false);
                    lightObject.transform.position = transform.position + at + Vector3.up * 0.4f;

                    var light = lightObject.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = torchColour;
                    light.intensity = torchIntensity;
                    light.range = torchRange;
                    light.shadows = LightShadows.None;

                    // Firelight is never still. The same component the school's failing
                    // fluorescents use, but it reads completely differently on a flame.
                    lightObject.AddComponent<FlickeringLight>();
                }
            }
        }

        // ---- markers ---------------------------------------------------------------

        private void ScanMarkers()
        {
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    Vector3 position = CellToLocal(r, c) + transform.position;

                    switch (char.ToUpperInvariant(rows[r][c]))
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

        private void ScanHidingSpots()
        {
            for (int r = 1; r < Rows - 1; r++)
            {
                for (int c = 1; c < Columns - 1; c++)
                {
                    if (rows[r][c] == '#') continue;

                    bool north = rows[r - 1][c] == '#';
                    bool south = rows[r + 1][c] == '#';
                    bool west = rows[r][c - 1] == '#';
                    bool east = rows[r][c + 1] == '#';

                    int walls = (north ? 1 : 0) + (south ? 1 : 0) + (west ? 1 : 0) + (east ? 1 : 0);
                    if (walls < 2) continue;

                    Vector3 facing = Vector3.zero;
                    if (north) facing += Vector3.forward;
                    if (south) facing += Vector3.back;
                    if (west) facing += Vector3.right;
                    if (east) facing += Vector3.left;

                    AddHidingSpot(CellToLocal(r, c), -facing);
                }
            }
        }

        // ---- helpers -----------------------------------------------------------------

        private Vector3 CellToLocal(int r, int c)
        {
            float x = (c - (Columns - 1) * 0.5f) * cellSize;
            float z = ((Rows - 1) * 0.5f - r) * cellSize;
            return new Vector3(x, 0f, z);
        }

        private void AddHidingSpot(Vector3 localPosition, Vector3 facing)
        {
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.01f) facing = Vector3.forward;
            facing.Normalize();

            HidingSpots.Add(new Pose(transform.position + localPosition,
                                     Quaternion.LookRotation(facing, Vector3.up)));
        }

        private float Range(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

        private GameObject CreateBox(string name, Vector3 localCentre, Vector3 size,
                                     Material material, bool solid)
        {
            return CreatePrimitive(PrimitiveType.Cube, name, localCentre, size, material, solid);
        }

        private GameObject CreateCylinder(string name, Vector3 localCentre, Vector3 size,
                                          Material material, bool solid)
        {
            return CreatePrimitive(PrimitiveType.Cylinder, name, localCentre, size, material, solid);
        }

        private GameObject CreateDecoration(string name, Vector3 localCentre, Vector3 size, Material material)
        {
            return CreateBox(name, localCentre, size, material, false);
        }

        private GameObject CreatePrimitive(PrimitiveType type, string name, Vector3 localCentre,
                                           Vector3 size, Material material, bool solid)
        {
            var go = GameObject.CreatePrimitive(type);
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
    }
}
