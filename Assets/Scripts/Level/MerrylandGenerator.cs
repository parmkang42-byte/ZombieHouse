using System.Collections.Generic;
using UnityEngine;
using ZombieHouse.Fx;

namespace ZombieHouse.Level
{
    /// <summary>
    /// MERRYLAND — a regional theme park that closed in 1987 and was never demolished.
    ///
    /// The design problem with an abandoned funfair is that it is very easy to make it
    /// merely ruined, and ruins are not frightening. What unsettles people about a place
    /// like this is that it is *intact*: the paint has gone chalky and the lawns are waist
    /// high, but the teacups are still bolted to their turntable and the castle still says
    /// WELCOME over the gate. Everything here was built to reassure a child, and none of it
    /// has stopped trying.
    ///
    /// So the palette is sun-bleached rather than grimy, nothing is collapsed, and the
    /// lights that still work still work. The horror is carried entirely by the things
    /// walking around in the costumes.
    ///
    /// **Layout** is a hub and spoke, because that is what these parks actually are: you
    /// come in through the turnstiles at the south end, walk the midway north past the
    /// attractions, and the castle at the far end is both the landmark you navigate by and
    /// the way out. Everything is placed off a centre line rather than on a grid, which is
    /// why this is built like the town rather than like the house.
    ///
    /// Two attractions are enterable — the big top and the funhouse — using the same shell
    /// technique the town's shops use: real walls with a doorway wide enough for a
    /// NavMeshAgent, and the ground underneath left alone to serve as the floor.
    /// </summary>
    public class MerrylandGenerator : MonoBehaviour, ILevelSource
    {
        [Header("Scale")]
        [Tooltip("Half-length of the midway, in metres. The walk from the turnstiles to the "
                 + "castle is twice this.")]
        [SerializeField] private float midwayLength = 58f;
        [SerializeField] private float midwayWidth = 14f;
        [SerializeField] private float fenceHeight = 3.6f;

        [Header("Options")]
        [SerializeField] private bool generateOnAwake = true;
        [SerializeField] private int seed = 8571;

        [Header("Lights")]
        [Tooltip("Share of the midway lamps that still have a working bulb. The rest are "
                 + "dead glass, and the dark between the live ones is the level.")]
        [Range(0f, 1f)] [SerializeField] private float workingLampShare = 0.45f;
        [SerializeField] private Color lampColour = new Color(1f, 0.86f, 0.62f);
        [SerializeField] private float lampIntensity = 1.5f;
        [SerializeField] private float lampRange = 12f;

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

        /// <summary>Hiding, not tied up. Nobody tied anyone to a carousel.</summary>
        public bool SurvivorsAreBound => false;

        public List<Vector3> PowerUpSpawns { get; } = new List<Vector3>();
        public List<Vector3> BeltCrateSpawns { get; } = new List<Vector3>();
        public List<Vector3> PowerCellCandidates { get; } = new List<Vector3>();
        public Vector3 PowerCellSpawn { get; private set; }
        public Vector3 MotorPosition { get; private set; }

        private const string ContainerName = "MerrylandGeometry";
        private Transform _container;
        private System.Random _rng;

        /// <summary>Open ground away from the attractions, for scattering things onto.</summary>
        private readonly List<Vector3> _openSpots = new List<Vector3>();

        private void Awake()
        {
            if (generateOnAwake) Generate();
        }

        [ContextMenu("Generate Merryland")]
        public void Generate()
        {
            ClearGeometry();
            Reset();

            _rng = new System.Random(seed);
            _container = new GameObject(ContainerName).transform;
            _container.SetParent(transform, false);

            // In at the turnstiles, out through the castle gate at the far end.
            PlayerSpawn = transform.position + new Vector3(0f, 0.2f, -(midwayLength - 5f));
            ExitPosition = transform.position + new Vector3(0f, 0f, midwayLength - 6f);
            HasExit = true;

            LevelBounds = new Bounds(
                transform.position + Vector3.up * 10f,
                new Vector3(midwayWidth * 7f + 30f, 40f, midwayLength * 2.5f));

            BuildGround();
            BuildFence();
            BuildMidway();
            BuildTurnstiles();
            BuildCarousel(new Vector3(-24f, 0f, -26f));
            BuildBigTop(new Vector3(26f, 0f, -20f));
            BuildTeacups(new Vector3(-26f, 0f, 8f));
            BuildFerrisWheel(new Vector3(28f, 0f, 14f));
            BuildFunhouse(new Vector3(-27f, 0f, 34f));
            BuildStallRow();
            BuildCastle();
            BuildLamps();
            ScatterContents();

            // The motor is on the castle gate; the cell is out in the park, so finishing
            // means walking back through everything you have already woken up.
            MotorPosition = ExitPosition + new Vector3(5.0f, 0f, -1.8f);

            PowerUpSpawns.Add(PickOpenSpot());
            PowerUpSpawns.Add(PickOpenSpot());

            for (int i = 0; i < 4; i++) BeltCrateSpawns.Add(PickOpenSpot());

            BuildPowerCellCandidates();
            PowerCellSpawn = PowerCellPlacement.Draw(
                PowerCellCandidates, PlayerSpawn,
                transform.position + Vector3.back * (midwayLength - 12f));

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
            _openSpots.Clear();
            Generated = false;
        }

        [ContextMenu("Clear Merryland")]
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

        // ---- ground and boundary --------------------------------------------

        private void BuildGround()
        {
            CreateBox("Ground", new Vector3(0f, -0.5f, 0f),
                new Vector3(midwayWidth * 7f + 26f, 1f, midwayLength * 2.4f),
                ProtoMaterials.ParkGrass, true);
        }

        /// <summary>
        /// The perimeter. Solid, because a theme park's fence is the reason the level has
        /// edges — but built as one box per side rather than a ring of railings, since a
        /// hundred separate posts would cost the NavMesh bake a great deal and buy nothing:
        /// the player can only ever see the inside face.
        /// </summary>
        private void BuildFence()
        {
            float halfX = midwayWidth * 3.5f + 12f;
            float halfZ = midwayLength * 1.15f;

            for (int side = -1; side <= 1; side += 2)
            {
                CreateBox($"FenceEast_{side}", new Vector3(halfX * side, fenceHeight * 0.5f, 0f),
                    new Vector3(0.5f, fenceHeight, halfZ * 2f), ProtoMaterials.FenceRail, true);

                CreateBox($"FenceNorth_{side}", new Vector3(0f, fenceHeight * 0.5f, halfZ * side),
                    new Vector3(halfX * 2f, fenceHeight, 0.5f), ProtoMaterials.FenceRail, true);
            }
        }

        /// <summary>The paved strip you walk in on, laid as alternating slabs.</summary>
        private void BuildMidway()
        {
            CreateBox("Midway", new Vector3(0f, -0.02f, 0f),
                new Vector3(midwayWidth, 0.06f, midwayLength * 2f), ProtoMaterials.Midway, false);

            // A scatter of paler slabs, so the strip reads as laid rather than poured.
            for (int i = 0; i < 26; i++)
            {
                CreateDecoration($"Slab_{i}",
                    new Vector3(Range(-midwayWidth * 0.42f, midwayWidth * 0.42f), 0.01f,
                                Range(-midwayLength, midwayLength)),
                    new Vector3(Range(1.4f, 2.6f), 0.02f, Range(1.4f, 2.6f)),
                    ProtoMaterials.MidwayAlt);
            }

            // Bunting between the lamps, most of it down.
            for (int i = 0; i < 18; i++)
            {
                float z = Range(-midwayLength * 0.9f, midwayLength * 0.9f);
                CreateDecoration($"Bunting_{i}",
                    new Vector3(Range(-midwayWidth * 0.5f, midwayWidth * 0.5f), Range(0.02f, 0.06f), z),
                    new Vector3(Range(0.6f, 2.2f), 0.03f, 0.08f), ProtoMaterials.Bunting);
            }
        }

        /// <summary>The way in: a row of turnstiles and a ticket booth you walk past.</summary>
        private void BuildTurnstiles()
        {
            float z = -(midwayLength - 1.5f);

            for (int i = -2; i <= 2; i++)
            {
                // Gaps between them, so the row funnels rather than blocks.
                CreateBox($"Turnstile_{i}", new Vector3(i * 2.6f, 0.5f, z),
                    new Vector3(0.35f, 1.0f, 0.35f), ProtoMaterials.FenceRail, true);
            }

            CreateBox("TicketBooth", new Vector3(-midwayWidth * 0.5f - 2.4f, 1.3f, z - 1f),
                new Vector3(3.0f, 2.6f, 2.6f), ProtoMaterials.StallWood, true);

            CreateDecoration("BoothAwning", new Vector3(-midwayWidth * 0.5f - 2.4f, 2.75f, z - 1f),
                new Vector3(3.6f, 0.16f, 3.2f), ProtoMaterials.StallAwning);

            AddHidingSpot(World(new Vector3(-midwayWidth * 0.5f - 4.2f, 0f, z - 1f)), Vector3.right);
        }

        // ---- the attractions -------------------------------------------------

        /// <summary>
        /// The carousel: a raised deck, a centre column, a canopy, and the poles.
        ///
        /// The horses are deliberately absent and the poles are left standing empty. A
        /// carousel with its horses still on it is a nice piece of scenery; a carousel that
        /// has had its horses taken off it is a question, and the player will answer it
        /// themselves with something worse than anything that could be modelled.
        /// </summary>
        private void BuildCarousel(Vector3 at)
        {
            const float radius = 7.5f;

            CreateBox("CarouselDeck", at + new Vector3(0f, 0.22f, 0f),
                new Vector3(radius * 2f, 0.44f, radius * 2f), ProtoMaterials.CarouselPaint, true);

            CreateBox("CarouselColumn", at + new Vector3(0f, 2.4f, 0f),
                new Vector3(1.5f, 4.8f, 1.5f), ProtoMaterials.CarouselGilt, true);

            CreateDecoration("CarouselCanopy", at + new Vector3(0f, 4.9f, 0f),
                new Vector3(radius * 2.2f, 0.5f, radius * 2.2f), ProtoMaterials.CarouselPaint);

            const int poles = 12;
            for (int i = 0; i < poles; i++)
            {
                float angle = i * (360f / poles);
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * (Vector3.forward * (radius * 0.72f));

                CreateDecoration($"CarouselPole_{i}", at + offset + new Vector3(0f, 2.6f, 0f),
                    new Vector3(0.11f, 4.4f, 0.11f), ProtoMaterials.CarouselGilt);
            }

            ZombieSpawns.Add(World(at + new Vector3(radius * 0.5f, 0.5f, 0f)));
            AddHidingSpot(World(at + new Vector3(0f, 0f, -radius - 1.2f)), Vector3.back);
            AddHidingSpot(World(at + new Vector3(radius + 1.2f, 0f, 0f)), Vector3.right);
            _openSpots.Add(World(at + new Vector3(-radius - 3f, 0f, 4f)));
        }

        /// <summary>
        /// The big top — enterable, and the darkest place in the park.
        ///
        /// Built as a shell on the same terms as the town's shops: real walls, a doorway
        /// comfortably wider than a NavMeshAgent, and no floor of its own so the ground
        /// underneath stays the walkable surface. A slab here would put a lip in the
        /// doorway, and a lip is what stops an agent pathing through it.
        /// </summary>
        private void BuildBigTop(Vector3 at)
        {
            const float radius = 11f;
            const float wall = 0.4f;
            const float height = 7.5f;

            // Twelve wall segments around a circle, with one left out for the way in.
            const int segments = 12;

            // Segment 9 is the one facing -X, which is the side the midway is on. The first
            // version used segment 6 and put the door round the back of the tent.
            const int doorway = 9;

            // Panel width is the CHORD between neighbours, not a fraction of the radius.
            //
            // This is what sealed the tent. At radius 11 the twelve centres are 5.69 m apart,
            // and a panel of radius*0.58 is 6.38 m wide — so every panel overlapped its
            // neighbours by 0.7 m, and removing one left an opening of exactly nothing: the
            // two survivors either side simply met in the middle. The tent looked like it had
            // a door from outside and was welded shut.
            float chord = 2f * radius * Mathf.Sin(Mathf.PI / segments);
            float panelWidth = chord * 1.02f;   // a whisker of overlap, so there are no seams

            for (int i = 0; i < segments; i++)
            {
                if (i == doorway) continue;

                float angle = i * (360f / segments);
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * (Vector3.forward * radius);

                var panel = CreateBox($"BigTopWall_{i}", at + offset + new Vector3(0f, height * 0.4f, 0f),
                    new Vector3(panelWidth, height * 0.8f, wall),
                    i % 2 == 0 ? ProtoMaterials.TentCanvas : ProtoMaterials.TentStripe, true);

                panel.transform.rotation = Quaternion.Euler(0f, angle, 0f);
            }

            // The cone, and the pole through it.
            CreateDecoration("BigTopRoof", at + new Vector3(0f, height + 1.6f, 0f),
                new Vector3(radius * 2.1f, 3.2f, radius * 2.1f), ProtoMaterials.TentStripe);

            CreateDecoration("BigTopPole", at + new Vector3(0f, height * 0.5f + 2f, 0f),
                new Vector3(0.35f, height + 4f, 0.35f), ProtoMaterials.StallWood);

            // Tiered seating round the inside, which is cover and a place to be surprised from.
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f + 22f;

                // Nothing within sixty degrees of the doorway. A five-metre bench parked
                // across the inside of the entrance seals the tent just as effectively as a
                // wall does, and is considerably harder to spot.
                float doorAngle = doorway * (360f / segments);
                if (Mathf.Abs(Mathf.DeltaAngle(angle, doorAngle)) < 60f) continue;

                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * (Vector3.forward * (radius * 0.74f));

                var bench = CreateBox($"BigTopBench_{i}", at + offset + new Vector3(0f, 0.4f, 0f),
                    new Vector3(radius * 0.5f, 0.8f, 1.1f), ProtoMaterials.StallWood, true);
                bench.transform.rotation = Quaternion.Euler(0f, angle, 0f);
            }

            ZombieSpawns.Add(World(at + new Vector3(0f, 0f, -radius * 0.4f)));
            ZombieSpawns.Add(World(at + new Vector3(radius * 0.35f, 0f, radius * 0.3f)));
            AddHidingSpot(World(at + new Vector3(0f, 0f, radius * 0.45f)), Vector3.back);
            MedkitSpawns.Add(World(at + new Vector3(-1.5f, 0.4f, 0f)));
        }

        /// <summary>The teacup ride: a turntable of chipped cups you can walk between.</summary>
        private void BuildTeacups(Vector3 at)
        {
            const float radius = 6.5f;

            CreateBox("TeacupDeck", at + new Vector3(0f, 0.18f, 0f),
                new Vector3(radius * 2f, 0.36f, radius * 2f), ProtoMaterials.MidwayAlt, true);

            const int cups = 6;
            for (int i = 0; i < cups; i++)
            {
                float angle = i * (360f / cups);
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * (Vector3.forward * (radius * 0.6f));

                CreateBox($"Teacup_{i}", at + offset + new Vector3(0f, 0.9f, 0f),
                    new Vector3(2.1f, 1.1f, 2.1f), ProtoMaterials.Teacup, true);
            }

            ZombieSpawns.Add(World(at + new Vector3(0f, 0.4f, 0f)));
            AddHidingSpot(World(at + new Vector3(-radius - 1.4f, 0f, 0f)), Vector3.left);
            _openSpots.Add(World(at + new Vector3(0f, 0f, -radius - 4f)));
        }

        /// <summary>
        /// The wheel. Purely a landmark: its base is solid and the wheel itself is out of
        /// reach, so it costs the NavMesh nothing and gives the player something to
        /// navigate the whole park by.
        /// </summary>
        private void BuildFerrisWheel(Vector3 at)
        {
            CreateBox("FerrisBase", at + new Vector3(0f, 1.2f, 0f),
                new Vector3(7f, 2.4f, 5f), ProtoMaterials.FerrisSteel, true);

            for (int side = -1; side <= 1; side += 2)
            {
                var leg = CreateDecoration($"FerrisLeg_{side}", at + new Vector3(2.6f * side, 8f, 0f),
                    new Vector3(0.6f, 14f, 0.6f), ProtoMaterials.FerrisSteel);
                leg.transform.rotation = Quaternion.Euler(0f, 0f, 11f * side);
            }

            // The wheel: sixteen spokes and a gondola on every other one.
            const int spokes = 16;
            for (int i = 0; i < spokes; i++)
            {
                float angle = i * (360f / spokes);
                Vector3 offset = Quaternion.Euler(angle, 0f, 0f) * (Vector3.up * 9f);

                var spoke = CreateDecoration($"FerrisSpoke_{i}", at + new Vector3(0f, 15f, 0f) + offset * 0.5f,
                    new Vector3(0.22f, 9f, 0.22f), ProtoMaterials.FerrisSteel);
                spoke.transform.rotation = Quaternion.Euler(angle, 0f, 0f);

                if (i % 2 != 0) continue;

                CreateDecoration($"FerrisCar_{i}", at + new Vector3(0f, 15f, 0f) + offset,
                    new Vector3(1.8f, 1.6f, 1.6f),
                    i % 4 == 0 ? ProtoMaterials.TentStripe : ProtoMaterials.CarouselPaint);
            }

            _openSpots.Add(World(at + new Vector3(-6f, 0f, 0f)));
            AddHidingSpot(World(at + new Vector3(0f, 0f, -4.2f)), Vector3.back);
        }

        /// <summary>
        /// The funhouse — the second enterable building, and a dead end on purpose.
        ///
        /// A room with one way in and out is a trap the player walks into knowingly, which
        /// is a different and better feeling than being ambushed: they can see the doorway
        /// behind them the whole time, and the level has taught them by now that something
        /// may well be standing in it when they turn round.
        /// </summary>
        private void BuildFunhouse(Vector3 at)
        {
            const float wide = 13f;
            const float deep = 10f;
            const float height = 5.2f;
            const float wall = 0.4f;
            const float gap = 2.2f;

            // Front wall, facing the midway (+x), with a doorway in it.
            float pier = (wide - gap) * 0.5f;
            for (int end = -1; end <= 1; end += 2)
            {
                CreateBox($"FunhouseFront_{end}",
                    at + new Vector3(deep * 0.5f, height * 0.5f, (gap * 0.5f + pier * 0.5f) * end),
                    new Vector3(wall, height, pier), ProtoMaterials.CarouselPaint, true);
            }

            CreateBox("FunhouseLintel",
                at + new Vector3(deep * 0.5f, 3.0f + (height - 3.0f) * 0.5f, 0f),
                new Vector3(wall, height - 3.0f, gap), ProtoMaterials.CarouselPaint, true);

            CreateBox("FunhouseBack", at + new Vector3(-deep * 0.5f, height * 0.5f, 0f),
                new Vector3(wall, height, wide), ProtoMaterials.CarouselPaint, true);

            for (int end = -1; end <= 1; end += 2)
            {
                CreateBox($"FunhouseSide_{end}", at + new Vector3(0f, height * 0.5f, wide * 0.5f * end),
                    new Vector3(deep, height, wall), ProtoMaterials.CarouselPaint, true);
            }

            CreateBox("FunhouseRoof", at + new Vector3(0f, height - 0.1f, 0f),
                new Vector3(deep, 0.2f, wide), ProtoMaterials.CastleRoof, true);

            // A grinning face over the entrance, which is what these always had.
            CreateDecoration("FunhouseFace", at + new Vector3(deep * 0.5f + 0.3f, 3.9f, 0f),
                new Vector3(0.2f, 2.0f, 4.0f), ProtoMaterials.MascotGrin);

            // Mirrors inside: thin slabs that break the room into blind corners.
            for (int i = 0; i < 5; i++)
            {
                var mirror = CreateBox($"FunhouseMirror_{i}",
                    at + new Vector3(Range(-deep * 0.3f, deep * 0.3f), 1.4f,
                                     Range(-wide * 0.32f, wide * 0.32f)),
                    new Vector3(0.14f, 2.8f, Range(2.0f, 3.4f)), ProtoMaterials.Glass, true);
                mirror.transform.rotation = Quaternion.Euler(0f, Range(0f, 180f), 0f);
            }

            ZombieSpawns.Add(World(at + new Vector3(-deep * 0.25f, 0f, 0f)));
            ZombieSpawns.Add(World(at + new Vector3(0f, 0f, wide * 0.3f)));
            AddHidingSpot(World(at + new Vector3(-deep * 0.3f, 0f, -wide * 0.3f)), Vector3.right);
            BatterySpawns.Add(World(at + new Vector3(-deep * 0.3f, 0.4f, wide * 0.35f)));
        }

        /// <summary>A row of games booths down one side of the midway.</summary>
        private void BuildStallRow()
        {
            float x = midwayWidth * 0.5f + 3.4f;

            for (int i = 0; i < 6; i++)
            {
                float z = -midwayLength * 0.55f + i * 9f;

                CreateBox($"Stall_{i}", new Vector3(x, 1.2f, z),
                    new Vector3(3.2f, 2.4f, 4.0f), ProtoMaterials.StallWood, true);

                CreateDecoration($"StallAwning_{i}", new Vector3(x - 1.9f, 2.5f, z),
                    new Vector3(2.0f, 0.14f, 4.4f), ProtoMaterials.StallAwning);

                CreateDecoration($"StallCounter_{i}", new Vector3(x - 1.7f, 1.0f, z),
                    new Vector3(0.5f, 0.14f, 3.8f), ProtoMaterials.StallWood);

                // Prizes still on the shelf, which is the detail that dates the place.
                for (int p = 0; p < 3; p++)
                {
                    CreateDecoration($"Prize_{i}_{p}",
                        new Vector3(x + 0.4f, 1.9f, z - 1.4f + p * 1.4f),
                        new Vector3(0.34f, 0.42f, 0.30f),
                        p % 2 == 0 ? ProtoMaterials.MascotFur : ProtoMaterials.MascotFurAlt);
                }

                AddHidingSpot(World(new Vector3(x + 2.4f, 0f, z)), Vector3.right);
                if (i % 2 == 0) ZombieSpawns.Add(World(new Vector3(x + 2.2f, 0f, z + 2f)));
                _openSpots.Add(World(new Vector3(x - 3.2f, 0f, z)));
            }
        }

        /// <summary>
        /// The castle at the head of the midway, and the gate you leave through.
        ///
        /// Painted plywood over a frame, which is what these were — the fairy-tale castle at
        /// the end of a park like this is a facade about a foot thick. Keeping it obviously
        /// fake is the right call: a real castle would be a different level, and the whole
        /// point of Merryland is that everything in it is a cheap imitation of something
        /// wonderful.
        /// </summary>
        private void BuildCastle()
        {
            float z = midwayLength - 2f;
            const float wide = 30f;
            const float height = 12f;
            const float gap = 4.5f;

            float pier = (wide - gap) * 0.5f;
            for (int end = -1; end <= 1; end += 2)
            {
                CreateBox($"CastleWall_{end}",
                    new Vector3((gap * 0.5f + pier * 0.5f) * end, height * 0.4f, z),
                    new Vector3(pier, height * 0.8f, 1.2f), ProtoMaterials.CastleStone, true);
            }

            CreateBox("CastleArch", new Vector3(0f, 5.6f + 1.6f, z),
                new Vector3(gap, 3.2f, 1.2f), ProtoMaterials.CastleStone, true);

            // Turrets: four towers with cones, tallest in the middle.
            for (int i = -2; i <= 2; i++)
            {
                if (i == 0) continue;

                float tx = i * 8.5f;
                float th = 14f - Mathf.Abs(i) * 2.5f;

                CreateBox($"Turret_{i}", new Vector3(tx, th * 0.5f, z),
                    new Vector3(3.4f, th, 3.4f), ProtoMaterials.CastleStone, true);

                CreateDecoration($"TurretCone_{i}", new Vector3(tx, th + 1.6f, z),
                    new Vector3(4.0f, 3.2f, 4.0f), ProtoMaterials.CastleRoof);
            }

            CreateBox("CastleKeep", new Vector3(0f, 11f, z + 2.5f),
                new Vector3(11f, 8f, 4f), ProtoMaterials.CastleStone, true);

            CreateDecoration("KeepCone", new Vector3(0f, 16.4f, z + 2.5f),
                new Vector3(12f, 4.4f, 5f), ProtoMaterials.CastleRoof);

            // The sign over the gate, still welcoming people in.
            CreateDecoration("WelcomeSign", new Vector3(0f, 9.4f, z - 0.8f),
                new Vector3(9f, 1.6f, 0.3f), ProtoMaterials.Bunting);

            SurvivorSpawns.Add(World(new Vector3(-11f, 0f, z - 4f)));
            AddHidingSpot(World(new Vector3(-6.5f, 0f, z - 2.5f)), Vector3.back);
            AddHidingSpot(World(new Vector3(6.5f, 0f, z - 2.5f)), Vector3.back);
        }

        /// <summary>Lamp posts down the midway, most of them dead.</summary>
        private void BuildLamps()
        {
            int count = Mathf.Max(4, Mathf.RoundToInt(midwayLength * 2f / 11f));

            for (int i = 0; i < count; i++)
            {
                float z = -midwayLength + 6f + i * (midwayLength * 2f - 12f) / Mathf.Max(1, count - 1);
                float x = (midwayWidth * 0.5f + 1.2f) * (i % 2 == 0 ? 1f : -1f);

                CreateDecoration($"LampPost_{i}", new Vector3(x, 2.1f, z),
                    new Vector3(0.18f, 4.2f, 0.18f), ProtoMaterials.FenceRail);

                bool lit = _rng.NextDouble() < workingLampShare;

                CreateDecoration($"LampGlobe_{i}", new Vector3(x, 4.3f, z),
                    new Vector3(0.5f, 0.5f, 0.5f),
                    lit ? ProtoMaterials.LanternGlass : ProtoMaterials.WindowDark);

                if (!lit) continue;

                var lightObject = new GameObject($"LampLight_{i}");
                lightObject.transform.SetParent(_container, false);
                lightObject.transform.position = transform.position + new Vector3(x, 4.3f, z);

                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = lampColour;
                light.intensity = lampIntensity;
                light.range = lampRange;
                light.shadows = LightShadows.None;
            }
        }

        // ---- contents --------------------------------------------------------

        /// <summary>
        /// Spawn markers, pickups and survivors scattered over the open ground.
        ///
        /// The attractions have already added their own markers as they were built, which is
        /// deliberate: a zombie standing in the big top belongs to the big top, and deriving
        /// it from the geometry means moving the tent moves its occupants with it.
        /// </summary>
        private void ScatterContents()
        {
            for (int i = 0; i < 14; i++) ZombieSpawns.Add(PickOpenSpot());

            for (int i = 0; i < 8; i++) AmmoSpawns.Add(PickOpenSpot() + Vector3.up * 0.4f);
            for (int i = 0; i < 5; i++) MedkitSpawns.Add(PickOpenSpot() + Vector3.up * 0.4f);
            for (int i = 0; i < 4; i++) BatterySpawns.Add(PickOpenSpot() + Vector3.up * 0.4f);

            // Three more survivors out in the park; the fourth is at the castle.
            SurvivorSpawns.Add(World(new Vector3(-midwayWidth * 0.5f - 6f, 0f, -midwayLength * 0.4f)));
            SurvivorSpawns.Add(World(new Vector3(midwayWidth * 0.5f + 7f, 0f, midwayLength * 0.35f)));
            SurvivorSpawns.Add(World(new Vector3(-8f, 0f, midwayLength * 0.1f)));

            for (int i = 0; i < 8; i++)
            {
                Vector3 spot = PickOpenSpot();
                AddHidingSpot(spot, DirectionToMidway(spot));
            }
        }

        /// <summary>
        /// Where the power cell might be. Never on the midway itself, so walking the length
        /// of the park is not enough to find it — you have to go into the attractions.
        /// </summary>
        private void BuildPowerCellCandidates()
        {
            PowerCellCandidates.Add(World(new Vector3(-24f, 0.5f, -26f)));   // the carousel deck
            PowerCellCandidates.Add(World(new Vector3(26f, 0.4f, -22f)));    // inside the big top
            PowerCellCandidates.Add(World(new Vector3(-26f, 0.4f, 8f)));     // the teacups
            PowerCellCandidates.Add(World(new Vector3(22f, 0.4f, 14f)));     // by the wheel
            PowerCellCandidates.Add(World(new Vector3(-24f, 0.4f, 34f)));    // inside the funhouse
            PowerCellCandidates.Add(World(new Vector3(midwayWidth * 0.5f + 6f, 0.4f, -8f)));   // behind a stall
            PowerCellCandidates.Add(World(new Vector3(-midwayWidth * 0.5f - 5f, 0.4f, 44f)));  // castle lawn
        }

        // ---- helpers ---------------------------------------------------------

        /// <summary>
        /// A point on open ground, away from the midway's centre line.
        ///
        /// Biased off the middle on purpose: things dropped on the centre of the promenade
        /// can be collected by walking in a straight line, and a level whose contents can all
        /// be had without leaving the path is a corridor with scenery either side of it.
        /// </summary>
        private Vector3 PickOpenSpot()
        {
            // Returns WORLD, unlike everything else in this file — _openSpots is filled
            // through World() and the fallback adds transform.position explicitly.
            if (_openSpots.Count > 0 && _rng.NextDouble() < 0.35)
            {
                int index = _rng.Next(_openSpots.Count);
                Vector3 taken = _openSpots[index];
                _openSpots.RemoveAt(index);
                return taken + new Vector3(Range(-1.5f, 1.5f), 0f, Range(-1.5f, 1.5f));
            }

            float side = _rng.NextDouble() < 0.5 ? -1f : 1f;
            float x = side * Range(midwayWidth * 0.55f, midwayWidth * 2.4f);
            float z = Range(-midwayLength * 0.92f, midwayLength * 0.88f);

            return transform.position + new Vector3(x, 0f, z);
        }

        /// <summary>Which way something at this spot should be facing to watch the path.</summary>
        private Vector3 DirectionToMidway(Vector3 spot)
        {
            Vector3 toPath = new Vector3(transform.position.x - spot.x, 0f, 0f);
            return toPath.sqrMagnitude < 0.01f ? Vector3.forward : toPath.normalized;
        }

        /// <summary>
        /// Local offset to world position.
        ///
        /// Every builder in this file works in local coordinates, because CreatePrimitive
        /// adds transform.position itself. Everything the rest of the game reads — spawn
        /// markers, hiding spots, power-cell candidates — has to be world. Mixing them is
        /// invisible while the generator sits at the origin and takes the level apart the
        /// moment anyone moves it, so the conversion is spelled out at every call site
        /// rather than left implicit.
        /// </summary>
        private Vector3 World(Vector3 local) => transform.position + local;

        /// <summary>Takes a WORLD position, like everything else on HidingSpots.</summary>
        private void AddHidingSpot(Vector3 world, Vector3 facing)
        {
            HidingSpots.Add(new Pose(world, Quaternion.LookRotation(
                facing.sqrMagnitude < 0.01f ? Vector3.forward : facing, Vector3.up)));
        }

        private float Range(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

        private GameObject CreateBox(string name, Vector3 localCentre, Vector3 size,
                                     Material material, bool solid)
        {
            return CreatePrimitive(PrimitiveType.Cube, name, localCentre, size, material, solid);
        }

        /// <summary>A collider-free box: drawn, but invisible to physics and the NavMesh.</summary>
        private GameObject CreateDecoration(string name, Vector3 localCentre, Vector3 size,
                                            Material material)
        {
            return CreatePrimitive(PrimitiveType.Cube, name, localCentre, size, material, false);
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
