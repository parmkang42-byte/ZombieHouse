using System.Collections.Generic;
using UnityEngine;

namespace ZombieHouse.Level
{
    /// <summary>
    /// Builds the school: one long double-loaded corridor with classrooms down both sides,
    /// a gym at one end and a cafeteria at the other.
    ///
    /// Same ASCII-plan idea as the house, but single storey and much simpler for it — no
    /// stairs, no storeys to keep in sync, no roof to cut holes in. What it adds instead is
    /// the thing that makes a school frightening: **lockers**. A bank of lockers every few
    /// metres turns a straight corridor into a run of blind alcoves, so a hallway you can
    /// see the whole length of still hides a dozen places something can be standing.
    ///
    /// Legend:  '#' wall   '.' floor   'D' doorway
    ///          'P' player start   'Z' zombie spawn   'R' survivor (a child, hiding)
    ///          'A' ammo   'M' medkit   'V' torch battery
    ///          'W' possible power cell position   'G' door motor   'U' Uzi   'E' fire door
    ///          'L' lockers   'K' desk   'T' cafeteria table   'B' bookshelf
    ///          'Y' gym equipment   'S' stage
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class SchoolGenerator : MonoBehaviour, ILevelSource
    {
        [Header("Scale")]
        [SerializeField] private float cellSize = 2f;
        [SerializeField] private float wallHeight = 3.4f;

        [Tooltip("How tall the door leaves are. Shorter than the wall, so the frame above "
                 + "them still reads.")]
        [SerializeField] private float doorHeight = 2.6f;

        [Tooltip("Layer the doors are built on. It must be one the NavMesh baker ignores, "
                 + "or a closed door bakes as a wall and seals the room behind it.")]
        [SerializeField] private int doorLayer;
        [SerializeField] private float wallThickness = 0.22f;

        [Header("Options")]
        [SerializeField] private bool generateOnAwake = true;
        [SerializeField] private bool buildProps = true;
        [SerializeField] private bool buildCeiling = true;

        [Header("Fluorescents")]
        [Tooltip("One tube every N cells along the corridors and in each room.")]
        [SerializeField] private float lightSpacingCells = 4f;
        [Tooltip("Share of the tubes that are dead — grey glass and no light at all.")]
        [Range(0f, 1f)] [SerializeField] private float deadTubeShare = 0.42f;
        [Tooltip("Share of the live ones that flicker rather than burn steadily.")]
        [Range(0f, 1f)] [SerializeField] private float flickerShare = 0.45f;
        [SerializeField] private Color tubeColour = new Color(0.82f, 0.90f, 1f);
        [SerializeField] private float tubeIntensity = 1.35f;
        [SerializeField] private float tubeRange = 9f;

        [Header("Floor plan — every row must be the same length")]
        [SerializeField]
        private string[] rows =
        {
            "##########################################",
            "#.YV....Y.#.KAK.#.Z.K..#.Z.K..#BX..B#....#",
            "#.........#.W.K.#.K.M..#.K.K..#.Z...#.W..#",
            "#.Y..Z..Y.#.K.K.#.K.KZ.#.K.M..#BN..B#.R..#",
            "#..U......#.K.K.#.V.K..#.W.KZ.#..A..#..Z.#",
            "#.SSS.S...#.....#......#......#.....#....#",
            "#####D#######D#####D######D######D####D###",
            "#..............LL........................#",
            "#.P............Z....N........Z...N.......#",
            "#........LL..........LL............Z.....#",
            "#####D#######D#####D######D######D####D###",
            "#..Z......#.K.K.#.Z.W..#.T.T..#..M..#.Z..#",
            "#.....R...#.KAK.#.T.T.U#.T.V..#Z....#....#",
            "#..W......#.KZK.#.T.T..#.X.T..#..R..#....#",
            "#...M.N...#.K.K.#.T.TA.#.Z.T..#...Z.#.V..#",
            "#.........#.....#......#......#.....#.G.E#",
            "##########################################"
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

        /// <summary>The children here are hiding, not tied up.</summary>
        public bool SurvivorsAreBound => false;

        public List<Vector3> PowerUpSpawns { get; } = new List<Vector3>();
        public List<Vector3> BeltCrateSpawns { get; } = new List<Vector3>();
        public List<Vector3> PowerCellCandidates { get; } = new List<Vector3>();
        public Vector3 PowerCellSpawn { get; private set; }
        public Vector3 MotorPosition { get; private set; }

        private const string ContainerName = "SchoolGeometry";
        private Transform _container;
        private System.Random _rng;

        private int Rows => rows.Length;
        private int Columns => rows.Length == 0 ? 0 : rows[0].Length;

        private void Awake()
        {
            if (generateOnAwake) Generate();
        }

        [ContextMenu("Generate School")]
        public void Generate()
        {
            ClearGeometry();
            Reset();

            if (!ValidateLayout()) return;

            _rng = new System.Random(20260819);
            _container = new GameObject(ContainerName).transform;
            _container.SetParent(transform, false);

            float width = Columns * cellSize;
            float depth = Rows * cellSize;

            LevelBounds = new Bounds(
                transform.position + new Vector3(0f, wallHeight * 0.5f, 0f),
                new Vector3(width + 6f, wallHeight + 8f, depth + 6f));

            BuildFloor(width, depth);
            BuildWalls();
            BuildChalkboards();
            if (buildProps) BuildProps();
            BuildFluorescents();
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

        [ContextMenu("Clear School")]
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
                Debug.LogError("[School] The floor plan needs at least three rows.");
                return false;
            }

            int length = rows[0].Length;
            for (int r = 0; r < rows.Length; r++)
            {
                if (rows[r].Length == length) continue;
                Debug.LogError($"[School] Row {r} is {rows[r].Length} characters; row 0 is {length}.");
                return false;
            }

            return true;
        }

        // ---- shell -----------------------------------------------------------

        private void BuildFloor(float width, float depth)
        {
            // Chequered linoleum, which is what a school floor is, and it gives the
            // corridor a sense of length as you walk it.
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    if (rows[r][c] == '#') continue;

                    Material tile = (r + c) % 2 == 0 ? ProtoMaterials.Linoleum : ProtoMaterials.LinoleumAlt;
                    if (IsGym(r, c)) tile = ProtoMaterials.GymFloor;

                    CreateBox($"Floor_{r}_{c}", CellToLocal(r, c) + Vector3.down * 0.05f,
                        new Vector3(cellSize, 0.1f, cellSize), tile, true);
                }
            }

            _ = width;
            _ = depth;
        }

        /// <summary>The gym is the block of 'Y' and 'S' cells at the west end.</summary>
        private bool IsGym(int r, int c)
        {
            char ch = char.ToUpperInvariant(rows[r][c]);
            if (ch == 'Y' || ch == 'S') return true;

            // Everything inside the same room as the gym equipment, roughly.
            return r < 7 && c < 10;
        }

        private void BuildWalls()
        {
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    if (rows[r][c] != '#') continue;

                    CreateBox($"Wall_{r}_{c}", CellToLocal(r, c) + Vector3.up * (wallHeight * 0.5f),
                        new Vector3(cellSize + wallThickness, wallHeight, cellSize + wallThickness),
                        ProtoMaterials.SchoolWall, true);
                }
            }

            // Door frames, so a 'D' reads as a doorway rather than as a gap.
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    if (char.ToUpperInvariant(rows[r][c]) != 'D') continue;

                    CreateDecoration($"DoorHead_{r}_{c}",
                        CellToLocal(r, c) + Vector3.up * (wallHeight - 0.3f),
                        new Vector3(cellSize, 0.6f, cellSize * 0.5f), ProtoMaterials.Trim);

                    BuildDoor(r, c);
                }
            }
        }

        /// <summary>True if the cell is off the plan or a wall. Off-plan counts as wall.</summary>
        private bool IsWall(int r, int c)
        {
            if (r < 0 || r >= Rows || c < 0 || c >= Columns) return true;
            return rows[r][c] == '#';
        }

        /// <summary>
        /// A hinged door in a doorway.
        ///
        /// It is built on the door layer, which <see cref="RuntimeNavMeshBaker"/> does not
        /// bake — so a hundred doors change navigation by nothing at all and every level
        /// verification stays valid. A closed door that baked as a wall would seal
        /// classrooms and strand spawns, and the failure would look like a level-design
        /// mistake rather than a door one.
        /// </summary>
        private void BuildDoor(int r, int c)
        {
            // Which way the opening runs. Walls to left and right mean the gap is spanned
            // across the columns; otherwise it is spanned across the rows.
            bool acrossColumns = IsWall(r, c - 1) && IsWall(r, c + 1);

            Vector3 centre = CellToLocal(r, c);

            var pivot = new GameObject($"Door_{r}_{c}");
            pivot.layer = doorLayer;
            pivot.transform.SetParent(_container, false);

            // The hinge sits at one edge of the opening, not in the middle, or the leaf
            // sweeps through the wall when it turns.
            float half = cellSize * 0.5f;
            Vector3 hingeOffset = acrossColumns ? new Vector3(-half, 0f, 0f)
                                                : new Vector3(0f, 0f, -half);

            pivot.transform.position = transform.position + centre + hingeOffset;
            if (!acrossColumns) pivot.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            // Pivot -> hinge -> leaf. The hinge sits on the pivot at the hanging edge and
            // is the thing that turns; the leaf hangs off it, offset by half the opening.
            // Rotating the leaf directly would spin it about its own centre.
            var hinge = new GameObject("Hinge");
            hinge.layer = doorLayer;
            hinge.transform.SetParent(pivot.transform, false);

            var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leaf.name = "Leaf";
            leaf.layer = doorLayer;
            leaf.transform.SetParent(hinge.transform, false);
            leaf.transform.localPosition = new Vector3(half, doorHeight * 0.5f, 0f);
            leaf.transform.localScale = new Vector3(cellSize, doorHeight, 0.09f);
            leaf.GetComponent<MeshRenderer>().sharedMaterial = ProtoMaterials.Trim;

            // A handle, on the swinging edge — which is also the clearest way to see at a
            // glance which side a door is hung on.
            var handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handle.name = "Handle";
            handle.layer = doorLayer;
            handle.transform.SetParent(hinge.transform, false);
            handle.transform.localPosition = new Vector3(cellSize * 0.86f, doorHeight * 0.47f, 0.075f);
            handle.transform.localScale = new Vector3(0.14f, 0.05f, 0.06f);
            handle.GetComponent<MeshRenderer>().sharedMaterial = ProtoMaterials.GunEdge;

            var handleCollider = handle.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(handleCollider); else DestroyImmediate(handleCollider);

            var door = pivot.AddComponent<Door>();

            // A trigger a little wider than the leaf, so anything walking into the doorway
            // shoves it rather than clipping the very edge and passing through.
            var trigger = pivot.GetComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(cellSize * 1.4f, doorHeight, cellSize * 1.1f);
            trigger.center = new Vector3(half, doorHeight * 0.5f, 0f);

            // Alternate which side each door swings, so a corridor of them does not look
            // like it was installed by a machine.
            door.Initialise(hinge.transform, ((r + c) % 2 == 0) ? 1f : -1f);
        }

        /// <summary>
        /// Chalkboards: one per classroom, on a wall the desks face.
        ///
        /// Found from the desks rather than from a marker in the plan, because a board
        /// belongs wherever the room is and the plan already says where the rooms are. A
        /// wall cell next to a desk is a classroom wall by definition, and the spacing rule
        /// below stops a room with eight desks getting eight boards.
        /// </summary>
        private void BuildChalkboards()
        {
            var placed = new List<Vector2Int>();

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    if (char.ToUpperInvariant(rows[r][c]) != 'K') continue;

                    // Look outward from the desk for a wall to hang a board on.
                    for (int d = 0; d < 4; d++)
                    {
                        int dr = d == 0 ? -1 : d == 1 ? 1 : 0;
                        int dc = d == 2 ? -1 : d == 3 ? 1 : 0;

                        int wr = r + dr * 2;
                        int wc = c + dc * 2;

                        if (!IsWall(wr, wc)) continue;
                        if (wr < 0 || wr >= Rows || wc < 0 || wc >= Columns) continue;

                        // One board per classroom, not one per desk.
                        bool crowded = false;
                        foreach (Vector2Int seen in placed)
                            if (Mathf.Abs(seen.x - wr) + Mathf.Abs(seen.y - wc) < 6) crowded = true;

                        if (crowded) continue;

                        placed.Add(new Vector2Int(wr, wc));
                        HangChalkboard(wr, wc, -dr, -dc);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// One board, flat against the wall.
        ///
        /// Built as a painting rather than as furniture: everything sits within about three
        /// centimetres of the wall face, and there is no tray, no chalk ledge and nothing
        /// with depth for the player to catch on. A classroom wall wants a dark rectangle
        /// with a border and some ghosting on it — the trays and stubs of chalk that were
        /// here first read as clutter at eye height, stuck out into a corridor people brush
        /// past, and bought nothing at the distance anyone actually sees them from.
        ///
        /// Collider-free, like every other wall decoration: the wall behind it already
        /// stops you, and a second surface in the same place is a seam to get stuck on.
        /// </summary>
        private void HangChalkboard(int r, int c, int intoRoomR, int intoRoomC)
        {
            // Just proud of the wall face, or it z-fights with the wall behind it.
            Vector3 facing = new Vector3(intoRoomC, 0f, -intoRoomR);
            Vector3 outward = facing * (cellSize * 0.5f + 0.02f);
            Vector3 at = CellToLocal(r, c) + outward + Vector3.up * 1.6f;

            bool alongX = intoRoomC == 0;

            // The board itself: wide, shallow, and thin enough to read as painted on.
            Vector3 boardSize = alongX ? new Vector3(cellSize * 1.5f, 1.1f, 0.02f)
                                       : new Vector3(0.02f, 1.1f, cellSize * 1.5f);

            CreateDecoration($"Chalkboard_{r}_{c}", at, boardSize, ProtoMaterials.Chalkboard);

            // A painted border, a shade lighter, sitting a millimetre in front. Four thin
            // strips rather than a frame with depth.
            Vector3 railH = alongX ? new Vector3(cellSize * 1.56f, 0.05f, 0.012f)
                                   : new Vector3(0.012f, 0.05f, cellSize * 1.56f);
            Vector3 railV = alongX ? new Vector3(0.05f, 1.16f, 0.012f)
                                   : new Vector3(0.012f, 1.16f, 0.05f);

            Vector3 nudge = facing * 0.008f;
            Vector3 across = alongX ? Vector3.right : Vector3.forward;

            CreateDecoration($"ChalkEdgeTop_{r}_{c}", at + Vector3.up * 0.575f + nudge,
                             railH, ProtoMaterials.ChalkTray);
            CreateDecoration($"ChalkEdgeBottom_{r}_{c}", at - Vector3.up * 0.575f + nudge,
                             railH, ProtoMaterials.ChalkTray);
            CreateDecoration($"ChalkEdgeLeft_{r}_{c}", at - across * (cellSize * 0.755f) + nudge,
                             railV, ProtoMaterials.ChalkTray);
            CreateDecoration($"ChalkEdgeRight_{r}_{c}", at + across * (cellSize * 0.755f) + nudge,
                             railV, ProtoMaterials.ChalkTray);

            // Ghosting: what a board looks like after a term of being half-wiped. This is
            // the only part that says the room was used, so it does the most work.
            for (int i = 0; i < 4; i++)
            {
                float offsetAcross = (i - 1.5f) * cellSize * 0.33f;
                float offsetUp = ((i % 2) - 0.5f) * 0.34f;

                Vector3 smearSize = alongX
                    ? new Vector3(cellSize * 0.30f, 0.16f + i * 0.02f, 0.006f)
                    : new Vector3(0.006f, 0.16f + i * 0.02f, cellSize * 0.30f);

                CreateDecoration($"ChalkSmear_{r}_{c}_{i}",
                                 at + across * offsetAcross + Vector3.up * offsetUp + nudge * 1.6f,
                                 smearSize, ProtoMaterials.ChalkDust);
            }
        }

        private void BuildCeiling(float width, float depth)
        {
            CreateBox("Ceiling", new Vector3(0f, wallHeight + 0.1f, 0f),
                new Vector3(width, 0.2f, depth), ProtoMaterials.LinoleumAlt, true);
        }

        // ---- props -----------------------------------------------------------

        private void BuildProps()
        {
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    Vector3 at = CellToLocal(r, c);

                    switch (char.ToUpperInvariant(rows[r][c]))
                    {
                        case 'L': BuildLockers(r, c, at); break;
                        case 'K': BuildDesk(r, c, at); break;
                        case 'T': BuildCafeteriaTable(r, c, at); break;
                        case 'B': BuildBookshelf(r, c, at); break;
                        case 'Y': BuildGymEquipment(r, c, at); break;
                        case 'S': BuildStage(r, c, at); break;
                    }
                }
            }

            BuildWhiteboards();
        }

        /// <summary>
        /// A bank of lockers against the corridor wall. These are the level: a corridor
        /// you can see the whole length of still hides something behind every bank.
        /// </summary>
        private void BuildLockers(int r, int c, Vector3 at)
        {
            CreateBox($"Lockers_{r}_{c}", at + Vector3.up * 0.95f,
                new Vector3(cellSize * 0.92f, 1.9f, 0.6f), ProtoMaterials.Locker, true);

            // Doors, so it reads as lockers rather than as a green box.
            for (int i = 0; i < 3; i++)
            {
                CreateDecoration($"LockerDoor_{r}_{c}_{i}",
                    at + new Vector3(-cellSize * 0.3f + i * (cellSize * 0.3f), 0.95f, 0.31f),
                    new Vector3(cellSize * 0.26f, 1.8f, 0.03f), ProtoMaterials.LinoleumAlt);

                CreateDecoration($"LockerVent_{r}_{c}_{i}",
                    at + new Vector3(-cellSize * 0.3f + i * (cellSize * 0.3f), 1.6f, 0.33f),
                    new Vector3(cellSize * 0.16f, 0.12f, 0.02f), ProtoMaterials.Trim);
            }

            // Somewhere to wait, tucked at the end of the bank facing along the corridor.
            AddHidingSpot(at + new Vector3(cellSize * 0.55f, 0f, 0f), Vector3.forward);
        }

        private void BuildDesk(int r, int c, Vector3 at)
        {
            var top = CreateBox($"DeskTop_{r}_{c}", at + Vector3.up * 0.62f,
                new Vector3(0.85f, 0.06f, 0.55f), ProtoMaterials.DeskTop, true);

            var legs = new GameObject[2];
            for (int s = -1; s <= 1; s += 2)
            {
                legs[(s + 1) / 2] = CreateDecoration($"DeskLeg_{r}_{c}_{s}",
                    at + new Vector3(0.36f * s, 0.31f, 0f),
                    new Vector3(0.05f, 0.62f, 0.5f), ProtoMaterials.Metal);
            }

            // A whole desk — sloped lid, pencil groove, shelf, tubular frame — hung over the
            // tabletop box and the legs, which keep collision and stop rendering. Scaling
            // the tabletop up to fit the mesh instead would turn a desk you can walk under
            // into a solid block, and quietly change what the NavMesh bakes.
            PropLibrary.Overlay(top, "Desk",
                                localCentre: new Vector3(0f, -0.24f / 0.06f, 0f),
                                size: new Vector3(1f, 0.75f / 0.06f, 1f),
                                material: ProtoMaterials.DeskTop,
                                hide: new[] { top, legs[0], legs[1] });

            // A chair, knocked over about half the time.
            var chair = CreateBox($"Chair_{r}_{c}", at + new Vector3(0f, 0.28f, -0.55f),
                new Vector3(0.42f, 0.55f, 0.42f), ProtoMaterials.DeskTop, true);

            if (_rng.NextDouble() < 0.45)
                chair.transform.rotation = Quaternion.Euler(84f, Range(0f, 360f), 0f);

            // Dressed after the knock-over roll, so a chair lying on its side is a chair
            // lying on its side rather than a box wearing a chair.
            PropLibrary.Dress(chair, "Chair");

            // Under a desk is the classic place for a child to be hiding.
            AddHidingSpot(at + new Vector3(0f, 0f, 0.5f), Vector3.back);
        }

        private void BuildCafeteriaTable(int r, int c, Vector3 at)
        {
            CreateBox($"CafeTop_{r}_{c}", at + Vector3.up * 0.72f,
                new Vector3(cellSize * 0.85f, 0.07f, 0.75f), ProtoMaterials.DeskTop, true);

            CreateDecoration($"CafeLeg_{r}_{c}", at + Vector3.up * 0.36f,
                new Vector3(0.1f, 0.72f, 0.5f), ProtoMaterials.Metal);

            for (int s = -1; s <= 1; s += 2)
            {
                CreateBox($"Bench_{r}_{c}_{s}", at + new Vector3(0f, 0.42f, 0.62f * s),
                    new Vector3(cellSize * 0.85f, 0.06f, 0.3f), ProtoMaterials.DeskTop, true);
            }

            AddHidingSpot(at + new Vector3(0f, 0f, 0.9f), Vector3.back);
        }

        private void BuildBookshelf(int r, int c, Vector3 at)
        {
            CreateBox($"Shelf_{r}_{c}", at + Vector3.up * 1f,
                new Vector3(cellSize * 0.85f, 2f, 0.45f), ProtoMaterials.Wood, true);

            for (int i = 0; i < 4; i++)
            {
                CreateDecoration($"Books_{r}_{c}_{i}", at + new Vector3(0f, 0.35f + i * 0.45f, 0.24f),
                    new Vector3(cellSize * 0.78f, 0.3f, 0.06f),
                    i % 2 == 0 ? ProtoMaterials.Backpack : ProtoMaterials.Cardigan);
            }

            AddHidingSpot(at + new Vector3(0f, 0f, 0.7f), Vector3.back);
        }

        private void BuildGymEquipment(int r, int c, Vector3 at)
        {
            // A climbing frame against the wall, a vaulting bench, or a rack of balls.
            int kind = _rng.Next(0, 3);

            if (kind == 0)
            {
                for (int i = 0; i < 5; i++)
                {
                    CreateBox($"Bar_{r}_{c}_{i}", at + new Vector3(0f, 0.5f + i * 0.5f, 0f),
                        new Vector3(cellSize * 0.55f, 0.08f, 0.08f), ProtoMaterials.Wood, true);
                }
            }
            else if (kind == 1)
            {
                CreateBox($"Vault_{r}_{c}", at + Vector3.up * 0.55f,
                    new Vector3(0.6f, 1.1f, 1.3f), ProtoMaterials.BootLeather, true);
                AddHidingSpot(at + new Vector3(0.9f, 0f, 0f), Vector3.right);
            }
            else
            {
                CreateBox($"BallRack_{r}_{c}", at + Vector3.up * 0.45f,
                    new Vector3(cellSize * 0.5f, 0.9f, 0.6f), ProtoMaterials.Metal, true);

                for (int i = 0; i < 4; i++)
                {
                    CreateDecoration($"Ball_{r}_{c}_{i}",
                        at + new Vector3(Range(-0.4f, 0.4f), 1.05f, Range(-0.2f, 0.2f)),
                        Vector3.one * 0.3f, ProtoMaterials.Backpack);
                }
            }
        }

        private void BuildStage(int r, int c, Vector3 at)
        {
            CreateBox($"Stage_{r}_{c}", at + Vector3.up * 0.45f,
                new Vector3(cellSize, 0.9f, cellSize), ProtoMaterials.Wood, true);

            // The curtain at the back of the stage — and behind it is somewhere to hide.
            if (c > 0 && rows[r][c - 1] == '#')
            {
                CreateDecoration($"Curtain_{r}_{c}", at + new Vector3(-cellSize * 0.4f, 1.9f, 0f),
                    new Vector3(0.12f, 2.6f, cellSize), ProtoMaterials.Cardigan);

                AddHidingSpot(at + new Vector3(-cellSize * 0.25f, 0.9f, 0f), Vector3.right);
            }
        }

        /// <summary>A whiteboard on the wall of every classroom that has desks in it.</summary>
        private void BuildWhiteboards()
        {
            for (int r = 1; r < Rows - 1; r++)
            {
                for (int c = 1; c < Columns - 1; c++)
                {
                    if (char.ToUpperInvariant(rows[r][c]) != 'K') continue;
                    if (rows[r - 1][c] != '#') continue;

                    CreateDecoration($"Whiteboard_{r}_{c}",
                        CellToLocal(r - 1, c) + new Vector3(0f, 1.7f, cellSize * 0.5f),
                        new Vector3(cellSize * 0.9f, 1.1f, 0.06f), ProtoMaterials.Whiteboard);
                }
            }
        }

        // ---- lighting ---------------------------------------------------------

        /// <summary>
        /// Fluorescent tubes, and this is the level's whole mood. Nearly half are dead —
        /// grey glass, no light — and nearly half of what remains flickers. A school at
        /// night is frightening because it is a place built to be full and lit, and a
        /// corridor lit in stuttering patches says that better than darkness would.
        /// </summary>
        private void BuildFluorescents()
        {
            int step = Mathf.Max(2, Mathf.RoundToInt(lightSpacingCells));

            for (int r = 1; r < Rows - 1; r += step)
            {
                for (int c = 1; c < Columns - 1; c += step)
                {
                    if (rows[r][c] == '#') continue;

                    Vector3 at = CellToLocal(r, c) + Vector3.up * (wallHeight - 0.28f);
                    bool dead = _rng.NextDouble() < deadTubeShare;

                    CreateDecoration($"Tube_{r}_{c}", at,
                        new Vector3(0.22f, 0.1f, cellSize * 0.9f),
                        dead ? ProtoMaterials.LinoleumAlt : ProtoMaterials.Fluorescent);

                    CreateDecoration($"TubeHousing_{r}_{c}", at + Vector3.up * 0.1f,
                        new Vector3(0.34f, 0.08f, cellSize), ProtoMaterials.Metal);

                    if (dead) continue;

                    var lightObject = new GameObject($"Fluoro_{r}_{c}");
                    lightObject.transform.SetParent(_container, false);
                    lightObject.transform.position = transform.position + at + Vector3.down * 0.15f;

                    var light = lightObject.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = tubeColour;
                    light.intensity = tubeIntensity;
                    light.range = tubeRange;
                    LevelLighting.MakeRoomLight(light);

                    if (_rng.NextDouble() < flickerShare)
                        lightObject.AddComponent<FlickeringLight>();
                }
            }
        }

        // ---- markers -----------------------------------------------------------

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

        /// <summary>Corners of rooms, which the props do not already cover.</summary>
        private void ScanHidingSpots()
        {
            for (int r = 1; r < Rows - 1; r++)
            {
                for (int c = 1; c < Columns - 1; c++)
                {
                    if (rows[r][c] == '#') continue;

                    bool wallNorth = rows[r - 1][c] == '#';
                    bool wallSouth = rows[r + 1][c] == '#';
                    bool wallWest = rows[r][c - 1] == '#';
                    bool wallEast = rows[r][c + 1] == '#';

                    int walls = (wallNorth ? 1 : 0) + (wallSouth ? 1 : 0)
                                + (wallWest ? 1 : 0) + (wallEast ? 1 : 0);
                    if (walls < 2) continue;

                    Vector3 facing = Vector3.zero;
                    if (wallNorth) facing += Vector3.forward;
                    if (wallSouth) facing += Vector3.back;
                    if (wallWest) facing += Vector3.right;
                    if (wallEast) facing += Vector3.left;

                    AddHidingSpot(CellToLocal(r, c), -facing);
                }
            }
        }

        // ---- helpers -----------------------------------------------------------

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
