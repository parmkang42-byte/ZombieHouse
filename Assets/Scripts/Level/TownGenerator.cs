using System.Collections.Generic;
using UnityEngine;

namespace ZombieHouse.Level
{
    /// <summary>
    /// Builds the town: one wide dirt street with false-front buildings down both sides,
    /// boardwalks under the porches, alleys between the blocks, and a ring of mesa walls
    /// holding the whole thing in.
    ///
    /// Where the house is a grid of rooms and the forest is scattered geometry, this is a
    /// street — a long corridor with cover down both edges and gaps you cannot see into.
    /// That shape is the level design: everything worth having is on the street, and
    /// everything that wants you is in an alley off it.
    ///
    /// Same <see cref="ILevelSource"/> contract as the other two, so LevelDirector,
    /// ZombieSpawner and the NavMesh baker work here without knowing where they are.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class TownGenerator : MonoBehaviour, ILevelSource
    {
        [Header("Extent")]
        [SerializeField] private int seed = 20260818;
        [Tooltip("Half-length of the street, in metres. The player crosses twice this.")]
        [SerializeField] private float streetLength = 62f;
        [SerializeField] private float streetWidth = 15f;
        [SerializeField] private float mesaHeight = 16f;

        [Header("Buildings")]
        [Tooltip("Buildings per side. They are laid out end to end with alleys between.")]
        [SerializeField] private int buildingsPerSide = 7;
        [SerializeField] private Vector2 buildingWidth = new Vector2(9f, 15f);
        [SerializeField] private Vector2 buildingDepth = new Vector2(10f, 16f);
        [SerializeField] private Vector2 buildingHeight = new Vector2(4.6f, 7.4f);
        [Tooltip("Gap between neighbours. Wide enough to walk down, dark enough to wait in.")]
        [SerializeField] private Vector2 alleyWidth = new Vector2(2.4f, 4.2f);

        [Header("Windows")]
        [Tooltip("Windows per building front. Half of them get an upper storey.")]
        [SerializeField] private int windowsPerBuilding = 3;
        [Tooltip("Share of windows with a lamp still burning behind them.")]
        [Range(0f, 1f)] [SerializeField] private float litWindowShare = 0.28f;
        [Tooltip("Share of the dark ones that are smashed out rather than intact.")]
        [Range(0f, 1f)] [SerializeField] private float brokenWindowShare = 0.4f;

        [Header("Rolling cacti")]
        [Tooltip("Balls of cactus blowing down the street. Decoration with no colliders, "
                 + "so they never block a shot or carve the NavMesh.")]
        [SerializeField] private int cactusCount = 9;
        [SerializeField] private Vector2 cactusRadius = new Vector2(0.35f, 0.7f);
        [SerializeField] private Vector2 cactusSpeed = new Vector2(3.2f, 6.5f);

        [Header("Street clutter")]
        [SerializeField] private int barrelCount = 34;
        [SerializeField] private int crateCount = 26;
        [SerializeField] private int wagonCount = 6;
        [SerializeField] private int troughCount = 5;

        [Header("Contents")]
        [SerializeField] private int ammoCount = 9;
        [SerializeField] private int medkitCount = 8;
        [SerializeField] private int batteryCount = 5;
        [Tooltip("Hostages tied up around the town. All of them have to be cut loose.")]
        [SerializeField] private int hostageCount = 4;
        [SerializeField] private int markedZombieSpawns = 12;

        [Header("Atmosphere")]
        [Tooltip("Street lamps down the main drag. The only light that is not yours.")]
        [SerializeField] private int lampCount = 8;
        [SerializeField] private Color lampColour = new Color(1f, 0.72f, 0.38f);
        [SerializeField] private float lampIntensity = 1.5f;
        [SerializeField] private float lampRange = 13f;

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
        public List<Pose> HidingSpots { get; } = new List<Pose>();

        /// <summary>Everyone here is tied to a post rather than hiding.</summary>
        public bool SurvivorsAreBound => true;

        public List<Vector3> PowerUpSpawns { get; } = new List<Vector3>();
        public List<Vector3> BeltCrateSpawns { get; } = new List<Vector3>();
        public List<Vector3> PowerCellCandidates { get; } = new List<Vector3>();
        public Vector3 PowerCellSpawn { get; private set; }
        public Vector3 MotorPosition { get; private set; }

        private const string ContainerName = "TownGeometry";
        private Transform _container;
        private System.Random _rng;

        /// <summary>Alley mouths, kept so clutter and hostages can be placed at them.</summary>
        private readonly List<Vector3> _alleyMouths = new List<Vector3>();
        private readonly List<Vector3> _porchPosts = new List<Vector3>();

        private void Awake()
        {
            if (generateOnAwake) Generate();
        }

        [ContextMenu("Generate Town")]
        public void Generate()
        {
            ClearGeometry();
            Reset();

            _rng = new System.Random(seed);
            _container = new GameObject(ContainerName).transform;
            _container.SetParent(transform, false);

            // In at the south end of the street, out through the gate at the north.
            PlayerSpawn = transform.position + new Vector3(0f, 0.2f, -(streetLength - 6f));
            ExitPosition = transform.position + new Vector3(0f, 0f, streetLength - 4f);
            HasExit = true;

            LevelBounds = new Bounds(
                transform.position + Vector3.up * (mesaHeight * 0.5f),
                new Vector3(streetWidth * 6f + 40f, mesaHeight + 24f, streetLength * 2.4f));

            BuildGround();
            BuildMesaWalls();
            BuildStreet();
            BuildBlocks();
            BuildGate();
            BuildClutter();
            BuildLamps();
            BuildCacti();
            ScatterContents();

            // Motor on the gate. The cell is up an alley — never on the street itself,
            // so walking the length of the town is not enough to find it.
            MotorPosition = ExitPosition + new Vector3(4.2f, 0f, -1.5f);
            // Two Uzis down the street, both off the centre line so you have to leave
            // the middle of the road for them.
            PowerUpSpawns.Add(PickOpenSpot());
            PowerUpSpawns.Add(PickOpenSpot());

            // Belt crates down the street, where the fighting is.
            for (int i = 0; i < 4; i++) BeltCrateSpawns.Add(PickOpenSpot());

            BuildPowerCellCandidates();
            PowerCellSpawn = PowerCellPlacement.Draw(PowerCellCandidates, PlayerSpawn,
                                                     transform.position + Vector3.back * (streetLength - 14f));

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
            _alleyMouths.Clear();
            _porchPosts.Clear();
            Generated = false;
        }

        [ContextMenu("Clear Town")]
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

        // ---- ground and bounds ----------------------------------------------

        private void BuildGround()
        {
            CreateBox("Ground", new Vector3(0f, -0.5f, 0f),
                new Vector3(streetWidth * 6f + 44f, 1f, streetLength * 2.5f),
                ProtoMaterials.Dust, true);
        }

        /// <summary>
        /// Mesa walls instead of the forest's cliffs — same job, different rock. They stop
        /// the NavMesh as well as the player, so nothing can path around the town.
        /// </summary>
        private void BuildMesaWalls()
        {
            float halfWidth = streetWidth * 3f + 18f;
            float halfLength = streetLength * 1.15f;

            for (int side = -1; side <= 1; side += 2)
            {
                CreateBox($"MesaSide_{side}", new Vector3(halfWidth * side, mesaHeight * 0.5f, 0f),
                    new Vector3(10f, mesaHeight, halfLength * 2f + 20f), ProtoMaterials.Mesa, true);

                CreateBox($"MesaEnd_{side}", new Vector3(0f, mesaHeight * 0.5f, halfLength * side),
                    new Vector3(halfWidth * 2f + 20f, mesaHeight, 10f), ProtoMaterials.Mesa, true);
            }
        }

        private void BuildStreet()
        {
            // A rutted dirt strip, slightly darker than the ground around it, so the eye
            // follows it to the far end without needing a sign.
            CreateBox("Street", new Vector3(0f, -0.02f, 0f),
                new Vector3(streetWidth, 0.06f, streetLength * 2f), ProtoMaterials.Dust, false);

            for (int i = 0; i < 14; i++)
            {
                float z = Range(-streetLength, streetLength);
                CreateDecoration($"Rut_{i}", new Vector3(Range(-streetWidth * 0.35f, streetWidth * 0.35f), 0.01f, z),
                    new Vector3(Range(0.5f, 1.4f), 0.02f, Range(5f, 14f)), ProtoMaterials.Mesa);
            }
        }

        // ---- the blocks ------------------------------------------------------

        /// <summary>
        /// Both sides of the street, laid out end to end from the south. Every building
        /// gets a false front, a porch with posts and a boardwalk; every gap between two
        /// of them is an alley, and every alley is somewhere to be waiting.
        /// </summary>
        private void BuildBlocks()
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float z = -streetLength + 12f;
                int index = 0;

                while (z < streetLength - 16f && index < buildingsPerSide)
                {
                    float width = Range(buildingWidth.x, buildingWidth.y);
                    float depth = Range(buildingDepth.x, buildingDepth.y);
                    float height = Range(buildingHeight.x, buildingHeight.y);

                    BuildBuilding(side, z + width * 0.5f, width, depth, height, index);

                    float alley = Range(alleyWidth.x, alleyWidth.y);
                    float alleyZ = z + width + alley * 0.5f;

                    if (alleyZ < streetLength - 16f)
                        BuildAlley(side, alleyZ, alley, depth);

                    z += width + alley;
                    index++;
                }
            }
        }

        private void BuildBuilding(float side, float z, float width, float depth, float height, int index)
        {
            float streetEdge = streetWidth * 0.5f + 2.2f;   // boardwalk sits in front of this
            float centreX = (streetEdge + depth * 0.5f) * side;

            // The body of the building — a shell now, not a block. You can go in, and so can
            // everything else, which is the point: the street used to be the whole level and
            // every fight on it had the same shape.
            Material wall = index % 3 == 0 ? ProtoMaterials.Adobe : ProtoMaterials.Plank;
            BuildInterior(side, z, width, depth, height, index, wall);

            // The false front: a flat parapet standing above the roofline, which is the
            // single most recognisable thing about a street like this.
            //
            // It needs the doorway cut through it too, and that is not obvious until you walk
            // into it. This slab stands 0.3 m proud of the building and runs from the ground
            // to above the roof, so a doorway in the wall behind it opens onto half a metre of
            // solid plank — a door you can see through and not walk through.
            float frontX = (streetEdge + 0.3f) * side;
            BuildFrontageWithDoorway($"FalseFront_{side}_{index}", frontX, 0.5f,
                                     z, width + 0.4f, height + 2.2f, ProtoMaterials.PlankPale);

            // Boardwalk: a raised plank walkway under the porch, and cover to crouch behind.
            float walkX = (streetWidth * 0.5f + 1.1f) * side;
            CreateBox($"Boardwalk_{side}_{index}", new Vector3(walkX, 0.14f, z),
                new Vector3(2.2f, 0.28f, width), ProtoMaterials.Plank, true);

            // Porch roof on posts, which is what makes the boardwalk a dark place.
            float roofY = 3.1f;
            CreateBox($"PorchRoof_{side}_{index}", new Vector3(walkX, roofY, z),
                new Vector3(2.6f, 0.18f, width), ProtoMaterials.Plank, true);

            for (int p = -1; p <= 1; p += 2)
            {
                float postZ = z + (width * 0.42f) * p;
                float postX = (streetWidth * 0.5f + 0.2f) * side;

                CreateBox($"PorchPost_{side}_{index}_{p}", new Vector3(postX, roofY * 0.5f, postZ),
                    new Vector3(0.22f, roofY, 0.22f), ProtoMaterials.Plank, true);

                _porchPosts.Add(new Vector3(postX, 0f, postZ));
            }

            // A doorway, and then the glass.
            CreateDecoration($"Door_{side}_{index}", new Vector3(frontX - 0.3f * side, 1.05f, z),
                new Vector3(0.12f, 2.1f, 1.2f), ProtoMaterials.Trim);

            BuildWindows(side, index, z, width, height, frontX);

            // Under the porch, tucked against the front, facing the street.
            AddHidingSpot(new Vector3(walkX, 0f, z + width * 0.3f), Vector3.right * -side);
        }

        /// <summary>
        /// The front elevation's glass. Three windows at street level and, on the taller
        /// buildings, a row above them.
        ///
        /// Some are lit and some are not, and that is the gameplay rather than the
        /// decoration: a burning window throws a real pool of light across the boardwalk
        /// and out into the street, so the town is lit in patches you can plan around
        /// instead of the flat gloom it had before. The dark ones matter too — a black
        /// pane at head height reads as somewhere something could be standing, which is
        /// most of what a street like this is for.
        /// </summary>
        /// <summary>
        /// Four walls, a roof and a doorway, in place of what used to be a solid block.
        ///
        /// The floor is deliberately not built. The desert ground already runs under every
        /// building and is already baked into the NavMesh, so adding a slab on top would put
        /// a 6 cm lip in every doorway for nothing — and a lip is exactly the sort of thing
        /// that stops an agent pathing through and turns a room into a place zombies watch
        /// you from.
        /// </summary>
        private void BuildInterior(float side, float z, float width, float depth, float height,
                                   int index, Material wall)
        {
            const float thickness = 0.35f;

            float streetEdge = streetWidth * 0.5f + 2.2f;
            float innerX = streetEdge + thickness * 0.5f;          // front wall centre
            float backX = streetEdge + depth - thickness * 0.5f;   // back wall centre

            // Front, with the way in.
            BuildFrontageWithDoorway($"Building_{side}_{index}_Front", innerX * side, thickness,
                                     z, width, height, wall);

            CreateBox($"Building_{side}_{index}_Back", new Vector3(backX * side, height * 0.5f, z),
                new Vector3(thickness, height, width), wall, true);

            // The two side walls, running front to back.
            float midX = (streetEdge + depth * 0.5f) * side;

            for (int end = -1; end <= 1; end += 2)
            {
                CreateBox($"Building_{side}_{index}_Side{end}",
                    new Vector3(midX, height * 0.5f, z + (width * 0.5f - thickness * 0.5f) * end),
                    new Vector3(depth, height, thickness), wall, true);
            }

            CreateBox($"Building_{side}_{index}_Roof",
                new Vector3(midX, height - 0.1f, z), new Vector3(depth, 0.2f, width), wall, true);

            FurnishInterior(side, z, width, depth, index, midX);
        }

        /// <summary>
        /// A wall with a hole in it: two piers and a lintel over the top.
        ///
        /// Used for both the building's own front and the false front standing in front of
        /// it, with the same z and the same gap, so the two openings line up into one
        /// doorway. The gap is comfortably wider than a NavMeshAgent's 0.72 m diameter —
        /// a doorway an agent can only just fit through is one it will refuse to path
        /// through as soon as anything else is standing near it.
        /// </summary>
        private void BuildFrontageWithDoorway(string name, float x, float thickness,
                                              float z, float width, float height, Material material)
        {
            const float gap = 1.9f;
            const float openingHeight = 2.6f;

            float pier = (width - gap) * 0.5f;

            if (pier > 0.05f)
            {
                for (int end = -1; end <= 1; end += 2)
                {
                    CreateBox($"{name}_Pier{end}",
                        new Vector3(x, height * 0.5f, z + (gap * 0.5f + pier * 0.5f) * end),
                        new Vector3(thickness, height, pier), material, true);
                }
            }

            // The lintel: everything above the opening, so the wall reads as continuous.
            float lintel = height - openingHeight;
            if (lintel > 0.05f)
            {
                CreateBox($"{name}_Lintel",
                    new Vector3(x, openingHeight + lintel * 0.5f, z),
                    new Vector3(thickness, lintel, gap), material, true);
            }
        }

        /// <summary>
        /// What is waiting inside. Crates to break the sightline, a chair, and somewhere for
        /// something to be standing when you come through the door.
        ///
        /// The spawn marker is the reason this is worth doing at all. An empty room is a
        /// cul-de-sac the player checks once and never enters again; a room that has had
        /// something in it twice is one they clear properly every time.
        /// </summary>
        private void FurnishInterior(float side, float z, float width, float depth,
                                     int index, float midX)
        {
            float streetEdge = streetWidth * 0.5f + 2.2f;
            float backX = (streetEdge + depth * 0.75f) * side;

            // Crates against the back wall, dressed with the real mesh.
            int crates = 1 + (index % 3);
            for (int i = 0; i < crates; i++)
            {
                float size = Range(0.7f, 1.0f);
                Vector3 at = new Vector3(backX + Range(-0.6f, 0.6f), size * 0.5f,
                                         z + Range(-width * 0.3f, width * 0.3f));

                var crate = CreateBox($"ShopCrate_{side}_{index}_{i}", at,
                    new Vector3(size, size, size), ProtoMaterials.Plank, true);
                crate.transform.rotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);
                PropLibrary.Dress(crate, "Crate");
            }

            // A chair, knocked over as often as not.
            var chair = CreateBox($"ShopChair_{side}_{index}",
                new Vector3((streetEdge + depth * 0.45f) * side, 0.28f, z + Range(-1.2f, 1.2f)),
                new Vector3(0.42f, 0.55f, 0.42f), ProtoMaterials.Plank, true);

            if (_rng.NextDouble() < 0.5)
                chair.transform.rotation = Quaternion.Euler(84f, Range(0f, 360f), 0f);
            else
                chair.transform.rotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);

            PropLibrary.Dress(chair, "Chair");

            // Somewhere to be, and somewhere to come from. Both are inside, so the room is a
            // place the level uses rather than a cupboard with scenery in it.
            ZombieSpawns.Add(new Vector3((streetEdge + depth * 0.6f) * side, 0f, z));
            AddHidingSpot(new Vector3((streetEdge + depth * 0.35f) * side, 0f, z + width * 0.25f),
                          Vector3.right * -side);
        }

        private void BuildWindows(float side, float index, float z, float width, float height, float frontX)
        {
            int count = Mathf.Max(1, windowsPerBuilding);
            float paneX = frontX - 0.34f * side;

            for (int i = 0; i < count; i++)
            {
                // Spread across the front, skipping the middle where the door is.
                float t = (i + 0.5f) / count;
                float offset = Mathf.Lerp(-width * 0.36f, width * 0.36f, t);
                if (Mathf.Abs(offset) < 0.9f) offset += width * 0.22f;

                BuildOneWindow(side, $"{index}_{i}", paneX, 2.05f, z + offset, true);

                // An upper storey wherever the building is tall enough to carry one.
                // Upper windows glow but cast no light: they are three storeys above the
                // boardwalk, so a real light there lands on nothing the player walks on
                // and only costs a pixel light. The glass still reads as lit.
                if (height > 6f && i % 2 == 0)
                    BuildOneWindow(side, $"{index}_{i}u", paneX, height - 1.1f, z + offset, false);
            }
        }

        private void BuildOneWindow(float side, string id, float paneX, float y, float z, bool castsLight)
        {
            const float paneHeight = 1.25f;
            const float paneWidth = 1.5f;

            bool lit = _rng.NextDouble() < litWindowShare;
            bool broken = !lit && _rng.NextDouble() < brokenWindowShare;

            // Frame first, a little larger than the glass, so the pane sits in something.
            CreateDecoration($"WindowFrame_{side}_{id}", new Vector3(paneX + 0.03f * side, y, z),
                new Vector3(0.10f, paneHeight + 0.22f, paneWidth + 0.22f), ProtoMaterials.Trim);

            if (broken)
            {
                // Nothing but jagged shards left at the edges: the hole is the point.
                for (int shard = 0; shard < 3; shard++)
                {
                    float sy = y + Mathf.Lerp(-paneHeight * 0.4f, paneHeight * 0.4f, (float)_rng.NextDouble());
                    float sz = z + Mathf.Lerp(-paneWidth * 0.42f, paneWidth * 0.42f, (float)_rng.NextDouble());

                    var piece = CreateDecoration($"WindowShard_{side}_{id}_{shard}",
                        new Vector3(paneX, sy, sz),
                        new Vector3(0.05f, Range(0.12f, 0.34f), Range(0.08f, 0.26f)),
                        ProtoMaterials.WindowDark);
                    piece.transform.rotation = Quaternion.Euler(Range(-25f, 25f), 0f, Range(-25f, 25f));
                }

                // Boards nailed over the worst of it on about half of them.
                if (_rng.NextDouble() < 0.5)
                {
                    for (int board = 0; board < 2; board++)
                    {
                        var plank = CreateDecoration($"WindowBoard_{side}_{id}_{board}",
                            new Vector3(paneX - 0.02f * side, y + (board == 0 ? 0.22f : -0.24f), z),
                            new Vector3(0.06f, 0.22f, paneWidth + 0.3f), ProtoMaterials.Plank);
                        plank.transform.rotation = Quaternion.Euler(Range(-9f, 9f), 0f, 0f);
                    }
                }

                return;
            }

            Material glass = lit ? ProtoMaterials.WindowLit : ProtoMaterials.WindowDark;
            CreateDecoration($"WindowPane_{side}_{id}", new Vector3(paneX, y, z),
                new Vector3(0.06f, paneHeight, paneWidth), glass);

            // Mullions: two bars turn a rectangle into a window.
            CreateDecoration($"WindowBarV_{side}_{id}", new Vector3(paneX - 0.02f * side, y, z),
                new Vector3(0.05f, paneHeight, 0.07f), ProtoMaterials.Trim);
            CreateDecoration($"WindowBarH_{side}_{id}", new Vector3(paneX - 0.02f * side, y, z),
                new Vector3(0.05f, 0.07f, paneWidth), ProtoMaterials.Trim);

            if (!lit || !castsLight) return;

            // The lamp inside, placed just outside the pane so it lights the street
            // rather than the inside of a solid building.
            var lightObject = new GameObject($"WindowLight_{side}_{id}");
            lightObject.transform.SetParent(_container, false);
            lightObject.transform.position = transform.position + new Vector3(paneX - 0.7f * side, y, z);

            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.78f, 0.46f);
            light.intensity = 1.05f;
            light.range = 7.5f;
            LevelLighting.MakeRoomLight(light);
        }

        /// <summary>
        /// Cactus balls on the wind. They are built outside the static geometry container
        /// because they move — marking a moving object static is the quiet way to get
        /// wrong lighting and a warning in the console.
        /// </summary>
        private void BuildCacti()
        {
            var root = new GameObject("RollingCacti").transform;
            root.SetParent(_container, false);

            for (int i = 0; i < cactusCount; i++)
            {
                float radius = Range(cactusRadius.x, cactusRadius.y);
                float z = Range(-streetLength + 8f, streetLength - 10f);
                float x = Range(-streetWidth * 0.35f, streetWidth * 0.35f);

                var ball = new GameObject($"Cactus_{i}");
                ball.transform.SetParent(root, false);
                ball.transform.position = transform.position + new Vector3(x, radius, z);

                // The body: a couple of overlapping lobes rather than one sphere, so it
                // tumbles unevenly and reads as a tangle instead of a beach ball.
                for (int lobe = 0; lobe < 3; lobe++)
                {
                    var offset = new Vector3(Range(-0.22f, 0.22f), Range(-0.22f, 0.22f), Range(-0.22f, 0.22f))
                                 * radius;

                    CreateLoosePart(ball.transform, $"Lobe_{lobe}", PrimitiveType.Sphere, offset,
                        Vector3.one * (radius * Range(1.2f, 1.85f)), ProtoMaterials.Cactus);
                }

                // Spines, sticking out in every direction.
                int spines = 14;
                for (int spine = 0; spine < spines; spine++)
                {
                    float yaw = Range(0f, 360f);
                    float pitch = Range(-80f, 80f);
                    Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

                    var needle = CreateLoosePart(ball.transform, $"Spine_{spine}", PrimitiveType.Cylinder,
                        rotation * Vector3.forward * (radius * 0.95f),
                        new Vector3(0.03f, radius * 0.42f, 0.03f), ProtoMaterials.CactusSpine);

                    needle.transform.localRotation = rotation * Quaternion.Euler(90f, 0f, 0f);
                }

                var roller = ball.AddComponent<RollingCactus>();
                roller.Configure(transform.position + new Vector3(0f, 0f, 0f),
                                 streetLength - 6f, streetWidth * 0.42f, radius,
                                 Range(cactusSpeed.x, cactusSpeed.y),
                                 Range(0f, 12f),
                                 _rng.Next(0, 4) == 0 ? -1f : 1f);
            }
        }

        /// <summary>
        /// A collider-free, **non-static** primitive. Everything else in the town is baked
        /// scenery; these are the only pieces that move.
        /// </summary>
        private GameObject CreateLoosePart(Transform parent, string name, PrimitiveType type,
                                           Vector3 localPosition, Vector3 localScale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;

            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying) Destroy(collider);
                else DestroyImmediate(collider);
            }

            return go;
        }

        /// <summary>
        /// The gap between two buildings. Nothing is built in it — the point is that it is
        /// empty, unlit and just wide enough to hold something you cannot see from the
        /// street. Two waiting positions: one at the mouth, one deep in.
        /// </summary>
        private void BuildAlley(float side, float z, float width, float depth)
        {
            float streetEdge = streetWidth * 0.5f + 2.2f;
            var mouth = new Vector3((streetEdge + 1f) * side, 0f, z);
            _alleyMouths.Add(mouth);

            // A crate or two down the far end, so the alley is not a bare slot.
            var alleyCrate = CreateBox($"AlleyCrate_{side}_{z:0}",
                new Vector3((streetEdge + depth * 0.6f) * side, 0.45f, z),
                new Vector3(0.9f, 0.9f, 0.9f), ProtoMaterials.Plank, true);

            // Dress, not Overlay: the crate is exactly one box and the mesh is the same
            // size, so swapping what it draws cannot change what it collides with.
            PropLibrary.Dress(alleyCrate, "Crate");

            AddHidingSpot(mouth, Vector3.right * -side);
            AddHidingSpot(new Vector3((streetEdge + depth * 0.35f) * side, 0f, z), Vector3.right * -side);
        }

        /// <summary>The way out: a gate through the north mesa with a lit trail beyond it.</summary>
        private void BuildGate()
        {
            Vector3 exitLocal = ExitPosition - transform.position;

            for (int side = -1; side <= 1; side += 2)
            {
                CreateBox($"GatePost_{side}", exitLocal + new Vector3(3.4f * side, 2.6f, 0f),
                    new Vector3(0.7f, 5.2f, 0.7f), ProtoMaterials.Plank, true);
            }

            CreateBox("GateBeam", exitLocal + new Vector3(0f, 5.1f, 0f),
                new Vector3(7.5f, 0.5f, 0.5f), ProtoMaterials.Plank, true);

            // The church at the end of the street, behind the gate: something to walk
            // towards for the whole level.
            CreateBox("Church", exitLocal + new Vector3(0f, 4f, 12f),
                new Vector3(11f, 8f, 14f), ProtoMaterials.PlankPale, true);
            CreateBox("ChurchSpire", exitLocal + new Vector3(0f, 11f, 12f),
                new Vector3(2.4f, 6f, 2.4f), ProtoMaterials.PlankPale, true);
        }

        // ---- clutter ---------------------------------------------------------

        private void BuildClutter()
        {
            for (int i = 0; i < barrelCount; i++)
            {
                Vector3 spot = StreetEdgeSpot();
                var barrel = CreateCylinder($"Barrel_{i}", spot + Vector3.up * 0.55f,
                    new Vector3(0.7f, 0.55f, 0.7f), ProtoMaterials.Plank, true);
                barrel.transform.rotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);

                AddHidingSpot(spot, DirectionToStreet(spot));
            }

            for (int i = 0; i < crateCount; i++)
            {
                Vector3 spot = StreetEdgeSpot();
                float size = Range(0.7f, 1.2f);
                var crate = CreateBox($"Crate_{i}", spot + Vector3.up * (size * 0.5f),
                    new Vector3(size, size, size), ProtoMaterials.Plank, true);
                crate.transform.rotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);
                PropLibrary.Dress(crate, "Crate");
            }

            for (int i = 0; i < wagonCount; i++)
            {
                Vector3 spot = StreetEdgeSpot();
                BuildWagon(i, spot);
                AddHidingSpot(spot + new Vector3(0f, 0f, 1.6f), DirectionToStreet(spot));
            }

            for (int i = 0; i < troughCount; i++)
            {
                Vector3 spot = StreetEdgeSpot();
                CreateBox($"Trough_{i}", spot + Vector3.up * 0.35f,
                    new Vector3(1.1f, 0.7f, 3.2f), ProtoMaterials.Plank, true);

                // A hitching rail beside every trough — and something to tie a person to.
                CreateBox($"HitchRail_{i}", spot + new Vector3(1.4f, 1.0f, 0f),
                    new Vector3(0.14f, 0.14f, 3.4f), ProtoMaterials.Plank, true);

                AddHidingSpot(spot, DirectionToStreet(spot));
            }
        }

        /// <summary>A buckboard: bed, sideboards and four wheels. Solid, so it is real cover.</summary>
        private void BuildWagon(int index, Vector3 spot)
        {
            float yaw = Range(-25f, 25f) + (spot.x > 0f ? 180f : 0f);
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);

            var bed = CreateBox($"WagonBed_{index}", spot + Vector3.up * 1f,
                new Vector3(2.1f, 0.3f, 3.6f), ProtoMaterials.Plank, true);
            bed.transform.rotation = rotation;

            for (int s = -1; s <= 1; s += 2)
            {
                var board = CreateBox($"WagonSide_{index}_{s}",
                    spot + rotation * new Vector3(1f * s, 1.35f, 0f),
                    new Vector3(0.14f, 0.7f, 3.6f), ProtoMaterials.Plank, true);
                board.transform.rotation = rotation;

                for (int w = -1; w <= 1; w += 2)
                {
                    var wheel = CreateCylinder($"WagonWheel_{index}_{s}_{w}",
                        spot + rotation * new Vector3(1.05f * s, 0.62f, 1.3f * w),
                        new Vector3(1.25f, 0.09f, 1.25f), ProtoMaterials.Plank, false);
                    wheel.transform.rotation = rotation * Quaternion.Euler(0f, 0f, 90f);
                }
            }
        }

        private void BuildLamps()
        {
            for (int i = 0; i < lampCount; i++)
            {
                float t = (i + 0.5f) / lampCount;
                float z = Mathf.Lerp(-streetLength + 8f, streetLength - 8f, t);
                float x = (streetWidth * 0.5f - 0.6f) * (i % 2 == 0 ? -1f : 1f);

                CreateBox($"LampPost_{i}", new Vector3(x, 1.6f, z),
                    new Vector3(0.16f, 3.2f, 0.16f), ProtoMaterials.Plank, true);

                CreateDecoration($"LampGlass_{i}", new Vector3(x, 3.35f, z),
                    new Vector3(0.34f, 0.42f, 0.34f), ProtoMaterials.LanternGlass);

                var lightObject = new GameObject($"Lamp_{i}");
                lightObject.transform.SetParent(_container, false);
                lightObject.transform.position = transform.position + new Vector3(x, 3.3f, z);

                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = lampColour;
                light.intensity = lampIntensity;
                light.range = lampRange;
                LevelLighting.MakeRoomLight(light);
            }
        }

        // ---- contents --------------------------------------------------------

        private void ScatterContents()
        {
            for (int i = 0; i < ammoCount; i++) AmmoSpawns.Add(PickOpenSpot());
            for (int i = 0; i < medkitCount; i++) MedkitSpawns.Add(PickOpenSpot());
            for (int i = 0; i < batteryCount; i++) BatterySpawns.Add(PickOpenSpot());
            for (int i = 0; i < markedZombieSpawns; i++) ZombieSpawns.Add(PickOpenSpot());

            // Hostages are tied to the porch posts, spread down the length of the street
            // rather than dropped at random — someone put them there.
            for (int i = 0; i < hostageCount; i++)
            {
                Vector3 post = PickPorchPost(i);
                SurvivorSpawns.Add(transform.position + post + Vector3.right * Mathf.Sign(post.x) * -0.9f);
            }
        }

        /// <summary>
        /// A post from a different part of town each time, so the four hostages are not
        /// all on one block. Falls back to an open spot if the town has no porches.
        /// </summary>
        private Vector3 PickPorchPost(int index)
        {
            if (_porchPosts.Count == 0)
                return PickOpenSpot() - transform.position;

            // Walk the list in even strides, offset by the seed, rather than sampling.
            int stride = Mathf.Max(1, _porchPosts.Count / Mathf.Max(1, hostageCount));
            int at = (index * stride + _rng.Next(0, stride)) % _porchPosts.Count;
            return _porchPosts[at];
        }

        /// <summary>
        /// One candidate at the far end of each alley. An alley is unlit, invisible from
        /// the street, and there are a dozen of them — so the cell is somewhere you have
        /// to leave the road and walk into the dark to find, and it is a different one
        /// every time you press Play.
        /// </summary>
        private void BuildPowerCellCandidates()
        {
            float streetEdge = streetWidth * 0.5f + 2.2f;

            foreach (Vector3 mouth in _alleyMouths)
            {
                float side = Mathf.Sign(mouth.x);
                float depth = streetEdge + Range(6f, 11f);
                Vector3 world = transform.position + new Vector3(depth * side, 0f, mouth.z);

                // The alleys nearest the gate would make the carry trivial; the motor is
                // on the gate, so skip anything within a good walk of it.
                if (Vector3.Distance(world, MotorPosition) < 34f) continue;

                PowerCellCandidates.Add(world);
            }

            if (PowerCellCandidates.Count == 0)
                PowerCellCandidates.Add(transform.position + new Vector3(0f, 0f, -(streetLength - 14f)));
        }

        private Vector3 PickOpenSpot()
        {
            for (int attempt = 0; attempt < 40; attempt++)
            {
                float z = Range(-streetLength + 10f, streetLength - 12f);
                float x = Range(-streetWidth * 0.42f, streetWidth * 0.42f);

                // Keep the start and the gate clear.
                if (Mathf.Abs(z - (PlayerSpawn.z - transform.position.z)) < 9f) continue;
                if (Mathf.Abs(z - (ExitPosition.z - transform.position.z)) < 8f) continue;

                return transform.position + new Vector3(x, 0f, z);
            }

            return transform.position + new Vector3(0f, 0f, Range(-streetLength * 0.4f, streetLength * 0.4f));
        }

        /// <summary>Somewhere along the edge of the street, where the clutter belongs.</summary>
        private Vector3 StreetEdgeSpot()
        {
            float side = _rng.Next(0, 2) == 0 ? -1f : 1f;
            float x = (streetWidth * 0.5f - Range(0.2f, 2.4f)) * side;
            float z = Range(-streetLength + 10f, streetLength - 12f);
            return new Vector3(x, 0f, z);
        }

        private Vector3 DirectionToStreet(Vector3 spot)
        {
            return new Vector3(-Mathf.Sign(spot.x), 0f, 0f);
        }

        private void AddHidingSpot(Vector3 localPosition, Vector3 facing)
        {
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.01f) facing = Vector3.forward;
            facing.Normalize();

            HidingSpots.Add(new Pose(transform.position + localPosition,
                                     Quaternion.LookRotation(facing, Vector3.up)));
        }

        // ---- helpers ---------------------------------------------------------

        private float Range(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

        private GameObject CreateBox(string name, Vector3 localCentre, Vector3 size, Material material, bool solid)
        {
            return CreatePrimitive(PrimitiveType.Cube, name, localCentre, size, material, solid);
        }

        private GameObject CreateCylinder(string name, Vector3 localCentre, Vector3 size, Material material, bool solid)
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
