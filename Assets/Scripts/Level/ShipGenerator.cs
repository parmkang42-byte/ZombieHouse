using System.Collections.Generic;
using UnityEngine;

namespace ZombieHouse.Level
{
    /// <summary>One deck's plan. Every row must be the same length.</summary>
    [System.Serializable]
    public class DeckPlan
    {
        public string name = "Deck";

        [Tooltip("# bulkhead  . deck  space = over the side  P start  Z sailor  A ammo  "
                 + "M medkit  V battery  R survivor  W power cell  G motor  E exit  U Uzi  "
                 + "B belt crate  K container  L gull perch  "
                 + "> < ^ v companionway up (arrow points uphill)")]
        public string[] rows;
    }

    /// <summary>
    /// THE CORMORANT — a freighter dead in the water, built from stacked deck plans.
    ///
    /// Four decks, and the level runs *vertically* rather than along, which is the point:
    /// every other level in this game is a walk from one end of something to the other, and
    /// this one is a climb. You start in the hold, go DOWN into the engine room for the power
    /// cell, then all the way up through the accommodation to the boat deck. The route
    /// crosses itself twice, so the way back is through rooms you have already emptied — and
    /// on a ship you cannot go round, only through.
    ///
    /// **The plan format is the house's**, deliberately. Stacked ASCII floors joined by
    /// companionways written as arrows, with the deck above automatically losing its plating
    /// over a stair run so the hatch is open. That algorithm is the most thoroughly proven
    /// thing in this project — it is what level 1 has always been — and a ship is a house
    /// with worse stairs.
    ///
    /// It is NOT a copy of HouseGenerator. A ship has no free-standing furniture, no interior
    /// doors worth the trouble, and one hull outline shared by every deck, so most of what
    /// that file does is not wanted here. What is shared is the idea, not the code.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class ShipGenerator : MonoBehaviour, ILevelSource
    {
        [Header("Scale")]
        [SerializeField] private float cellSize = 2.2f;
        [Tooltip("Deckhead height. 2.6 m is realistic for a freighter and is also what "
                 + "makes the companionways climbable — see BuildStairs.")]
        [SerializeField] private float deckHeight = 2.6f;
        [SerializeField] private float plateThickness = 0.3f;

        [Header("Options")]
        [SerializeField] private bool generateOnAwake = true;
        [SerializeField] private int seed = 4417;

        [Header("Lights")]
        [Tooltip("Share of the deckhead lamps still burning. Below decks this is the only "
                 + "light there is, so it is the single biggest knob on how the level feels.")]
        [Range(0f, 1f)] [SerializeField] private float workingLampShare = 0.34f;
        [SerializeField] private Color lampColour = new Color(1f, 0.79f, 0.55f);

        [Header("Decks — bottom first")]
        [SerializeField]
        private DeckPlan[] decks =
        {
            // Companionways are STAGGERED across the beam: deck 0 climbs at column 4, deck 1
            // at column 8, deck 2 back at column 4. Stacking them would put each hatch
            // directly under the next staircase, and the landing you step onto at the top
            // would have no plating under it.
            //
            // Each run is four cells, which BuildStairs turns into twelve steps of 0.29 m.
            // The baker's agentClimb is 0.45, so anything shorter simply does not connect —
            // a one-cell run gave 0.875 m steps and left all four decks as separate islands.
            new DeckPlan
            {
                name = "Engine room",
                rows = new[]
                {
                    "      #      ",
                    "    #####    ",
                    "   #.....#   ",
                    "  #..K.K..#  ",
                    " #....W....# ",
                    " #.K.....K.# ",
                    " #.........# ",
                    " #..K...K..# ",
                    " #....Z....# ",
                    " #.K.....K.# ",
                    " #.........# ",
                    " #.K.Z.K...# ",
                    " #.........# ",
                    " #..K...K..# ",
                    " #..^......# ",
                    " #..^......# ",
                    " #..^......# ",
                    " #..^......# ",
                    "  #...W...#  ",
                    "  #########  ",
                    "             ",
                }
            },
            new DeckPlan
            {
                // Rows 14-17 at column 4 are plain deck: the engine room's companionway
                // comes up through them.
                name = "Hold",
                rows = new[]
                {
                    "      #      ",
                    "    #####    ",
                    "   #..A..#   ",
                    "  #.KK.KK.#  ",
                    " #....Z....# ",
                    " #.KK...KK.# ",
                    " #.........# ",
                    " #.K.KKK.K.# ",
                    " #....P....# ",
                    " #......^..# ",
                    " #......^..# ",
                    " #......^..# ",
                    " #......^..# ",
                    " #.KK...KK.# ",
                    " #.........# ",
                    " #.K.....K.# ",
                    " #......B..# ",
                    " #.........# ",
                    "  #.M.U.Z.#  ",
                    "  #########  ",
                    "             ",
                }
            },
            new DeckPlan
            {
                // Rows 9-12 at column 8 are plain deck: the hold's companionway comes up
                // through them.
                name = "Accommodation",
                rows = new[]
                {
                    "      #      ",
                    "    #####    ",
                    "   #.....#   ",
                    "  #.#...#.#  ",
                    " #..^....R.# ",
                    " #Z.^......# ",
                    " #..^......# ",
                    " #..^....Z.# ",
                    " #....V....# ",
                    " #.........# ",
                    " #...M.....# ",
                    " #.........# ",
                    " #.......Z.# ",
                    " #....A....# ",
                    " #..#####..# ",
                    " #Z.#.R.#..# ",
                    " #..#...#..# ",
                    " #..#...#.Z# ",
                    "  #.......#  ",
                    "  #########  ",
                    "             ",
                }
            },
            new DeckPlan
            {
                // Rows 4-7 at column 4 are plain deck: the accommodation's companionway
                // comes up through them.
                name = "Weather deck",
                rows = new[]
                {
                    "      #      ",
                    "    #####    ",
                    "   #..L..#   ",
                    "  #.......#  ",
                    " #.......L.# ",
                    " #....E....# ",
                    " #.L.......# ",
                    " #.........# ",
                    " #..K...K..# ",
                    " #....G....# ",
                    " #.L.....L.# ",
                    " #.........# ",
                    " #..K...K..# ",
                    " #....Z....# ",
                    " #.L.....L.# ",
                    " #.........# ",
                    " #..K...K..# ",
                    " #....B....# ",
                    "  #.L.U.L.#  ",
                    "  #########  ",
                    "             ",
                }
            },
        };

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

        /// <summary>Crew hiding in cabins, not tied up.</summary>
        public bool SurvivorsAreBound => false;

        public List<Vector3> PowerUpSpawns { get; } = new List<Vector3>();
        public List<Vector3> BeltCrateSpawns { get; } = new List<Vector3>();
        public List<Vector3> PowerCellCandidates { get; } = new List<Vector3>();
        public Vector3 PowerCellSpawn { get; private set; }
        public Vector3 MotorPosition { get; private set; }

        /// <summary>Where the gulls sit when they are not at you. Read by the spawner.</summary>
        public List<Vector3> PerchPositions { get; } = new List<Vector3>();

        /// <summary>One companionway, as the verify needs to see it.</summary>
        public struct Companionway
        {
            public int FromDeck;
            public Vector3 Foot;      // the bottom step, on the lower deck
            public Vector3 Landing;   // the cell you step onto at the top
        }

        /// <summary>
        /// Every companionway, with both ends.
        ///
        /// "Deck 2 is severed" says a flight failed but not which end. There are only three
        /// possibilities — the foot cannot be reached, the flight does not bake, or the
        /// landing is blocked — and they need different fixes. The generator knows all three
        /// positions, so it may as well hand them over.
        /// </summary>
        public List<Companionway> Companionways { get; } = new List<Companionway>();

        private const string ContainerName = "ShipGeometry";
        private Transform _container;
        private System.Random _rng;

        /// <summary>Cells that lose their plating because a companionway comes up through them.</summary>
        private List<HashSet<long>> _holes;

        public int DeckCount => decks != null ? decks.Length : 0;
        public float DeckSpacing => deckHeight + plateThickness;

        private int Rows => DeckCount == 0 || decks[0].rows == null ? 0 : decks[0].rows.Length;
        private int Columns => Rows == 0 ? 0 : decks[0].rows[0].Length;

        private void Awake()
        {
            if (generateOnAwake) Generate();
        }

        [ContextMenu("Generate Ship")]
        public void Generate()
        {
            ClearGeometry();
            Reset();

            if (!ValidatePlans()) return;

            _rng = new System.Random(seed);
            _container = new GameObject(ContainerName).transform;
            _container.SetParent(transform, false);

            float beam = Columns * cellSize;
            float length = Rows * cellSize;

            LevelBounds = new Bounds(
                transform.position + Vector3.up * (DeckCount * DeckSpacing * 0.5f),
                new Vector3(beam + 20f, DeckCount * DeckSpacing + 18f, length + 20f));

            CutCompanionways();

            for (int d = 0; d < DeckCount; d++)
            {
                BuildDeckPlating(d);
                BuildBulkheads(d);
                BuildContents(d);
                BuildStairs(d);
                BuildLamps(d);
            }

            BuildSea();

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
            HidingSpots.Clear();
            PowerCellCandidates.Clear();
            PowerUpSpawns.Clear();
            BeltCrateSpawns.Clear();
            PerchPositions.Clear();
            Companionways.Clear();
            HasExit = false;
            Generated = false;
        }

        [ContextMenu("Clear Ship")]
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

        /// <summary>
        /// Every deck must be the same size, or the whole coordinate system is a lie.
        ///
        /// Checked rather than assumed because a ragged plan does not throw — it silently
        /// builds a ship whose upper decks are offset from the lower ones, and a companionway
        /// that used to line up now comes out inside a bulkhead.
        /// </summary>
        private bool ValidatePlans()
        {
            if (DeckCount == 0 || Rows == 0 || Columns == 0)
            {
                Debug.LogError("[Ship] No deck plans.");
                return false;
            }

            for (int d = 0; d < DeckCount; d++)
            {
                if (decks[d].rows == null || decks[d].rows.Length != Rows)
                {
                    Debug.LogError($"[Ship] Deck {d} ({decks[d].name}) has " +
                                   $"{decks[d].rows?.Length ?? 0} rows against deck 0's {Rows}.");
                    return false;
                }

                for (int r = 0; r < Rows; r++)
                {
                    if (decks[d].rows[r].Length == Columns) continue;

                    Debug.LogError($"[Ship] Deck {d} ({decks[d].name}) row {r} is " +
                                   $"{decks[d].rows[r].Length} characters against {Columns}.");
                    return false;
                }
            }

            return true;
        }

        // ---- the plan ---------------------------------------------------------

        private char At(int deck, int row, int column)
        {
            if (deck < 0 || deck >= DeckCount) return ' ';
            if (row < 0 || row >= Rows || column < 0 || column >= Columns) return ' ';
            return decks[deck].rows[row][column];
        }

        private static bool IsOutside(char c) => c == ' ';
        private static bool IsBulkhead(char c) => c == '#';
        private static bool IsStair(char c) => c == '>' || c == '<' || c == '^' || c == 'v';

        private static void StairDirection(char c, out int rowStep, out int columnStep)
        {
            rowStep = c == '^' ? -1 : c == 'v' ? 1 : 0;
            columnStep = c == '<' ? -1 : c == '>' ? 1 : 0;
        }

        private static long CellKey(int row, int column) => ((long)row << 32) ^ (uint)column;

        private Vector3 CellToLocal(float row, float column)
        {
            return new Vector3((column - (Columns - 1) * 0.5f) * cellSize, 0f,
                               ((Rows - 1) * 0.5f - row) * cellSize);
        }

        private float DeckBaseY(int deck) => deck * DeckSpacing;

        // ---- companionways ----------------------------------------------------

        /// <summary>
        /// Works out which cells lose their plating, so a companionway comes up through an
        /// open hatch rather than into the underside of the deck above.
        ///
        /// Derived from the stair run itself rather than marked separately in the plan. Two
        /// things that have to stay in sync is one thing too many — move a companionway and
        /// its hatch moves with it.
        /// </summary>
        private void CutCompanionways()
        {
            _holes = new List<HashSet<long>>(DeckCount);
            for (int d = 0; d < DeckCount; d++) _holes.Add(new HashSet<long>());

            for (int d = 0; d < DeckCount - 1; d++)
            {
                foreach (StairRun run in FindRuns(d))
                {
                    for (int i = 0; i < run.Length; i++)
                    {
                        int row = run.StartRow + run.RowStep * i;
                        int column = run.StartColumn + run.ColumnStep * i;
                        _holes[d + 1].Add(CellKey(row, column));
                    }
                }
            }
        }

        private struct StairRun
        {
            public int StartRow, StartColumn, RowStep, ColumnStep, Length;
        }

        /// <summary>Contiguous runs of the same stair character on one deck.</summary>
        private List<StairRun> FindRuns(int deck)
        {
            var runs = new List<StairRun>();
            var seen = new HashSet<long>();

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    char here = At(deck, r, c);
                    if (!IsStair(here) || seen.Contains(CellKey(r, c))) continue;

                    StairDirection(here, out int rowStep, out int columnStep);

                    // Walk back to the true start, so a run is found once however it is hit.
                    int startRow = r, startColumn = c;
                    while (At(deck, startRow - rowStep, startColumn - columnStep) == here)
                    {
                        startRow -= rowStep;
                        startColumn -= columnStep;
                    }

                    int length = 0;
                    int row = startRow, column = startColumn;
                    while (At(deck, row, column) == here)
                    {
                        seen.Add(CellKey(row, column));
                        length++;
                        row += rowStep;
                        column += columnStep;
                    }

                    runs.Add(new StairRun
                    {
                        StartRow = startRow, StartColumn = startColumn,
                        RowStep = rowStep, ColumnStep = columnStep, Length = length,
                    });
                }
            }

            return runs;
        }

        // ---- geometry ---------------------------------------------------------

        private void BuildDeckPlating(int deck)
        {
            float y = DeckBaseY(deck);

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    if (IsOutside(At(deck, r, c))) continue;
                    if (_holes[deck].Contains(CellKey(r, c))) continue;

                    Vector3 at = CellToLocal(r, c) + Vector3.up * (y - plateThickness * 0.5f);

                    CreateBox($"Plate_{deck}_{r}_{c}", at,
                        new Vector3(cellSize, plateThickness, cellSize),
                        (r + c) % 2 == 0 ? ProtoMaterials.DeckPlate : ProtoMaterials.DeckPlateAlt,
                        true);
                }
            }
        }

        private void BuildBulkheads(int deck)
        {
            float y = DeckBaseY(deck);

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    if (!IsBulkhead(At(deck, r, c))) continue;

                    // A bulkhead with nothing walkable beside it is out over the sea and
                    // would only cost the NavMesh bake time to consider.
                    if (!TouchesDeck(deck, r, c)) continue;

                    Vector3 at = CellToLocal(r, c) + Vector3.up * (y + deckHeight * 0.5f);

                    CreateBox($"Bulkhead_{deck}_{r}_{c}", at,
                        new Vector3(cellSize, deckHeight, cellSize),
                        ProtoMaterials.HullPlate, true);
                }
            }
        }

        private bool TouchesDeck(int deck, int r, int c)
        {
            for (int dr = -1; dr <= 1; dr++)
            {
                for (int dc = -1; dc <= 1; dc++)
                {
                    if (dr == 0 && dc == 0) continue;

                    char neighbour = At(deck, r + dr, c + dc);
                    if (!IsOutside(neighbour) && !IsBulkhead(neighbour)) return true;
                }
            }

            return false;
        }

        /// <summary>
        /// A companionway: solid treads climbing to the deck above.
        ///
        /// Each step is solid all the way down to the deck it starts from, exactly as the
        /// house's stairs are — a flight of floating treads is prettier and produces a
        /// NavMesh full of holes.
        /// </summary>
        private void BuildStairs(int deck)
        {
            if (deck >= DeckCount - 1) return;

            float baseY = DeckBaseY(deck);

            foreach (StairRun run in FindRuns(deck))
            {
                float runLength = run.Length * cellSize;

                // Step count is derived from the AGENT, not from the run length, and both
                // limits matter:
                //
                //   rise  <= 0.40   against the baker's agentClimb of 0.45. Taller and the
                //                   NavMesh simply will not link one step to the next.
                //   tread >= 1.00   against the agent's 0.8 m diameter. This is the one that
                //                   caught us: twelve steps over 8.8 m gave a 0.73 m tread,
                //                   erosion took 0.4 off each side, and nothing was left to
                //                   stand on. Every step baked as an island — the flight was
                //                   navigable end to end and connected to nothing, which
                //                   looks exactly like a working staircase.
                const float MaxRise = 0.40f;
                const float MinTread = 1.0f;

                int steps = Mathf.Max(2, Mathf.CeilToInt(DeckSpacing / MaxRise));
                float rise = DeckSpacing / steps;
                float tread = runLength / steps;

                if (tread < MinTread)
                {
                    Debug.LogWarning($"[Ship] A companionway on deck {deck} has a {tread:0.00} m " +
                                     $"tread over {run.Length} cells. Below {MinTread:0.0} m an " +
                                     "agent cannot stand on the step and the flight will not " +
                                     "connect — lengthen the run in the plan.");
                }

                Vector3 bottom = CellToLocal(run.StartRow, run.StartColumn);
                Vector3 direction = new Vector3(run.ColumnStep, 0f, -run.RowStep);
                Vector3 start = bottom - direction * (cellSize * 0.5f);

                bool alongX = run.ColumnStep != 0;
                float width = cellSize * 0.86f;

                // The landing is the cell one step beyond the top of the run, on the deck
                // above — that is the plating you actually step onto, and it is a different
                // cell from the last stair cell.
                int topRow = run.StartRow + run.RowStep * (run.Length - 1);
                int topColumn = run.StartColumn + run.ColumnStep * (run.Length - 1);

                Companionways.Add(new Companionway
                {
                    FromDeck = deck,
                    Foot = transform.position + bottom + Vector3.up * baseY,
                    Landing = transform.position
                            + CellToLocal(topRow + run.RowStep, topColumn + run.ColumnStep)
                            + Vector3.up * DeckBaseY(deck + 1),
                });

                for (int i = 0; i < steps; i++)
                {
                    float top = (i + 1) * rise;
                    Vector3 centre = start + direction * (tread * (i + 0.5f));

                    Vector3 size = alongX
                        ? new Vector3(tread, top, width)
                        : new Vector3(width, top, tread);

                    CreateBox($"Companionway_{deck}_{run.StartRow}_{run.StartColumn}_{i}",
                        new Vector3(centre.x, baseY + top * 0.5f, centre.z),
                        size, ProtoMaterials.Grating, true);
                }
            }
        }

        // ---- contents ---------------------------------------------------------

        private void BuildContents(int deck)
        {
            float y = DeckBaseY(deck);

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    char here = At(deck, r, c);
                    Vector3 at = CellToLocal(r, c) + Vector3.up * y;
                    Vector3 world = transform.position + at;

                    switch (here)
                    {
                        case 'P':
                            PlayerSpawn = world + Vector3.up * 0.2f;
                            break;

                        case 'E':
                            ExitPosition = world;
                            HasExit = true;
                            BuildDavit(at);
                            break;

                        case 'G':
                            MotorPosition = world;
                            break;

                        case 'Z': ZombieSpawns.Add(world); break;
                        case 'A': AmmoSpawns.Add(world + Vector3.up * 0.4f); break;
                        case 'M': MedkitSpawns.Add(world + Vector3.up * 0.4f); break;
                        case 'V': BatterySpawns.Add(world + Vector3.up * 0.4f); break;
                        case 'R': SurvivorSpawns.Add(world); break;
                        case 'W': PowerCellCandidates.Add(world + Vector3.up * 0.4f); break;
                        case 'U': PowerUpSpawns.Add(world + Vector3.up * 0.4f); break;
                        case 'B': BeltCrateSpawns.Add(world + Vector3.up * 0.4f); break;

                        case 'K':
                            BuildContainer(at, deck, r, c);
                            HidingSpots.Add(new Pose(world + Vector3.forward * cellSize,
                                                     Quaternion.LookRotation(Vector3.back, Vector3.up)));
                            break;

                        case 'L':
                            // A gull perch: a rail stanchion they sit on. The position is
                            // stored a little above the rail, which is where the bird is.
                            BuildRailPost(at);
                            PerchPositions.Add(world + Vector3.up * 1.3f);
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// A shipping container. Solid, and the reason the hold is a maze rather than a room.
        /// </summary>
        private void BuildContainer(Vector3 at, int deck, int r, int c)
        {
            float height = deckHeight * 0.72f;

            var box = CreateBox($"Container_{deck}_{r}_{c}",
                at + Vector3.up * (height * 0.5f),
                new Vector3(cellSize * 0.94f, height, cellSize * 0.94f),
                (r * 7 + c * 3) % 3 == 0 ? ProtoMaterials.ContainerRust
                                         : ProtoMaterials.ContainerPaint, true);

            box.transform.rotation = Quaternion.Euler(0f, Range(-2f, 2f), 0f);

            // A rib down each face, which is what makes a box read as a container.
            for (int side = -1; side <= 1; side += 2)
            {
                CreateDecoration($"ContainerRib_{deck}_{r}_{c}_{side}",
                    at + new Vector3(cellSize * 0.48f * side, height * 0.5f, 0f),
                    new Vector3(0.06f, height * 0.9f, cellSize * 0.9f),
                    ProtoMaterials.ContainerRust);
            }
        }

        private void BuildRailPost(Vector3 at)
        {
            CreateDecoration($"RailPost_{at.x:0.0}_{at.z:0.0}",
                at + Vector3.up * 0.6f, new Vector3(0.09f, 1.2f, 0.09f), ProtoMaterials.HullPlate);

            CreateDecoration($"RailTop_{at.x:0.0}_{at.z:0.0}",
                at + Vector3.up * 1.2f, new Vector3(cellSize, 0.07f, 0.07f), ProtoMaterials.HullPlate);
        }

        /// <summary>The lifeboat davit: the way off, and the only thing pointing outward.</summary>
        private void BuildDavit(Vector3 at)
        {
            CreateDecoration("DavitArm", at + Vector3.up * 2.6f,
                new Vector3(0.22f, 0.22f, cellSize * 2.2f), ProtoMaterials.HullPlate);

            for (int side = -1; side <= 1; side += 2)
            {
                CreateDecoration($"DavitPost_{side}", at + new Vector3(cellSize * 0.5f * side, 1.3f, 0f),
                    new Vector3(0.2f, 2.6f, 0.2f), ProtoMaterials.HullPlate);
            }

            CreateDecoration("Lifeboat", at + new Vector3(0f, 1.5f, cellSize * 1.1f),
                new Vector3(1.6f, 1.1f, 3.6f), ProtoMaterials.ContainerPaint);
        }

        /// <summary>
        /// Deckhead lamps, most of them dead.
        ///
        /// Only below decks. The weather deck has the sky, such as it is, and hanging lamps
        /// over an open deck would read as a car park rather than as a ship at night.
        /// </summary>
        private void BuildLamps(int deck)
        {
            if (deck == DeckCount - 1) return;

            float y = DeckBaseY(deck) + deckHeight - 0.35f;

            for (int r = 2; r < Rows - 2; r += 4)
            {
                for (int c = 2; c < Columns - 2; c += 4)
                {
                    char here = At(deck, r, c);
                    if (IsOutside(here) || IsBulkhead(here)) continue;

                    Vector3 at = CellToLocal(r, c) + Vector3.up * y;
                    bool lit = _rng.NextDouble() < workingLampShare;

                    CreateDecoration($"Deckhead_{deck}_{r}_{c}", at,
                        new Vector3(0.34f, 0.12f, 0.34f),
                        lit ? ProtoMaterials.LanternGlass : ProtoMaterials.WindowDark);

                    if (!lit) continue;

                    var lightObject = new GameObject($"DeckLight_{deck}_{r}_{c}");
                    lightObject.transform.SetParent(_container, false);
                    lightObject.transform.position = transform.position + at - Vector3.up * 0.2f;

                    var light = lightObject.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = lampColour;
                    light.intensity = 1.35f;
                    light.range = 8.5f;
                    light.shadows = LightShadows.None;
                }
            }
        }

        /// <summary>
        /// The sea: one enormous plate below the keel, with no collider.
        ///
        /// Collider-free on purpose. It exists so the player can see water past the rail
        /// rather than the skybox, and a solid sea would bake into the NavMesh as an
        /// infinite walkable plain around the ship — every "is the level connected" check
        /// would pass trivially and mean nothing.
        /// </summary>
        private void BuildSea()
        {
            CreateDecoration("Sea", new Vector3(0f, -2.5f, 0f),
                new Vector3(400f, 0.5f, 400f), ProtoMaterials.Water);
        }

        // ---- helpers ----------------------------------------------------------

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

        private GameObject CreateDecoration(string name, Vector3 localCentre, Vector3 size,
                                            Material material)
        {
            return CreateBox(name, localCentre, size, material, false);
        }
    }
}
