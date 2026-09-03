using UnityEngine;
using ZombieHouse.Core;
using ZombieHouse.Enemies;
using ZombieHouse.Items;

namespace ZombieHouse.Level
{
    /// <summary>
    /// Reads the markers the <see cref="HouseGenerator"/> produced and wires the level:
    /// puts the player at 'P', hands the spawner the 'Z' points, drops pickups at 'A'/'M'/'V',
    /// builds the exit at 'E', and sizes the NavMesh bake volume to the house.
    ///
    /// Runs after the generator (-100) and before the NavMesh bake (-50).
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public class LevelDirector : MonoBehaviour
    {
        [Header("Scene references")]
        [Tooltip("The house or the forest — anything implementing ILevelSource.")]
        [SerializeField] private MonoBehaviour levelSourceBehaviour;

        private ILevelSource house;
        [SerializeField] private ZombieSpawner spawner;
        [SerializeField] private RuntimeNavMeshBaker navMeshBaker;
        [SerializeField] private Transform player;

        [Header("Zombie")]
        [SerializeField] private GameObject zombiePrefab;

        [Header("Boss")]
        [Tooltip("The giant that guards the way out. Left empty, the level simply has no "
                 + "boss and the exit opens on the usual three conditions.")]
        [SerializeField] private GameObject bossPrefab;

        [Tooltip("Which archetype it wears. The scale, health and reach all come from "
                 + "there, so one ordinary prefab serves as its own boss.")]
        [SerializeField] private ZombieKind bossKind = ZombieKind.BossZombie;

        [Tooltip("How far from the exit it waits. Far enough that it is not standing in "
                 + "the doorway, close enough that reaching the exit means meeting it.")]
        [SerializeField] private float bossStandoff = 7f;

        [Header("Pickup amounts")]
        [SerializeField] private int ammoPerBox = 24;
        [SerializeField] private int healthPerMedkit = 40;
        [SerializeField] private int cellsPerBattery = 1;

        [Header("Exit")]
        [SerializeField] private float exitRadius = 1.6f;

        /// <summary>Built-in "Ignore Raycast" layer: keeps triggers out of the NavMesh bake and out of bullet paths.</summary>
        private const int TriggerLayer = 2;

        private void Awake()
        {
            house = levelSourceBehaviour as ILevelSource;

            if (house == null)
            {
                // Whichever generator is in the scene.
                foreach (MonoBehaviour candidate in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                {
                    if (candidate is ILevelSource source) { house = source; break; }
                }
            }

            if (spawner == null) spawner = FindAnyObjectByType<ZombieSpawner>();
            if (navMeshBaker == null) navMeshBaker = FindAnyObjectByType<RuntimeNavMeshBaker>();

            if (player == null)
            {
                var playerObject = GameObject.FindGameObjectWithTag("Player");
                if (playerObject != null) player = playerObject.transform;
            }

            if (house == null)
            {
                Debug.LogError("[LevelDirector] No level generator in the scene.");
                return;
            }

            if (!house.Generated) house.Generate();

            PlacePlayer();
            SizeNavMeshVolume();
            PlacePickups();
            PlaceBoss();
            PlaceSurvivors();
            PlacePowerCellAndMotor();
            PlacePowerUps();
            PlaceBeltCrates();
            BuildExit();
            ConfigureSpawner();
        }

        private void PlacePlayer()
        {
            if (player == null) return;

            // CharacterController fights direct transform writes; disable it for the teleport.
            var controller = player.GetComponent<CharacterController>();
            bool wasEnabled = controller != null && controller.enabled;
            if (controller != null) controller.enabled = false;

            player.position = house.PlayerSpawn;

            if (controller != null) controller.enabled = wasEnabled;
        }

        private void SizeNavMeshVolume()
        {
            if (navMeshBaker == null) return;
            Bounds bounds = house.LevelBounds;
            navMeshBaker.SetBakeVolume(bounds.center, bounds.size);
        }

        /// <summary>
        /// Puts the level's boss near the exit, asleep.
        ///
        /// Near rather than *in* the doorway on purpose: a boss blocking the exit collider
        /// would be a wall, and the fight wants a room. It stays dormant until the level's
        /// other conditions are met, so walking past it on the way to the power cell does
        /// nothing at all — which is the whole effect. You will have seen it.
        /// </summary>
        private void PlaceBoss()
        {
            if (bossPrefab == null || house == null || !house.HasExit) return;

            Vector3 exit = house.ExitPosition;
            Vector3 towardsLevel = (house.PlayerSpawn - exit);
            towardsLevel.y = 0f;

            Vector3 wanted = exit + (towardsLevel.sqrMagnitude > 0.01f
                ? towardsLevel.normalized * bossStandoff
                : Vector3.forward * bossStandoff);

            // It has to be somewhere it can path from, or it wakes and stands still.
            UnityEngine.AI.NavMeshHit hit;
            if (!UnityEngine.AI.NavMesh.SamplePosition(wanted, out hit, 12f,
                                                       UnityEngine.AI.NavMesh.AllAreas))
            {
                Debug.LogWarning("[LevelDirector] No navigable ground near the exit for the boss.");
                return;
            }

            ZombieProfile.NextKindOverride = bossKind;

            var boss = Instantiate(bossPrefab, hit.position,
                                   Quaternion.LookRotation(exit - hit.position, Vector3.up));
            boss.name = "Boss_" + bossKind;
            boss.AddComponent<LevelBoss>();
        }

        private void PlacePickups()
        {
            var root = new GameObject("Pickups").transform;
            root.SetParent(transform, false);

            foreach (Vector3 position in house.AmmoSpawns)
                CreatePickup(root, position, PickupKind.Ammo, ammoPerBox);

            foreach (Vector3 position in house.MedkitSpawns)
                CreatePickup(root, position, PickupKind.Health, healthPerMedkit);


            foreach (Vector3 position in house.BatterySpawns)
                CreatePickup(root, position, PickupKind.Battery, cellsPerBattery);
        }

        /// <summary>
        /// Stands a survivor at each marked spot, turned to face the middle of the level
        /// so you meet them face on rather than walking up behind someone's back.
        /// </summary>
        private void PlaceSurvivors()
        {
            if (house.SurvivorSpawns.Count == 0) return;

            var root = new GameObject("Survivors").transform;
            root.SetParent(transform, false);

            Vector3 centre = house.LevelBounds.center;

            foreach (Vector3 position in house.SurvivorSpawns)
            {
                GameObject survivor = SurvivorFactory.Create("Survivor", house.SurvivorsAreBound);
                survivor.transform.SetParent(root, false);
                survivor.transform.position = position;

                Vector3 outward = position - centre;
                outward.y = 0f;
                if (outward.sqrMagnitude > 0.01f)
                    survivor.transform.rotation = Quaternion.LookRotation(-outward.normalized, Vector3.up);
            }
        }

        /// <summary>
        /// The two Uzis. Built here at runtime like everything else the director places,
        /// and deliberately given a wide trigger: a power-up you walk past because you
        /// clipped the corner of it is a bad power-up.
        /// </summary>
        private void PlacePowerUps()
        {
            if (house.PowerUpSpawns.Count == 0) return;

            var root = new GameObject("PowerUps").transform;
            root.SetParent(transform, false);

            foreach (Vector3 position in house.PowerUpSpawns)
            {
                var crate = new GameObject("UziPickup");
                crate.transform.SetParent(root, false);
                crate.transform.position = position + Vector3.up * 0.75f;

                // A rough silhouette of the weapon, spinning: receiver, magazine, stock.
                CreateShape(crate.transform, "Receiver", PrimitiveType.Cube, Vector3.zero,
                            new Vector3(0.34f, 0.13f, 0.11f), ProtoMaterials.GunBlack);
                CreateShape(crate.transform, "Magazine", PrimitiveType.Cube, new Vector3(0f, -0.14f, 0f),
                            new Vector3(0.07f, 0.20f, 0.09f), ProtoMaterials.GunEdge);
                CreateShape(crate.transform, "Barrel", PrimitiveType.Cube, new Vector3(0.22f, 0.02f, 0f),
                            new Vector3(0.16f, 0.05f, 0.05f), ProtoMaterials.GunEdge);
                CreateShape(crate.transform, "Stock", PrimitiveType.Cube, new Vector3(-0.24f, 0.02f, 0f),
                            new Vector3(0.16f, 0.04f, 0.04f), ProtoMaterials.GunEdge);

                // A halo, so a black weapon on a dark floor is still findable. This one
                // is allowed to be a beacon — unlike the power cell, finding it is not
                // meant to be the challenge.
                var glowObject = new GameObject("PickupGlow");
                glowObject.transform.SetParent(crate.transform, false);

                var glow = glowObject.AddComponent<Light>();
                glow.type = LightType.Point;
                glow.color = new Color(0.65f, 0.85f, 1f);
                glow.range = 7f;
                glow.intensity = 1.4f;
                glow.shadows = LightShadows.None;

                var trigger = crate.AddComponent<SphereCollider>();
                trigger.isTrigger = true;
                trigger.radius = 1.9f;

                SetLayerRecursively(crate, TriggerLayer);
                crate.AddComponent<WeaponPickup>();
            }
        }

        /// <summary>
        /// Belt boxes for the gatling gun. Deliberately drab — an olive ammunition crate
        /// with a coil of link on top — because unlike the weapon itself these are not
        /// meant to shine across a room. You find them because you are fighting where
        /// they are, not because they called to you.
        /// </summary>
        private void PlaceBeltCrates()
        {
            if (house.BeltCrateSpawns.Count == 0) return;

            var root = new GameObject("BeltCrates").transform;
            root.SetParent(transform, false);

            foreach (Vector3 position in house.BeltCrateSpawns)
            {
                var crate = new GameObject("BeltCrate");
                crate.transform.SetParent(root, false);
                crate.transform.position = position + Vector3.up * 0.28f;

                CreateShape(crate.transform, "Box", PrimitiveType.Cube, Vector3.zero,
                            new Vector3(0.52f, 0.34f, 0.36f), ProtoMaterials.BeltCrate);

                CreateShape(crate.transform, "Lid", PrimitiveType.Cube, new Vector3(0f, 0.19f, 0f),
                            new Vector3(0.55f, 0.05f, 0.39f), ProtoMaterials.GunEdge);

                // A coil of link over the edge, which is what says "belt" rather than "box".
                for (int i = 0; i < 5; i++)
                {
                    CreateShape(crate.transform, "Link_" + i,
                                new Vector3(0.20f, 0.14f - i * 0.055f, -0.10f + i * 0.03f),
                                new Vector3(0.10f, 0.04f, 0.05f), ProtoMaterials.GunEdge);
                }

                CreateShape(crate.transform, "Stencil", PrimitiveType.Cube, new Vector3(0f, 0.02f, 0.19f),
                            new Vector3(0.30f, 0.10f, 0.02f), ProtoMaterials.CellStripe);

                var trigger = crate.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = new Vector3(2.6f, 2.4f, 2.6f);

                SetLayerRecursively(crate, TriggerLayer);
                crate.AddComponent<BeltCrate>();
            }
        }

        /// <summary>
        /// The power cell where the level says, and the motor where the level says — which
        /// is always beside the way out, and always a long way from the cell.
        ///
        /// Both are built here at runtime rather than saved into the scene, the same as
        /// the pickups and the survivors, so nothing here has to survive a scene save.
        /// </summary>
        private void PlacePowerCellAndMotor()
        {
            var root = new GameObject("Power").transform;
            root.SetParent(transform, false);

            BuildPowerCell(root, house.PowerCellSpawn + Vector3.up * 0.35f);
            BuildMotor(root, house.MotorPosition);
        }

        /// <summary>
        /// A crate-sized battery: dark case, a lit hazard stripe so it is findable in a
        /// black wood, and no collider — you pick it up by pressing Space next to it, and
        /// a collider would only get in the way of walking up to it.
        /// </summary>
        private void BuildPowerCell(Transform parent, Vector3 position)
        {
            var cell = new GameObject("PowerCell");
            cell.transform.SetParent(parent, false);
            cell.transform.position = position;

            CreateShape(cell.transform, "Case", PrimitiveType.Cube, Vector3.zero,
                        new Vector3(0.62f, 0.46f, 0.42f), ProtoMaterials.CellCase);

            CreateShape(cell.transform, "Stripe", PrimitiveType.Cube, new Vector3(0f, 0.12f, 0f),
                        new Vector3(0.64f, 0.08f, 0.44f), ProtoMaterials.CellStripe);

            CreateShape(cell.transform, "TerminalL", PrimitiveType.Cylinder, new Vector3(-0.16f, 0.26f, 0f),
                        new Vector3(0.10f, 0.05f, 0.10f), ProtoMaterials.Metal);
            CreateShape(cell.transform, "TerminalR", PrimitiveType.Cylinder, new Vector3(0.16f, 0.26f, 0f),
                        new Vector3(0.10f, 0.05f, 0.10f), ProtoMaterials.Metal);

            CreateShape(cell.transform, "Handle", PrimitiveType.Cube, new Vector3(0f, 0.29f, 0f),
                        new Vector3(0.30f, 0.04f, 0.05f), ProtoMaterials.Metal);

            // A charge lamp rather than a beacon. It used to throw light 6.5 m and give
            // the cell away from across a room; now it barely reaches past the crate, so
            // it confirms what you are looking at once your torch is on it and does
            // nothing to help you from the doorway. Finding it is the search, not this.
            var glowObject = new GameObject("CellGlow");
            glowObject.transform.SetParent(cell.transform, false);

            var glow = glowObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.72f, 0.22f);
            glow.range = 2.4f;
            glow.intensity = 0.5f;
            glow.shadows = LightShadows.None;

            cell.AddComponent<PowerCell>();
        }

        /// <summary>The winch motor: a housing, a drum that turns once it is live, and a lamp.</summary>
        private void BuildMotor(Transform parent, Vector3 position)
        {
            var motor = new GameObject("DoorMotor");
            motor.transform.SetParent(parent, false);
            motor.transform.position = position;

            CreateShape(motor.transform, "Housing", PrimitiveType.Cube, new Vector3(0f, 0.55f, 0f),
                        new Vector3(0.9f, 1.1f, 0.7f), ProtoMaterials.MotorHousing);

            CreateShape(motor.transform, "Plinth", PrimitiveType.Cube, new Vector3(0f, 0.08f, 0f),
                        new Vector3(1.1f, 0.16f, 0.9f), ProtoMaterials.MotorHousing);

            var drum = CreateShape(motor.transform, "Drum", PrimitiveType.Cylinder,
                                   new Vector3(0f, 0.95f, 0.42f),
                                   new Vector3(0.5f, 0.12f, 0.5f), ProtoMaterials.CellStripe);
            drum.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            CreateShape(motor.transform, "Socket", PrimitiveType.Cube, new Vector3(0f, 0.55f, -0.38f),
                        new Vector3(0.66f, 0.5f, 0.12f), ProtoMaterials.Metal);

            var lampObject = new GameObject("MotorLamp");
            lampObject.transform.SetParent(motor.transform, false);
            lampObject.transform.localPosition = new Vector3(0f, 1.25f, 0f);

            var lamp = lampObject.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.shadows = LightShadows.None;

            motor.AddComponent<DoorMotor>();
        }

        /// <summary>A collider-free display shape. Neither the cell nor the motor blocks anything.</summary>
        private static GameObject CreateShape(Transform parent, string name,
                                              Vector3 localPosition, Vector3 localScale, Material material)
        {
            return CreateShape(parent, name, PrimitiveType.Cube, localPosition, localScale, material);
        }

        private static GameObject CreateShape(Transform parent, string name, PrimitiveType type,
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

        private void CreatePickup(Transform parent, Vector3 position, PickupKind kind, int amount)
        {
            // A battery is a cylinder — the one pickup you can tell from its silhouette
            // across a dark room, which is the point of the shape being different at all.
            PrimitiveType shape = kind == PickupKind.Health ? PrimitiveType.Capsule
                                : kind == PickupKind.Battery ? PrimitiveType.Cylinder
                                : PrimitiveType.Cube;

            var go = GameObject.CreatePrimitive(shape);
            go.name = kind == PickupKind.Ammo ? "AmmoBox"
                    : kind == PickupKind.Health ? "Medkit" : "Battery";
            go.transform.SetParent(parent, false);
            go.transform.position = position + Vector3.up * 0.6f;

            go.transform.localScale = kind == PickupKind.Ammo ? new Vector3(0.45f, 0.28f, 0.32f)
                                    : kind == PickupKind.Health ? new Vector3(0.3f, 0.2f, 0.3f)
                                    : new Vector3(0.16f, 0.22f, 0.16f);

            go.GetComponent<MeshRenderer>().sharedMaterial =
                kind == PickupKind.Ammo ? ProtoMaterials.AmmoBox
                : kind == PickupKind.Health ? ProtoMaterials.Medkit
                : ProtoMaterials.Battery;

            var collider = go.GetComponent<Collider>();
            collider.isTrigger = true;

            // A generous trigger so you grab it by walking over it, not onto it.
            var trigger = go.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 2.2f;

            SetLayerRecursively(go, TriggerLayer);
            go.AddComponent<Pickup>().Configure(kind, amount);
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
        }

        private void BuildExit()
        {
            if (!house.HasExit) return;

            var go = new GameObject("ExitZone");
            go.transform.SetParent(transform, false);
            go.transform.position = house.ExitPosition + Vector3.up * 0.05f;

            var trigger = go.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = exitRadius;

            var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pad.name = "Pad";
            pad.transform.SetParent(go.transform, false);
            pad.transform.localPosition = Vector3.zero;
            pad.transform.localScale = new Vector3(exitRadius * 2f, 0.04f, exitRadius * 2f);
            pad.GetComponent<MeshRenderer>().sharedMaterial = ProtoMaterials.ExitGlow;

            var padCollider = pad.GetComponent<Collider>();
            if (padCollider != null) Destroy(padCollider);

            BuildDoorway(go.transform);

            var lightObject = new GameObject("ExitLight");
            lightObject.transform.SetParent(go.transform, false);
            lightObject.transform.localPosition = Vector3.up * 1.5f;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 8f;
            light.intensity = 0.2f;

            SetLayerRecursively(go, TriggerLayer);
            go.AddComponent<ExitZone>();
        }

        /// <summary>
        /// The way out, built as an actual door: a frame, a lintel, and a leaf standing
        /// ajar. The exit sits in a gap in the outer wall, so without this it would read
        /// as a hole rather than as somewhere to leave by.
        ///
        /// The frame faces whichever way the wall runs, worked out from where the solid
        /// wall is rather than being told.
        /// </summary>
        private void BuildDoorway(Transform exit)
        {
            // Look for the wall either side; the door faces along the open axis.
            bool wallOnX = Physics.Raycast(exit.position + Vector3.up, Vector3.right, 1.6f, 1)
                           || Physics.Raycast(exit.position + Vector3.up, Vector3.left, 1.6f, 1);

            Vector3 across = wallOnX ? Vector3.forward : Vector3.right;
            Vector3 through = wallOnX ? Vector3.right : Vector3.forward;

            const float doorWidth = 1.5f;
            const float doorHeight = 2.4f;
            const float postThickness = 0.16f;

            CreateDoorPart(exit, "DoorPostA", across * (doorWidth * 0.5f) + Vector3.up * (doorHeight * 0.5f),
                new Vector3(postThickness, doorHeight, postThickness), ProtoMaterials.Wood);
            CreateDoorPart(exit, "DoorPostB", -across * (doorWidth * 0.5f) + Vector3.up * (doorHeight * 0.5f),
                new Vector3(postThickness, doorHeight, postThickness), ProtoMaterials.Wood);

            Vector3 lintelSize = wallOnX
                ? new Vector3(postThickness, 0.22f, doorWidth + postThickness)
                : new Vector3(doorWidth + postThickness, 0.22f, postThickness);

            CreateDoorPart(exit, "DoorLintel", Vector3.up * (doorHeight + 0.1f), lintelSize, ProtoMaterials.Wood);

            // The leaf, swung open on the far side so the way through stays clear.
            var leaf = CreateDoorPart(exit, "DoorLeaf",
                across * (doorWidth * 0.45f) + through * 0.5f + Vector3.up * (doorHeight * 0.5f),
                new Vector3(0.07f, doorHeight - 0.15f, doorWidth * 0.9f), ProtoMaterials.Wood);
            leaf.transform.rotation = Quaternion.LookRotation(across) * Quaternion.Euler(0f, 68f, 0f);
        }

        private GameObject CreateDoorPart(Transform parent, string name, Vector3 offset,
                                          Vector3 size, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.position = parent.position + offset;
            part.transform.localScale = size;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;

            // Decoration only — the doorway must never block the way out.
            var collider = part.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            part.layer = TriggerLayer;
            return part;
        }

        private void ConfigureSpawner()
        {
            if (spawner == null) return;

            GameObject prefab = zombiePrefab;
            if (prefab == null)
            {
                // No prefab assigned — build one at runtime and keep it out of the level.
                prefab = ZombieFactory.Create("ZombieTemplate");
                prefab.SetActive(false);
                prefab.transform.SetParent(transform, false);
                Debug.LogWarning("[LevelDirector] No zombie prefab assigned; using a runtime-built template.");
            }

            spawner.Configure(prefab, house.ZombieSpawns, house.HidingSpots);

            // A level that has creatures placed by hand hands them over now. The prefab
            // and the kind were set when the scene was built; only the positions have to
            // wait for the level to have generated itself.
            if (house is ILurkerSource lurkers) spawner.SetLurkerPositions(lurkers.LurkerSpawns);
        }
    }
}
