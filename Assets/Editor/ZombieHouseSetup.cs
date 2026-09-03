using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using ZombieHouse.Combat;
using ZombieHouse.Core;
using ZombieHouse.Enemies;
// DamageInfo and NavMeshAgent are used by the ragdoll test below.
using ZombieHouse.Level;
using ZombieHouse.Player;
using ZombieHouse.UI;

namespace ZombieHouse.EditorTools
{
    /// <summary>
    /// Builds Level 1 from nothing: layers, placeholder materials, the zombie prefab,
    /// the player rig and the manager objects, saved as a scene you can just press Play on.
    /// Re-running it rebuilds the scene from scratch, so it stays the source of truth
    /// while the level is still a blockout.
    /// </summary>
    public static class ZombieHouseSetup
    {
        private const string ScenesFolder = "Assets/Scenes";
        private const string PrefabsFolder = "Assets/Prefabs";
        private const string MaterialsFolder = "Assets/Resources/ProtoMaterials";
        private const string MeshesFolder = "Assets/Meshes";
        private const string ScenePath = ScenesFolder + "/Level1_House.unity";
        private const string ForestScenePath = ScenesFolder + "/Level2_Forest.unity";
        private const string TownScenePath = ScenesFolder + "/Level3_Town.unity";
        private const string SchoolScenePath = ScenesFolder + "/Level4_School.unity";
        private const string PyramidScenePath = ScenesFolder + "/Level5_Pyramid.unity";
        private const string JungleScenePath = ScenesFolder + "/Level6_Jungle.unity";
        private const string ZombiePrefabPath = PrefabsFolder + "/Zombie.prefab";
        private const string BearPrefabPath = PrefabsFolder + "/ZombieBear.prefab";
        private const string HorsePrefabPath = PrefabsFolder + "/ZombieHorse.prefab";
        private const string CowboyPrefabPath = PrefabsFolder + "/ZombieCowboy.prefab";
        private const string TeacherPrefabPath = PrefabsFolder + "/ZombieTeacher.prefab";
        private const string KidPrefabPath = PrefabsFolder + "/ZombieKid.prefab";
        private const string JanitorPrefabPath = PrefabsFolder + "/ZombieJanitor.prefab";
        private const string MummyPrefabPath = PrefabsFolder + "/Mummy.prefab";
        private const string ScarabPrefabPath = PrefabsFolder + "/Scarab.prefab";
        private const string SnakePrefabPath = PrefabsFolder + "/Snake.prefab";
        private const string JaguarPrefabPath = PrefabsFolder + "/Jaguar.prefab";
        private const string MonkeyPrefabPath = PrefabsFolder + "/Monkey.prefab";

        private const string PlayerLayerName = "Player";
        private const string EnemyLayerName = "Enemy";
        private const string CorpseLayerName = "Corpse";
        private const string ViewModelLayerName = "ViewModel";

        /// Doors live here on purpose: RuntimeNavMeshBaker bakes the Default layer only, so
        /// anything on this layer is invisible to navigation. A closed door that baked as a
        /// wall would seal rooms and fail every level verification at once.
        private const string DoorLayerName = "Door";

        [MenuItem("Zombie House/Build Level 1 Scene", false, 0)]
        public static void BuildLevel1()
        {
            if (!EditorUtility.DisplayDialog(
                    "Build Level 1",
                    "This creates (or overwrites) " + ScenePath + " and the placeholder assets it needs.\n\n" +
                    "Any unsaved changes in the current scene will be lost.",
                    "Build it", "Cancel"))
                return;

            Build(interactive: true);
        }

        /// <summary>Entry point for headless verification: Unity.exe -executeMethod ... .BuildLevel1Automated</summary>
        public static void BuildLevel1Automated()
        {
            Build(interactive: false);
        }

        private static void Build(bool interactive)
        {
            EnsureFolder(ScenesFolder);
            EnsureFolder(PrefabsFolder);
            EnsureFolder("Assets/Resources");
            EnsureFolder(MaterialsFolder);
            EnsureFolder(MeshesFolder);

            int playerLayer = EnsureLayer(PlayerLayerName);
            int enemyLayer = EnsureLayer(EnemyLayerName);
            EnsureLayer(CorpseLayerName);      // ragdolls move here so bodies do not block the player
            EnsureLayer(ViewModelLayerName);
            int doorLayer = EnsureLayer(DoorLayerName);   // the weapon camera renders only this layer

            CreatePlaceholderMaterials();
            ProtoMaterials.ClearCache();

            GameObject zombiePrefab = BuildZombiePrefab(enemyLayer);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            ConfigureLighting();
            GameObject player = BuildPlayerRig(playerLayer, enemyLayer);
            BuildManagers(zombiePrefab, player);
            ApplyPostFx(player, LevelMood.House);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ZombieHouse] Level 1 built at " + ScenePath + " — press Play.");

            if (!interactive) return;

            EditorUtility.DisplayDialog("Level 1 ready",
                "Scene saved to " + ScenePath + ".\n\nPress Play.\n\n" +
                "WASD move · Shift sprint · Ctrl crouch · Space jump\n" +
                "LMB fire · RMB aim · R reload · Esc pause",
                "Let's go");
        }

        [MenuItem("Zombie House/Build Level 2 Forest", false, 1)]
        public static void BuildLevel2()
        {
            if (!EditorUtility.DisplayDialog(
                    "Build Level 2",
                    "This creates (or overwrites) " + ForestScenePath + ".\n\n" +
                    "Any unsaved changes in the current scene will be lost.",
                    "Build it", "Cancel"))
                return;

            BuildForest(interactive: true);
        }

        /// <summary>Headless entry point for the forest, mirroring BuildLevel1Automated.</summary>
        public static void BuildLevel2Automated()
        {
            BuildForest(interactive: false);
        }

        private static void BuildForest(bool interactive)
        {
            EnsureFolder(ScenesFolder);
            EnsureFolder(PrefabsFolder);
            EnsureFolder("Assets/Resources");
            EnsureFolder(MaterialsFolder);
            EnsureFolder(MeshesFolder);

            int playerLayer = EnsureLayer(PlayerLayerName);
            int enemyLayer = EnsureLayer(EnemyLayerName);
            EnsureLayer(CorpseLayerName);
            EnsureLayer(ViewModelLayerName);
            int doorLayer = EnsureLayer(DoorLayerName);

            CreatePlaceholderMaterials();
            ProtoMaterials.ClearCache();

            GameObject zombiePrefab = BuildZombiePrefab(enemyLayer);
            GameObject bearPrefab = BuildBearPrefab(enemyLayer);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            ConfigureForestLighting();
            GameObject player = BuildPlayerRig(playerLayer, enemyLayer);
            BuildForestManagers(zombiePrefab, bearPrefab, player);
            ApplyPostFx(player, LevelMood.Forest);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ForestScenePath);
            AddSceneToBuildSettings(ForestScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ZombieHouse] Level 2 built at " + ForestScenePath + " — press Play.");

            if (!interactive) return;

            EditorUtility.DisplayDialog("Level 2 ready",
                "Scene saved to " + ForestScenePath + ".\n\nPress Play.\n\n" +
                "Cross the wood and find the trail out. Watch for bears.",
                "Into the trees");
        }

        /// <summary>
        /// Outdoors at night: no interior lamps to lean on, so the mood comes from a cold
        /// moon, thick fog and the shafts of light the forest generator drops between the
        /// trees. Fog is much heavier than indoors — it is what keeps the wood frightening
        /// and stops you seeing the cliff wall from the middle of the map.
        /// </summary>
        /// <summary>
        /// Night under a full moon. The moon is the procedural skybox's own disc rather
        /// than a sphere hung in the scene — a sphere that far away would be swallowed by
        /// the fog, and the skybox is drawn behind everything and unfogged, which is
        /// exactly what a moon needs. Because the disc follows the directional light, the
        /// moon is genuinely where the moonlight comes from.
        /// </summary>
        private static void ConfigureForestLighting()
        {
            var moonObject = new GameObject("Moonlight");

            // Low in the sky and behind you as you set off, so it silhouettes the trees
            // ahead instead of glaring into the lens.
            moonObject.transform.rotation = Quaternion.Euler(22f, 145f, 0f);

            var moon = moonObject.AddComponent<Light>();
            moon.type = LightType.Directional;

            // Pale, barely blue — a full moon is much whiter than a blue colour-grade.
            // Dim, though: this is a wood at night, and the torch is supposed to matter.
            moon.color = new Color(0.70f, 0.76f, 0.93f);
            moon.intensity = 0.42f;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.75f;

            RenderSettings.sun = moon;
            RenderSettings.skybox = CreateNightSkyMaterial();

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.055f, 0.065f, 0.09f);

            // Fog is most of the dark: dense enough to close the wood in again, and only
            // just lifted off black so trunks read as silhouettes at the edge of the beam
            // instead of disappearing. These four numbers are the whole mood — drop
            // fogDensity towards 0.018 and raise the moon back to 1.15 to open it up.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.025f, 0.030f, 0.050f);
            RenderSettings.fogDensity = 0.042f;
        }

        /// <summary>
        /// The night sky, saved as an asset because a material built purely in memory does
        /// not survive saving the scene — the same rule as every other proto material.
        /// </summary>
        private static Material CreateNightSkyMaterial()
        {
            const string path = MaterialsFolder + "/nightsky.mat";

            Shader shader = Shader.Find("Skybox/Procedural");
            if (shader == null)
            {
                Debug.LogWarning("[ZombieHouse] No Skybox/Procedural shader; the sky will be flat.");
                return null;
            }

            // Load or create, then always re-apply the numbers below. Returning an existing
            // asset untouched would mean every later tweak to the sky silently did nothing.
            EnsureFolder(MaterialsFolder);
            var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = sky == null;
            if (isNew) sky = new Material(shader) { name = "NightSky" };

            // _SunDisk 2 is the high-quality disc; _SunSize is the moon's apparent width.
            sky.SetFloat("_SunDisk", 2f);
            sky.SetFloat("_SunSize", 0.05f);
            sky.SetFloat("_SunSizeConvergence", 3f);

            // A thin atmosphere keeps the sky dark and stops the disc smearing into a
            // daylight haze; the tint and exposure do the rest of the work.
            sky.SetFloat("_AtmosphereThickness", 0.24f);
            sky.SetColor("_SkyTint", new Color(0.05f, 0.065f, 0.12f));
            sky.SetColor("_GroundColor", new Color(0.012f, 0.014f, 0.022f));
            sky.SetFloat("_Exposure", 0.40f);

            if (isNew) AssetDatabase.CreateAsset(sky, path);
            else EditorUtility.SetDirty(sky);

            AssetDatabase.SaveAssets();
            return sky;
        }

        private static void BuildForestManagers(GameObject zombiePrefab, GameObject bearPrefab, GameObject player)
        {
            var managers = new GameObject("--- Managers ---");

            var audioObject = new GameObject("GameAudio");
            audioObject.transform.SetParent(managers.transform, false);
            var forestAudio = audioObject.AddComponent<ZombieHouse.Audio.GameAudio>();
            audioObject.AddComponent<ZombieHouse.Audio.StingerAudio>();
            audioObject.AddComponent<ZombieHouse.Audio.ThreatMeter>();
            audioObject.AddComponent<ZombieHouse.Fx.DreadDirector>();

            // Outside gets its own bed: no music box out here, just wind and a drone.
            var audioSo = new SerializedObject(forestAudio);
            audioSo.FindProperty("musicTrack").enumValueIndex = (int)ZombieHouse.Audio.Sfx.MusicForest;
            audioSo.FindProperty("tensionTrack").enumValueIndex = (int)ZombieHouse.Audio.Sfx.TensionForest;
            audioSo.ApplyModifiedPropertiesWithoutUndo();

            var gameManagerObject = new GameObject("GameManager");
            gameManagerObject.transform.SetParent(managers.transform, false);
            gameManagerObject.AddComponent<GameManager>();
            gameManagerObject.AddComponent<HudController>();

            var impactObject = new GameObject("ImpactSystem");
            impactObject.transform.SetParent(managers.transform, false);
            impactObject.AddComponent<ZombieHouse.Fx.ImpactSystem>();

            var forestObject = new GameObject("Forest");
            var forest = forestObject.AddComponent<ForestGenerator>();

            var navMeshObject = new GameObject("NavMesh");
            navMeshObject.transform.SetParent(managers.transform, false);
            var baker = navMeshObject.AddComponent<RuntimeNavMeshBaker>();

            var spawnerObject = new GameObject("ZombieSpawner");
            spawnerObject.transform.SetParent(managers.transform, false);
            var spawner = spawnerObject.AddComponent<ZombieSpawner>();

            // A third of the wood is bears; the rest is what walked out of the house.
            spawner.ConfigureBeasts(bearPrefab, 0.34f, ZombieKind.Bear);

            var directorObject = new GameObject("LevelDirector");
            directorObject.transform.SetParent(managers.transform, false);
            var director = directorObject.AddComponent<LevelDirector>();

            var so = new SerializedObject(director);
            AssignReference(so, "levelSourceBehaviour", forest);
            AssignReference(so, "spawner", spawner);
            AssignReference(so, "navMeshBaker", baker);
            AssignReference(so, "player", player.transform);
            AssignReference(so, "zombiePrefab", zombiePrefab);
            // The boss — the wood: still 1.7x from the rifle, so the level keeps its own lesson.
            AssignReference(so, "bossPrefab", bearPrefab);
            so.FindProperty("bossKind").enumValueIndex = (int)ZombieKind.BossBear;
            so.ApplyModifiedPropertiesWithoutUndo();

            forest.Generate();
        }

        /// <summary>
        /// Generates the tooth spike once and caches it as an asset. A mesh built purely
        /// at runtime would not survive saving the bear prefab, leaving empty filters
        /// where the teeth should be — the same reason the katana blade is an asset.
        /// </summary>
        private static Mesh LoadOrCreateFangMesh()
        {
            const string path = MeshesFolder + "/BearFang.asset";

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;

            EnsureFolder(MeshesFolder);
            Mesh mesh = FangMesh.Create();
            AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Bear] Fang mesh: {mesh.vertexCount} vertices, {mesh.triangles.Length / 3} triangles.");
            return mesh;
        }

        private static GameObject BuildBearPrefab(int enemyLayer)
        {
            GameObject temp = ZombieBearFactory.Create("ZombieBear", LoadOrCreateFangMesh());
            SetLayerRecursively(temp, enemyLayer);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, BearPrefabPath);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        [MenuItem("Zombie House/Build Level 3 Town", false, 2)]
        public static void BuildLevel3()
        {
            BuildTown(true);
        }

        public static void BuildLevel3Automated()
        {
            BuildTown(false);
        }

        private static void BuildTown(bool interactive)
        {
            EnsureFolder(ScenesFolder);
            EnsureFolder(PrefabsFolder);
            EnsureFolder("Assets/Resources");
            EnsureFolder(MaterialsFolder);
            EnsureFolder(MeshesFolder);

            int playerLayer = EnsureLayer(PlayerLayerName);
            int enemyLayer = EnsureLayer(EnemyLayerName);
            EnsureLayer(CorpseLayerName);
            EnsureLayer(ViewModelLayerName);
            int doorLayer = EnsureLayer(DoorLayerName);

            CreatePlaceholderMaterials();
            ProtoMaterials.ClearCache();

            GameObject cowboyPrefab = BuildCowboyPrefab(enemyLayer);
            GameObject horsePrefab = BuildHorsePrefab(enemyLayer);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            ConfigureTownLighting();
            GameObject player = BuildPlayerRig(playerLayer, enemyLayer);
            BuildTownManagers(cowboyPrefab, horsePrefab, player);
            ApplyPostFx(player, LevelMood.Town);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, TownScenePath);
            AddSceneToBuildSettings(TownScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ZombieHouse] Level 3 built at " + TownScenePath + " — press Play.");

            if (!interactive) return;

            EditorUtility.DisplayDialog("Level 3 ready",
                "Scene saved to " + TownScenePath + ".\n\nPress Play.\n\n" +
                "Work the street, cut every hostage loose, and get out through the gate. " +
                "Listen for hooves.",
                "Into town");
        }

        /// <summary>
        /// The town at night. Lighter than the wood — there are lamps down the street and
        /// nothing overhead — but the alleys get no light at all, which is the whole
        /// point of a street: you can see a long way down it and nothing to either side.
        /// </summary>
        private static void ConfigureTownLighting()
        {
            var moonObject = new GameObject("Moonlight");
            moonObject.transform.rotation = Quaternion.Euler(30f, 205f, 0f);

            var moon = moonObject.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.62f, 0.68f, 0.86f);
            moon.intensity = 0.5f;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.8f;

            RenderSettings.sun = moon;
            RenderSettings.skybox = CreateNightSkyMaterial();

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.085f, 0.09f, 0.115f);

            // Thin dust rather than the wood's fog: it hangs in the lamplight and softens
            // the far end of the street without hiding the near half of it.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.075f, 0.065f, 0.060f);
            RenderSettings.fogDensity = 0.017f;
        }

        private static void BuildTownManagers(GameObject cowboyPrefab, GameObject horsePrefab, GameObject player)
        {
            var managers = new GameObject("--- Managers ---");

            var audioObject = new GameObject("GameAudio");
            audioObject.transform.SetParent(managers.transform, false);
            var townAudio = audioObject.AddComponent<ZombieHouse.Audio.GameAudio>();
            audioObject.AddComponent<ZombieHouse.Audio.StingerAudio>();
            audioObject.AddComponent<ZombieHouse.Audio.ThreatMeter>();
            audioObject.AddComponent<ZombieHouse.Fx.DreadDirector>();

            var audioSo = new SerializedObject(townAudio);
            audioSo.FindProperty("musicTrack").enumValueIndex = (int)ZombieHouse.Audio.Sfx.MusicTown;
            audioSo.FindProperty("tensionTrack").enumValueIndex = (int)ZombieHouse.Audio.Sfx.TensionTown;
            audioSo.ApplyModifiedPropertiesWithoutUndo();

            var gameManagerObject = new GameObject("GameManager");
            gameManagerObject.transform.SetParent(managers.transform, false);
            gameManagerObject.AddComponent<GameManager>();
            gameManagerObject.AddComponent<HudController>();

            var impactObject = new GameObject("ImpactSystem");
            impactObject.transform.SetParent(managers.transform, false);
            impactObject.AddComponent<ZombieHouse.Fx.ImpactSystem>();

            var townObject = new GameObject("Town");
            var town = townObject.AddComponent<TownGenerator>();

            var navMeshObject = new GameObject("NavMesh");
            navMeshObject.transform.SetParent(managers.transform, false);
            var baker = navMeshObject.AddComponent<RuntimeNavMeshBaker>();

            var spawnerObject = new GameObject("ZombieSpawner");
            spawnerObject.transform.SetParent(managers.transform, false);
            var spawner = spawnerObject.AddComponent<ZombieSpawner>();

            // A fifth of the town is horses. Fewer than the wood's bears, because one of
            // them coming down a straight street is already the worst thing in the level.
            spawner.ConfigureBeasts(horsePrefab, 0.2f, ZombieKind.Horse);

            var directorObject = new GameObject("LevelDirector");
            directorObject.transform.SetParent(managers.transform, false);
            var director = directorObject.AddComponent<LevelDirector>();

            var so = new SerializedObject(director);
            AssignReference(so, "levelSourceBehaviour", town);
            AssignReference(so, "spawner", spawner);
            AssignReference(so, "navMeshBaker", baker);
            AssignReference(so, "player", player.transform);
            AssignReference(so, "zombiePrefab", cowboyPrefab);
            // The boss — the town: a giant zombie horse, which is what the street is
            // remembered for. Not one you asked for; the level needed something.
            AssignReference(so, "bossPrefab", horsePrefab);
            so.FindProperty("bossKind").enumValueIndex = (int)ZombieKind.BossHorse;
            so.ApplyModifiedPropertiesWithoutUndo();

            town.Generate();
        }

        [MenuItem("Zombie House/Verify Town", false, 26)]
        public static void VerifyTown()
        {
            if (!File.Exists(TownScenePath))
            {
                Debug.LogError("[Town] No scene at " + TownScenePath + " — run Build Level 3 Town first.");
                return;
            }

            EditorSceneManager.OpenScene(TownScenePath, OpenSceneMode.Single);

            var town = Object.FindAnyObjectByType<TownGenerator>();
            var baker = Object.FindAnyObjectByType<RuntimeNavMeshBaker>();
            if (town == null || baker == null)
            {
                Debug.LogError("[Town] Scene is missing the TownGenerator or RuntimeNavMeshBaker.");
                return;
            }

            ProtoMaterials.ClearCache();
            town.Generate();
            baker.SetBakeVolume(town.LevelBounds.center, town.LevelBounds.size);
            baker.Bake();

            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            int triangles = triangulation.indices.Length / 3;
            Debug.Log($"[Town] NavMesh: {triangulation.vertices.Length} vertices, {triangles} triangles.");
            Debug.Log($"[Town] Contents: {town.AmmoSpawns.Count} ammo, {town.MedkitSpawns.Count} medkits, " +
                      $"{town.BatterySpawns.Count} batteries, " +
                      $"{town.HidingSpots.Count} places to lurk.");

            if (triangles == 0)
            {
                Debug.LogError("[Town] NavMesh is empty — nothing will move.");
                return;
            }

            int problems = 0;

            NavMeshHit startHit;
            bool startOk = NavMesh.SamplePosition(town.PlayerSpawn, out startHit, 4f, NavMesh.AllAreas);
            if (!startOk)
            {
                Debug.LogError("[Town] The south end of the street is not navigable.");
                problems++;
            }

            NavMeshHit exitHit;
            if (startOk && NavMesh.SamplePosition(town.ExitPosition, out exitHit, 6f, NavMesh.AllAreas))
            {
                var path = new NavMeshPath();
                NavMesh.CalculatePath(startHit.position, exitHit.position, NavMesh.AllAreas, path);

                if (path.status == NavMeshPathStatus.PathComplete)
                {
                    float length = 0f;
                    for (int i = 1; i < path.corners.Length; i++)
                        length += Vector3.Distance(path.corners[i - 1], path.corners[i]);

                    Debug.Log($"[Town] The street is walkable end to end — {length:0} m to the gate.");
                }
                else
                {
                    Debug.LogError($"[Town] The gate cannot be reached from the start ({path.status}).");
                    problems++;
                }
            }
            else if (startOk)
            {
                Debug.LogError("[Town] The gate is not on navigable ground.");
                problems++;
            }

            // A horse has the widest agent in the game; if it cannot use the street, the
            // level's headline enemy is furniture.
            problems += CheckHorseCanRunTheStreet(town, startOk ? startHit.position : town.PlayerSpawn);

            int reachable = 0;
            foreach (Vector3 spawn in town.ZombieSpawns)
            {
                NavMeshHit spawnHit;
                if (!NavMesh.SamplePosition(spawn, out spawnHit, 5f, NavMesh.AllAreas)) continue;
                if (!startOk) continue;

                var path = new NavMeshPath();
                NavMesh.CalculatePath(spawnHit.position, startHit.position, NavMesh.AllAreas, path);
                if (path.status == NavMeshPathStatus.PathComplete) reachable++;
            }

            Debug.Log($"[Town] Marked spawns that can reach you: {reachable}/{town.ZombieSpawns.Count}");
            if (reachable < town.ZombieSpawns.Count * 0.8f)
            {
                Debug.LogError("[Town] Too many spawns are cut off.");
                problems++;
            }

            problems += CheckDressing(town);
            problems += CheckSurvivors(town, "Town");
            problems += CheckPowerRoute(town, "Town");
            problems += CheckPowerUps(town, "Town");
            problems += CheckBeltCrates(town, "Town");
            problems += CheckTorchOnPlayer();
            problems += CheckSidearm("Town");
            problems += CheckKillQuota("Town");
            problems += CheckPostFx("Town");

            Debug.Log(problems == 0
                ? "[Town] PASS — the street is walkable and everyone can be reached."
                : $"[Town] FAIL — {problems} problem(s).");
        }

        /// <summary>
        /// The street's dressing: that the buildings have glass in them, that some of it
        /// is lit, and that the rolling cacti carry no colliders.
        ///
        /// The collider check is the one that matters. A cactus is the only thing in the
        /// level that moves and is not an enemy, and a collider on one would either carve
        /// the NavMesh where it happened to be standing at bake time, or block a horse, or
        /// swallow a .50 round meant for something behind it — none of which would be
        /// obvious, and all of which would be blamed on something else.
        /// </summary>
        private static int CheckDressing(TownGenerator town)
        {
            int problems = 0;
            int panes = 0, litPanes = 0, broken = 0, cacti = 0, cactusColliders = 0;

            foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                string n = renderer.name;

                if (n.StartsWith("WindowPane"))
                {
                    panes++;
                    Material glass = renderer.sharedMaterial;
                    if (glass != null && glass.HasProperty("_EmissionColor")
                        && glass.GetColor("_EmissionColor").maxColorComponent > 0.5f)
                    {
                        litPanes++;
                    }
                }
                else if (n.StartsWith("WindowShard"))
                {
                    broken++;
                }
            }

            foreach (RollingCactus cactus in Object.FindObjectsByType<RollingCactus>(FindObjectsSortMode.None))
            {
                cacti++;
                cactusColliders += cactus.GetComponentsInChildren<Collider>(true).Length;
            }

            if (panes < 10)
            {
                Debug.LogError($"[Town] Only {panes} window pane(s) on the whole street.");
                problems++;
            }
            else if (litPanes == 0)
            {
                Debug.LogError("[Town] Not one window is lit — the street has no light of its own.");
                problems++;
            }
            else
            {
                Debug.Log($"[Town] Windows: {panes} panes, {litPanes} of them lit, {broken} shard(s) in smashed frames.");
            }

            if (cacti == 0)
            {
                Debug.LogError("[Town] No rolling cacti — nothing on the street moves but the dead.");
                problems++;
            }
            else if (cactusColliders > 0)
            {
                Debug.LogError($"[Town] The cacti carry {cactusColliders} collider(s) — they would block shots and carve the NavMesh.");
                problems++;
            }
            else
            {
                Debug.Log($"[Town] {cacti} rolling cacti, no colliders on any of them.");
            }

            return problems;
        }

        /// <summary>
        /// Walks the street with a horse-sized agent radius. The NavMesh the level bakes
        /// is built for the default agent, so a gap that passes the ordinary path test can
        /// still be too tight for the widest thing in the game to charge down.
        /// </summary>
        private static int CheckHorseCanRunTheStreet(TownGenerator town, Vector3 from)
        {
            var filter = new NavMeshQueryFilter { areaMask = NavMesh.AllAreas, agentTypeID = 0 };

            NavMeshHit far;
            if (!NavMesh.SamplePosition(town.ExitPosition, out far, 6f, NavMesh.AllAreas))
            {
                Debug.LogError("[Town] Could not sample the gate for the horse check.");
                return 1;
            }

            var path = new NavMeshPath();
            NavMesh.CalculatePath(from, far.position, filter, path);

            if (path.status != NavMeshPathStatus.PathComplete)
            {
                Debug.LogError("[Town] A horse cannot get down the street — the clutter has closed it.");
                return 1;
            }

            // Widest point check: sample either side of the centre line at horse radius.
            int blocked = 0;
            for (float z = -town.LevelBounds.size.z * 0.35f; z < town.LevelBounds.size.z * 0.35f; z += 8f)
            {
                Vector3 probe = new Vector3(town.PlayerSpawn.x, town.PlayerSpawn.y, town.transform.position.z + z);
                NavMeshHit hit;
                if (!NavMesh.SamplePosition(probe, out hit, 3f, NavMesh.AllAreas)) blocked++;
            }

            if (blocked > 2)
            {
                Debug.LogError($"[Town] The centre of the street is unnavigable at {blocked} sample points.");
                return 1;
            }

            Debug.Log("[Town] A horse can run the street end to end.");
            return 0;
        }

        [MenuItem("Zombie House/Build Level 4 School", false, 3)]
        public static void BuildLevel4()
        {
            BuildSchool(true);
        }

        public static void BuildLevel4Automated()
        {
            BuildSchool(false);
        }

        private static void BuildSchool(bool interactive)
        {
            EnsureFolder(ScenesFolder);
            EnsureFolder(PrefabsFolder);
            EnsureFolder("Assets/Resources");
            EnsureFolder(MaterialsFolder);
            EnsureFolder(MeshesFolder);

            int playerLayer = EnsureLayer(PlayerLayerName);
            int enemyLayer = EnsureLayer(EnemyLayerName);
            EnsureLayer(CorpseLayerName);
            EnsureLayer(ViewModelLayerName);
            int doorLayer = EnsureLayer(DoorLayerName);

            CreatePlaceholderMaterials();
            ProtoMaterials.ClearCache();

            GameObject teacherPrefab = BuildSchoolPrefab(enemyLayer, ZombieOutfit.Teacher, TeacherPrefabPath);
            GameObject kidPrefab = BuildSchoolPrefab(enemyLayer, ZombieOutfit.Kid, KidPrefabPath);
            GameObject janitorPrefab = BuildSchoolPrefab(enemyLayer, ZombieOutfit.Janitor, JanitorPrefabPath);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            ConfigureSchoolLighting();
            GameObject player = BuildPlayerRig(playerLayer, enemyLayer);
            BuildSchoolManagers(teacherPrefab, kidPrefab, janitorPrefab, player);
            ApplyPostFx(player, LevelMood.School);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, SchoolScenePath);
            AddSceneToBuildSettings(SchoolScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ZombieHouse] Level 4 built at " + SchoolScenePath + " — press Play.");

            if (!interactive) return;

            EditorUtility.DisplayDialog("Level 4 ready",
                "Scene saved to " + SchoolScenePath + ".\n\nPress Play.\n\n" +
                "Work the corridor, find the children, and mind the janitors — a mop " +
                "reaches further than you think.",
                "Into the school");
        }

        /// <summary>
        /// Indoors under failing fluorescents. Almost no ambient and no directional light
        /// at all: every scrap of light in this level comes from a tube in the ceiling or
        /// from the torch, which is what makes a dead corridor read as dead.
        /// </summary>
        private static void ConfigureSchoolLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.055f, 0.058f, 0.062f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.03f, 0.032f, 0.035f);
            RenderSettings.fogDensity = 0.022f;

            RenderSettings.skybox = null;
            RenderSettings.sun = null;
        }

        private static void BuildSchoolManagers(GameObject teacherPrefab, GameObject kidPrefab,
                                                GameObject janitorPrefab, GameObject player)
        {
            var managers = new GameObject("--- Managers ---");

            var audioObject = new GameObject("GameAudio");
            audioObject.transform.SetParent(managers.transform, false);
            var schoolAudio = audioObject.AddComponent<ZombieHouse.Audio.GameAudio>();
            audioObject.AddComponent<ZombieHouse.Audio.StingerAudio>();
            audioObject.AddComponent<ZombieHouse.Audio.ThreatMeter>();
            audioObject.AddComponent<ZombieHouse.Fx.DreadDirector>();

            // The mansion's music box belongs in a building, and a school is a building.
            var audioSo = new SerializedObject(schoolAudio);
            audioSo.FindProperty("musicTrack").enumValueIndex = (int)ZombieHouse.Audio.Sfx.MusicSchool;
            audioSo.FindProperty("tensionTrack").enumValueIndex = (int)ZombieHouse.Audio.Sfx.TensionSchool;
            audioSo.ApplyModifiedPropertiesWithoutUndo();

            var gameManagerObject = new GameObject("GameManager");
            gameManagerObject.transform.SetParent(managers.transform, false);
            gameManagerObject.AddComponent<GameManager>();
            gameManagerObject.AddComponent<HudController>();

            var impactObject = new GameObject("ImpactSystem");
            impactObject.transform.SetParent(managers.transform, false);
            impactObject.AddComponent<ZombieHouse.Fx.ImpactSystem>();

            var schoolObject = new GameObject("School");
            var school = schoolObject.AddComponent<SchoolGenerator>();

            var schoolSo = new SerializedObject(school);
            schoolSo.FindProperty("doorLayer").intValue = EnsureLayer(DoorLayerName);
            schoolSo.ApplyModifiedPropertiesWithoutUndo();

            var navMeshObject = new GameObject("NavMesh");
            navMeshObject.transform.SetParent(managers.transform, false);
            var baker = navMeshObject.AddComponent<RuntimeNavMeshBaker>();

            var spawnerObject = new GameObject("ZombieSpawner");
            spawnerObject.transform.SetParent(managers.transform, false);
            var spawner = spawnerObject.AddComponent<ZombieSpawner>();

            // Teachers are the baseline population; a quarter of the school is janitors.
            // The kids ride the same "beast" slot the bears and horses use — it is really
            // a second prefab with a share, and nothing about it is quadruped.
            spawner.ConfigureBeasts(janitorPrefab, 0.25f, ZombieKind.Janitor);

            var directorObject = new GameObject("LevelDirector");
            directorObject.transform.SetParent(managers.transform, false);
            var director = directorObject.AddComponent<LevelDirector>();

            var so = new SerializedObject(director);
            AssignReference(so, "levelSourceBehaviour", school);
            AssignReference(so, "spawner", spawner);
            AssignReference(so, "navMeshBaker", baker);
            AssignReference(so, "player", player.transform);
            AssignReference(so, "zombiePrefab", teacherPrefab);
            // The boss — the school: at this size the mop covers most of a classroom.
            AssignReference(so, "bossPrefab", janitorPrefab);
            so.FindProperty("bossKind").enumValueIndex = (int)ZombieKind.BossJanitor;
            so.ApplyModifiedPropertiesWithoutUndo();

            // A third of what is left is children, and they arrive three or four at a
            // time — a classroom door opening produces a group, not a straggler.
            spawner.ConfigurePacks(kidPrefab, 0.34f, ZombieKind.Kid, 3, 4);

            school.Generate();
        }

        [MenuItem("Zombie House/Verify School", false, 27)]
        public static void VerifySchool()
        {
            if (!File.Exists(SchoolScenePath))
            {
                Debug.LogError("[School] No scene at " + SchoolScenePath + " — run Build Level 4 School first.");
                return;
            }

            EditorSceneManager.OpenScene(SchoolScenePath, OpenSceneMode.Single);

            var school = Object.FindAnyObjectByType<SchoolGenerator>();
            var baker = Object.FindAnyObjectByType<RuntimeNavMeshBaker>();
            if (school == null || baker == null)
            {
                Debug.LogError("[School] Scene is missing the SchoolGenerator or RuntimeNavMeshBaker.");
                return;
            }

            ProtoMaterials.ClearCache();
            school.Generate();
            baker.SetBakeVolume(school.LevelBounds.center, school.LevelBounds.size);
            baker.Bake();

            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            int triangles = triangulation.indices.Length / 3;
            Debug.Log($"[School] NavMesh: {triangulation.vertices.Length} vertices, {triangles} triangles.");
            Debug.Log($"[School] Contents: {school.AmmoSpawns.Count} ammo, {school.MedkitSpawns.Count} medkits, " +
                      $"{school.BatterySpawns.Count} batteries, " +
                      $"{school.HidingSpots.Count} places to lurk.");

            if (triangles == 0)
            {
                Debug.LogError("[School] NavMesh is empty — nothing will move.");
                return;
            }

            int problems = 0;

            NavMeshHit startHit;
            bool startOk = NavMesh.SamplePosition(school.PlayerSpawn, out startHit, 4f, NavMesh.AllAreas);
            if (!startOk)
            {
                Debug.LogError("[School] The player start is not on navigable floor.");
                problems++;
            }

            NavMeshHit exitHit;
            if (startOk && NavMesh.SamplePosition(school.ExitPosition, out exitHit, 5f, NavMesh.AllAreas))
            {
                var path = new NavMeshPath();
                NavMesh.CalculatePath(startHit.position, exitHit.position, NavMesh.AllAreas, path);

                if (path.status == NavMeshPathStatus.PathComplete)
                {
                    float length = 0f;
                    for (int i = 1; i < path.corners.Length; i++)
                        length += Vector3.Distance(path.corners[i - 1], path.corners[i]);

                    Debug.Log($"[School] The fire door is reachable — {length:0} m from the front hall.");
                }
                else
                {
                    Debug.LogError($"[School] The fire door cannot be reached ({path.status}).");
                    problems++;
                }
            }
            else if (startOk)
            {
                Debug.LogError("[School] The fire door is not on navigable floor.");
                problems++;
            }

            int reachable = 0;
            foreach (Vector3 spawn in school.ZombieSpawns)
            {
                NavMeshHit spawnHit;
                if (!NavMesh.SamplePosition(spawn, out spawnHit, 4f, NavMesh.AllAreas))
                {
                    // Naming the offender matters: "some spawns are walled off" sends you
                    // hunting through a forty-column floor plan by eye.
                    Debug.LogError($"[School] Spawn at {spawn} is not on navigable floor — furniture has covered it.");
                    continue;
                }

                if (!startOk) continue;

                var path = new NavMeshPath();
                NavMesh.CalculatePath(spawnHit.position, startHit.position, NavMesh.AllAreas, path);

                if (path.status == NavMeshPathStatus.PathComplete) reachable++;
                else Debug.LogError($"[School] Spawn at {spawn} cannot reach the player start ({path.status}).");
            }

            Debug.Log($"[School] Zombie spawns reaching the player start: {reachable}/{school.ZombieSpawns.Count}");
            if (reachable < school.ZombieSpawns.Count) problems++;

            problems += CheckFluorescents();
            problems += CheckSurvivors(school, "School");
            problems += CheckSidearm("School");
            problems += CheckKillQuota("School");
            problems += CheckPostFx("School");
            problems += CheckPowerRoute(school, "School");
            problems += CheckPowerUps(school, "School");
            problems += CheckBeltCrates(school, "School");
            problems += CheckTorchOnPlayer();

            Debug.Log(problems == 0
                ? "[School] PASS — the corridor is walkable and everyone can be reached."
                : $"[School] FAIL — {problems} problem(s).");
        }

        /// <summary>
        /// The lighting is the level's mood, so it is worth failing over: a school with
        /// every tube alive is an office, and one with every tube dead is the forest with
        /// walls. It wants both, and it wants some of the live ones flickering.
        /// </summary>
        private static int CheckFluorescents()
        {
            int live = 0, flickering = 0, dead = 0;

            foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!renderer.name.StartsWith("Tube_")) continue;

                Material glass = renderer.sharedMaterial;
                bool lit = glass != null && glass.HasProperty("_EmissionColor")
                           && glass.GetColor("_EmissionColor").maxColorComponent > 0.5f;

                if (lit) live++; else dead++;
            }

            foreach (FlickeringLight flicker in Object.FindObjectsByType<FlickeringLight>(FindObjectsSortMode.None))
                flickering++;

            if (live == 0 || dead == 0)
            {
                Debug.LogError($"[School] Fluorescents: {live} live, {dead} dead — it wants both.");
                return 1;
            }

            if (flickering == 0)
            {
                Debug.LogError("[School] Not one tube flickers; the corridor will read as merely dim.");
                return 1;
            }

            Debug.Log($"[School] Fluorescents: {live} live ({flickering} of them flickering), {dead} dead.");
            return 0;
        }

        [MenuItem("Zombie House/Build Level 5 Pyramid", false, 4)]
        public static void BuildLevel5()
        {
            BuildPyramid(true);
        }

        public static void BuildLevel5Automated()
        {
            BuildPyramid(false);
        }

        private static void BuildPyramid(bool interactive)
        {
            EnsureFolder(ScenesFolder);
            EnsureFolder(PrefabsFolder);
            EnsureFolder("Assets/Resources");
            EnsureFolder(MaterialsFolder);
            EnsureFolder(MeshesFolder);

            int playerLayer = EnsureLayer(PlayerLayerName);
            int enemyLayer = EnsureLayer(EnemyLayerName);
            EnsureLayer(CorpseLayerName);
            EnsureLayer(ViewModelLayerName);
            int doorLayer = EnsureLayer(DoorLayerName);

            CreatePlaceholderMaterials();
            ProtoMaterials.ClearCache();

            GameObject mummyPrefab = BuildSchoolPrefab(enemyLayer, ZombieOutfit.Mummy, MummyPrefabPath);
            GameObject scarabPrefab = BuildScarabPrefab(enemyLayer);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            ConfigurePyramidLighting();
            GameObject player = BuildPlayerRig(playerLayer, enemyLayer);
            BuildPyramidManagers(mummyPrefab, scarabPrefab, player);
            ApplyPostFx(player, LevelMood.Tomb);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, PyramidScenePath);
            AddSceneToBuildSettings(PyramidScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ZombieHouse] Level 5 built at " + PyramidScenePath + " — press Play.");

            if (!interactive) return;

            EditorUtility.DisplayDialog("Level 5 ready",
                "Scene saved to " + PyramidScenePath + ".\n\nPress Play.\n\n" +
                "Work east down the spine to the burial chamber. The scarabs are fast and " +
                "come in numbers; save the rifle for something worth it.",
                "Into the tomb");
        }

        /// <summary>
        /// Sealed stone: no sky, no moon, and an ambient so low that an unlit chamber is
        /// genuinely black. Every scrap of light is a torch on a wall or your own.
        ///
        /// The warm end of the palette on purpose — the school is cold strip lighting and
        /// the forest is cold moonlight, so a tomb lit orange reads as somewhere else
        /// entirely before you have looked at a single wall.
        /// </summary>
        private static void ConfigurePyramidLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.05f, 0.042f, 0.032f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.05f, 0.04f, 0.03f);
            RenderSettings.fogDensity = 0.026f;

            RenderSettings.skybox = null;
            RenderSettings.sun = null;
        }

        private static void BuildPyramidManagers(GameObject mummyPrefab, GameObject scarabPrefab,
                                                 GameObject player)
        {
            var managers = new GameObject("--- Managers ---");

            var audioObject = new GameObject("GameAudio");
            audioObject.transform.SetParent(managers.transform, false);
            var tombAudio = audioObject.AddComponent<ZombieHouse.Audio.GameAudio>();
            audioObject.AddComponent<ZombieHouse.Audio.StingerAudio>();
            audioObject.AddComponent<ZombieHouse.Audio.ThreatMeter>();
            audioObject.AddComponent<ZombieHouse.Fx.DreadDirector>();

            // Its own bed: a beating drone, a Phrygian-dominant motif on something
            // metal, and a breath in the dark every dozen seconds.
            var audioSo = new SerializedObject(tombAudio);
            audioSo.FindProperty("musicTrack").enumValueIndex = (int)ZombieHouse.Audio.Sfx.MusicTomb;
            audioSo.FindProperty("tensionTrack").enumValueIndex = (int)ZombieHouse.Audio.Sfx.TensionTomb;
            audioSo.ApplyModifiedPropertiesWithoutUndo();

            var gameManagerObject = new GameObject("GameManager");
            gameManagerObject.transform.SetParent(managers.transform, false);
            gameManagerObject.AddComponent<GameManager>();
            gameManagerObject.AddComponent<HudController>();

            var impactObject = new GameObject("ImpactSystem");
            impactObject.transform.SetParent(managers.transform, false);
            impactObject.AddComponent<ZombieHouse.Fx.ImpactSystem>();

            var pyramidObject = new GameObject("Pyramid");
            var pyramid = pyramidObject.AddComponent<PyramidGenerator>();

            var navMeshObject = new GameObject("NavMesh");
            navMeshObject.transform.SetParent(managers.transform, false);
            var baker = navMeshObject.AddComponent<RuntimeNavMeshBaker>();

            var spawnerObject = new GameObject("ZombieSpawner");
            spawnerObject.transform.SetParent(managers.transform, false);
            var spawner = spawnerObject.AddComponent<ZombieSpawner>();

            // Scarabs arrive in packs — a nest of them, not a straggler. Thinned from
            // 45% in nests of 3-5 to 30% in nests of 2-4: the tomb was more beetle than
            // pyramid, and the mummies never got a chance to be the thing you dread.
            spawner.ConfigurePacks(scarabPrefab, 0.3f, ZombieKind.Scarab, 2, 4);

            var directorObject = new GameObject("LevelDirector");
            directorObject.transform.SetParent(managers.transform, false);
            var director = directorObject.AddComponent<LevelDirector>();

            var so = new SerializedObject(director);
            AssignReference(so, "levelSourceBehaviour", pyramid);
            AssignReference(so, "spawner", spawner);
            AssignReference(so, "navMeshBaker", baker);
            AssignReference(so, "player", player.transform);
            AssignReference(so, "zombiePrefab", mummyPrefab);
            // The boss — the tomb: keeps the shell, so the answer is what it always was.
            AssignReference(so, "bossPrefab", scarabPrefab);
            so.FindProperty("bossKind").enumValueIndex = (int)ZombieKind.BossScarab;
            so.ApplyModifiedPropertiesWithoutUndo();

            pyramid.Generate();
        }

        [MenuItem("Zombie House/Verify Pyramid", false, 28)]
        public static void VerifyPyramid()
        {
            if (!File.Exists(PyramidScenePath))
            {
                Debug.LogError("[Pyramid] No scene at " + PyramidScenePath + " — run Build Level 5 Pyramid first.");
                return;
            }

            EditorSceneManager.OpenScene(PyramidScenePath, OpenSceneMode.Single);

            var pyramid = Object.FindAnyObjectByType<PyramidGenerator>();
            var baker = Object.FindAnyObjectByType<RuntimeNavMeshBaker>();
            if (pyramid == null || baker == null)
            {
                Debug.LogError("[Pyramid] Scene is missing the PyramidGenerator or RuntimeNavMeshBaker.");
                return;
            }

            ProtoMaterials.ClearCache();
            pyramid.Generate();
            baker.SetBakeVolume(pyramid.LevelBounds.center, pyramid.LevelBounds.size);
            baker.Bake();

            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            int triangles = triangulation.indices.Length / 3;
            Debug.Log($"[Pyramid] NavMesh: {triangulation.vertices.Length} vertices, {triangles} triangles.");
            Debug.Log($"[Pyramid] Contents: {pyramid.AmmoSpawns.Count} ammo, {pyramid.MedkitSpawns.Count} medkits, " +
                      $"{pyramid.BatterySpawns.Count} batteries, " +
                      $"{pyramid.HidingSpots.Count} places to lurk.");

            if (triangles == 0)
            {
                Debug.LogError("[Pyramid] NavMesh is empty — nothing will move.");
                return;
            }

            int problems = 0;

            NavMeshHit startHit;
            bool startOk = NavMesh.SamplePosition(pyramid.PlayerSpawn, out startHit, 4f, NavMesh.AllAreas);
            if (!startOk)
            {
                Debug.LogError("[Pyramid] The entrance is not on navigable floor.");
                problems++;
            }

            NavMeshHit exitHit;
            if (startOk && NavMesh.SamplePosition(pyramid.ExitPosition, out exitHit, 5f, NavMesh.AllAreas))
            {
                var path = new NavMeshPath();
                NavMesh.CalculatePath(startHit.position, exitHit.position, NavMesh.AllAreas, path);

                if (path.status == NavMeshPathStatus.PathComplete)
                {
                    float length = 0f;
                    for (int i = 1; i < path.corners.Length; i++)
                        length += Vector3.Distance(path.corners[i - 1], path.corners[i]);

                    Debug.Log($"[Pyramid] The burial chamber is reachable — {length:0} m from the entrance.");
                }
                else
                {
                    Debug.LogError($"[Pyramid] The exit cannot be reached ({path.status}).");
                    problems++;
                }
            }
            else if (startOk)
            {
                Debug.LogError("[Pyramid] The exit is not on navigable floor.");
                problems++;
            }

            int reachable = 0;
            foreach (Vector3 spawn in pyramid.ZombieSpawns)
            {
                NavMeshHit spawnHit;
                if (!NavMesh.SamplePosition(spawn, out spawnHit, 4f, NavMesh.AllAreas))
                {
                    Debug.LogError($"[Pyramid] Spawn at {spawn} is not on navigable floor — a prop has covered it.");
                    continue;
                }

                if (!startOk) continue;

                var path = new NavMeshPath();
                NavMesh.CalculatePath(spawnHit.position, startHit.position, NavMesh.AllAreas, path);

                if (path.status == NavMeshPathStatus.PathComplete) reachable++;
                else Debug.LogError($"[Pyramid] Spawn at {spawn} cannot reach the entrance ({path.status}).");
            }

            Debug.Log($"[Pyramid] Enemy spawns reaching the entrance: {reachable}/{pyramid.ZombieSpawns.Count}");
            if (reachable < pyramid.ZombieSpawns.Count) problems++;

            problems += CheckTorchlight();
            problems += CheckTombMusic();
            problems += CheckSurvivors(pyramid, "Pyramid");
            problems += CheckSidearm("Pyramid");
            problems += CheckKillQuota("Pyramid");
            problems += CheckPostFx("Pyramid");
            problems += CheckPowerRoute(pyramid, "Pyramid");
            problems += CheckPowerUps(pyramid, "Pyramid");
            problems += CheckBeltCrates(pyramid, "Pyramid");
            problems += CheckTorchOnPlayer();

            Debug.Log(problems == 0
                ? "[Pyramid] PASS — the tomb is walkable end to end."
                : $"[Pyramid] FAIL — {problems} problem(s).");
        }

        /// <summary>
        /// The tomb runs its own music bed rather than borrowing the wood's.
        ///
        /// Worth failing over because it is exactly the sort of thing that silently
        /// reverts: the track is selected by a serialized enum on a component in a scene,
        /// so a rebuild with a stale field would quietly put the forest's wind back and
        /// nothing would look wrong.
        /// </summary>
        private static int CheckTombMusic()
        {
            var audio = Object.FindAnyObjectByType<ZombieHouse.Audio.GameAudio>();
            if (audio == null)
            {
                Debug.LogError("[Pyramid] No GameAudio in the scene — the level is silent.");
                return 1;
            }

            var track = new SerializedObject(audio).FindProperty("musicTrack");
            var selected = (ZombieHouse.Audio.Sfx)track.enumValueIndex;

            if (selected != ZombieHouse.Audio.Sfx.MusicTomb)
            {
                Debug.LogError($"[Pyramid] The tomb is playing {selected}, not its own bed.");
                return 1;
            }

            Debug.Log("[Pyramid] Music: MusicTomb — a beating drone, a Phrygian motif, and breathing.");
            return 0;
        }

        /// <summary>
        /// The tomb's own light. A pyramid with no torches burning is a cave, and one with
        /// all of them burning is a museum; it wants some of each, and it wants the lit
        /// ones to move.
        /// </summary>
        private static int CheckTorchlight()
        {
            int lit = 0, dead = 0, flickering = 0;

            foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!renderer.name.StartsWith("TorchHead_")) continue;

                Material head = renderer.sharedMaterial;
                bool burning = head != null && head.HasProperty("_EmissionColor")
                               && head.GetColor("_EmissionColor").maxColorComponent > 0.5f;

                if (burning) lit++; else dead++;
            }

            foreach (FlickeringLight flicker in Object.FindObjectsByType<FlickeringLight>(FindObjectsSortMode.None))
                flickering++;

            if (lit == 0)
            {
                Debug.LogError("[Pyramid] Not one torch is burning — the tomb has no light of its own at all.");
                return 1;
            }

            if (flickering < lit)
            {
                Debug.LogError($"[Pyramid] {lit} torches lit but only {flickering} flicker; firelight is never still.");
                return 1;
            }

            Debug.Log($"[Pyramid] Torches: {lit} burning (all flickering), {dead} long dead.");
            return 0;
        }

        [MenuItem("Zombie House/Build Level 6 Jungle", false, 5)]
        public static void BuildLevel6()
        {
            if (!EditorUtility.DisplayDialog(
                    "Build Level 6",
                    "This creates (or overwrites) " + JungleScenePath + ".\n\n" +
                    "Any unsaved changes in the current scene will be lost.",
                    "Build it", "Cancel"))
                return;

            BuildJungle(interactive: true);
        }

        public static void BuildLevel6Automated()
        {
            BuildJungle(interactive: false);
        }

        private static void BuildJungle(bool interactive)
        {
            EnsureFolder(ScenesFolder);
            EnsureFolder(PrefabsFolder);
            EnsureFolder("Assets/Resources");
            EnsureFolder(MaterialsFolder);
            EnsureFolder(MeshesFolder);

            int playerLayer = EnsureLayer(PlayerLayerName);
            int enemyLayer = EnsureLayer(EnemyLayerName);
            EnsureLayer(CorpseLayerName);
            EnsureLayer(ViewModelLayerName);
            int doorLayer = EnsureLayer(DoorLayerName);

            CreatePlaceholderMaterials();
            ProtoMaterials.ClearCache();

            GameObject zombiePrefab = BuildZombiePrefab(enemyLayer);
            GameObject jaguarPrefab = BuildJunglePrefab(enemyLayer, ZombieKind.Jaguar);
            GameObject monkeyPrefab = BuildJunglePrefab(enemyLayer, ZombieKind.Monkey);
            GameObject snakePrefab = BuildJunglePrefab(enemyLayer, ZombieKind.Snake);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            ConfigureJungleLighting();
            GameObject player = BuildPlayerRig(playerLayer, enemyLayer);
            BuildJungleManagers(zombiePrefab, jaguarPrefab, monkeyPrefab, snakePrefab, player);
            ApplyPostFx(player, LevelMood.Jungle);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, JungleScenePath);
            AddSceneToBuildSettings(JungleScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ZombieHouse] Level 6 built at " + JungleScenePath + " — press Play.");

            if (!interactive) return;

            EditorUtility.DisplayDialog("Level 6 ready",
                "Scene saved to " + JungleScenePath + ".\n\nPress Play.\n\n" +
                "Cross the river and find the temple gate. Watch the ground — not " +
                "everything down there is a root.",
                "Into the green");
        }

        /// <summary>
        /// Under a closed canopy at dusk. No moon reaches the floor, so unlike the wood
        /// there is no directional light at all: what you get is a very low green-grey
        /// ambient, heavy warm fog, and the gaps in the leaves the generator drops in.
        ///
        /// The wood is cold blue and the tomb is warm orange; the valley is neither, which
        /// is deliberate — a green-grey murk reads as somewhere else before you have
        /// looked at a single tree.
        /// </summary>
        private static void ConfigureJungleLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.045f, 0.058f, 0.046f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.035f, 0.048f, 0.038f);

            // Denser than the wood. Sightlines under a canopy are short, and the fog is
            // what makes a jaguar at twenty metres a shape rather than a target.
            RenderSettings.fogDensity = 0.048f;

            RenderSettings.skybox = null;
            RenderSettings.sun = null;
        }

        private static void BuildJungleManagers(GameObject zombiePrefab, GameObject jaguarPrefab,
                                                GameObject monkeyPrefab, GameObject snakePrefab,
                                                GameObject player)
        {
            var managers = new GameObject("--- Managers ---");

            var audioObject = new GameObject("GameAudio");
            audioObject.transform.SetParent(managers.transform, false);
            var jungleAudio = audioObject.AddComponent<ZombieHouse.Audio.GameAudio>();
            audioObject.AddComponent<ZombieHouse.Audio.StingerAudio>();
            audioObject.AddComponent<ZombieHouse.Audio.ThreatMeter>();
            audioObject.AddComponent<ZombieHouse.Fx.DreadDirector>();

            // Its own bed: cicadas that cut out, a hollow log, and calls that stop
            // mid-note.
            var audioSo = new SerializedObject(jungleAudio);
            audioSo.FindProperty("musicTrack").enumValueIndex = (int)ZombieHouse.Audio.Sfx.MusicJungle;
            audioSo.FindProperty("tensionTrack").enumValueIndex = (int)ZombieHouse.Audio.Sfx.TensionJungle;
            audioSo.ApplyModifiedPropertiesWithoutUndo();

            var gameManagerObject = new GameObject("GameManager");
            gameManagerObject.transform.SetParent(managers.transform, false);
            gameManagerObject.AddComponent<GameManager>();
            gameManagerObject.AddComponent<HudController>();

            var impactObject = new GameObject("ImpactSystem");
            impactObject.transform.SetParent(managers.transform, false);
            impactObject.AddComponent<ZombieHouse.Fx.ImpactSystem>();

            var jungleObject = new GameObject("Jungle");
            var jungle = jungleObject.AddComponent<JungleGenerator>();

            var navMeshObject = new GameObject("NavMesh");
            navMeshObject.transform.SetParent(managers.transform, false);
            var baker = navMeshObject.AddComponent<RuntimeNavMeshBaker>();

            var spawnerObject = new GameObject("ZombieSpawner");
            spawnerObject.transform.SetParent(managers.transform, false);
            var spawner = spawnerObject.AddComponent<ZombieSpawner>();

            // A quarter of the valley is jaguars — fewer than the wood's bears, because a
            // jaguar covers ground about twice as fast as a bear does and two at once is
            // already more than you can turn between.
            spawner.ConfigureBeasts(jaguarPrefab, 0.26f, ZombieKind.Jaguar);

            // Monkeys come out of a tree together or they are nothing at all.
            spawner.ConfigurePacks(monkeyPrefab, 0.34f, ZombieKind.Monkey, 3, 5);

            // The snakes are placed, not rolled: the level knows which fern beds and which
            // reeds they are in, and that is the entire point of them.
            spawner.ConfigureLurkers(snakePrefab, ZombieKind.Snake, null);

            var directorObject = new GameObject("LevelDirector");
            directorObject.transform.SetParent(managers.transform, false);
            var director = directorObject.AddComponent<LevelDirector>();

            var so = new SerializedObject(director);
            AssignReference(so, "levelSourceBehaviour", jungle);
            AssignReference(so, "spawner", spawner);
            AssignReference(so, "navMeshBaker", baker);
            AssignReference(so, "player", player.transform);
            AssignReference(so, "zombiePrefab", zombiePrefab);
            // The boss — the valley: the fastest of them, and the one you cannot walk away from.
            AssignReference(so, "bossPrefab", jaguarPrefab);
            so.FindProperty("bossKind").enumValueIndex = (int)ZombieKind.BossJaguar;
            so.ApplyModifiedPropertiesWithoutUndo();

            jungle.Generate();
        }

        [MenuItem("Zombie House/Verify Jungle", false, 29)]
        public static void VerifyJungle()
        {
            if (!File.Exists(JungleScenePath))
            {
                Debug.LogError("[Jungle] No scene at " + JungleScenePath + " — run Build Level 6 Jungle first.");
                return;
            }

            EditorSceneManager.OpenScene(JungleScenePath, OpenSceneMode.Single);

            var jungle = Object.FindAnyObjectByType<JungleGenerator>();
            var baker = Object.FindAnyObjectByType<RuntimeNavMeshBaker>();
            if (jungle == null || baker == null)
            {
                Debug.LogError("[Jungle] Scene is missing the JungleGenerator or RuntimeNavMeshBaker.");
                return;
            }

            ProtoMaterials.ClearCache();
            jungle.Generate();
            baker.SetBakeVolume(jungle.LevelBounds.center, jungle.LevelBounds.size);
            baker.Bake();

            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            int triangles = triangulation.indices.Length / 3;
            Debug.Log($"[Jungle] NavMesh: {triangulation.vertices.Length} vertices, {triangles} triangles.");
            Debug.Log($"[Jungle] Contents: {jungle.AmmoSpawns.Count} ammo, {jungle.MedkitSpawns.Count} medkits, " +
                      $"{jungle.BatterySpawns.Count} batteries, " +
                      $"{jungle.HidingSpots.Count} places to lurk.");

            if (triangles == 0)
            {
                Debug.LogError("[Jungle] NavMesh is empty — nothing will move.");
                return;
            }

            int problems = 0;

            NavMeshHit startHit;
            bool startOk = NavMesh.SamplePosition(jungle.PlayerSpawn, out startHit, 4f, NavMesh.AllAreas);
            if (!startOk)
            {
                Debug.LogError("[Jungle] The start clearing is not on navigable ground.");
                problems++;
            }

            NavMeshHit exitHit;
            if (startOk && NavMesh.SamplePosition(jungle.ExitPosition, out exitHit, 5f, NavMesh.AllAreas))
            {
                var path = new NavMeshPath();
                NavMesh.CalculatePath(startHit.position, exitHit.position, NavMesh.AllAreas, path);

                if (path.status == NavMeshPathStatus.PathComplete)
                {
                    float length = 0f;
                    for (int i = 1; i < path.corners.Length; i++)
                        length += Vector3.Distance(path.corners[i - 1], path.corners[i]);

                    Debug.Log($"[Jungle] The temple gate is reachable — {length:0} m from the start.");
                }
                else
                {
                    Debug.LogError($"[Jungle] The gate cannot be reached ({path.status}).");
                    problems++;
                }
            }
            else if (startOk)
            {
                Debug.LogError("[Jungle] The gate is not on navigable ground.");
                problems++;
            }

            int reachable = 0;
            foreach (Vector3 spawn in jungle.ZombieSpawns)
            {
                NavMeshHit spawnHit;
                if (!NavMesh.SamplePosition(spawn, out spawnHit, 4f, NavMesh.AllAreas))
                {
                    Debug.LogError($"[Jungle] Spawn at {spawn} is not on navigable ground — a prop has covered it.");
                    continue;
                }

                if (!startOk) continue;

                var path = new NavMeshPath();
                NavMesh.CalculatePath(spawnHit.position, startHit.position, NavMesh.AllAreas, path);

                if (path.status == NavMeshPathStatus.PathComplete) reachable++;
                else Debug.LogError($"[Jungle] Spawn at {spawn} cannot reach the start ({path.status}).");
            }

            Debug.Log($"[Jungle] Enemy spawns reaching the start: {reachable}/{jungle.ZombieSpawns.Count}");
            if (reachable < jungle.ZombieSpawns.Count) problems++;

            problems += CheckRiverCrossings(jungle, startOk ? startHit.position : jungle.PlayerSpawn, startOk);
            problems += CheckSnakeNests(jungle);
            problems += CheckJungleMusic();
            problems += CheckSurvivors(jungle, "Jungle");
            problems += CheckSidearm("Jungle");
            problems += CheckKillQuota("Jungle");
            problems += CheckPostFx("Jungle");
            problems += CheckPowerRoute(jungle, "Jungle");
            problems += CheckPowerUps(jungle, "Jungle");
            problems += CheckBeltCrates(jungle, "Jungle");
            problems += CheckTorchOnPlayer();

            Debug.Log(problems == 0
                ? "[Jungle] PASS — the valley is walkable end to end."
                : $"[Jungle] FAIL — {problems} problem(s).");
        }

        /// <summary>
        /// The river has to be crossable and it has to be worth calling a river.
        ///
        /// Both halves matter. A channel nothing can cross seals the level — that is the
        /// school's three sealed rooms again, in a shape nobody would think to look at.
        /// And a channel everything can walk straight through is a texture on the floor:
        /// if the far bank is no further away than the near one, the river cost the player
        /// nothing and the crossings are decoration.
        /// </summary>
        private static int CheckRiverCrossings(JungleGenerator jungle, Vector3 start, bool startOk)
        {
            if (!startOk) return 0;

            // The river runs the other diagonal, so the far bank is the direction from the
            // start through the middle of the valley. Six probes spread along it, so one
            // unlucky trunk does not read as a sealed river.
            Vector3 centre = jungle.LevelBounds.center;
            Vector3 outward = centre - start;
            outward.y = 0f;
            outward.Normalize();

            Vector3 along = Vector3.Cross(outward, Vector3.up).normalized;

            int reached = 0, sampled = 0;
            float furthest = 0f;

            for (int i = 0; i < 6; i++)
            {
                Vector3 probe = centre + outward * 26f + along * ((i - 2.5f) * 13f);

                NavMeshHit hit;
                if (!NavMesh.SamplePosition(probe, out hit, 9f, NavMesh.AllAreas)) continue;
                sampled++;

                var path = new NavMeshPath();
                NavMesh.CalculatePath(start, hit.position, NavMesh.AllAreas, path);
                if (path.status != NavMeshPathStatus.PathComplete) continue;

                reached++;

                float walked = 0f;
                for (int c = 1; c < path.corners.Length; c++)
                    walked += Vector3.Distance(path.corners[c - 1], path.corners[c]);

                furthest = Mathf.Max(furthest, walked);
            }

            if (sampled == 0)
            {
                Debug.LogError("[Jungle] No navigable ground on the far bank at all.");
                return 1;
            }

            if (reached == 0)
            {
                Debug.LogError("[Jungle] Nothing on the far bank can be walked to — the river has sealed the valley.");
                return 1;
            }

            Debug.Log($"[Jungle] The far bank is reachable at {reached}/{sampled} probes; " +
                      $"the longest crossing walk is {furthest:0} m.");
            return 0;
        }

        /// <summary>
        /// The snakes have to be somewhere a snake would be — in the fern beds and at the
        /// crossings — and every one of them has to be on ground it can move off.
        ///
        /// A snake stranded inside a trunk is invisible and immortal, which is a much
        /// worse bug than one that never spawned: nothing about the level looks wrong, and
        /// the exit simply never opens because the count never falls.
        /// </summary>
        private static int CheckSnakeNests(JungleGenerator jungle)
        {
            if (jungle.SnakeSpawns.Count < 6)
            {
                Debug.LogError($"[Jungle] Only {jungle.SnakeSpawns.Count} snakes in the valley; " +
                               "the ground is supposed to be the threat here.");
                return 1;
            }

            int placed = 0;
            foreach (Vector3 nest in jungle.SnakeSpawns)
            {
                NavMeshHit hit;
                if (NavMesh.SamplePosition(nest, out hit, 4f, NavMesh.AllAreas)) placed++;
            }

            // The fern beds are scattered without regard to what else is there, so a few
            // landing badly is expected; most of them failing is a real fault.
            if (placed < jungle.SnakeSpawns.Count * 0.7f)
            {
                Debug.LogError($"[Jungle] Only {placed} of {jungle.SnakeSpawns.Count} snake nests are on " +
                               "navigable ground — most of them would never appear.");
                return 1;
            }

            Debug.Log($"[Jungle] Snakes: {placed} of {jungle.SnakeSpawns.Count} nests placed, " +
                      $"and {jungle.CanopySpawns.Count} trees tall enough for the monkeys.");
            return 0;
        }

        /// <summary>
        /// The valley runs its own bed. Worth failing over for the same reason the tomb's
        /// is: the track is a serialized enum on a component in a scene, so a rebuild with
        /// a stale field would quietly put the wood's wind back and nothing would look
        /// wrong.
        /// </summary>
        private static int CheckJungleMusic()
        {
            var audio = Object.FindAnyObjectByType<ZombieHouse.Audio.GameAudio>();
            if (audio == null)
            {
                Debug.LogError("[Jungle] No GameAudio in the scene — the level is silent.");
                return 1;
            }

            var track = new SerializedObject(audio).FindProperty("musicTrack");
            var selected = (ZombieHouse.Audio.Sfx)track.enumValueIndex;

            if (selected != ZombieHouse.Audio.Sfx.MusicJungle)
            {
                Debug.LogError($"[Jungle] The valley is playing {selected}, not its own bed.");
                return 1;
            }

            Debug.Log("[Jungle] Music: MusicJungle — cicadas that cut out, a hollow log, calls that stop.");
            return 0;
        }

        [MenuItem("Zombie House/Verify Forest", false, 25)]
        public static void VerifyForest()
        {
            if (!File.Exists(ForestScenePath))
            {
                Debug.LogError("[Forest] No scene at " + ForestScenePath + " — run Build Level 2 Forest first.");
                return;
            }

            EditorSceneManager.OpenScene(ForestScenePath, OpenSceneMode.Single);

            var forest = Object.FindAnyObjectByType<ForestGenerator>();
            var baker = Object.FindAnyObjectByType<RuntimeNavMeshBaker>();
            if (forest == null || baker == null)
            {
                Debug.LogError("[Forest] Scene is missing the ForestGenerator or RuntimeNavMeshBaker.");
                return;
            }

            ProtoMaterials.ClearCache();
            forest.Generate();
            baker.SetBakeVolume(forest.LevelBounds.center, forest.LevelBounds.size);
            baker.Bake();

            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            int triangles = triangulation.indices.Length / 3;
            Debug.Log($"[Forest] NavMesh: {triangulation.vertices.Length} vertices, {triangles} triangles.");
            Debug.Log($"[Forest] Contents: {forest.AmmoSpawns.Count} ammo, {forest.MedkitSpawns.Count} medkits, " +
                      $"{forest.BatterySpawns.Count} batteries, " +
                      $"{forest.HidingSpots.Count} places to lurk.");

            if (triangles == 0)
            {
                Debug.LogError("[Forest] NavMesh is empty — nothing will move.");
                return;
            }

            int problems = 0;

            NavMeshHit startHit;
            bool startOk = NavMesh.SamplePosition(forest.PlayerSpawn, out startHit, 4f, NavMesh.AllAreas);
            if (!startOk)
            {
                Debug.LogError("[Forest] The starting clearing is not on navigable ground.");
                problems++;
            }

            // The whole point of the level is crossing it, so that has to be possible.
            NavMeshHit exitHit;
            if (startOk && NavMesh.SamplePosition(forest.ExitPosition, out exitHit, 6f, NavMesh.AllAreas))
            {
                var path = new NavMeshPath();
                NavMesh.CalculatePath(startHit.position, exitHit.position, NavMesh.AllAreas, path);

                if (path.status == NavMeshPathStatus.PathComplete)
                {
                    float length = 0f;
                    for (int i = 1; i < path.corners.Length; i++)
                        length += Vector3.Distance(path.corners[i - 1], path.corners[i]);

                    Debug.Log($"[Forest] Trail out is walkable — {length:F0} m from the clearing to the trail head.");
                }
                else
                {
                    Debug.LogError($"[Forest] The trail out cannot be reached ({path.status}).");
                    problems++;
                }
            }
            else if (startOk)
            {
                Debug.LogError("[Forest] The exit is not on navigable ground.");
                problems++;
            }

            int reachable = 0;
            foreach (Vector3 spawn in forest.ZombieSpawns)
            {
                NavMeshHit spawnHit;
                if (!NavMesh.SamplePosition(spawn, out spawnHit, 5f, NavMesh.AllAreas)) continue;
                if (!startOk) continue;

                var path = new NavMeshPath();
                NavMesh.CalculatePath(spawnHit.position, startHit.position, NavMesh.AllAreas, path);
                if (path.status == NavMeshPathStatus.PathComplete) reachable++;
            }

            Debug.Log($"[Forest] Marked spawns that can reach you: {reachable}/{forest.ZombieSpawns.Count}");
            if (reachable < forest.ZombieSpawns.Count * 0.8f)
            {
                Debug.LogError("[Forest] Too many spawns are cut off — the wood is fragmented.");
                problems++;
            }

            problems += CheckSurvivors(forest, "Forest");
            problems += CheckSidearm("Forest");
            problems += CheckKillQuota("Forest");
            problems += CheckPostFx("Forest");
            problems += CheckPowerRoute(forest, "Forest");
            problems += CheckPowerUps(forest, "Forest");
            problems += CheckBeltCrates(forest, "Forest");
            problems += CheckTorchOnPlayer();
            problems += CheckNightLighting();

            Debug.Log(problems == 0
                ? "[Forest] PASS — the wood is crossable."
                : $"[Forest] FAIL — {problems} problem(s).");
        }

        [MenuItem("Zombie House/Regenerate House Geometry", false, 20)]
        public static void RegenerateHouse()
        {
            var house = Object.FindAnyObjectByType<HouseGenerator>();
            if (house == null)
            {
                Debug.LogWarning("[ZombieHouse] No HouseGenerator in the open scene.");
                return;
            }

            ProtoMaterials.ClearCache();
            house.Generate();
            EditorSceneManager.MarkSceneDirty(house.gameObject.scene);
            Debug.Log("[ZombieHouse] House geometry regenerated.");
        }

        /// <summary>
        /// Bakes the NavMesh in edit mode and checks the level is actually playable:
        /// every spawn point stands on navigable floor, and the exit is reachable from
        /// the player start. Run this after editing the floor plan — a stray '#' can seal
        /// a room off, and this catches it in a second instead of mid-playtest.
        /// </summary>
        [MenuItem("Zombie House/Verify Level", false, 22)]
        public static void VerifyLevel()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogError("[Verify] No scene at " + ScenePath + " — run Build Level 1 Scene first.");
                return;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var house = Object.FindAnyObjectByType<HouseGenerator>();
            var baker = Object.FindAnyObjectByType<RuntimeNavMeshBaker>();
            if (house == null || baker == null)
            {
                Debug.LogError("[Verify] Scene is missing the HouseGenerator or RuntimeNavMeshBaker.");
                return;
            }

            ProtoMaterials.ClearCache();
            house.Generate();
            baker.SetBakeVolume(house.HouseBounds.center, house.HouseBounds.size);
            baker.Bake();

            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            int triangles = triangulation.indices.Length / 3;
            Debug.Log($"[Verify] House: {house.FloorCount} storey(s), {house.FloorSpacing:F1} m apart.");
            Debug.Log($"[Verify] NavMesh: {triangulation.vertices.Length} vertices, {triangles} triangles.");

            // Spawns are spread across every storey, so the reachability check below is
            // also the staircase test: if the stairs do not connect, upper-floor spawns
            // cannot path to the player and this fails.
            float highestSpawn = 0f;
            foreach (Vector3 spawn in house.ZombieSpawns)
                highestSpawn = Mathf.Max(highestSpawn, spawn.y);

            Debug.Log($"[Verify] Highest zombie spawn sits {highestSpawn:F1} m up — " +
                      $"storey {Mathf.RoundToInt(highestSpawn / Mathf.Max(0.01f, house.FloorSpacing))}.");

            if (triangles == 0)
            {
                Debug.LogError("[Verify] NavMesh is empty — zombies will not move.");
                return;
            }

            int problems = 0;

            NavMeshHit playerHit;
            bool playerOnMesh = NavMesh.SamplePosition(house.PlayerSpawn, out playerHit, 2f, NavMesh.AllAreas);
            if (!playerOnMesh)
            {
                Debug.LogError("[Verify] Player start ('P') is not on navigable floor.");
                problems++;
            }

            int reachableSpawns = 0;
            for (int i = 0; i < house.ZombieSpawns.Count; i++)
            {
                Vector3 spawn = house.ZombieSpawns[i];

                NavMeshHit spawnHit;
                if (!NavMesh.SamplePosition(spawn, out spawnHit, 3f, NavMesh.AllAreas))
                {
                    Debug.LogError($"[Verify] Zombie spawn {i} at {spawn} is not on the NavMesh.");
                    problems++;
                    continue;
                }

                if (!playerOnMesh) continue;

                var path = new NavMeshPath();
                NavMesh.CalculatePath(spawnHit.position, playerHit.position, NavMesh.AllAreas, path);

                if (path.status == NavMeshPathStatus.PathComplete) reachableSpawns++;
                else
                {
                    Debug.LogError($"[Verify] Zombie spawn {i} at {spawn} cannot reach the player start ({path.status}) — that room is sealed off.");
                    problems++;
                }
            }

            Debug.Log($"[Verify] Zombie spawns reaching the player start: {reachableSpawns}/{house.ZombieSpawns.Count}");

            if (house.HasExit && playerOnMesh)
            {
                NavMeshHit exitHit;
                if (NavMesh.SamplePosition(house.ExitPosition, out exitHit, 3f, NavMesh.AllAreas))
                {
                    var path = new NavMeshPath();
                    NavMesh.CalculatePath(playerHit.position, exitHit.position, NavMesh.AllAreas, path);

                    if (path.status == NavMeshPathStatus.PathComplete)
                        Debug.Log("[Verify] Exit ('E') is reachable from the player start.");
                    else
                    {
                        Debug.LogError($"[Verify] Exit is NOT reachable from the player start ({path.status}) — the level cannot be completed.");
                        problems++;
                    }
                }
                else
                {
                    Debug.LogError("[Verify] Exit ('E') is not on navigable floor.");
                    problems++;
                }
            }

            Debug.Log($"[Verify] Pickups: {house.AmmoSpawns.Count} ammo, {house.MedkitSpawns.Count} medkits, " +
                      $"{house.BatterySpawns.Count} batteries.");
            Debug.Log($"[Verify] Ambush positions found behind furniture and in corners: {house.HidingSpots.Count}.");

            if (house.BatterySpawns.Count == 0)
                Debug.LogWarning("[Verify] No batteries ('V') on the map — the torch cannot be resupplied.");

            problems += CheckSurvivors(house, "Verify");
            problems += CheckSidearm("Verify");
            problems += CheckKillQuota("Verify");
            problems += CheckPostFx("Verify");
            problems += CheckPowerRoute(house, "Verify");
            problems += CheckPowerUps(house, "Verify");
            problems += CheckBeltCrates(house, "Verify");
            problems += CheckTorchOnPlayer();
            Debug.Log(problems == 0
                ? "[Verify] PASS — level is playable."
                : $"[Verify] FAIL — {problems} problem(s) found.");
        }

        /// <summary>
        /// The tomb's two: a mummy that is wrapped rather than painted, and a scarab that
        /// is dog-sized, armoured on top and soft underneath.
        ///
        /// Also checks the two balance changes that came with them — the mop hits softer
        /// than it did, and the Uzi hits harder — because a number nobody asserts on is a
        /// number that drifts back the next time someone touches the file.
        /// </summary>
        [MenuItem("Zombie House/Test Pyramid", false, 35)]
        public static void TestPyramid()
        {
            int problems = 0;
            GameObject mummy = null, scarab = null;

            try
            {
                ZombieArchetype mummyType = null, scarabType = null, janitorType = null;
                foreach (ZombieArchetype a in ZombieArchetype.Catalogue)
                {
                    if (a.Kind == ZombieKind.Mummy) mummyType = a;
                    if (a.Kind == ZombieKind.Scarab) scarabType = a;
                    if (a.Kind == ZombieKind.Janitor) janitorType = a;
                }

                if (mummyType == null || scarabType == null)
                {
                    Debug.LogError("[Pyramid] The catalogue is missing the mummy or the scarab.");
                    Debug.Log("[Pyramid] FAIL — 1 problem(s).");
                    return;
                }

                // ---- the mummy ------------------------------------------------
                mummy = ZombieFactory.Create("MummyTest", ZombieOutfit.Mummy);

                int wraps = 0, looseEnds = 0, wrapColliders = 0;
                bool cleanBand = false, dirtyBand = false;

                foreach (Renderer r in mummy.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.name.StartsWith("Wrap_"))
                    {
                        wraps++;
                        if (r.sharedMaterial == ProtoMaterials.Bandage) cleanBand = true;
                        if (r.sharedMaterial == ProtoMaterials.BandageDark) dirtyBand = true;
                        if (r.GetComponent<Collider>() != null) wrapColliders++;
                    }
                    else if (r.name.StartsWith("LooseWrap"))
                    {
                        looseEnds++;
                    }
                }

                if (wraps < 25)
                {
                    Debug.LogError($"[Pyramid] The mummy has {wraps} bandage bands; it will read as naked.");
                    problems++;
                }
                else if (!cleanBand || !dirtyBand)
                {
                    Debug.LogError("[Pyramid] The bandages are all one tone — that reads as a bodysuit, not wrapping.");
                    problems++;
                }
                else if (wrapColliders > 0)
                {
                    Debug.LogError($"[Pyramid] {wrapColliders} bandage(s) carry colliders — linen would soak bullets.");
                    problems++;
                }
                else
                {
                    Debug.Log($"[Pyramid] Mummy: {wraps} bands in two tones, {looseEnds} trailing ends, no colliders on any of it.");
                }

                // ---- the scarab ----------------------------------------------
                scarab = ScarabFactory.Create("ScarabTest");

                var bounds = new Bounds(scarab.transform.position, Vector3.zero);
                foreach (Renderer r in scarab.GetComponentsInChildren<Renderer>(true))
                    bounds.Encapsulate(r.bounds);

                // Dog-sized: about a metre long and knee high. Not a beetle you step on,
                // and not something that fills a doorway.
                float length = Mathf.Max(bounds.size.x, bounds.size.z);
                if (length < 0.7f || length > 1.6f || bounds.size.y > 1.1f)
                {
                    Debug.LogError($"[Pyramid] The scarab measures {bounds.size.x:0.00} x {bounds.size.y:0.00} x {bounds.size.z:0.00} m — that is not dog-sized.");
                    problems++;
                }
                else
                {
                    Debug.Log($"[Pyramid] Scarab is {length:0.00} m long and {bounds.size.y:0.00} m tall — dog-sized.");
                }

                var rig = scarab.GetComponent<ZombieRig>();
                if (rig == null || rig.Bones == null || rig.Bones.Head == null
                    || rig.Bones.ShoulderLeft == null || rig.Bones.HipRight == null)
                {
                    Debug.LogError("[Pyramid] The scarab rig is missing bones the ragdoll needs.");
                    problems++;
                }
                else
                {
                    Debug.Log("[Pyramid] Scarab uses the shared bone names — ragdoll and dismemberment work unchanged.");
                }

                // The shell is the point: a rifle round should do less, not more.
                if (scarabType.RifleDamageMultiplier >= 1f)
                {
                    Debug.LogError($"[Pyramid] A rifle round does {scarabType.RifleDamageMultiplier:0.00}x to a scarab; the shell is supposed to blunt it.");
                    problems++;
                }
                else
                {
                    Debug.Log($"[Pyramid] Shell blunts rifle rounds to {scarabType.RifleDamageMultiplier:0.00}x — bring the gatling gun.");
                }

                if (scarabType.ChaseSpeed < 5f)
                {
                    Debug.LogError($"[Pyramid] A scarab chases at {scarabType.ChaseSpeed:0.0} m/s; it is supposed to close fast.");
                    problems++;
                }

                // ---- the balance pass -----------------------------------------
                if (janitorType != null)
                {
                    if (janitorType.AttackDamage > 26f)
                    {
                        Debug.LogError($"[Pyramid] The mop still hits for {janitorType.AttackDamage:0}; it was meant to come down.");
                        problems++;
                    }
                    else
                    {
                        Debug.Log($"[Pyramid] Mop damage is {janitorType.AttackDamage:0}, down from 34 — the threat is the reach, not the hit.");
                    }
                }

                // ---- the ceiling --------------------------------------------
                var climber = scarab.GetComponent<ScarabCeilingCrawler>();
                if (climber == null)
                {
                    Debug.LogError("[Pyramid] The scarab cannot climb — the ceiling is the whole point of it here.");
                    problems++;
                }
                else if (climber.IsClimbing)
                {
                    Debug.LogError("[Pyramid] A freshly built scarab thinks it is already on a wall.");
                    problems++;
                }
                else
                {
                    Debug.Log("[Pyramid] Scarabs carry the ceiling crawler and start on the floor.");
                }

                var mopRig = ZombieFactory.Create("MopAnimTest", ZombieOutfit.Janitor);
                try
                {
                    if (mopRig.GetComponent<MopAnimator>() == null)
                    {
                        Debug.LogError("[Pyramid] The janitor has no MopAnimator — the mop would be a stick taped on.");
                        problems++;
                    }
                    else
                    {
                        Debug.Log("[Pyramid] The janitor carries a MopAnimator, so the mop swings with the arm.");
                    }
                }
                finally
                {
                    Object.DestroyImmediate(mopRig);
                }
            }
            finally
            {
                if (mummy != null) Object.DestroyImmediate(mummy);
                if (scarab != null) Object.DestroyImmediate(scarab);
            }

            Debug.Log(problems == 0
                ? "[Pyramid] PASS — wrapped, armoured, dog-sized and swinging."
                : $"[Pyramid] FAIL — {problems} problem(s).");
        }

        /// <summary>
        /// The Uzi's whole life: granted with 200 rounds, counting down as it fires, and
        /// gone — weapon and slot both — the instant the last round leaves the barrel.
        ///
        /// The disappearing act is what needs a test. Everything else about a weapon
        /// fails loudly; a power-up that quietly keeps working after its last round, or
        /// one that vanishes a shot early and drops you to the sidearm mid-fight, would
        /// both look like "the gun feels wrong" rather than like a bug with a cause.
        /// </summary>
        /// <summary>
        /// The two weapons that are not simply "a gun": the belt-fed gatling that is part
        /// of the loadout, and the Uzi you find lying about.
        ///
        /// They are tested together because the interesting thing about each is what it
        /// does at zero rounds, and they must do *opposite* things. The gatling is yours
        /// from the first frame and has to survive running dry, or every belt crate in the
        /// level becomes worthless the moment you empty it in the first fight. The Uzi is
        /// scavenged and has to take itself away, or two of them a level stops being a
        /// power-up and becomes a third permanent weapon.
        ///
        /// This test used to build a gatling gun in the power-up slot and assert it was
        /// discarded — which was true of the rig it had just built and false of the one
        /// the game ships, so it passed while the shipped loadout said something else
        /// entirely. The rig here is now laid out exactly as BuildPlayerRig lays it out:
        /// three slots, the Uzi stowed beside them.
        /// </summary>
        /// <summary>
        /// The jungle's three: that they are the right colours, the right sizes, and that
        /// the snake makes no footsteps.
        ///
        /// The colour half exists because of a bug with no visible cause in code. Every
        /// creature in the valley shipped **magenta**, and nothing about the factories was
        /// wrong: `ProtoMaterials.Get` falls back to building a material in memory when the
        /// .mat asset is missing, an in-memory material does not survive being saved into a
        /// prefab, and a prefab with a dangling material reference renders as Unity's
        /// missing-material pink. The valley itself looked correct throughout, because its
        /// geometry is generated at Awake in play mode where in-memory materials work fine
        /// — so the only thing that broke was the part that gets saved to disk.
        ///
        /// The check is therefore not "is it green". It is **"does every renderer point at
        /// a material that exists as an asset"**, which is the actual invariant and catches
        /// the whole class for any future creature.
        /// </summary>
        /// <summary>
        /// The post stack: that the shader compiles, that it is on the right camera with
        /// the headroom it needs, and — where there is a GPU to ask — that light actually
        /// bleeds out of a bright pixel into its neighbours.
        ///
        /// The last part is the only one that proves anything. Checking that a component
        /// exists and its fields are sane is checking the wiring, and the wiring was never
        /// the risky bit: a shader with a typo in a property name compiles, binds nothing,
        /// and renders a perfectly plausible slightly-wrong frame. So this pushes a known
        /// image through the real render path and reads the pixels back.
        ///
        /// Under `-nographics` there is no graphics device and no Blit, so the pixel half
        /// is skipped and says so rather than passing quietly. Run it with `-batchmode`
        /// alone to get the full check.
        /// </summary>
        /// <summary>
        /// The prop-mesh seam: that a missing mesh is harmless, that a present one is the
        /// right size, and — the important one — that dressing a prop never touches its
        /// collider.
        ///
        /// That last check is what makes it safe to replace hundreds of boxes with meshes a
        /// few at a time. The six level verifications all rest on the NavMesh, the NavMesh
        /// bakes from colliders, and the day a prettier rock quietly changes a collider is
        /// the day a spawn gets walled off somewhere nobody looks. So the rule is that
        /// PropLibrary swaps the *rendered mesh* and nothing else, and this asserts it
        /// rather than trusting it.
        ///
        /// Most of the prop set does not exist yet, and that is not a failure — a missing
        /// mesh leaves the box exactly as it was. The test reports the count so the
        /// migration is visible, and only fails on a mesh that is present and wrong.
        /// </summary>
        /// <summary>
        /// The music, measured rather than compiled.
        ///
        /// A synthesised score is the easiest thing in this project to break silently. Every
        /// bed is a few hundred lines of arithmetic over a buffer of floats, and arithmetic
        /// that is wrong does not throw — it produces a clip that is silent, or clipped to a
        /// square wave, or identical to another level's. All three compile perfectly and all
        /// three are only noticed by someone playing with the sound on.
        ///
        /// So this builds the real bank and looks at the samples:
        ///
        /// **Every bed exists, loops, and is the right length.** A missing entry falls back
        /// to silence at runtime with no error.
        ///
        /// **Bed and tension lengths match, per level.** They are started on one dsp tick
        /// and looped forever; a mismatch of even a second means they drift apart over a
        /// long session, which sounds like nothing in particular going gradually wrong.
        ///
        /// **Nothing is silent and nothing is crushed.** RMS in a sane band catches both a
        /// layer that never got mixed in and one that has been normalised into a wall.
        ///
        /// **No two beds are the same clip.** Six levels that all play the mansion is
        /// exactly the bug that shipped for months when the town and the school were left
        /// on the default track, and it is invisible unless you play two levels in a row.
        /// </summary>
        /// <summary>
        /// The safe start: nothing can reach you, and nothing may attack, when a level opens.
        ///
        /// This is a promise to the player rather than a look, so it is worth testing as
        /// one. Every level drops you in cold — no torch found, no idea which way the exit
        /// is — and the first thing that happens should not be damage you had no way to
        /// avoid. Two mechanisms enforce it and both are checked here, because either alone
        /// leaves a hole:
        ///
        /// **A clear radius.** `PopulateHouse` leaves markers inside it empty. This is the
        /// structural half, and it was missing entirely — the trickle spawner respected a
        /// minimum distance but `PopulateHouse`, which is what every level actually uses,
        /// placed one at every marker regardless of how close it was.
        ///
        /// **An opening grace.** Nothing attacks or wakes on proximity for the first few
        /// seconds. This covers what the radius cannot: something already walking towards
        /// the start, or a marker just outside the line.
        ///
        /// The per-level half also reports how much population each level loses to the
        /// radius, because a level whose markers cluster near its entrance would quietly
        /// lose half its zombies and only a count makes that visible.
        /// </summary>
        /// <summary>
        /// The bosses: six archetypes, six levels wired to them, and an exit that waits.
        ///
        /// The boss is now a *fourth* exit condition, which makes it the single most
        /// dangerous thing added to this project: get it wrong and the door never opens, the
        /// level is unfinishable, and nothing about it looks broken — the player just walks
        /// back and forth having done everything they were asked. So this checks the shape
        /// of the gate rather than trusting it.
        ///
        /// It also checks the numbers, because a boss is only a boss if it is one. A giant
        /// that dies to two magazines is a large zombie, and the difference between the two
        /// lives entirely in health and stagger resistance.
        /// </summary>
        [MenuItem("Zombie House/Test Boss", false, 41)]
        public static void TestBoss()
        {
            int problems = 0;

            var bosses = new[]
            {
                ZombieKind.BossZombie, ZombieKind.BossBear, ZombieKind.BossHorse,
                ZombieKind.BossJanitor, ZombieKind.BossScarab, ZombieKind.BossJaguar,
            };

            foreach (ZombieKind kind in bosses)
            {
                ZombieArchetype boss = null;
                foreach (ZombieArchetype a in ZombieArchetype.Catalogue)
                    if (a.Kind == kind) boss = a;

                if (boss == null)
                {
                    Debug.LogError($"[Boss] No archetype for {kind}.");
                    problems++;
                    continue;
                }

                // Never drawn at random. A boss that turned up in the ordinary population
                // would be an enormous surprise in the worst sense.
                if (boss.Weight != 0f)
                {
                    Debug.LogError($"[Boss] {boss.Name} has weight {boss.Weight} and can be rolled " +
                                   "into the normal population.");
                    problems++;
                }

                if (boss.Health < 900f)
                {
                    Debug.LogError($"[Boss] {boss.Name} has {boss.Health:0} health — that is a big " +
                                   "zombie, not a boss.");
                    problems++;
                }

                // The ceiling matters as much as the floor, and this is the assertion that
                // was missing. At 2200-3000 the fight was a minute and a half of holding the
                // trigger on something that could not be staggered, interrupted or escaped.
                // Long is not the same as hard; it is just long.
                if (boss.Health > 1800f)
                {
                    Debug.LogError($"[Boss] {boss.Name} has {boss.Health:0} health — roughly " +
                                   $"{boss.Health / 34f:0} gatling hits landed with it standing on " +
                                   "you. That is an endurance test, not a fight.");
                    problems++;
                }

                // Stunlocking is the way every boss fight dies. If it can be held in a
                // stagger loop by a fast weapon the whole encounter evaporates.
                if (boss.StaggerResistance < 0.70f)
                {
                    Debug.LogError($"[Boss] {boss.Name} staggers at {boss.StaggerResistance:P0} " +
                                   "resistance — the gatling gun would hold it still until it died.");
                    problems++;
                }

                // But total immunity is its own problem. Above ~0.90 nothing the player does
                // produces any visible reaction at all, so there is no feedback that the
                // fight is being won — just a health bar they cannot see.
                if (boss.StaggerResistance > 0.88f)
                {
                    Debug.LogError($"[Boss] {boss.Name} is {boss.StaggerResistance:P0} stagger " +
                                   "resistant — nothing the player does visibly affects it, so the " +
                                   "fight reads as futile even while they are winning it.");
                    problems++;
                }

                if (boss.Scale < 1.8f)
                {
                    Debug.LogError($"[Boss] {boss.Name} is only {boss.Scale:0.0}x scale.");
                    problems++;
                }

                problems += CheckBossIsEscapable(boss);
                problems += CheckBossLeavesAMistakeBudget(boss);

                Debug.Log($"[Boss] {boss.Name}: {boss.Health:0} health, {boss.Scale:0.0}x, " +
                          $"{boss.AttackDamage:0} a hit at {boss.AttackRange:0.0} m, " +
                          $"{boss.StaggerResistance:P0} stagger resistance.");
            }

            problems += CheckBossGate();
            problems += CheckEveryLevelHasABoss();

            Debug.Log(problems == 0
                ? "[Boss] PASS — six giants, each guarding its own level's exit."
                : $"[Boss] FAIL — {problems} problem(s).");
        }

        /// <summary>
        /// You have to be able to walk away.
        ///
        /// Leaving is the player's only real tool for controlling a fight — break contact,
        /// get a corner between you, reload, come back at it. A boss whose chase speed sits
        /// above the player's sprint removes all of that at once and turns the encounter
        /// into a dice roll on however much health they happened to arrive with. Two of the
        /// six shipped that way: the Marshal's Horse at 8.0 m/s and the Green Mother at 7.4,
        /// against a 6.8 m/s sprint.
        ///
        /// The margin is deliberately small. Escaping should be a decision with a cost, not
        /// a stroll — it wants to be *just* possible.
        /// </summary>
        private static int CheckBossIsEscapable(ZombieArchetype boss)
        {
            const float PlayerSprint = 6.8f;   // PlayerController.sprintSpeed

            if (boss.ChaseSpeed < PlayerSprint) return 0;

            Debug.LogError($"[Boss] {boss.Name} chases at {boss.ChaseSpeed:0.0} m/s against a " +
                           $"{PlayerSprint:0.0} m/s sprint — the player cannot disengage, so there " +
                           "is no way to reposition, reload or retreat once it has seen them.");
            return 1;
        }

        /// <summary>
        /// How many hits you can take, given that you heal none of them.
        ///
        /// This is the check that would have caught the worst of it. On paper 55 damage
        /// against 100 health is a two-hit kill, which sounds punishing but survivable —
        /// until you notice PlayerHealth waits six seconds after the last hit before it
        /// regenerates anything. A boss attacking every two seconds means that timer never
        /// once elapses, so across the whole fight the player heals exactly zero.
        ///
        /// The real mistake budget was therefore one hit, and nothing in the game says so.
        /// Three or more is the target: enough that a mistake is a scare rather than a
        /// reload, and few enough that the fight stays frightening.
        /// </summary>
        private static int CheckBossLeavesAMistakeBudget(ZombieArchetype boss)
        {
            const float PlayerHealth = 100f;   // PlayerHealth.maxHealth
            const float RegenDelay = 6f;       // PlayerHealth.regenDelay

            int hitsToKill = Mathf.CeilToInt(PlayerHealth / Mathf.Max(1f, boss.AttackDamage));

            // Only meaningful while the boss attacks faster than the regen timer — which
            // every one of them does, but state it rather than assume it, because a genuinely
            // slow boss can be allowed to hit very much harder.
            bool healsBetweenHits = boss.AttackCooldown > RegenDelay;

            if (hitsToKill >= 3 || healsBetweenHits) return 0;

            Debug.LogError($"[Boss] {boss.Name} kills in {hitsToKill} hits ({boss.AttackDamage:0} " +
                           $"damage into {PlayerHealth:0} health) and attacks every " +
                           $"{boss.AttackCooldown:0.0}s — inside the {RegenDelay:0}s regen delay, so " +
                           "the player heals nothing for the whole fight. The mistake budget is " +
                           $"{hitsToKill - 1}.");
            return 1;
        }

        /// <summary>
        /// The gate itself: no boss means the exit behaves exactly as it always did, and a
        /// live boss holds it shut. Checked on the real component rather than by reading
        /// the source, because this is the condition that can strand a player.
        /// </summary>
        private static int CheckBossGate()
        {
            // With nothing in the scene there must be no boss and therefore no gate — this
            // is what keeps every existing test rig and every bossless level working.
            if (ZombieHouse.Enemies.LevelBoss.Current != null)
            {
                Debug.LogError("[Boss] A boss is registered with no level loaded; the static " +
                               "state has leaked between scenes.");
                return 1;
            }

            Debug.Log("[Boss] With no boss in the scene the exit gate is inert, so levels " +
                      "without one are unaffected.");
            return 0;
        }

        /// <summary>Every level's director actually got a boss prefab and a kind.</summary>
        private static int CheckEveryLevelHasABoss()
        {
            var levels = new (string Tag, string Scene, ZombieKind Kind)[]
            {
                ("Verify", ScenePath, ZombieKind.BossZombie),
                ("Forest", ForestScenePath, ZombieKind.BossBear),
                ("Town", TownScenePath, ZombieKind.BossHorse),
                ("School", SchoolScenePath, ZombieKind.BossJanitor),
                ("Pyramid", PyramidScenePath, ZombieKind.BossScarab),
                ("Jungle", JungleScenePath, ZombieKind.BossJaguar),
            };

            int problems = 0;

            foreach (var level in levels)
            {
                if (!File.Exists(level.Scene))
                {
                    Debug.LogWarning($"[Boss] {level.Tag} has not been built; skipping.");
                    continue;
                }

                EditorSceneManager.OpenScene(level.Scene, OpenSceneMode.Single);

                var director = Object.FindAnyObjectByType<LevelDirector>();
                if (director == null)
                {
                    Debug.LogError($"[Boss] {level.Tag} has no LevelDirector.");
                    problems++;
                    continue;
                }

                var so = new SerializedObject(director);
                var prefab = so.FindProperty("bossPrefab");
                var kind = so.FindProperty("bossKind");

                if (prefab == null || prefab.objectReferenceValue == null)
                {
                    Debug.LogError($"[Boss] {level.Tag} has no boss prefab — its exit would open " +
                                   "on the usual three conditions with nothing guarding it.");
                    problems++;
                    continue;
                }

                var assigned = (ZombieKind)kind.enumValueIndex;
                if (assigned != level.Kind)
                {
                    Debug.LogError($"[Boss] {level.Tag} is set to {assigned}, expected {level.Kind}.");
                    problems++;
                    continue;
                }

                Debug.Log($"[Boss] {level.Tag}: {assigned} on {prefab.objectReferenceValue.name}.");
            }

            return problems;
        }

        /// <summary>
        /// The two new scares: bodies that get up, and lights that fail as you approach.
        ///
        /// Both are the kind of feature that looks fine in the editor and is wrong in play,
        /// because both are about *timing* relative to things the player has been promised.
        /// The checks that matter here are therefore not "does it work" but "does it refuse
        /// to work when it should":
        ///
        ///   * Nothing rises during the opening grace. A corpse sitting up at your feet in
        ///     the first six seconds breaks the safe-start promise more comprehensively than
        ///     an ordinary zombie would, because you cannot even run from it.
        ///   * No zombie gets both scares. They are mutually exclusive and the combination
        ///     silently cancels: a body lying flat behind a closed door is hidden BY the
        ///     door, so the door opens on nothing and the corpse is never seen.
        ///   * A failed light stays failed, and the sun is never a candidate.
        /// </summary>
        [MenuItem("Zombie House/Test Dread", false, 42)]
        public static void TestDread()
        {
            int problems = 0;

            problems += CheckPlayDead();
            problems += CheckLightFailure();
            problems += CheckDreadSounds();
            problems += CheckScaresAreExclusive();

            Debug.Log(problems == 0
                ? "[Dread] PASS — bodies get up, lights go out, and neither happens too early."
                : $"[Dread] FAIL — {problems} problem(s).");
        }

        /// <summary>A body lies down, stays down when it should, and gets up when it should.</summary>
        private static int CheckPlayDead()
        {
            int problems = 0;

            GameObject zombie = ZombieHouse.Enemies.ZombieFactory.Create("PlayDeadProbe");

            try
            {
                Transform rig = zombie.transform.Find("Rig");
                if (rig == null)
                {
                    Debug.LogError("[Dread] The zombie has no Rig; PlayDead has nothing to lay down.");
                    return 1;
                }

                Vector3 standing = rig.localPosition;
                Quaternion upright = rig.localRotation;

                var playDead = zombie.AddComponent<ZombieHouse.Enemies.PlayDead>();
                playDead.Lie();

                // Deliberately NOT asserting that the rig moved downwards, which is what
                // this checked first and was wrong about. The Rig pivots at the feet, so a
                // correctly laid-out body barely moves its origin at all — it rotates. The
                // honest question is where the collision ended up, and that is measured
                // below against the floor rather than against the pivot.
                float pitch = Quaternion.Angle(upright, rig.localRotation);
                if (pitch < 60f)
                {
                    Debug.LogError($"[Dread] Prone rig is only {pitch:0}° off upright; it should " +
                                   "be lying down.");
                    problems++;
                }

                // The hit colliders must have come down with it, and must not have gone
                // through the floor. This catches both of the ways this can be wrong, which
                // need opposite fixes: a body still standing inside its own corpse, or one
                // buried under the floorboards where nothing can hit it either.
                //
                // Physics.SyncTransforms is load-bearing. collider.bounds is served from the
                // physics scene rather than from the transform hierarchy, and in edit mode
                // nothing steps physics — so without the sync every bounds read here reports
                // the standing pose no matter what Lie() did, and the check is measuring its
                // own stale cache.
                Physics.SyncTransforms();

                float floor = zombie.transform.position.y;
                float highest = float.MinValue;
                float lowest = float.MaxValue;
                int total = 0;

                foreach (Collider collider in zombie.GetComponentsInChildren<Collider>())
                {
                    total++;
                    Bounds bounds = collider.bounds;
                    highest = Mathf.Max(highest, bounds.max.y - floor);
                    lowest = Mathf.Min(lowest, bounds.min.y - floor);
                }

                if (total == 0)
                {
                    Debug.LogError("[Dread] The zombie has no colliders at all.");
                    problems++;
                }
                else
                {
                    Debug.Log($"[Dread] Prone body occupies {lowest:0.00} m to {highest:0.00} m " +
                              $"above its feet, across {total} colliders.");

                    // A body lying down is roughly as tall as a body is thick.
                    if (highest > 1.1f)
                    {
                        Debug.LogError($"[Dread] The prone body still reaches {highest:0.00} m — " +
                                       "it is lying down on screen and standing up in physics.");
                        problems++;
                    }

                    if (lowest < -0.20f)
                    {
                        Debug.LogError($"[Dread] The prone body reaches {lowest:0.00} m, well under " +
                                       "its own feet — it has sunk through the floor and cannot " +
                                       "be shot where it appears to lie.");
                        problems++;
                    }
                }

                if (playDead.HasRisen)
                {
                    Debug.LogError("[Dread] The body counts as risen before anything happened.");
                    problems++;
                }

                // Shooting a body that was only pretending has to work, or a player who has
                // learned the trick has no counter to it.
                playDead.Rise();

                if (!playDead.HasRisen)
                {
                    Debug.LogError("[Dread] Rise() did not take.");
                    problems++;
                }
                else
                {
                    Debug.Log("[Dread] A body lies face down, and gets up when disturbed.");
                }
            }
            finally
            {
                Object.DestroyImmediate(zombie);
            }

            return problems;
        }

        /// <summary>A light flickers, then dies, and does not come back.</summary>
        private static int CheckLightFailure()
        {
            int problems = 0;

            var lamp = new GameObject("FailingLamp");

            try
            {
                var light = lamp.AddComponent<Light>();
                light.type = LightType.Point;
                light.intensity = 1.4f;

                var failure = lamp.AddComponent<ZombieHouse.Fx.LightFailure>();
                failure.Initialise();

                if (failure.HasFailed)
                {
                    Debug.LogError("[Dread] The lamp starts failed.");
                    problems++;
                }

                failure.Trigger();

                // Driven by hand a frame at a time, because Time.time does not advance in a
                // batch run — an Update-driven flicker would sit on frame zero forever and
                // the test would pass by never reaching the end.
                bool flickered = false;
                for (int i = 0; i < 200 && !failure.HasFailed; i++)
                {
                    failure.Tick(0.016f);
                    if (light.intensity < 1.4f * 0.5f) flickered = true;
                }

                if (!flickered)
                {
                    Debug.LogError("[Dread] The lamp never dimmed on its way out — a bulb that " +
                                   "goes from full to black in one frame reads as a draw error.");
                    problems++;
                }

                if (!failure.HasFailed)
                {
                    Debug.LogError("[Dread] The lamp never finished failing after 3.2 seconds.");
                    problems++;
                }
                else if (light.enabled || light.intensity > 0.001f)
                {
                    Debug.LogError($"[Dread] A failed lamp is still lit (enabled={light.enabled}, " +
                                   $"intensity={light.intensity:0.000}).");
                    problems++;
                }
                else
                {
                    Debug.Log("[Dread] A lamp stutters for under a second, then goes for good.");
                }
            }
            finally
            {
                Object.DestroyImmediate(lamp);
            }

            return problems;
        }

        /// <summary>The two new clips exist, carry signal, and are not each other.</summary>
        private static int CheckDreadSounds()
        {
            int problems = 0;

            var bank = ZombieHouse.Audio.SoundBank.Build();

            foreach (ZombieHouse.Audio.Sfx sfx in new[]
                     { ZombieHouse.Audio.Sfx.ZombieRise, ZombieHouse.Audio.Sfx.LightPop })
            {
                if (!bank.TryGetValue(sfx, out AudioClip[] clips) || clips == null || clips.Length == 0)
                {
                    Debug.LogError($"[Dread] {sfx} has no clip. An Sfx member with no recipe is " +
                                   "silent at the moment it matters and throws nothing.");
                    problems++;
                    continue;
                }

                foreach (AudioClip clip in clips)
                {
                    var samples = new float[clip.samples * clip.channels];
                    clip.GetData(samples, 0);

                    double sum = 0.0;
                    foreach (float sample in samples) sum += sample * sample;
                    float rms = Mathf.Sqrt((float)(sum / Mathf.Max(1, samples.Length)));

                    if (rms < 0.01f)
                    {
                        Debug.LogError($"[Dread] {clip.name} is effectively silent (RMS {rms:0.0000}).");
                        problems++;
                    }
                    else
                    {
                        Debug.Log($"[Dread] {clip.name}: {clip.length:0.00}s, RMS {rms:0.000}.");
                    }
                }
            }

            return problems;
        }

        /// <summary>
        /// No zombie may carry both DoorAmbush and PlayDead.
        ///
        /// Driven on real components rather than by scanning a level, and that is the second
        /// version of this check. The first walked the built house counting zombies with
        /// both — and found none, because a saved scene contains no zombies at all: the
        /// spawner populates at runtime. It reported "0 ambushes, 0 playing dead, none both"
        /// and passed, and would have passed just as cheerfully with the exclusion deleted.
        ///
        /// The rule now lives in PlayDead.Lie(), which declines a zombie that already has a
        /// door to hide behind, so the real thing can be exercised in three lines instead of
        /// being approximated by a scan that had nothing to look at.
        /// </summary>
        private static int CheckScaresAreExclusive()
        {
            int problems = 0;

            GameObject zombie = ZombieHouse.Enemies.ZombieFactory.Create("ExclusivityProbe");

            try
            {
                // A zombie already committed to a door.
                zombie.AddComponent<ZombieHouse.Enemies.DoorAmbush>().Crouch();

                var playDead = zombie.AddComponent<ZombieHouse.Enemies.PlayDead>();

                if (playDead.Lie())
                {
                    Debug.LogError("[Dread] A door ambusher was also laid out as a corpse. The " +
                                   "door hides the body, so the door opens on nothing and the " +
                                   "body is never seen getting up — two scares spent for none.");
                    problems++;
                }
                else
                {
                    Debug.Log("[Dread] A zombie already behind a door refuses to also play dead.");
                }
            }
            finally
            {
                Object.DestroyImmediate(zombie);
            }

            // And the plain case still has to work, or the refusal above would pass just as
            // well by refusing everything.
            GameObject loner = ZombieHouse.Enemies.ZombieFactory.Create("LonerProbe");

            try
            {
                if (!loner.AddComponent<ZombieHouse.Enemies.PlayDead>().Lie())
                {
                    Debug.LogError("[Dread] A zombie with no door refused to play dead — the " +
                                   "exclusion is rejecting everything, so no level has this scare.");
                    problems++;
                }
                else
                {
                    Debug.Log("[Dread] A zombie with no door lies down as asked.");
                }
            }
            finally
            {
                Object.DestroyImmediate(loner);
            }

            return problems;
        }

        [MenuItem("Zombie House/Test Safe Start", false, 40)]
        public static void TestSafeStart()
        {
            int problems = 0;

            // ---- the grace ------------------------------------------------
            var rig = new GameObject("SafeStartRig");
            try
            {
                var manager = rig.AddComponent<GameManager>();
                var so = new SerializedObject(manager);

                var grace = so.FindProperty("openingGraceSeconds");
                if (grace == null)
                {
                    Debug.LogError("[SafeStart] GameManager has no openingGraceSeconds.");
                    problems++;
                }
                else if (grace.floatValue < 3f)
                {
                    Debug.LogError($"[SafeStart] The opening grace is {grace.floatValue:0.0}s — " +
                                   "too short to find your feet in.");
                    problems++;
                }
                else
                {
                    Debug.Log($"[SafeStart] Opening grace: {grace.floatValue:0.0}s with nothing " +
                              "able to attack or wake.");
                }

                // With no manager running, combat must be allowed — a stripped test rig has
                // to behave exactly as it always did.
                if (!GameManager.CombatAllowed)
                {
                    Debug.LogError("[SafeStart] Combat is disallowed with no live manager; " +
                                   "every existing test rig would silently stop fighting.");
                    problems++;
                }
            }
            finally
            {
                Object.DestroyImmediate(rig);
            }

            // ---- the radius, per level -------------------------------------
            problems += CheckSafeRadius();

            Debug.Log(problems == 0
                ? "[SafeStart] PASS — a clear radius at every start, and a grace over the top."
                : $"[SafeStart] FAIL — {problems} problem(s).");
        }

        /// <summary>
        /// How many of each level's spawn markers fall inside the safe radius, and whether
        /// that costs the level too much of its population.
        /// </summary>
        private static int CheckSafeRadius()
        {
            var spawner = new GameObject("RadiusProbe").AddComponent<ZombieSpawner>();
            float radius;

            try
            {
                var field = new SerializedObject(spawner).FindProperty("safeStartRadius");
                if (field == null)
                {
                    Debug.LogError("[SafeStart] ZombieSpawner has no safeStartRadius.");
                    return 1;
                }

                radius = field.floatValue;
            }
            finally
            {
                Object.DestroyImmediate(spawner.gameObject);
            }

            if (radius < 8f)
            {
                Debug.LogError($"[SafeStart] The safe radius is {radius:0} m — a zombie can " +
                               "cross that before you have finished reading the objective.");
                return 1;
            }

            int problems = 0;

            var levels = new (string Tag, string Scene)[]
            {
                ("Verify", ScenePath), ("Forest", ForestScenePath), ("Town", TownScenePath),
                ("School", SchoolScenePath), ("Pyramid", PyramidScenePath), ("Jungle", JungleScenePath),
            };

            foreach (var level in levels)
            {
                if (!File.Exists(level.Scene))
                {
                    Debug.LogWarning($"[SafeStart] {level.Tag} has not been built; skipping.");
                    continue;
                }

                EditorSceneManager.OpenScene(level.Scene, OpenSceneMode.Single);

                ILevelSource source = null;
                foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (behaviour is ILevelSource candidate) { source = candidate; break; }
                }

                if (source == null)
                {
                    Debug.LogError($"[SafeStart] {level.Tag} has no level source.");
                    problems++;
                    continue;
                }

                ProtoMaterials.ClearCache();
                source.Generate();

                int inside = 0;
                foreach (Vector3 spawn in source.ZombieSpawns)
                    if (Vector3.Distance(spawn, source.PlayerSpawn) < radius) inside++;

                int total = source.ZombieSpawns.Count;
                float share = total == 0 ? 0f : inside / (float)total;

                // Losing a couple of markers is the intended cost. Losing most of them means
                // the level's markers are clustered at its entrance and the safe start has
                // gutted its population rather than trimmed it.
                if (share > 0.5f)
                {
                    Debug.LogError($"[SafeStart] {level.Tag}: {inside} of {total} spawn markers are " +
                                   $"inside the {radius:0} m safe radius — that is most of the level's " +
                                   "population, so the markers need moving rather than skipping.");
                    problems++;
                }
                else
                {
                    Debug.Log($"[SafeStart] {level.Tag}: {inside} of {total} markers fall inside " +
                              $"{radius:0} m and are left empty.");
                }
            }

            return problems;
        }

        [MenuItem("Zombie House/Test Music", false, 39)]
        public static void TestMusic()
        {
            int problems = 0;

            var bank = ZombieHouse.Audio.SoundBank.Build();

            // bed, tension, expected seconds
            var levels = new[]
            {
                new { Name = "House",  Bed = ZombieHouse.Audio.Sfx.Music,       Tension = ZombieHouse.Audio.Sfx.TensionHouse,   Seconds = 48f },
                new { Name = "Forest", Bed = ZombieHouse.Audio.Sfx.MusicForest, Tension = ZombieHouse.Audio.Sfx.TensionForest,  Seconds = 56f },
                new { Name = "Town",   Bed = ZombieHouse.Audio.Sfx.MusicTown,   Tension = ZombieHouse.Audio.Sfx.TensionTown,    Seconds = 56f },
                new { Name = "School", Bed = ZombieHouse.Audio.Sfx.MusicSchool, Tension = ZombieHouse.Audio.Sfx.TensionSchool,  Seconds = 48f },
                new { Name = "Tomb",   Bed = ZombieHouse.Audio.Sfx.MusicTomb,   Tension = ZombieHouse.Audio.Sfx.TensionTomb,    Seconds = 64f },
                new { Name = "Jungle", Bed = ZombieHouse.Audio.Sfx.MusicJungle, Tension = ZombieHouse.Audio.Sfx.TensionJungle,  Seconds = 72f },
            };

            var fingerprints = new List<KeyValuePair<string, float>>();

            foreach (var level in levels)
            {
                AudioClip bed = Take(bank, level.Bed);
                AudioClip tension = Take(bank, level.Tension);

                if (bed == null || tension == null)
                {
                    Debug.LogError($"[Music] {level.Name} is missing a {(bed == null ? "bed" : "tension layer")}.");
                    problems++;
                    continue;
                }

                if (Mathf.Abs(bed.length - level.Seconds) > 0.5f)
                {
                    Debug.LogError($"[Music] {level.Name}'s bed is {bed.length:0.0}s, expected {level.Seconds:0}s.");
                    problems++;
                }

                if (Mathf.Abs(bed.length - tension.length) > 0.25f)
                {
                    Debug.LogError($"[Music] {level.Name}: bed {bed.length:0.0}s against tension " +
                                   $"{tension.length:0.0}s — looped together they will drift apart.");
                    problems++;
                }

                float bedRms = Rms(bed);
                float tensionRms = Rms(tension);

                if (bedRms < 0.01f)
                {
                    Debug.LogError($"[Music] {level.Name}'s bed is effectively silent (RMS {bedRms:0.0000}).");
                    problems++;
                }
                else if (bedRms > 0.45f)
                {
                    Debug.LogError($"[Music] {level.Name}'s bed is crushed (RMS {bedRms:0.000}) — " +
                                   "something is clipping.");
                    problems++;
                }

                if (tensionRms < 0.01f)
                {
                    Debug.LogError($"[Music] {level.Name}'s tension layer is silent.");
                    problems++;
                }

                fingerprints.Add(new KeyValuePair<string, float>(level.Name, Fingerprint(bed)));

                Debug.Log($"[Music] {level.Name}: bed {bed.length:0}s RMS {bedRms:0.000}, " +
                          $"tension {tension.length:0}s RMS {tensionRms:0.000}.");
            }

            // Six distinct beds. Compared on a cheap spectral-ish signature rather than
            // sample-by-sample, because two clips can differ in noise and still be the
            // same piece of music.
            for (int a = 0; a < fingerprints.Count; a++)
            {
                for (int b = a + 1; b < fingerprints.Count; b++)
                {
                    if (Mathf.Abs(fingerprints[a].Value - fingerprints[b].Value) > 0.0005f) continue;

                    Debug.LogError($"[Music] {fingerprints[a].Key} and {fingerprints[b].Key} are the " +
                                   "same piece of music.");
                    problems++;
                }
            }

            problems += CheckDreadPrimitives();

            Debug.Log(problems == 0
                ? "[Music] PASS — six distinct beds, layers length-matched, nothing silent or crushed."
                : $"[Music] FAIL — {problems} problem(s).");
        }

        private static AudioClip Take(Dictionary<ZombieHouse.Audio.Sfx, AudioClip[]> bank,
                                      ZombieHouse.Audio.Sfx key)
        {
            AudioClip[] clips;
            if (!bank.TryGetValue(key, out clips) || clips == null || clips.Length == 0) return null;
            return clips[0];
        }

        private static float[] Samples(AudioClip clip)
        {
            var data = new float[clip.samples * clip.channels];
            clip.GetData(data, 0);
            return data;
        }

        private static float Rms(AudioClip clip)
        {
            float[] data = Samples(clip);
            double sum = 0.0;

            for (int i = 0; i < data.Length; i++) sum += data[i] * (double)data[i];

            return Mathf.Sqrt((float)(sum / Mathf.Max(1, data.Length)));
        }

        /// <summary>
        /// A cheap signature: mean absolute level in each of eight slices of the clip.
        /// Folded to one number, which is enough to tell two different pieces apart while
        /// being insensitive to the random seed jitter inside one of them.
        /// </summary>
        private static float Fingerprint(AudioClip clip)
        {
            float[] data = Samples(clip);
            int slice = Mathf.Max(1, data.Length / 8);

            float signature = 0f;

            for (int s = 0; s < 8; s++)
            {
                double sum = 0.0;
                int start = s * slice;
                int end = Mathf.Min(start + slice, data.Length);

                for (int i = start; i < end; i++) sum += Mathf.Abs(data[i]);

                signature += (float)(sum / Mathf.Max(1, end - start)) * (s + 1);
            }

            return signature;
        }

        /// <summary>
        /// The new synthesis primitives, checked on their defining property rather than on
        /// how they sound — a reverse envelope that decays is not a reverse envelope, and a
        /// sub drop that never gets low is just a hum.
        /// </summary>
        private static int CheckDreadPrimitives()
        {
            int problems = 0;
            var rng = new System.Random(11);

            // A reverse envelope must be quietest at the start and loudest at the end of
            // its swell, which is the exact opposite of every other envelope here.
            var swell = ZombieHouse.Audio.ProceduralAudio.Buffer(2f);
            ZombieHouse.Audio.ProceduralAudio.AddSine(swell, 220f, 220f, 1f);
            ZombieHouse.Audio.ProceduralAudio.ApplyReverseEnvelope(swell, 1.8f);

            float head = Peak(swell, 0, swell.Length / 8);
            float tail = Peak(swell, swell.Length * 5 / 8, swell.Length * 7 / 8);

            if (head >= tail * 0.5f)
            {
                Debug.LogError($"[Music] The reverse envelope is not reversed: {head:0.000} at the " +
                               $"start against {tail:0.000} later. It should grow, not decay.");
                problems++;
            }
            else
            {
                Debug.Log($"[Music] Reverse envelope: {head:0.000} at the start, {tail:0.000} before " +
                          "the stop — it swells and then simply ceases.");
            }

            // Formants must actually reshape the spectrum, or a whisper is only hiss.
            var plain = ZombieHouse.Audio.ProceduralAudio.Buffer(1f);
            ZombieHouse.Audio.ProceduralAudio.AddNoise(plain, 1f, rng);

            var voiced = (float[])plain.Clone();
            ZombieHouse.Audio.ProceduralAudio.AddFormants(voiced);

            float difference = 0f;
            for (int i = 0; i < plain.Length; i++) difference += Mathf.Abs(plain[i] - voiced[i]);
            difference /= plain.Length;

            if (difference < 0.01f)
            {
                Debug.LogError("[Music] AddFormants barely changed the noise — the whispers are hiss.");
                problems++;
            }

            return problems;
        }

        private static float Peak(float[] data, int from, int to)
        {
            float peak = 0f;
            for (int i = Mathf.Max(0, from); i < Mathf.Min(data.Length, to); i++)
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));

            return peak;
        }

        [MenuItem("Zombie House/Test Props", false, 38)]
        public static void TestProps()
        {
            int problems = 0;

            ProtoMaterials.ClearCache();
            PropLibrary.ClearCache();

            // ---- a missing prop must be a no-op ----------------------------
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                box.transform.localScale = new Vector3(2f, 0.7f, 1.4f);
                box.transform.rotation = Quaternion.Euler(11f, 42f, -7f);

                Mesh originalMesh = box.GetComponent<MeshFilter>().sharedMesh;
                var originalCollider = box.GetComponent<BoxCollider>();
                Vector3 colliderSize = originalCollider.size;
                Vector3 colliderCentre = originalCollider.center;
                Vector3 scale = box.transform.localScale;
                Quaternion rotation = box.transform.rotation;

                if (PropLibrary.Dress(box, "NoSuchPropExists"))
                {
                    Debug.LogError("[Props] Dressing a prop with no mesh claimed to have worked.");
                    problems++;
                }

                if (box.GetComponent<MeshFilter>().sharedMesh != originalMesh)
                {
                    Debug.LogError("[Props] A missing prop still changed the mesh.");
                    problems++;
                }

                // ---- a present prop must leave the collider alone -----------
                // Stand a known mesh in for a real prop, so this holds even before a single
                // boulder has been exported. What is being tested is the seam, not the art.
                Mesh stand_in = box.GetComponent<MeshFilter>().sharedMesh;
                var filter = box.GetComponent<MeshFilter>();
                filter.sharedMesh = stand_in;

                var afterCollider = box.GetComponent<BoxCollider>();
                if (afterCollider == null)
                {
                    Debug.LogError("[Props] The collider was removed.");
                    problems++;
                }
                else if (afterCollider.size != colliderSize || afterCollider.center != colliderCentre)
                {
                    Debug.LogError($"[Props] The collider moved: {colliderSize} to {afterCollider.size}.");
                    problems++;
                }

                if (box.transform.localScale != scale)
                {
                    Debug.LogError($"[Props] The scale changed: {scale} to {box.transform.localScale}.");
                    problems++;
                }

                // Rotation too, and this one was learned the hard way. The check used to
                // cover the collider's size and centre only, so a `randomYaw` option that
                // spun the whole transform for visual variety slipped straight past it —
                // a BoxCollider's size does not change when you rotate its transform, but
                // what the NavMesh bakes certainly does. It cost the forest two navigation
                // triangles and the valley nine before anyone noticed.
                if (box.transform.rotation != rotation)
                {
                    Debug.LogError($"[Props] The rotation changed: {rotation.eulerAngles} to " +
                                   $"{box.transform.rotation.eulerAngles}. The transform carries the " +
                                   "collider, so turning it re-bakes the NavMesh.");
                    problems++;
                }
            }
            finally
            {
                Object.DestroyImmediate(box);
            }

            // ---- whatever has actually been built ---------------------------
            problems += CheckBuiltProps();

            Debug.Log(problems == 0
                ? "[Props] PASS — a missing prop is a no-op, and dressing one never touches its collider."
                : $"[Props] FAIL — {problems} problem(s).");
        }

        /// <summary>
        /// Every mesh sitting in Resources/Props, checked against the unit-box contract.
        ///
        /// The contract exists because the generators size props through localScale on a
        /// 1x1x1 cube. A mesh that is not unit-sized is not slightly wrong, it is wrong by
        /// whatever its own dimensions happen to be — a boulder exported at Blender's
        /// default two-metre icosphere arrives at twice the size the level asked for, in
        /// every level, and looks like a level-design mistake rather than an export one.
        /// </summary>
        private static int CheckBuiltProps()
        {
            const string folder = "Assets/Resources/Props";

            if (!Directory.Exists(folder))
            {
                Debug.Log("[Props] No props built yet — every level is still boxes, which is " +
                          "exactly what the fallback is for. Run Tools_Props/*.py through Blender.");
                return 0;
            }

            Mesh[] meshes = Resources.LoadAll<Mesh>("Props");
            if (meshes.Length == 0)
            {
                Debug.Log("[Props] Resources/Props exists but holds no meshes yet.");
                return 0;
            }

            int problems = 0;
            int triangles = 0;

            foreach (Mesh mesh in meshes)
            {
                triangles += mesh.triangles.Length / 3;

                if (!PropLibrary.IsUnitSized(mesh))
                {
                    Bounds b = mesh.bounds;
                    Debug.LogError($"[Props] {mesh.name} is {b.size.x:0.00} x {b.size.y:0.00} x " +
                                   $"{b.size.z:0.00} centred at {b.center} — props must be a unit box " +
                                   "at the origin, or the level's localScale means nothing.");
                    problems++;
                }

                // A prop is scenery. If one of these is heavier than a whole creature, the
                // decimation step in its generator did not run.
                int count = mesh.triangles.Length / 3;
                if (count > 1500)
                {
                    Debug.LogError($"[Props] {mesh.name} is {count} triangles; a prop that " +
                                   "appears sixty times in a level needs decimating.");
                    problems++;
                }
            }

            if (problems == 0)
            {
                Debug.Log($"[Props] {meshes.Length} prop mesh(es) built, {triangles} triangles " +
                          "between them, all unit-sized.");
            }

            return problems;
        }

        [MenuItem("Zombie House/Test PostFx", false, 37)]
        public static void TestPostFx()
        {
            int problems = 0;

            // ---- the shader ------------------------------------------------
            Shader shader = Resources.Load<Shader>("Shaders/ZombiePost");
            if (shader == null)
            {
                Debug.LogError("[PostFx] Shaders/ZombiePost is not under Resources — it would " +
                               "resolve in the editor and vanish in a build.");
                Debug.Log("[PostFx] FAIL — 1 problem(s).");
                return;
            }

            if (!shader.isSupported)
            {
                Debug.LogError("[PostFx] The post shader failed to compile on this target.");
                problems++;
            }
            else
            {
                Debug.Log($"[PostFx] Shader: {shader.name}, {shader.passCount} pass(es), compiles.");
            }

            // ---- where it sits --------------------------------------------
            var rig = new GameObject("PostFxTestRig");
            try
            {
                var cameraObject = new GameObject("Camera");
                cameraObject.transform.SetParent(rig.transform, false);
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;

                var stack = cameraObject.AddComponent<ZombieHouse.Fx.PostProcessStack>();

                if (!stack.Ready)
                {
                    Debug.LogError("[PostFx] The stack cannot find its own shader at runtime.");
                    problems++;
                }

                // Bloom with no headroom above 1 has nothing to find: every emissive in the
                // game is over-bright and that is the entire point of having it.
                if (!camera.allowHDR)
                {
                    Debug.LogError("[PostFx] The camera is not HDR, so nothing can exceed 1 and " +
                                   "nothing will ever bloom.");
                    problems++;
                }

                problems += CheckBloomActuallyBlooms(stack);
            }
            finally
            {
                Object.DestroyImmediate(rig);
            }

            problems += CheckLevelMoodsDiffer();

            Debug.Log(problems == 0
                ? "[PostFx] PASS — the shader compiles, the camera has headroom, and light bleeds."
                : $"[PostFx] FAIL — {problems} problem(s).");
        }

        /// <summary>
        /// Pushes one over-bright pixel through the real render path and checks that its
        /// neighbours came back brighter than they went in. That is bloom, defined in the
        /// only terms that matter.
        /// </summary>
        private static int CheckBloomActuallyBlooms(ZombieHouse.Fx.PostProcessStack stack)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Debug.Log("[PostFx] No graphics device (-nographics): skipping the pixel check. " +
                          "Run this with -batchmode and no -nographics to prove the bloom.");
                return 0;
            }

            const int size = 128;
            const int centre = size / 2;

            RenderTexture source = null, destination = null;
            Texture2D input = null, output = null;
            RenderTexture previous = RenderTexture.active;

            try
            {
                // A dim field with one very bright pixel in the middle. Half format so the
                // bright pixel can genuinely exceed 1 — in LDR it would clamp and there
                // would be nothing above the threshold to find.
                input = new Texture2D(size, size, TextureFormat.RGBAHalf, false);
                var pixels = new Color[size * size];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(0.02f, 0.02f, 0.02f, 1f);

                for (int y = centre - 1; y <= centre + 1; y++)
                    for (int x = centre - 1; x <= centre + 1; x++)
                        pixels[y * size + x] = new Color(8f, 8f, 8f, 1f);

                input.SetPixels(pixels);
                input.Apply();

                source = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGBHalf);
                destination = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGBHalf);

                Graphics.Blit(input, source);

                // Grade off, bloom on: this is a measurement, and a contrast curve or a
                // vignette in the way would make the numbers mean something else.
                stack.Configure(bloom: 1.5f, bloomThreshold: 1f, filter: Color.white,
                                grade: 1f, saturate: 1f, vignette: 0f, grain: 0f);
                stack.Render(source, destination);

                RenderTexture.active = destination;
                output = new Texture2D(size, size, TextureFormat.RGBAHalf, false);
                output.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                output.Apply();

                // Three samples walking away from the highlight, plus the far corner.
                //
                // Deliberately judged on *shape* rather than on any absolute number. The
                // first version of this asserted the near sample cleared 0.05 and failed a
                // perfectly good bloom at 0.047 — because the ACES curve crushes the dark
                // end hard, and a scene sitting at 0.02 tonemaps to about 0.010. Absolute
                // brightness after a tonemap is not a thing worth having an opinion about.
                // A localised falloff is: it is what distinguishes a bloom from someone
                // adding a constant to the whole frame.
                float close = output.GetPixel(centre + 6, centre).grayscale;
                float mid = output.GetPixel(centre + 14, centre).grayscale;
                float outer = output.GetPixel(centre + 26, centre).grayscale;
                float corner = output.GetPixel(3, 3).grayscale;

                if (close <= corner * 1.5f)
                {
                    Debug.LogError($"[PostFx] Six pixels from an 8.0 highlight the image is {close:0.000}, " +
                                   $"against {corner:0.000} in the far corner — no light bled out of it. " +
                                   "The bloom is not running.");
                    return 1;
                }

                if (!(close > mid && mid > outer))
                {
                    Debug.LogError($"[PostFx] Brightness does not fall off with distance from the " +
                                   $"highlight ({close:0.000}, {mid:0.000}, {outer:0.000}) — that is a " +
                                   "uniform lift, not a bloom.");
                    return 1;
                }

                if (outer <= corner)
                {
                    Debug.LogError($"[PostFx] The glow does not reach 26 pixels ({outer:0.000} against a " +
                                   $"{corner:0.000} floor); the blur is far too tight to be worth having.");
                    return 1;
                }

                Debug.Log($"[PostFx] Bloom measured across the falloff: {close:0.000} at 6 px, " +
                          $"{mid:0.000} at 14, {outer:0.000} at 26, {corner:0.000} in the corner. " +
                          "Light bleeds, and only where it should.");
                return 0;
            }
            finally
            {
                RenderTexture.active = previous;
                if (destination != null) RenderTexture.ReleaseTemporary(destination);
                if (source != null) RenderTexture.ReleaseTemporary(source);
                if (input != null) Object.DestroyImmediate(input);
                if (output != null) Object.DestroyImmediate(output);
            }
        }

        /// <summary>
        /// Six levels, six grades. If two of them come out identical the moods have been
        /// copy-pasted and one of the levels has quietly lost its identity — which is
        /// invisible unless you happen to play both in the same sitting.
        /// </summary>
        private static int CheckLevelMoodsDiffer()
        {
            var moods = new[] { LevelMood.House, LevelMood.Forest, LevelMood.Town,
                                LevelMood.School, LevelMood.Tomb, LevelMood.Jungle };

            var filters = new Color[moods.Length];
            var blooms = new float[moods.Length];

            var rig = new GameObject("MoodRig");
            try
            {
                var cameraObject = new GameObject("Camera");
                cameraObject.transform.SetParent(rig.transform, false);
                cameraObject.AddComponent<Camera>().enabled = false;
                cameraObject.AddComponent<ZombieHouse.Fx.PostProcessStack>();

                for (int i = 0; i < moods.Length; i++)
                {
                    ApplyPostFx(rig, moods[i]);

                    var stack = rig.GetComponentInChildren<ZombieHouse.Fx.PostProcessStack>(true);
                    filters[i] = stack.ColourFilter;
                    blooms[i] = stack.BloomIntensity;
                }
            }
            finally
            {
                Object.DestroyImmediate(rig);
            }

            for (int a = 0; a < moods.Length; a++)
            {
                for (int b = a + 1; b < moods.Length; b++)
                {
                    bool sameFilter = (Vector4)filters[a] == (Vector4)filters[b];
                    if (sameFilter && Mathf.Approximately(blooms[a], blooms[b]))
                    {
                        Debug.LogError($"[PostFx] {moods[a]} and {moods[b]} have the same grade.");
                        return 1;
                    }
                }
            }

            Debug.Log($"[PostFx] Six distinct grades: " +
                      $"tomb warm at {blooms[4]:0.00} bloom, valley green-grey at {blooms[5]:0.00}, " +
                      $"wood cold at {blooms[1]:0.00}.");
            return 0;
        }

        [MenuItem("Zombie House/Test Jungle", false, 36)]
        public static void TestJungle()
        {
            int enemyLayer = EnsureLayer(EnemyLayerName);
            CreatePlaceholderMaterials();
            ProtoMaterials.ClearCache();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            int problems = 0;

            // A 3.4 m snake is an anaconda, which is the intent. The bound is here to
            // catch one built at the wrong scale entirely, not to police the species.
            problems += CheckJungleCreature(ZombieKind.Snake, enemyLayer, 2f, 4.2f);
            problems += CheckJungleCreature(ZombieKind.Jaguar, enemyLayer, 1.6f, 3.2f);
            problems += CheckJungleCreature(ZombieKind.Monkey, enemyLayer, 0.7f, 1.6f);

            // A snake has no feet. This is one line in the factory and exactly the kind of
            // thing that gets lost in a refactor, because nothing looks wrong — you just
            // hear a soft tap following a snake around.
            GameObject snake = JungleFactory.CreateSnake("SnakeAudioTest");
            try
            {
                var audio = snake.GetComponent<ZombieAudio>();
                var volume = new SerializedObject(audio).FindProperty("footstepVolume");

                if (volume == null || volume.floatValue > 0.001f)
                {
                    Debug.LogError("[Jungle] The snake still has footsteps.");
                    problems++;
                }
                else
                {
                    Debug.Log("[Jungle] The snake is silent underfoot, having no feet.");
                }
            }
            finally
            {
                Object.DestroyImmediate(snake);
            }

            Debug.Log(problems == 0
                ? "[Jungle] PASS — animal colours off real assets, sensible sizes, and a silent snake."
                : $"[Jungle] FAIL — {problems} problem(s).");
        }

        /// <summary>
        /// One creature: every renderer on a material that is a real asset, a plausible
        /// overall size, and nothing left tinted with the missing-material magenta.
        /// </summary>
        private static int CheckJungleCreature(ZombieKind kind, int enemyLayer,
                                               float minimumLength, float maximumLength)
        {
            GameObject creature = kind == ZombieKind.Snake ? JungleFactory.CreateSnake("SnakeTest")
                                : kind == ZombieKind.Monkey ? JungleFactory.CreateMonkey("MonkeyTest")
                                : JungleFactory.CreateJaguar("JaguarTest");

            try
            {
                SetLayerRecursively(creature, enemyLayer);

                int problems = 0;
                int parts = 0, orphaned = 0, magenta = 0;
                Bounds extent = new Bounds(creature.transform.position, Vector3.zero);

                foreach (Renderer renderer in creature.GetComponentsInChildren<Renderer>(true))
                {
                    parts++;
                    extent.Encapsulate(renderer.bounds);

                    Material material = renderer.sharedMaterial;

                    // The one that matters: an in-memory material has no asset path, so it
                    // will be a dangling reference the moment this is saved as a prefab.
                    if (material == null || !AssetDatabase.Contains(material))
                    {
                        orphaned++;
                        continue;
                    }

                    Color colour = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor")
                                 : material.HasProperty("_Color") ? material.GetColor("_Color")
                                 : Color.white;

                    // Unity's missing-material colour, and anything else that is more
                    // magenta than an animal has any business being.
                    if (colour.r > 0.6f && colour.b > 0.6f && colour.g < 0.35f) magenta++;
                }

                if (orphaned > 0)
                {
                    Debug.LogError($"[Jungle] The {kind} has {orphaned}/{parts} parts on materials that " +
                                   "are not assets — they would come out magenta once saved as a prefab. " +
                                   "Add the keys to CreatePlaceholderMaterials.");
                    problems++;
                }

                if (magenta > 0)
                {
                    Debug.LogError($"[Jungle] {magenta} of the {kind}'s parts are pink.");
                    problems++;
                }

                float length = Mathf.Max(extent.size.x, extent.size.z);
                if (length < minimumLength || length > maximumLength)
                {
                    Debug.LogError($"[Jungle] The {kind} is {length:0.00} m long; expected " +
                                   $"{minimumLength:0.0}–{maximumLength:0.0} m.");
                    problems++;
                }

                if (problems == 0)
                {
                    Debug.Log($"[Jungle] {kind}: {parts} parts, all on real material assets, " +
                              $"{length:0.00} m long and {extent.size.y:0.00} m tall.");
                }

                return problems;
            }
            finally
            {
                Object.DestroyImmediate(creature);
            }
        }

        [MenuItem("Zombie House/Test Weapons", false, 34)]
        public static void TestGatling()
        {
            int problems = 0;
            var rig = new GameObject("WeaponTestRig");

            try
            {
                var pistolObject = new GameObject("TestPistol");
                pistolObject.transform.SetParent(rig.transform, false);
                var pistol = pistolObject.AddComponent<Weapon>();
                pistol.ConfigureStats("Sidearm", 125f, 90f, 0f, 7, 42, 2.3f, 0.5f, 14f, 70f, 50f, 55f);

                var rifleObject = new GameObject("TestRifle");
                rifleObject.transform.SetParent(rig.transform, false);
                rifleObject.AddComponent<Weapon>();

                // Slot three: the gatling gun, in the loadout and belt-fed.
                var gatlingObject = new GameObject("TestGatling");
                gatlingObject.transform.SetParent(rig.transform, false);
                var gatling = gatlingObject.AddComponent<Weapon>();

                // Zero fire interval: edit mode does not advance Time.time, and this test
                // is about ammunition and spin-up rather than rate of fire.
                gatling.ConfigureStats("Gatling Gun", 34f, 55f, 0f, 100, 300, 3.4f, 1.6f, 3f, 74f, 66f, 45f);
                gatling.SetAutomatic(true);
                gatling.ConfigureRotary(spinUp: 0.85f, spinDown: 1.2f);
                gatling.ConfigureAsPowerUp(400, false);

                // Stowed: the Uzi, which is what you actually find on the floor.
                var uziObject = new GameObject("TestUzi");
                uziObject.transform.SetParent(rig.transform, false);
                var uzi = uziObject.AddComponent<Weapon>();
                uzi.ConfigureStats("Uzi", 52f, 60f, 0f, 32, 168, 1.6f, 0.9f, 4f, 72f, 60f, 30f);
                uzi.SetAutomatic(true);
                uzi.ConfigureAsPowerUp(200, true);

                var switcher = rig.AddComponent<WeaponSwitcher>();
                switcher.Configure(new[] { pistolObject, rifleObject, gatlingObject }, 0, 1);
                switcher.ConfigurePowerUp(uziObject);

                if (switcher.HasPowerUp)
                {
                    Debug.LogError("[Weapons] The rig starts out carrying the Uzi; it should be stowed.");
                    problems++;
                }

                // ---- the barrels have to come up to speed ---------------------
                // This is the gatling gun's whole character: the trigger starts it, it does
                // not fire it. One that shoots on the first frame is a fast rifle.
                if (gatling.SpinFraction > 0f)
                {
                    Debug.LogError($"[Weapons] The gatling starts at {gatling.SpinFraction:P0} spin; it should start stopped.");
                    problems++;
                }

                int before = gatling.AmmoInMagazine;
                gatling.TickSpin(0.1f, true);
                gatling.TryFire();

                if (gatling.AmmoInMagazine != before)
                {
                    Debug.LogError("[Weapons] It fired a tenth of a second after the trigger went down, before the barrels were up.");
                    problems++;
                }
                else
                {
                    Debug.Log($"[Weapons] A tenth of a second in the gatling is at {gatling.SpinFraction:P0} spin and will not fire.");
                }

                gatling.TickSpin(1f, true);
                if (gatling.SpinFraction < 1f)
                {
                    Debug.LogError($"[Weapons] After more than a second of holding it is only at {gatling.SpinFraction:P0}.");
                    problems++;
                }

                gatling.TryFire();
                if (gatling.AmmoInMagazine != before - 1)
                {
                    Debug.LogError("[Weapons] The barrels are at speed and it still will not fire.");
                    problems++;
                }
                else
                {
                    Debug.Log("[Weapons] Once they are at speed it fires — the trigger starts it, it does not fire it.");
                }

                gatling.TickSpin(0.6f, false);
                if (gatling.SpinFraction >= 1f)
                {
                    Debug.LogError("[Weapons] The barrels stayed at speed after the trigger was released.");
                    problems++;
                }
                else
                {
                    Debug.Log($"[Weapons] Released, it winds back down — {gatling.SpinFraction:P0} after half a second.");
                }

                // Everything else in the game must be unaffected by any of this.
                if (pistol.IsRotary || Mathf.Abs(pistol.SpinFraction - 1f) > 0.001f)
                {
                    Debug.LogError("[Weapons] The sidearm thinks it has barrels to spin up.");
                    problems++;
                }

                // ---- belt crates find it in the slots ------------------------
                // Keyed on the weapon being rotary, not on which slot it sits in. That is
                // the whole reason FeedBeltFed exists: the gatling moved out of the
                // power-up slot and a belt crate should not have noticed.
                int beforeFeed = gatling.TotalAmmo;
                int taken = switcher.FeedBeltFed(150);

                if (taken != 150 || gatling.TotalAmmo != beforeFeed + 150)
                {
                    Debug.LogError($"[Weapons] A belt crate gave the gatling {taken} rounds; it should have taken 150.");
                    problems++;
                }
                else
                {
                    Debug.Log($"[Weapons] A belt crate feeds it in the slots: {beforeFeed} to {gatling.TotalAmmo} rounds.");
                }

                int guard = 0;
                while (switcher.FeedBeltFed(150) > 0 && guard++ < 20) { }

                if (switcher.FeedBeltFed(150) != 0)
                {
                    Debug.LogError("[Weapons] A full gun still swallows belts — crates would vanish for nothing.");
                    problems++;
                }
                else
                {
                    Debug.Log($"[Weapons] It fills at {gatling.TotalAmmo} rounds and then refuses more.");
                }

                // ---- and firing it dry must NOT lose it ----------------------
                if (gatling.DiscardWhenEmpty)
                {
                    Debug.LogError("[Weapons] The gatling is marked disposable — you would lose it the first time it ran dry.");
                    problems++;
                }

                // Hold the trigger down and let it run itself dry, one frame at a time,
                // exactly as Update drives it — TickSpin, then TryFire, and *nothing
                // else*. No hand-fed TryReload, because the whole question is whether the
                // gun reloads itself when the belt runs out.
                //
                // The old version of this loop called TryReload by hand whenever the
                // magazine hit zero, which is why it never caught the deadlock: the spin
                // gate sat above the auto-reload in TryFire, TickSpin stopped winding the
                // moment the magazine emptied, and the reload the player would have got
                // for free was unreachable. A test that does the player's job for them
                // proves nothing about what happens when the player does not.
                const float frame = 1f / 60f;
                int startingAmmo = gatling.TotalAmmo;
                int fired = 0, frames = 0, reloadsSeen = 0;
                bool wasReloading = false;

                while (gatling.TotalAmmo > 0 && frames < 6000)
                {
                    frames++;

                    gatling.TickCooldown(frame);
                    gatling.TickReload(frame);
                    gatling.TickSpin(frame, true);
                    gatling.TryFire();

                    // Measured against the total, not the belt: a reload moves rounds from
                    // the reserve into the magazine, so a magazine diff misses the shot
                    // fired on the same frame one completes.
                    fired = startingAmmo - gatling.TotalAmmo;

                    if (gatling.IsReloading && !wasReloading) reloadsSeen++;
                    wasReloading = gatling.IsReloading;

                    // Nothing happening, nothing left to happen: it has jammed itself.
                    if (frames > 240 && fired == 0)
                    {
                        Debug.LogError("[Weapons] The gatling gun fired nothing in four seconds of held trigger.");
                        problems++;
                        break;
                    }
                }

                if (gatling.TotalAmmo > 0)
                {
                    Debug.LogError($"[Weapons] Holding the trigger left {gatling.TotalAmmo} rounds unfired after " +
                                   $"{frames} frames — it stopped reloading itself. This is the spin-gate deadlock: " +
                                   "TryFire must check the magazine before it checks the barrels.");
                    problems++;
                }
                else if (reloadsSeen < 3)
                {
                    Debug.LogError($"[Weapons] It reloaded itself {reloadsSeen} time(s) getting through " +
                                   $"{startingAmmo} rounds on a {gatling.MagazineSize}-round belt; expected more.");
                    problems++;
                }
                else
                {
                    Debug.Log($"[Weapons] Held the trigger from full to empty: {fired} rounds, " +
                              $"{reloadsSeen} belt changes it started on its own, no hand-holding.");
                }

                if (!gatlingObject.activeSelf || switcher.SlotCount < 3)
                {
                    Debug.LogError("[Weapons] The gatling ran dry and vanished; it is loadout, not a pickup.");
                    problems++;
                }
                else
                {
                    Debug.Log($"[Weapons] Fired {fired} rounds dry and the gatling is still in the slots, waiting for a crate.");
                }

                switcher.FeedBeltFed(150);
                if (gatling.TotalAmmo != 150)
                {
                    Debug.LogError($"[Weapons] An empty gatling took {gatling.TotalAmmo} from a crate, not 150.");
                    problems++;
                }
                else
                {
                    Debug.Log("[Weapons] A crate brings the empty gatling straight back — which is the point of them.");
                }

                // ---- and it reloads like anything else -----------------------
                // The whole reason this section exists: the gun used to be belt-only, and
                // running dry between crates left you carrying a paperweight. R has to
                // work, and an ordinary ammunition box has to be worth something.
                gatling.ConfigureResupply(roundsPerBox: 0, reserveCeiling: 800);

                // Yellow boxes are small-arms ammunition and must do nothing at all for the
                // belt-fed gun — that separation is what makes the green crates worth
                // crossing a level for. Without it they quietly became a second, far more
                // common supply and the crates stopped mattering.
                int beforeBox = gatling.TotalAmmo;
                int fromBox = gatling.AddAmmo(24);

                if (fromBox != 0 || gatling.TotalAmmo != beforeBox)
                {
                    Debug.LogError($"[Weapons] A yellow ammo box gave the gatling {fromBox} rounds; " +
                                   "it is belt-fed and should have refused.");
                    problems++;
                }
                else
                {
                    Debug.Log("[Weapons] Yellow boxes do nothing for the gatling — green crates only.");
                }

                // And the reverse: a magazine weapon must still take one.
                int uziBefore = uzi.TotalAmmo;
                if (uzi.AddAmmo(24) <= 0)
                {
                    Debug.LogError("[Weapons] The Uzi refused a yellow ammo box.");
                    problems++;
                }
                else
                {
                    Debug.Log($"[Weapons] The Uzi takes yellow boxes: {uziBefore} to {uzi.TotalAmmo}.");
                }

                // Put the belt back so the reload check below has something to load.
                switcher.FeedBeltFed(300);

                // Empty the magazine, then reload it from that reserve.
                while (gatling.AmmoInMagazine > 0)
                {
                    gatling.TickCooldown(10f);
                    gatling.TickSpin(1f, true);
                    gatling.TryFire();
                }

                int reserveBefore = gatling.ReserveAmmo;
                gatling.TryReload();
                gatling.TickReload(10f);

                if (gatling.AmmoInMagazine <= 0)
                {
                    Debug.LogError("[Weapons] R does not reload the gatling gun.");
                    problems++;
                }
                else if (gatling.ReserveAmmo >= reserveBefore)
                {
                    Debug.LogError("[Weapons] The gatling reloaded without spending reserve.");
                    problems++;
                }
                else
                {
                    Debug.Log($"[Weapons] Reloaded: {gatling.AmmoInMagazine} in the belt, " +
                              $"{gatling.ReserveAmmo} left in reserve.");
                }

                if (pistol.AmmoBoxRounds != 0)
                {
                    Debug.LogError("[Weapons] The sidearm has a belt-fed box size; boxes should decide for it.");
                    problems++;
                }

                // ---- the Uzi: granted, spent, and gone ------------------------
                if (!switcher.GrantPowerUp(200))
                {
                    Debug.LogError("[Weapons] The switcher refused to grant the Uzi.");
                    Debug.Log("[Weapons] FAIL — 1 problem(s).");
                    return;
                }

                if (uzi.TotalAmmo != 200)
                {
                    Debug.LogError($"[Weapons] The Uzi arrived with {uzi.TotalAmmo} rounds, not 200.");
                    problems++;
                }
                else if (!switcher.HasPowerUp)
                {
                    Debug.LogError("[Weapons] Granted, but the switcher has no slot for it.");
                    problems++;
                }
                else
                {
                    Debug.Log($"[Weapons] Uzi granted: {uzi.AmmoInMagazine} in the magazine, {uzi.ReserveAmmo} in reserve.");
                }

                if (!uzi.DiscardWhenEmpty)
                {
                    Debug.LogError("[Weapons] The Uzi does not know it is disposable — it would survive running dry.");
                    problems++;
                }

                // ---- the wheel, with four things on it ------------------------
                // Pistol, rifle, gatling, Uzi, round again. One button, one behaviour.
                switcher.Equip(0);
                bool cycleOk = switcher.NextWheelSlot() == 1;
                switcher.Equip(1);
                cycleOk &= switcher.NextWheelSlot() == 2;
                switcher.Equip(2);
                cycleOk &= switcher.NextWheelSlot() == 3;
                switcher.Equip(3);
                cycleOk &= switcher.NextWheelSlot() == 0;

                if (!cycleOk)
                {
                    Debug.LogError("[Weapons] The wheel does not cycle all four and back again.");
                    problems++;
                }
                else
                {
                    Debug.Log("[Weapons] The wheel cycles pistol, rifle, gatling, Uzi, round again.");
                }

                // ---- firing the Uzi dry --------------------------------------
                int uziStartingAmmo = uzi.TotalAmmo;
                int uziFired = 0, uziFrames = 0;
                while (uzi.TotalAmmo > 0 && uziFrames < 6000)
                {
                    uziFrames++;

                    uzi.TickCooldown(frame);
                    uzi.TickReload(frame);
                    uzi.TryFire();

                    uziFired = uziStartingAmmo - uzi.TotalAmmo;
                }

                if (uzi.TotalAmmo > 0)
                {
                    Debug.LogError($"[Weapons] The Uzi stopped with {uzi.TotalAmmo} rounds left — it is not " +
                                   "reloading itself either.");
                    problems++;
                }

                if (uziFired != 200)
                {
                    Debug.LogError($"[Weapons] The Uzi fired {uziFired} rounds; it was given 200.");
                    problems++;
                }

                if (switcher.HasPowerUp)
                {
                    Debug.LogError("[Weapons] The Uzi is empty and still in the slots — it should be gone.");
                    problems++;
                }
                else if (uziObject.activeSelf)
                {
                    Debug.LogError("[Weapons] The empty Uzi is still active in the rig.");
                    problems++;
                }
                else
                {
                    Debug.Log($"[Weapons] Fired all {uziFired} rounds and the Uzi took itself away.");
                }

                if (switcher.Current == null || switcher.Current.WeaponName != "Sidearm")
                {
                    string held = switcher.Current == null ? "nothing" : switcher.Current.WeaponName;
                    Debug.LogError($"[Weapons] After it ran out you are holding {held}, not the sidearm.");
                    problems++;
                }

                // Back to three slots, and the wheel with it.
                switcher.Equip(2);
                if (switcher.NextWheelSlot() != 0)
                {
                    Debug.LogError("[Weapons] With the Uzi gone the wheel no longer wraps at the gatling.");
                    problems++;
                }

                // A belt crate must still feed the gatling with no Uzi carried — the two
                // are unrelated, and the old code path could not tell them apart.
                if (switcher.FeedBeltFed(150) <= 0)
                {
                    Debug.LogError("[Weapons] With no Uzi carried, a belt crate no longer feeds the gatling.");
                    problems++;
                }

                // ---- a second Uzi is a full one ------------------------------
                if (!switcher.GrantPowerUp(200) || uzi.TotalAmmo != 200)
                {
                    Debug.LogError($"[Weapons] A second Uzi gave {uzi.TotalAmmo} rounds, not a fresh 200.");
                    problems++;
                }
                else
                {
                    Debug.Log("[Weapons] A second Uzi found later arrives full, and the slot comes back.");
                }
            }
            finally
            {
                Object.DestroyImmediate(rig);
            }

            Debug.Log(problems == 0
                ? "[Weapons] PASS — the gatling spins up, reloads and survives empty; the Uzi runs out and goes."
                : $"[Weapons] FAIL — {problems} problem(s).");
        }

        /// <summary>
        /// The school's three: that the janitor really does out-reach everything else,
        /// that a child is child-sized, and that the mop is scenery rather than a wall.
        ///
        /// The reach check is the one that matters. The janitor's whole design is that he
        /// hits you from outside the distance every other encounter has taught you is
        /// safe — if AttackRange ever stops reaching the AI he becomes a slow shambler
        /// with a prop, and nothing about the level would look broken.
        /// </summary>
        [MenuItem("Zombie House/Test School", false, 33)]
        public static void TestSchool()
        {
            int problems = 0;
            GameObject janitor = null, kid = null, teacher = null;

            try
            {
                ZombieArchetype janitorType = null, kidType = null, teacherType = null, shambler = null;
                foreach (ZombieArchetype a in ZombieArchetype.Catalogue)
                {
                    if (a.Kind == ZombieKind.Janitor) janitorType = a;
                    if (a.Kind == ZombieKind.Kid) kidType = a;
                    if (a.Kind == ZombieKind.Teacher) teacherType = a;
                    if (a.Kind == ZombieKind.Shambler) shambler = a;
                }

                if (janitorType == null || kidType == null || teacherType == null)
                {
                    Debug.LogError("[School] The catalogue is missing one of the school's types.");
                    Debug.Log("[School] FAIL — 1 problem(s).");
                    return;
                }

                // ---- reach --------------------------------------------------
                if (janitorType.AttackRange < teacherType.AttackRange * 1.8f)
                {
                    Debug.LogError($"[School] The mop reaches {janitorType.AttackRange:0.0} m against a teacher's " +
                                   $"{teacherType.AttackRange:0.0} m — that is not the weapon the level is built around.");
                    problems++;
                }
                else
                {
                    Debug.Log($"[School] Mop reach {janitorType.AttackRange:0.0} m against {teacherType.AttackRange:0.0} m " +
                              "for a teacher — he hits you from outside everything else's range.");
                }

                // The AI has to actually read it, or the number is decoration.
                var dummy = new GameObject("ReachDummy");
                try
                {
                    var ai = dummy.AddComponent<ZombieAI>();
                    ai.ApplyArchetype(janitorType);

                    var so = new SerializedObject(ai);
                    float applied = so.FindProperty("attackRange").floatValue;

                    if (!Mathf.Approximately(applied, janitorType.AttackRange))
                    {
                        Debug.LogError($"[School] ZombieAI kept an attack range of {applied:0.00}; the archetype says {janitorType.AttackRange:0.00}.");
                        problems++;
                    }
                    else
                    {
                        Debug.Log("[School] ZombieAI takes its reach from the archetype, so the mop is real.");
                    }
                }
                finally
                {
                    Object.DestroyImmediate(dummy);
                }

                // An archetype that leaves reach at zero must keep the AI's own default,
                // which is what every walker written before the janitor relies on.
                if (shambler != null && shambler.AttackRange <= 0f)
                {
                    var plainObject = new GameObject("DefaultDummy");
                    try
                    {
                        var plain = plainObject.AddComponent<ZombieAI>();
                        float before = new SerializedObject(plain).FindProperty("attackRange").floatValue;

                        plain.ApplyArchetype(shambler);
                        float after = new SerializedObject(plain).FindProperty("attackRange").floatValue;

                        if (!Mathf.Approximately(before, after))
                        {
                            Debug.LogError($"[School] An archetype with no reach set moved the default from {before:0.00} to {after:0.00}.");
                            problems++;
                        }
                        else
                        {
                            Debug.Log($"[School] Types with no reach set keep the AI default of {after:0.00} m — nothing older changed.");
                        }
                    }
                    finally
                    {
                        Object.DestroyImmediate(plainObject);
                    }
                }

                // ---- toughness ----------------------------------------------
                if (janitorType.Health < 500f || janitorType.StaggerResistance < 0.8f)
                {
                    Debug.LogError($"[School] The janitor is {janitorType.Health:0} health at {janitorType.StaggerResistance:P0} " +
                                   "stagger resistance — he is supposed to be hard to put down.");
                    problems++;
                }
                else
                {
                    Debug.Log($"[School] Janitor: {janitorType.Health:0} health, shrugs off {janitorType.StaggerResistance:P0} of hits.");
                }

                if (kidType.Scale > 0.85f)
                {
                    Debug.LogError($"[School] A child scales to {kidType.Scale:0.00} — that is an adult.");
                    problems++;
                }

                // ---- the clothes and the mop --------------------------------
                janitor = ZombieFactory.Create("JanitorTest", ZombieOutfit.Janitor);
                kid = ZombieFactory.Create("KidTest", ZombieOutfit.Kid);
                teacher = ZombieFactory.Create("TeacherTest", ZombieOutfit.Teacher);

                int mopParts = 0, mopColliders = 0;
                foreach (Renderer renderer in janitor.GetComponentsInChildren<Renderer>(true))
                {
                    if (!renderer.name.StartsWith("Mop")) continue;
                    mopParts++;
                    if (renderer.GetComponent<Collider>() != null) mopColliders++;
                }

                if (mopParts < 5)
                {
                    Debug.LogError($"[School] The mop is {mopParts} part(s); it will not read as a mop.");
                    problems++;
                }
                else if (mopColliders > 0)
                {
                    Debug.LogError($"[School] The mop carries {mopColliders} collider(s) — it would stop rounds meant for him.");
                    problems++;
                }
                else
                {
                    Debug.Log($"[School] Mop: {mopParts} parts, no colliders — the reach is the archetype, not the prop.");
                }

                bool coveralls = false, backpack = false, badge = false;
                foreach (Renderer r in janitor.GetComponentsInChildren<Renderer>(true))
                    if (r.name == "Coveralls") coveralls = true;
                foreach (Renderer r in kid.GetComponentsInChildren<Renderer>(true))
                    if (r.name == "Backpack") backpack = true;
                foreach (Renderer r in teacher.GetComponentsInChildren<Renderer>(true))
                    if (r.name == "StaffBadge") badge = true;

                if (!coveralls || !backpack || !badge)
                {
                    Debug.LogError($"[School] Outfits incomplete: coveralls {coveralls}, backpack {backpack}, badge {badge}.");
                    problems++;
                }
                else
                {
                    Debug.Log("[School] Dark coveralls, a school backpack and a staff badge — three silhouettes, told apart at a glance.");
                }

                int dressedColliders = 0;
                foreach (GameObject who in new[] { janitor, kid, teacher })
                {
                    foreach (Renderer r in who.GetComponentsInChildren<Renderer>(true))
                    {
                        bool clothing = r.name.StartsWith("Coverall") || r.name.StartsWith("Backpack")
                                        || r.name.StartsWith("Strap") || r.name.StartsWith("Cardigan")
                                        || r.name.StartsWith("Lanyard") || r.name.StartsWith("Staff")
                                        || r.name.StartsWith("Tshirt") || r.name.StartsWith("Lens")
                                        || r.name.StartsWith("Glasses") || r.name.StartsWith("ToolBelt");

                        if (clothing && r.GetComponent<Collider>() != null) dressedColliders++;
                    }
                }

                if (dressedColliders > 0)
                {
                    Debug.LogError($"[School] {dressedColliders} piece(s) of clothing carry colliders — a cardigan would soak body shots.");
                    problems++;
                }
            }
            finally
            {
                if (janitor != null) Object.DestroyImmediate(janitor);
                if (kid != null) Object.DestroyImmediate(kid);
                if (teacher != null) Object.DestroyImmediate(teacher);
            }

            Debug.Log(problems == 0
                ? "[School] PASS — the mop out-reaches everything, and none of it is a collider."
                : $"[School] FAIL — {problems} problem(s).");
        }

        /// <summary>
        /// The third condition on the door: that a level with a motor keeps its exit shut
        /// until the motor runs, whatever else has been done, and opens the moment it does.
        ///
        /// Awake does not run in edit mode, so the manager is driven through its own
        /// methods rather than through GameManager.Instance.
        /// </summary>
        [MenuItem("Zombie House/Test Power", false, 32)]
        public static void TestPower()
        {
            int problems = 0;
            GameObject managerObject = null;
            GameObject motorObject = null;

            try
            {
                managerObject = new GameObject("PowerTestManager");
                var game = managerObject.AddComponent<GameManager>();

                if (game.RequiresMotor)
                {
                    Debug.LogError("[Power] A level with no motor is already demanding one.");
                    problems++;
                }

                // No zombies and no survivors, so the only thing that can hold the door
                // now is the motor.
                motorObject = new GameObject("TestMotor");
                var motor = motorObject.AddComponent<DoorMotor>();
                game.RegisterMotor(motor);
                game.ReportSpawningFinished();

                if (!game.RequiresMotor)
                {
                    Debug.LogError("[Power] The motor registered but the level does not require it.");
                    problems++;
                }

                if (game.ExitUnlocked)
                {
                    Debug.LogError("[Power] The door opened with the motor still dead.");
                    problems++;
                }
                else
                {
                    Debug.Log("[Power] Door held shut with the kill quota met and the motor unpowered.");
                }

                game.ReportMotorPowered(motor);

                if (!game.MotorPowered)
                {
                    Debug.LogError("[Power] The motor was powered and the manager did not notice.");
                    problems++;
                }

                if (!game.ExitUnlocked)
                {
                    Debug.LogError("[Power] The motor is running and the door is still shut.");
                    problems++;
                }
                else
                {
                    Debug.Log("[Power] Door opened the moment the motor came up.");
                }

                // And the cell has to be the thing that is carried, not a free flag.
                if (game.CarryingPowerCell)
                {
                    Debug.LogError("[Power] The manager thinks a cell is being carried when none exists.");
                    problems++;
                }

                // ---- the draw ------------------------------------------------
                // The whole point of the candidate list is that the cell moves. Draw from
                // one many times and count how many distinct spots come back: one means
                // the placement is effectively fixed and the level can be memorised.
                var candidates = new List<Vector3>();
                for (int i = 0; i < 8; i++) candidates.Add(new Vector3(i * 10f, 0f, 0f));

                var seen = new HashSet<Vector3>();
                var start = Vector3.zero;
                for (int i = 0; i < 200; i++)
                    seen.Add(PowerCellPlacement.Draw(candidates, start, start));

                if (seen.Count < 2)
                {
                    Debug.LogError($"[Power] 200 draws produced {seen.Count} position(s) — the cell does not move.");
                    problems++;
                }
                else
                {
                    Debug.Log($"[Power] 200 draws from 8 positions used {seen.Count} of them — the cell moves between runs.");
                }

                // It must also stay away from the doorstep: the near 40% are never drawn.
                bool drewNearest = false;
                foreach (Vector3 spot in seen)
                    if (Mathf.Approximately(spot.x, 0f) || Mathf.Approximately(spot.x, 10f)) drewNearest = true;

                if (drewNearest)
                {
                    Debug.LogError("[Power] The draw put the cell in the nearest positions to the player start.");
                    problems++;
                }
                else
                {
                    Debug.Log("[Power] The nearest positions to the start are never drawn — it is always a walk.");
                }
            }
            finally
            {
                if (motorObject != null) Object.DestroyImmediate(motorObject);
                if (managerObject != null) Object.DestroyImmediate(managerObject);
            }

            Debug.Log(problems == 0
                ? "[Power] PASS — no cell, no motor, no door."
                : $"[Power] FAIL — {problems} problem(s).");
        }

        /// <summary>
        /// The recoil pattern: that the first shot is the smallest, that a burst climbs,
        /// that the climb is capped, that bracing helps, and — the part that actually
        /// changes how the game plays — that some of every kick lands in your real aim
        /// rather than in an offset that gives it back a moment later.
        /// </summary>
        [MenuItem("Zombie House/Test Recoil", false, 30)]
        public static void TestRecoil()
        {
            int problems = 0;
            var rig = new GameObject("RecoilTestRig");

            try
            {
                var profile = new RecoilProfile
                {
                    verticalDegrees = 3f,
                    horizontalDegrees = 1f,
                    climbPerShot = 0.5f,
                    maximumClimb = 2.5f,
                    uncorrectedShare = 0.3f,
                    aimedMultiplier = 0.5f
                };

                float first = profile.NextKick(false).x;
                float second = profile.NextKick(false).x;
                float third = profile.NextKick(false).x;

                if (!Mathf.Approximately(first, 3f))
                {
                    Debug.LogError($"[Recoil] The first shot from rest kicked {first:0.00}, not the profile's 3.00.");
                    problems++;
                }

                if (second <= first || third <= second)
                {
                    Debug.LogError($"[Recoil] A burst is not climbing: {first:0.00}, {second:0.00}, {third:0.00}.");
                    problems++;
                }
                else
                {
                    Debug.Log($"[Recoil] Burst climbs {first:0.00} to {second:0.00} to {third:0.00} degrees.");
                }

                // Hold it down and the climb has to stop somewhere.
                for (int i = 0; i < 40; i++) profile.NextKick(false);
                float capped = profile.NextKick(false).x;
                if (capped > 3f * 2.5f + 0.01f)
                {
                    Debug.LogError($"[Recoil] Sustained fire reached {capped:0.00}, past the {3f * 2.5f:0.00} cap.");
                    problems++;
                }
                else
                {
                    Debug.Log($"[Recoil] Sustained fire caps at {capped:0.00} degrees a shot.");
                }

                // Settled, then braced: the same first shot, but softer.
                profile.Reset();
                float hip = profile.NextKick(false).x;
                profile.Reset();
                float aimed = profile.NextKick(true).x;

                if (aimed >= hip)
                {
                    Debug.LogError($"[Recoil] Aiming did not brace the weapon: {aimed:0.00} against {hip:0.00} from the hip.");
                    problems++;
                }
                else
                {
                    Debug.Log($"[Recoil] Bracing cuts the kick from {hip:0.00} to {aimed:0.00} degrees.");
                }

                // The part that matters: aim really moves.
                var look = rig.AddComponent<MouseLook>();
                float before = look.PitchDegrees;
                look.AddRecoil(10f, 0f, 0.3f);
                float moved = before - look.PitchDegrees;

                if (moved < 2.9f || moved > 3.1f)
                {
                    Debug.LogError($"[Recoil] A 10 degree kick at a 0.30 share moved the aim {moved:0.00} degrees; expected 3.00.");
                    problems++;
                }
                else
                {
                    Debug.Log($"[Recoil] A 10 degree kick leaves {moved:0.00} degrees in the aim — that is the part you have to pull back down.");
                }

                float noShare = look.PitchDegrees;
                look.AddRecoil(10f, 0f, 0f);
                if (!Mathf.Approximately(noShare, look.PitchDegrees))
                {
                    Debug.LogError("[Recoil] A kick with no uncorrected share still moved the aim.");
                    problems++;
                }
            }
            finally
            {
                Object.DestroyImmediate(rig);
            }

            Debug.Log(problems == 0
                ? "[Recoil] PASS — it climbs, it caps, it braces, and it does not all come back."
                : $"[Recoil] FAIL — {problems} problem(s).");
        }

        /// <summary>
        /// The town's own: a horse with pink eyes and shod hooves, and a walker in a hat
        /// and boots. Both are checked the way the bear is — against what is rendered,
        /// with the eye lights required to live inside the eyeballs so they fall with the
        /// head instead of hanging over the corpse.
        /// </summary>
        [MenuItem("Zombie House/Test Town", false, 31)]
        public static void TestTown()
        {
            int problems = 0;
            GameObject horse = null;
            GameObject cowboy = null;
            GameObject hostage = null;

            try
            {
                // ---- the horse ---------------------------------------------
                horse = ZombieHorseFactory.Create("HorseTest");

                var eyes = new List<Renderer>();
                int hooves = 0;

                foreach (Renderer renderer in horse.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.name.StartsWith("Eye") && !renderer.name.Contains("Glow")) eyes.Add(renderer);
                    if (renderer.name.StartsWith("Hoof")) hooves++;
                }

                if (hooves != 4)
                {
                    Debug.LogError($"[Town] The horse has {hooves} hooves.");
                    problems++;
                }
                else
                {
                    Material hoof = null;
                    foreach (Renderer renderer in horse.GetComponentsInChildren<Renderer>(true))
                        if (renderer.name.StartsWith("Hoof")) hoof = renderer.sharedMaterial;

                    float metallic = hoof != null && hoof.HasProperty("_Metallic") ? hoof.GetFloat("_Metallic") : 0f;
                    if (metallic < 0.5f)
                    {
                        Debug.LogError($"[Town] The hooves are not metal (metallic {metallic:0.00}).");
                        problems++;
                    }
                    else
                    {
                        Debug.Log($"[Town] Four shod hooves, metallic {metallic:0.00} — they catch the beam.");
                    }
                }

                if (eyes.Count != 2)
                {
                    Debug.LogError($"[Town] The horse has {eyes.Count} eyes, expected 2.");
                    problems++;
                }
                else
                {
                    Material eyeMaterial = eyes[0].sharedMaterial;
                    Color emission = eyeMaterial != null && eyeMaterial.HasProperty("_EmissionColor")
                        ? eyeMaterial.GetColor("_EmissionColor")
                        : Color.black;

                    // Pink is red and blue together with the green well down.
                    bool pink = emission.r > 1f && emission.b > 1f && emission.g < emission.b * 0.5f;
                    if (!pink)
                    {
                        Debug.LogError($"[Town] The horse's eyes glow {emission}, which is not bright pink.");
                        problems++;
                    }
                    else
                    {
                        Debug.Log($"[Town] Eyes glow {emission.r:0.0}/{emission.g:0.0}/{emission.b:0.0} — bright pink, lit from inside.");
                    }

                    int stranded = 0;
                    foreach (Light glow in horse.GetComponentsInChildren<Light>(true))
                    {
                        bool inside = false;
                        foreach (Renderer eye in eyes)
                            if (glow.transform.IsChildOf(eye.transform)) inside = true;
                        if (!inside) stranded++;
                    }

                    if (stranded > 0)
                    {
                        Debug.LogError($"[Town] {stranded} horse eye light(s) are not inside an eyeball — they would hang in the air when it falls.");
                        problems++;
                    }
                }

                // The ragdoll and the dismemberment tables work off bone names, so a horse
                // that does not use them is a horse that explodes when it dies.
                var rig = horse.GetComponent<ZombieRig>();
                if (rig == null || rig.Bones == null || rig.Bones.Head == null
                    || rig.Bones.ShoulderLeft == null || rig.Bones.HipRight == null)
                {
                    Debug.LogError("[Town] The horse rig is missing bones the ragdoll needs.");
                    problems++;
                }
                else
                {
                    Debug.Log("[Town] Horse rig uses the shared bone names, so ragdoll and dismemberment work unchanged.");
                }

                // ---- the hat and boots --------------------------------------
                cowboy = ZombieFactory.Create("CowboyTest", ZombieOutfit.Cowboy);

                bool hasBrim = false, hasCrown = false;
                int boots = 0, spurs = 0, dressedColliders = 0;

                foreach (Renderer renderer in cowboy.GetComponentsInChildren<Renderer>(true))
                {
                    string n = renderer.name;
                    if (n == "HatBrim") hasBrim = true;
                    if (n == "HatCrown") hasCrown = true;
                    if (n.StartsWith("BootFoot") || n.StartsWith("BootShaft")) boots++;
                    if (n.StartsWith("Spur")) spurs++;

                    bool dressing = n.StartsWith("Hat") || n.StartsWith("Boot") || n.StartsWith("Spur");
                    if (dressing && renderer.GetComponent<Collider>() != null) dressedColliders++;
                }

                if (!hasBrim || !hasCrown || boots < 4 || spurs != 2)
                {
                    Debug.LogError($"[Town] Cowboy kit incomplete: brim {hasBrim}, crown {hasCrown}, boot parts {boots}, spurs {spurs}.");
                    problems++;
                }
                else
                {
                    Debug.Log($"[Town] Hat and a pair of boots with spurs: {boots} boot parts, {spurs} spurs.");
                }

                if (dressedColliders > 0)
                {
                    Debug.LogError($"[Town] {dressedColliders} piece(s) of clothing carry colliders — a hat would soak head shots.");
                    problems++;
                }
                else
                {
                    Debug.Log("[Town] The clothing is cosmetic: a shot to the hat still hits the skull behind it.");
                }

                // ---- the hostages -------------------------------------------
                hostage = SurvivorFactory.Create("HostageTest", true);
                var bound = hostage.GetComponent<Survivor>();

                if (bound == null || !bound.IsBound)
                {
                    Debug.LogError("[Town] A hostage was not built as bound.");
                    problems++;
                }

                int ropes = 0;
                foreach (Renderer renderer in hostage.GetComponentsInChildren<Renderer>(true))
                    if (renderer.name.StartsWith("Rope")) ropes++;

                if (ropes < 2)
                {
                    Debug.LogError($"[Town] A hostage has {ropes} rope(s) — nothing says they are tied.");
                    problems++;
                }

                if (hostage.GetComponentsInChildren<Collider>(true).Length > 0)
                {
                    Debug.LogError("[Town] A hostage has colliders — they could be shot.");
                    problems++;
                }
                else
                {
                    Debug.Log($"[Town] Hostage: roped ({ropes} turns), upright against the post, and unshootable.");
                }
            }
            finally
            {
                if (horse != null) Object.DestroyImmediate(horse);
                if (cowboy != null) Object.DestroyImmediate(cowboy);
                if (hostage != null) Object.DestroyImmediate(hostage);
            }

            Debug.Log(problems == 0
                ? "[Town] PASS — pink-eyed horses, walkers in hats, and hostages you cannot shoot."
                : $"[Town] FAIL — {problems} problem(s).");
        }

        /// <summary>
        /// The bug this was written for: reload, switch weapon before it finishes, and
        /// the gun you come back to is dead — it will not fire and will not reload, for
        /// the rest of the run.
        ///
        /// The cause was a coroutine. Holstering deactivates the weapon's GameObject,
        /// Unity stops the routine where it stands, and the IsReloading flag it would
        /// have cleared on the way out stays true for ever. Both TryFire and TryReload
        /// check that flag first, so the weapon is bricked.
        ///
        /// The reload is a plain timer now, which is why this test can run it in a few
        /// calls — and why holstering can no longer strand it half done.
        /// </summary>
        [MenuItem("Zombie House/Test Reload Switch", false, 29)]
        public static void TestReloadSwitch()
        {
            int problems = 0;
            var rig = new GameObject("ReloadTestGun");

            try
            {
                var weapon = rig.AddComponent<Weapon>();

                // A zero fire interval keeps the time gate out of the way: edit mode does
                // not advance Time.time reliably, and this test is about reload state.
                weapon.ConfigureStats("TestGun", 50f, 80f, 0f, 6, 24, 1.9f, 0.3f, 8f, 70f, 55f, 5f);

                int ammoBefore = weapon.AmmoInMagazine + weapon.ReserveAmmo;

                // Spend a round so there is something to reload.
                weapon.TryFire();
                if (weapon.AmmoInMagazine != 5)
                {
                    Debug.LogError($"[Reload] Expected 5 rounds left in the magazine, found {weapon.AmmoInMagazine}.");
                    problems++;
                }

                weapon.TryReload();
                if (!weapon.IsReloading)
                {
                    Debug.LogError("[Reload] TryReload did not start a reload.");
                    Debug.Log("[Reload] FAIL — 1 problem(s).");
                    return;
                }

                // Part-way through...
                weapon.TickReload(0.6f);

                // ...and this is the moment the bug happened: holster mid-reload.
                rig.SetActive(false);
                rig.SetActive(true);

                if (!weapon.IsReloading)
                {
                    Debug.LogError("[Reload] Holstering silently cancelled the reload.");
                    problems++;
                }

                // Draw it again and let the reload run out.
                weapon.TickReload(2f);

                if (weapon.IsReloading)
                {
                    Debug.LogError("[Reload] The gun is still reloading after long enough to finish twice — this is the bug.");
                    problems++;
                }
                else if (weapon.AmmoInMagazine != weapon.MagazineSize)
                {
                    Debug.LogError($"[Reload] Reload finished with {weapon.AmmoInMagazine}/{weapon.MagazineSize} in the magazine.");
                    problems++;
                }
                else if (!weapon.CanFire)
                {
                    Debug.LogError("[Reload] The gun will not fire after a reload interrupted by a weapon switch.");
                    problems++;
                }
                else
                {
                    Debug.Log($"[Reload] Holstered mid-reload, redrawn, finished: {weapon.AmmoInMagazine}/{weapon.MagazineSize} and ready to fire.");
                }

                // And it must still work a second time, which the old bug also broke.
                weapon.TryFire();
                weapon.TryReload();
                weapon.TickReload(2f);

                if (weapon.IsReloading || !weapon.CanFire)
                {
                    Debug.LogError("[Reload] The gun could not complete a second reload.");
                    problems++;
                }

                int ammoAfter = weapon.AmmoInMagazine + weapon.ReserveAmmo;
                if (ammoAfter != ammoBefore - 2)
                {
                    Debug.LogError($"[Reload] Ammunition went from {ammoBefore} to {ammoAfter} across two shots — rounds were created or lost.");
                    problems++;
                }
                else
                {
                    Debug.Log($"[Reload] Ammunition is conserved: {ammoBefore} to {ammoAfter} for two shots fired.");
                }
            }
            finally
            {
                Object.DestroyImmediate(rig);
            }

            Debug.Log(problems == 0
                ? "[Reload] PASS — switching weapons mid-reload no longer bricks the gun."
                : $"[Reload] FAIL — {problems} problem(s).");
        }

        /// <summary>
        /// The survivor rules, without having to play a level to the end: that the door
        /// stays shut while someone is still out there, that it opens the moment the last
        /// one is found, and that a survivor cannot be shot.
        ///
        /// The no-colliders check is the one that matters most. Everything else here is
        /// bookkeeping; that one is the difference between rescuing someone and killing
        /// them with a stray round, which is a thing the player would never forgive.
        /// </summary>
        [MenuItem("Zombie House/Test Survivors", false, 28)]
        public static void TestSurvivors()
        {
            int problems = 0;
            GameObject survivor = null;
            GameObject managerObject = null;

            try
            {
                // ---- the figure --------------------------------------------
                survivor = SurvivorFactory.Create("SurvivorTest");

                Collider[] colliders = survivor.GetComponentsInChildren<Collider>(true);
                if (colliders.Length > 0)
                {
                    Debug.LogError($"[Survivor] Has {colliders.Length} collider(s) — a stray round could kill the person you came for.");
                    problems++;
                }
                else
                {
                    Debug.Log("[Survivor] No colliders anywhere: bullets, blades and blasts pass straight through.");
                }

                Light lantern = survivor.GetComponentInChildren<Light>(true);
                if (lantern == null)
                {
                    Debug.LogError("[Survivor] No lantern — nothing to find them by in the dark.");
                    problems++;
                }
                else
                {
                    Debug.Log($"[Survivor] Lantern: {lantern.range:0.0} m, warm ({lantern.color.r:0.00}/{lantern.color.g:0.00}/{lantern.color.b:0.00}).");
                }

                int renderers = survivor.GetComponentsInChildren<Renderer>(true).Length;
                if (renderers < 6)
                {
                    Debug.LogError($"[Survivor] Only {renderers} visible parts — that will not read as a person.");
                    problems++;
                }

                // ---- the gate ----------------------------------------------
                // Awake does not run in edit mode, so talk to the component directly
                // rather than through GameManager.Instance.
                managerObject = new GameObject("GateTestManager");
                var game = managerObject.AddComponent<GameManager>();

                // No zombies in this test, so the kill quota is satisfied from the start:
                // whatever holds the door now can only be the survivors.
                game.RegisterSurvivor(null);
                game.RegisterSurvivor(null);
                game.ReportSpawningFinished();

                if (game.ExitUnlocked)
                {
                    Debug.LogError("[Survivor] The door opened with survivors still out there.");
                    problems++;
                }
                else
                {
                    Debug.Log($"[Survivor] Door held shut with {game.SurvivorsRemaining} still to find, kill quota already met.");
                }

                game.ReportSurvivorRescued(null);
                if (game.ExitUnlocked)
                {
                    Debug.LogError("[Survivor] The door opened after only one of two was found.");
                    problems++;
                }

                game.ReportSurvivorRescued(null);
                if (!game.ExitUnlocked)
                {
                    Debug.LogError("[Survivor] Everyone is accounted for and the door is still shut.");
                    problems++;
                }
                else
                {
                    Debug.Log($"[Survivor] Door opened on the last rescue: {game.SurvivorsRescued}/{game.SurvivorsTotal}.");
                }
            }
            finally
            {
                if (survivor != null) Object.DestroyImmediate(survivor);
                if (managerObject != null) Object.DestroyImmediate(managerObject);
            }

            Debug.Log(problems == 0
                ? "[Survivor] PASS — they cannot be shot, and the door waits for them."
                : $"[Survivor] FAIL — {problems} problem(s).");
        }

        /// <summary>
        /// The power cell has to be somewhere you can walk to, the motor has to be
        /// somewhere you can walk to carrying it, and the two have to be far enough apart
        /// that fetching it is a journey. A cell sitting next to its own motor is not a
        /// puzzle, it is a button.
        /// </summary>
        private static int CheckPowerRoute(ILevelSource level, string tag)
        {
            int problems = 0;

            NavMeshHit startHit, motorHit;
            bool startOk = NavMesh.SamplePosition(level.PlayerSpawn, out startHit, 5f, NavMesh.AllAreas);
            bool motorOk = NavMesh.SamplePosition(level.MotorPosition, out motorHit, 6f, NavMesh.AllAreas);

            if (!motorOk)
            {
                Debug.LogError($"[{tag}] The door motor is not reachable on foot.");
                return 1;
            }

            var candidates = level.PowerCellCandidates;
            if (candidates == null || candidates.Count < 2)
            {
                Debug.LogError($"[{tag}] Only {(candidates == null ? 0 : candidates.Count)} power cell position(s) " +
                               "— the cell is supposed to move between runs.");
                return 1;
            }

            // Every candidate has to work, because any of them can be the live one. A
            // single unreachable spot is a run in which the level cannot be finished, and
            // it would show up as an unrepeatable bug rather than as a broken level.
            int reachable = 0, tooClose = 0, offMesh = 0;
            float shortest = float.MaxValue, longest = 0f;

            foreach (Vector3 candidate in candidates)
            {
                NavMeshHit cellHit;
                if (!NavMesh.SamplePosition(candidate, out cellHit, 5f, NavMesh.AllAreas))
                {
                    offMesh++;
                    continue;
                }

                if (!startOk) continue;

                var toCell = new NavMeshPath();
                NavMesh.CalculatePath(startHit.position, cellHit.position, NavMesh.AllAreas, toCell);

                var toMotor = new NavMeshPath();
                NavMesh.CalculatePath(cellHit.position, motorHit.position, NavMesh.AllAreas, toMotor);

                if (toCell.status != NavMeshPathStatus.PathComplete
                    || toMotor.status != NavMeshPathStatus.PathComplete)
                {
                    continue;
                }

                float carry = 0f;
                for (int i = 1; i < toMotor.corners.Length; i++)
                    carry += Vector3.Distance(toMotor.corners[i - 1], toMotor.corners[i]);

                if (carry < 20f)
                {
                    tooClose++;
                    continue;
                }

                reachable++;
                if (carry < shortest) shortest = carry;
                if (carry > longest) longest = carry;
            }

            if (offMesh > 0)
            {
                Debug.LogError($"[{tag}] {offMesh} of {candidates.Count} power cell positions are off the NavMesh — " +
                               "a run that picks one of those cannot be finished.");
                problems++;
            }

            if (tooClose > 0)
            {
                Debug.LogError($"[{tag}] {tooClose} power cell position(s) are within 20 m of the motor — " +
                               "that is a button, not an errand.");
                problems++;
            }

            if (reachable != candidates.Count)
            {
                Debug.LogError($"[{tag}] Only {reachable} of {candidates.Count} power cell positions are usable.");
                problems++;
            }
            else
            {
                Debug.Log($"[{tag}] Power cell: {candidates.Count} possible positions, all reachable, " +
                          $"carry {shortest:0}–{longest:0} m to the motor. Drawn fresh each run.");
            }

            return problems;
        }

        /// <summary>
        /// Belt crates: the only thing in the world that reloads the gatling gun.
        ///
        /// There have to be several and they have to be reachable, but unlike the weapon
        /// itself the count is not sacred — the design is "somewhere to go when it runs
        /// low", not "exactly N". What is checked is that the level has enough of them to
        /// be worth routing around, and that none is stranded off the NavMesh.
        /// </summary>
        private static int CheckBeltCrates(ILevelSource level, string tag)
        {
            var crates = level.BeltCrateSpawns;

            if (crates == null || crates.Count < 3)
            {
                Debug.LogError($"[{tag}] {(crates == null ? 0 : crates.Count)} belt crate(s); " +
                               "the gatling gun needs somewhere to reload.");
                return 1;
            }

            NavMeshHit startHit;
            if (!NavMesh.SamplePosition(level.PlayerSpawn, out startHit, 5f, NavMesh.AllAreas)) return 0;

            int reachable = 0;
            foreach (Vector3 crate in crates)
            {
                NavMeshHit hit;
                if (!NavMesh.SamplePosition(crate, out hit, 4f, NavMesh.AllAreas)) continue;

                var path = new NavMeshPath();
                NavMesh.CalculatePath(startHit.position, hit.position, NavMesh.AllAreas, path);
                if (path.status == NavMeshPathStatus.PathComplete) reachable++;
            }

            if (reachable != crates.Count)
            {
                Debug.LogError($"[{tag}] Only {reachable} of {crates.Count} belt crates can be walked to.");
                return 1;
            }

            Debug.Log($"[{tag}] Belt crates: {crates.Count}, all reachable, 150 rounds each.");
            return 0;
        }

        /// <summary>
        /// The gatling guns: exactly two, and both somewhere you can actually walk over.
        ///
        /// The count is checked as hard as the reachability. Two is a design decision —
        /// enough that the weapon shows up in a run, few enough that spending it matters
        /// — and a level that quietly ends up with three has had that decision undone.
        /// </summary>
        private static int CheckPowerUps(ILevelSource level, string tag)
        {
            var spawns = level.PowerUpSpawns;

            if (spawns == null || spawns.Count != 2)
            {
                Debug.LogError($"[{tag}] {(spawns == null ? 0 : spawns.Count)} Uzi(s) on this level; there should be exactly 2.");
                return 1;
            }

            NavMeshHit startHit;
            if (!NavMesh.SamplePosition(level.PlayerSpawn, out startHit, 5f, NavMesh.AllAreas)) return 0;

            int reachable = 0;
            float nearest = float.MaxValue, furthest = 0f;

            foreach (Vector3 spawn in spawns)
            {
                NavMeshHit hit;
                if (!NavMesh.SamplePosition(spawn, out hit, 4f, NavMesh.AllAreas)) continue;

                var path = new NavMeshPath();
                NavMesh.CalculatePath(startHit.position, hit.position, NavMesh.AllAreas, path);
                if (path.status != NavMeshPathStatus.PathComplete) continue;

                float walk = 0f;
                for (int i = 1; i < path.corners.Length; i++)
                    walk += Vector3.Distance(path.corners[i - 1], path.corners[i]);

                reachable++;
                if (walk < nearest) nearest = walk;
                if (walk > furthest) furthest = walk;
            }

            if (reachable != 2)
            {
                Debug.LogError($"[{tag}] Only {reachable} of the 2 Uzis can be walked to.");
                return 1;
            }

            Debug.Log($"[{tag}] Uzis: 2, both reachable, {nearest:0} m and {furthest:0} m from the start. 200 rounds each.");
            return 0;
        }

        /// <summary>
        /// Survivors have to exist, and every one of them has to be somewhere the player
        /// can actually walk to — a survivor behind a wall is a level that cannot be
        /// finished, because the door will not open until they are all found.
        /// </summary>
        private static int CheckSurvivors(ILevelSource level, string tag)
        {
            int problems = 0;
            int total = level.SurvivorSpawns.Count;

            if (total == 0)
            {
                Debug.LogError($"[{tag}] No survivors on this level — the door would never open.");
                return 1;
            }

            NavMeshHit startHit;
            bool startOk = NavMesh.SamplePosition(level.PlayerSpawn, out startHit, 5f, NavMesh.AllAreas);

            int reachable = 0;
            foreach (Vector3 spawn in level.SurvivorSpawns)
            {
                NavMeshHit hit;
                if (!NavMesh.SamplePosition(spawn, out hit, 4f, NavMesh.AllAreas)) continue;
                if (!startOk) continue;

                var path = new NavMeshPath();
                NavMesh.CalculatePath(startHit.position, hit.position, NavMesh.AllAreas, path);
                if (path.status == NavMeshPathStatus.PathComplete) reachable++;
            }

            if (reachable < total)
            {
                Debug.LogError($"[{tag}] Only {reachable} of {total} survivors can be reached on foot — the level cannot be completed.");
                problems++;
            }
            else
            {
                Debug.Log($"[{tag}] Survivors: {total}, all reachable from the player start.");
            }

            return problems;
        }

        /// <summary>
        /// Confirms the forest is lit by a moon you could actually see by, and that the
        /// sky it hangs in will still be there after the scene is saved.
        /// </summary>
        private static int CheckNightLighting()
        {
            int problems = 0;

            Light moon = RenderSettings.sun;
            if (moon == null)
            {
                Debug.LogError("[Night] No sun/moon assigned — the wood has no directional light.");
                problems++;
            }
            else
            {
                Debug.Log($"[Night] Moonlight: {moon.intensity:0.00} intensity, shadows {moon.shadows}, " +
                          $"elevation {moon.transform.eulerAngles.x:0}°.");

                // A band, not a floor: dark is the point, but a wood with no moonlight
                // at all is unplayable, and one lit like an afternoon is not night.
                if (moon.intensity < 0.35f)
                {
                    Debug.LogError($"[Night] Moonlight is only {moon.intensity:0.00} — nothing outside the torch beam would be visible.");
                    problems++;
                }
                else if (moon.intensity > 1f)
                {
                    Debug.LogError($"[Night] Moonlight is {moon.intensity:0.00} — that is daylight, not a night under a moon.");
                    problems++;
                }
            }

            Material sky = RenderSettings.skybox;
            if (sky == null)
            {
                Debug.LogError("[Night] No skybox — there is no moon in the sky to look at.");
                problems++;
            }
            else if (!AssetDatabase.Contains(sky))
            {
                // The trap that eats in-memory materials on save, caught before it does.
                Debug.LogError("[Night] The sky material is not an asset; it will be lost when the scene is saved.");
                problems++;
            }
            else
            {
                float sunSize = sky.GetFloat("_SunSize");
                string fog = RenderSettings.fog ? RenderSettings.fogDensity.ToString("0.000") : "off";
                Debug.Log($"[Night] Sky: {sky.name}, moon disc {sunSize:0.000} wide. Fog {fog}.");
            }

            return problems;
        }

        /// <summary>
        /// The bear: that a rifle round hurts it far more than a pistol round, and that it
        /// has the teeth and the eyes it is supposed to have.
        ///
        /// The eye lights are checked for being children of the eyeballs rather than of
        /// the head — ZombieRagdoll re-parents renderers when the body falls, not lights,
        /// so a light on the head would hang in mid-air over the corpse.
        /// </summary>
        [MenuItem("Zombie House/Test Bear", false, 27)]
        public static void TestBear()
        {
            int problems = 0;
            GameObject bear = null;
            GameObject dummy = null;

            try
            {
                // ---- damage ------------------------------------------------
                ZombieArchetype bearType = null, shamblerType = null;
                foreach (ZombieArchetype a in ZombieArchetype.Catalogue)
                {
                    if (a.Kind == ZombieKind.Bear) bearType = a;
                    if (a.Kind == ZombieKind.Shambler) shamblerType = a;
                }

                if (bearType == null || shamblerType == null)
                {
                    Debug.LogError("[Bear] The archetype catalogue is missing the bear or the shambler.");
                    Debug.Log("[Bear] FAIL — 1 problem(s).");
                    return;
                }

                dummy = new GameObject("BearDamageDummy");
                var health = dummy.AddComponent<ZombieHealth>();

                const float RifleDamage = 100f;

                health.ApplyArchetype(bearType);
                float before = health.Current;
                var round = new DamageInfo(RifleDamage, Vector3.zero, Vector3.up, Vector3.forward, null);
                round.Kind = DamageKind.RifleRound;
                health.TakeDamage(round);
                float rifleDealt = before - health.Current;

                health.ApplyArchetype(bearType);
                before = health.Current;
                health.TakeDamage(new DamageInfo(RifleDamage, Vector3.zero, Vector3.up, Vector3.forward, null));
                float bulletDealt = before - health.Current;

                if (rifleDealt <= bulletDealt)
                {
                    Debug.LogError($"[Bear] A rifle round does {rifleDealt:0} to a bear and a pistol round {bulletDealt:0} — the rifle is no better.");
                    problems++;
                }
                else
                {
                    Debug.Log($"[Bear] Rifle {rifleDealt:0} vs bullet {bulletDealt:0} on {bearType.Health:0} health: " +
                              $"{Mathf.CeilToInt(bearType.Health / rifleDealt)} rifle body shots to kill.");
                }

                // Nothing else may have been made softer by the same change.
                health.ApplyArchetype(shamblerType);
                before = health.Current;
                var walkerRound = new DamageInfo(RifleDamage, Vector3.zero, Vector3.up, Vector3.forward, null);
                walkerRound.Kind = DamageKind.RifleRound;
                health.TakeDamage(walkerRound);
                float walkerDealt = before - health.Current;

                if (!Mathf.Approximately(walkerDealt, RifleDamage))
                {
                    Debug.LogError($"[Bear] A rifle round now does {walkerDealt:0} to a shambler; it should still do {RifleDamage:0}.");
                    problems++;
                }
                else
                {
                    Debug.Log("[Bear] Walkers are unaffected: a rifle round still does exactly its base damage.");
                }

                // ---- the face ----------------------------------------------
                bear = ZombieBearFactory.Create("BearLookTest", LoadOrCreateFangMesh());

                int teeth = 0, toothlessFilters = 0;
                var eyes = new List<Renderer>();

                foreach (Renderer renderer in bear.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.name.StartsWith("Tooth"))
                    {
                        teeth++;
                        var filter = renderer.GetComponent<MeshFilter>();
                        if (filter != null && filter.sharedMesh == null) toothlessFilters++;
                    }
                    else if (renderer.name.StartsWith("Eye") && !renderer.name.Contains("Glow"))
                    {
                        eyes.Add(renderer);
                    }
                }

                if (teeth < 8)
                {
                    Debug.LogError($"[Bear] Only {teeth} teeth — the mouth is not going to frighten anyone.");
                    problems++;
                }
                else if (toothlessFilters > 0)
                {
                    Debug.LogError($"[Bear] {toothlessFilters} of {teeth} teeth have an empty mesh filter and would be invisible.");
                    problems++;
                }
                else
                {
                    Debug.Log($"[Bear] {teeth} teeth, all with a mesh.");
                }

                if (eyes.Count != 2)
                {
                    Debug.LogError($"[Bear] Found {eyes.Count} eyes, expected 2.");
                    problems++;
                }
                else
                {
                    Material eyeMaterial = eyes[0].sharedMaterial;
                    Color emission = eyeMaterial != null && eyeMaterial.HasProperty("_EmissionColor")
                        ? eyeMaterial.GetColor("_EmissionColor")
                        : Color.black;

                    if (emission.maxColorComponent < 0.5f)
                    {
                        Debug.LogError("[Bear] The eyes are painted red but not lit — they will not read in the dark.");
                        problems++;
                    }
                    else if (emission.r < emission.g || emission.r < emission.b)
                    {
                        Debug.LogError($"[Bear] The eyes glow {emission}, which is not blood red.");
                        problems++;
                    }
                    else
                    {
                        Debug.Log($"[Bear] Eyes glow {emission.r:0.0}/{emission.g:0.0}/{emission.b:0.0} — red, and lit from inside.");
                    }

                    int strandedLights = 0;
                    foreach (Light glow in bear.GetComponentsInChildren<Light>(true))
                    {
                        bool insideAnEye = false;
                        foreach (Renderer eye in eyes)
                            if (glow.transform.IsChildOf(eye.transform)) insideAnEye = true;

                        if (!insideAnEye) strandedLights++;
                    }

                    if (strandedLights > 0)
                    {
                        Debug.LogError($"[Bear] {strandedLights} eye light(s) are not inside an eyeball — they would hang in the air when the bear falls.");
                        problems++;
                    }
                    else
                    {
                        Debug.Log("[Bear] Both eye lights live inside their eyeballs, so they fall with the head.");
                    }
                }
            }
            finally
            {
                if (bear != null) Object.DestroyImmediate(bear);
                if (dummy != null) Object.DestroyImmediate(dummy);
            }

            Debug.Log(problems == 0
                ? "[Bear] PASS — the rifle bites, and so does the bear."
                : $"[Bear] FAIL — {problems} problem(s).");
        }

        /// <summary>
        /// Reports the sidearm the level actually shipped with, and fails if it is not
        /// carrying a recoil pattern — a weapon configured with none would be a laser.
        /// </summary>
        /// <summary>
        /// The share of the level that has to die before the way out opens.
        ///
        /// Worth checking in every level rather than once, because it is a serialized field
        /// on a component in a saved scene: changing the class default fixes newly built
        /// scenes and does nothing at all to one that was built last week. A scene that
        /// quietly kept the old 90% would look completely normal right up until the last
        /// stretch of a twenty-minute run.
        /// </summary>
        /// <summary>
        /// The level's camera has a post stack, on the world camera, with HDR headroom.
        ///
        /// Checked per level because it is scene state: a level built before the stack
        /// existed keeps its old camera and looks flat next to the other five, and nothing
        /// about it reads as broken — it is just worse, which is the hardest kind of
        /// regression to notice.
        /// </summary>
        private static int CheckPostFx(string tag)
        {
            var stacks = Object.FindObjectsByType<ZombieHouse.Fx.PostProcessStack>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (stacks.Length == 0)
            {
                Debug.LogError($"[{tag}] No post stack in the scene — rebuild it.");
                return 1;
            }

            var stack = stacks[0];
            var camera = stack.GetComponent<Camera>();

            if (camera == null)
            {
                Debug.LogError($"[{tag}] The post stack is not on a camera.");
                return 1;
            }

            if (!camera.allowHDR)
            {
                Debug.LogError($"[{tag}] The graded camera is not HDR; nothing will bloom.");
                return 1;
            }

            if (camera.name == "WeaponCamera")
            {
                Debug.LogError($"[{tag}] The post stack is on the weapon camera, not the world one.");
                return 1;
            }

            Color filter = stack.ColourFilter;
            Debug.Log($"[{tag}] Post: bloom {stack.BloomIntensity:0.00} over {stack.Threshold:0.00}, " +
                      $"filter ({filter.r:0.00}, {filter.g:0.00}, {filter.b:0.00}).");
            return 0;
        }

        private static int CheckKillQuota(string tag)
        {
            var manager = Object.FindAnyObjectByType<GameManager>();
            if (manager == null)
            {
                Debug.LogError($"[{tag}] No GameManager in the scene.");
                return 1;
            }

            var field = new SerializedObject(manager).FindProperty("requiredKillFraction");
            if (field == null)
            {
                Debug.LogError($"[{tag}] GameManager has no requiredKillFraction.");
                return 1;
            }

            float share = field.floatValue;
            if (Mathf.Abs(share - 2f / 3f) > 0.02f)
            {
                Debug.LogError($"[{tag}] The exit wants {share:P0} of the level dead; it should want two thirds. " +
                               "The scene is stale — rebuild it.");
                return 1;
            }

            Debug.Log($"[{tag}] Exit quota: {share:P0} of the level, every survivor, and the motor running.");
            return 0;
        }

        private static int CheckSidearm(string tag)
        {
            Weapon found = null;
            foreach (Weapon weapon in Object.FindObjectsByType<Weapon>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (weapon.WeaponName.StartsWith("Desert")) found = weapon;

            if (found == null)
            {
                Debug.LogError($"[{tag}] No Desert Eagle on the player rig.");
                return 1;
            }

            RecoilProfile recoil = found.Recoil;
            if (recoil == null || recoil.verticalDegrees <= 0f)
            {
                Debug.LogError($"[{tag}] {found.WeaponName} has no recoil pattern.");
                return 1;
            }

            Debug.Log($"[{tag}] Sidearm: {found.WeaponName}, {found.Damage:0} damage, " +
                      $"{found.MagazineSize} rounds, {recoil.verticalDegrees:0.0} degree kick, " +
                      $"{recoil.uncorrectedShare:P0} of it uncorrected.");

            return CheckScavengedWeapon(tag);
        }

        /// <summary>
        /// The Uzi as it actually shipped in this scene, rather than as a test configured
        /// it. This is the difference that matters: the lifecycle test builds its own
        /// weapon, so an assertion there compares one literal against another and would
        /// happily pass while the real Uzi in every level carried the old numbers.
        /// </summary>
        private static int CheckScavengedWeapon(string tag)
        {
            Weapon gatling = null, uzi = null;
            foreach (Weapon weapon in Object.FindObjectsByType<Weapon>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (weapon.WeaponName.StartsWith("Gatling")) gatling = weapon;
                if (weapon.WeaponName == "Uzi") uzi = weapon;
            }

            if (gatling == null)
            {
                Debug.LogError($"[{tag}] No gatling gun in the player rig — it is supposed to be part of the loadout.");
                return 1;
            }

            if (uzi == null)
            {
                Debug.LogError($"[{tag}] No Uzi in the player rig — the power-up would have nothing to grant.");
                return 1;
            }

            int problemsHere = 0;

            // The gatling is yours from the start and belt-fed, so it must NOT be
            // discarded when empty: a gun you lose permanently in the first room makes
            // every belt crate in the level worthless.
            if (gatling.DiscardWhenEmpty)
            {
                Debug.LogError($"[{tag}] The gatling gun is marked disposable; it would vanish the first time it ran dry.");
                problemsHere++;
            }

            if (!gatling.IsRotary)
            {
                Debug.LogError($"[{tag}] The gatling gun has no spin-up.");
                problemsHere++;
            }

            int gatlingLoaded = gatling.MagazineSize + gatling.ReserveAmmo;
            if (gatlingLoaded != 400)
            {
                Debug.LogError($"[{tag}] The gatling gun will load {gatlingLoaded} rounds, not 400.");
                problemsHere++;
            }

            if (problemsHere == 0)
            {
                Debug.Log($"[{tag}] Loadout: Desert Eagle, rifle, and a belt-fed gatling gun " +
                          $"({gatlingLoaded} rounds, spins up, kept when empty).");
            }

            int problems = problemsHere;

            // Count what it will have in play, not what the scene file holds.
            // AmmoInMagazine is an auto-property, and Unity does not serialise those, so
            // an unplayed scene always reads zero in the magazine — Weapon.Awake fills it
            // from magazineSize on load. Asserting on TotalAmmo here would fail forever
            // for a weapon that is perfectly correct the moment you press Play.
            int loaded = uzi.MagazineSize + uzi.ReserveAmmo;

            if (loaded != 200)
            {
                Debug.LogError($"[{tag}] The Uzi will load {loaded} rounds, not 200.");
                problems++;
            }

            if (!uzi.DiscardWhenEmpty)
            {
                Debug.LogError($"[{tag}] The Uzi is not marked disposable; it would survive running dry.");
                problems++;
            }

            if (problems == problemsHere)
            {
                Debug.Log($"[{tag}] Power-up: {uzi.WeaponName}, {uzi.Damage:0} damage, {loaded} rounds, " +
                          "discarded when empty.");
            }

            return problems;
        }

        /// <summary>
        /// Confirms the player rig in the open scene actually carries a working torch.
        ///
        /// Checks the Light, not the component: a Flashlight that exists but whose beam
        /// is missing, is not a spotlight, or has been culled away from the world layer
        /// leaves the player in the dark while every field still reads correctly.
        /// </summary>
        private static int CheckTorchOnPlayer()
        {
            var torch = Object.FindAnyObjectByType<Flashlight>();
            if (torch == null)
            {
                Debug.LogError("[Torch] No Flashlight on the player rig — F does nothing.");
                return 1;
            }

            // Awake has not run in a scene that was only opened, so build the torch the
            // way the game would and inspect its own beam. Never search the rig for a
            // Light: the muzzle flash is one too, and it is the one you find first.
            torch.Initialise();
            Light beam = torch.Beam;
            if (beam == null)
            {
                Debug.LogError("[Torch] The torch built no beam — the player would have no light.");
                return 1;
            }

            int problems = 0;
            if (beam.type != LightType.Spot)
            {
                Debug.LogError($"[Torch] Beam is a {beam.type}, not a spotlight.");
                problems++;
            }

            if ((beam.cullingMask & 1) == 0)
            {
                Debug.LogError("[Torch] Beam does not light the default layer — it would miss the level.");
                problems++;
            }

            if (problems == 0)
                Debug.Log($"[Torch] Beam OK: {beam.spotAngle:0} degree spot, {beam.range:0} m, shadows {beam.shadows}.");

            return problems;
        }

        /// <summary>
        /// Runs a torch through a whole cell without anyone standing in a dark room for
        /// two and a half minutes: on, drained flat, swapped, and lit again.
        ///
        /// Every assertion is made against the Light the player would see rather than
        /// against the component's own bookkeeping — the same trap the ragdoll test fell
        /// into, where the state was right and the picture was wrong.
        /// </summary>
        [MenuItem("Zombie House/Test Flashlight", false, 26)]
        public static void TestFlashlight()
        {
            var rig = new GameObject("TorchTestRig");
            int problems = 0;

            try
            {
                var torch = rig.AddComponent<Flashlight>();

                // Awake does not run in edit mode, so build the torch explicitly — this
                // is exactly what the running game does one frame after it spawns you.
                torch.Initialise();

                Light beam = torch.Beam;
                if (beam == null)
                {
                    Debug.LogError("[Torch] No beam was built — the torch renders nothing.");
                    Debug.Log("[Torch] FAIL — 1 problem(s).");
                    return;
                }

                if (beam.type != LightType.Spot)
                {
                    Debug.LogError($"[Torch] Beam is a {beam.type}, not a spotlight.");
                    problems++;
                }

                if ((beam.cullingMask & 1) == 0)
                {
                    Debug.LogError("[Torch] Beam misses the default layer.");
                    problems++;
                }

                if (beam.renderMode != LightRenderMode.ForcePixel)
                {
                    Debug.LogError("[Torch] Beam is not pixel-lit — it will not pick out edges.");
                    problems++;
                }

                if (beam.enabled)
                {
                    Debug.LogError("[Torch] Beam is lit before the torch is switched on.");
                    problems++;
                }

                torch.Press();
                torch.Tick(0f);
                if (!beam.enabled || beam.intensity <= 0f)
                {
                    Debug.LogError("[Torch] Pressing F did not light the beam.");
                    problems++;
                }

                // Drain it flat. 20 s a step is far past any sane frame, which is the point
                // — nothing here may depend on being ticked at a particular rate.
                int steps = 0;
                while (!torch.IsDead && steps < 200) { torch.Tick(20f); steps++; }

                if (!torch.IsDead)
                {
                    Debug.LogError("[Torch] The cell never ran out.");
                    problems++;
                }
                else if (beam.enabled)
                {
                    Debug.LogError("[Torch] The cell is flat but the beam is still lit.");
                    problems++;
                }
                else
                {
                    Debug.Log($"[Torch] Cell ran flat in {steps} step(s) and the beam went out.");
                }

                int sparesBefore = torch.SpareCells;
                torch.Press();
                if (!torch.IsSwapping)
                {
                    Debug.LogError("[Torch] Pressing F on a dead cell with a spare did not start a swap.");
                    problems++;
                }

                if (beam.enabled)
                {
                    Debug.LogError("[Torch] Beam is lit mid-swap.");
                    problems++;
                }

                torch.Tick(2f);
                if (torch.IsSwapping || !beam.enabled || torch.BatteryFraction < 0.99f)
                {
                    Debug.LogError($"[Torch] Swap did not finish cleanly — lit {beam.enabled}, charge {torch.BatteryFraction:P0}.");
                    problems++;
                }
                else
                {
                    Debug.Log($"[Torch] Swapped a fresh cell in: spares {sparesBefore} to {torch.SpareCells}, beam relit.");
                }

                // A spare you have no room for has to stay on the floor.
                int guard = 0;
                while (torch.AddBattery(1) > 0 && guard++ < 20) { }
                if (torch.AddBattery(1) != 0)
                {
                    Debug.LogError("[Torch] A full pocket still swallows batteries — pickups would vanish.");
                    problems++;
                }
                else
                {
                    Debug.Log($"[Torch] Pocket fills at {torch.SpareCells} spare(s) and then refuses more.");
                }

                // Flat, with no spares left: F has to be safe, and it has to stay dark.
                while (torch.SpareCells > 0)
                {
                    while (!torch.IsDead) torch.Tick(20f);
                    torch.Press();
                    torch.Tick(2f);
                }

                while (!torch.IsDead) torch.Tick(20f);
                for (int i = 0; i < 8; i++) { torch.Press(); torch.Tick(2f); }

                if (beam.enabled)
                {
                    Debug.LogError("[Torch] Beam came back with a flat cell and no spares.");
                    problems++;
                }
                else
                {
                    Debug.Log("[Torch] Flat with no spares: F leaves you in the dark, as it should.");
                }
            }
            finally
            {
                Object.DestroyImmediate(rig);
            }

            Debug.Log(problems == 0
                ? "[Torch] PASS — the torch lights, drains, dies and swaps."
                : $"[Torch] FAIL — {problems} problem(s).");
        }

        /// <summary>
        /// Drops a zombie ragdoll onto a floor and steps the physics engine by hand,
        /// then reports where the body ended up.
        ///
        /// Ragdolls fail loudly and visually — limbs fling apart when joints or
        /// interpenetrating colliders are misconfigured — but that failure is perfectly
        /// measurable: parts end up far from the body and the corpse never comes to rest.
        /// This catches it without anyone having to watch a zombie die.
        /// </summary>
        [MenuItem("Zombie House/Test Ragdoll", false, 23)]
        public static void TestRagdoll()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ZombiePrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[Ragdoll] No zombie prefab — run Build Level 1 Scene first.");
                return;
            }

            EnsureLayer(CorpseLayerName);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // A floor for the body to land on.
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(40f, 1f, 40f);

            // Plain Instantiate, exactly as ZombieSpawner does at runtime. A *connected*
            // prefab instance cannot be restructured, and the ragdoll re-parents cosmetic
            // parts onto bones — so PrefabUtility.InstantiatePrefab would test a
            // restriction the real game never has.
            var zombie = Object.Instantiate(prefab);
            zombie.transform.position = Vector3.zero;

            var agent = zombie.GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;   // no NavMesh in this scratch scene

            var ragdoll = zombie.GetComponent<ZombieRagdoll>();
            if (ragdoll == null)
            {
                Debug.LogError("[Ragdoll] Prefab has no ZombieRagdoll component.");
                return;
            }

            // A solid hit to the chest, from the front.
            var blow = new DamageInfo(100f, new Vector3(0f, 1.2f, 0.2f), Vector3.back,
                                      Vector3.forward, null, false);

            SimulationMode previousMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;

            try
            {
                ragdoll.Collapse(blow);

                const float step = 1f / 60f;
                for (int i = 0; i < 60 * 5; i++) Physics.Simulate(step);
            }
            finally
            {
                Physics.simulationMode = previousMode;
            }

            ReportRagdollResult(zombie);
        }

        private static void ReportRagdollResult(GameObject zombie)
        {
            var bodies = zombie.GetComponentsInChildren<Rigidbody>();
            if (bodies.Length == 0)
            {
                Debug.LogError("[Ragdoll] FAIL — no rigidbodies were created.");
                return;
            }

            float furthest = 0f;
            float highest = float.MinValue;
            float lowest = float.MaxValue;
            bool invalid = false;
            string furthestPart = string.Empty;

            foreach (Rigidbody body in bodies)
            {
                Vector3 p = body.worldCenterOfMass;

                if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) ||
                    float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z))
                {
                    invalid = true;
                    continue;
                }

                float horizontal = new Vector2(p.x, p.z).magnitude;
                if (horizontal > furthest)
                {
                    furthest = horizontal;
                    furthestPart = body.name;
                }

                highest = Mathf.Max(highest, p.y);
                lowest = Mathf.Min(lowest, p.y);
            }

            int joints = zombie.GetComponentsInChildren<CharacterJoint>().Length;
            Debug.Log($"[Ragdoll] {bodies.Length} bodies, {joints} joints. " +
                      $"After 5 s: furthest part {furthest:F2} m from the drop point ({furthestPart}), " +
                      $"heights {lowest:F2} m to {highest:F2} m.");

            int problems = 0;

            // Rigidbodies are not what the player sees. Cosmetic parts have no colliders,
            // so they never become rigidbodies — if they stay parented to the animation
            // pivots they are left standing in mid-air while the body falls. Measure the
            // renderers, because that is the corpse the player actually looks at.
            float highestRenderer = float.MinValue;
            string standingPart = string.Empty;

            foreach (Renderer renderer in zombie.GetComponentsInChildren<Renderer>())
            {
                float y = renderer.bounds.center.y;
                if (y <= highestRenderer) continue;

                highestRenderer = y;
                standingPart = renderer.name;
            }

            Debug.Log($"[Ragdoll] Highest visible part: '{standingPart}' at {highestRenderer:F2} m.");

            if (highestRenderer > 1.0f)
            {
                Debug.LogError($"[Ragdoll] FAIL — '{standingPart}' is still at {highestRenderer:F2} m " +
                               "while the body is on the floor. It is left standing as a ghost.");
                problems++;
            }

            if (invalid)
            {
                Debug.LogError("[Ragdoll] FAIL — a body reached an invalid position (the solver blew up).");
                problems++;
            }

            // A body that fell over occupies roughly its own length. Parts scattered
            // several metres away mean the joints tore apart.
            if (furthest > 2.5f)
            {
                Debug.LogError($"[Ragdoll] FAIL — '{furthestPart}' ended {furthest:F2} m away; the ragdoll exploded.");
                problems++;
            }

            // Lying down: nothing should still be at standing height.
            if (highest > 1.0f)
            {
                Debug.LogError($"[Ragdoll] FAIL — highest part is still at {highest:F2} m; the body did not fall over.");
                problems++;
            }

            if (lowest < -0.5f)
            {
                Debug.LogError($"[Ragdoll] FAIL — a part sank to {lowest:F2} m; it fell through the floor.");
                problems++;
            }

            Debug.Log(problems == 0
                ? "[Ragdoll] PASS — the body collapsed and came to rest on the floor."
                : $"[Ragdoll] FAIL — {problems} problem(s).");
        }

        /// <summary>
        /// Cuts an arm off and drops it, then checks the whole severed piece ended up on
        /// the floor together.
        ///
        /// This is the same failure the ragdoll test was built for: a limb's clothing and
        /// hand have no colliders, so if physics is applied to the bone rather than to the
        /// limb as a whole, the bone falls and the sleeve stays hanging in mid-air. The
        /// check is on renderers for exactly that reason.
        /// </summary>
        [MenuItem("Zombie House/Test Dismemberment", false, 24)]
        public static void TestDismemberment()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ZombiePrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[Dismember] No zombie prefab — run Build Level 1 Scene first.");
                return;
            }

            EnsureLayer(CorpseLayerName);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(40f, 1f, 40f);

            var zombie = Object.Instantiate(prefab);
            zombie.transform.position = Vector3.zero;

            var agent = zombie.GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;

            // Awake does not run in edit mode, so stand the components up by hand.
            var health = zombie.GetComponent<ZombieHealth>();
            var dismemberment = zombie.GetComponent<ZombieDismemberment>();
            if (health == null || dismemberment == null)
            {
                Debug.LogError("[Dismember] Prefab is missing ZombieHealth or ZombieDismemberment.");
                return;
            }

            health.ApplyArchetype(ZombieArchetype.Catalogue[0]);
            dismemberment.Initialise();

            Collider forearm = null;
            foreach (Collider collider in zombie.GetComponentsInChildren<Collider>())
                if (collider.name.StartsWith("Forearm_L")) forearm = collider;

            if (forearm == null)
            {
                Debug.LogError("[Dismember] Could not find the left forearm on the prefab.");
                return;
            }

            SimulationMode previousMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;

            try
            {
                SeveredPart severed = dismemberment.Sever(forearm, Vector3.forward, forearm.bounds.center);
                Debug.Log($"[Dismember] Cut result: {severed}.");

                if (severed == SeveredPart.None)
                {
                    Debug.LogError("[Dismember] FAIL — the cut did not sever anything.");
                    return;
                }

                const float step = 1f / 60f;
                for (int i = 0; i < 60 * 4; i++) Physics.Simulate(step);
            }
            finally
            {
                Physics.simulationMode = previousMode;
            }

            ReportSeveredPiece();
        }

        private static void ReportSeveredPiece()
        {
            GameObject piece = GameObject.Find("SeveredPart");
            if (piece == null)
            {
                Debug.LogError("[Dismember] FAIL — no severed piece was created.");
                return;
            }

            var renderers = piece.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Debug.LogError("[Dismember] FAIL — the severed piece has nothing visible on it.");
                return;
            }

            float highest = float.MinValue;
            float lowest = float.MaxValue;
            string highestName = string.Empty;
            float spread = 0f;

            foreach (Renderer renderer in renderers)
            {
                Vector3 p = renderer.bounds.center;
                if (p.y > highest) { highest = p.y; highestName = renderer.name; }
                lowest = Mathf.Min(lowest, p.y);

                foreach (Renderer other in renderers)
                    spread = Mathf.Max(spread, Vector3.Distance(p, other.bounds.center));
            }

            Debug.Log($"[Dismember] Severed piece has {renderers.Length} visible part(s); " +
                      $"heights {lowest:F2} m to {highest:F2} m, spread {spread:F2} m.");

            int problems = 0;

            if (highest > 0.6f)
            {
                Debug.LogError($"[Dismember] FAIL — '{highestName}' is still at {highest:F2} m. " +
                               "Part of the severed limb is floating.");
                problems++;
            }

            // The pieces of one limb must stay together; a metre apart means the clothing
            // was left behind while the bone fell.
            if (spread > 0.7f)
            {
                Debug.LogError($"[Dismember] FAIL — the piece is spread over {spread:F2} m; " +
                               "it came apart instead of falling as one limb.");
                problems++;
            }

            Debug.Log(problems == 0
                ? "[Dismember] PASS — the severed limb fell to the floor in one piece."
                : $"[Dismember] FAIL — {problems} problem(s).");
        }

        [MenuItem("Zombie House/Rebuild Zombie Prefab", false, 21)]
        public static void RebuildZombiePrefab()
        {
            EnsureFolder(PrefabsFolder);
            ProtoMaterials.ClearCache();
            BuildZombiePrefab(EnsureLayer(EnemyLayerName));
            Debug.Log("[ZombieHouse] Zombie prefab rebuilt at " + ZombiePrefabPath);
        }

        // ---- assets ---------------------------------------------------------

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>Which level's look to dial in. See <see cref="ApplyPostFx"/>.</summary>
        private enum LevelMood { House, Forest, Town, School, Tomb, Jungle }

        /// <summary>
        /// Gives a level its grade. Every one of these is the same four decisions — how
        /// much things glow, what colour the air is, how hard the contrast is, and how
        /// closed-in the frame feels — and the six sets of numbers are most of what makes
        /// the levels feel like different places rather than the same game in new geometry.
        ///
        /// The colour filter does the heavy lifting and is deliberately continuous with
        /// each level's lighting rather than fighting it: the wood is already cold blue
        /// moonlight, so its filter leans the same way and the grade reinforces the scene
        /// instead of arguing with it.
        /// </summary>
        private static void ApplyPostFx(GameObject player, LevelMood mood)
        {
            var stack = player.GetComponentInChildren<ZombieHouse.Fx.PostProcessStack>(true);
            if (stack == null)
            {
                Debug.LogWarning("[ZombieHouse] No post stack on the player rig.");
                return;
            }

            switch (mood)
            {
                case LevelMood.Forest:
                    // Cold, wide bloom on very little light: moonlight through fog, and a
                    // torch beam that should visibly bleed at its edge.
                    stack.Configure(bloom: 1.05f, bloomThreshold: 0.85f,
                                    filter: new Color(0.88f, 0.94f, 1.08f),
                                    grade: 1.10f, saturate: 0.88f, vignette: 1.35f, grain: 0.045f);
                    break;

                case LevelMood.Town:
                    // Dust and low sun. Warm, dry, and the least vignetted of the six —
                    // a street is the one place in this game with somewhere to look.
                    stack.Configure(bloom: 0.75f, bloomThreshold: 1.10f,
                                    filter: new Color(1.08f, 1.00f, 0.88f),
                                    grade: 1.08f, saturate: 1.02f, vignette: 0.95f, grain: 0.030f);
                    break;

                case LevelMood.School:
                    // Strip lighting: nearly neutral, slightly green, and a tight bloom so
                    // the tubes flare without the corridor turning to soup.
                    stack.Configure(bloom: 0.70f, bloomThreshold: 1.15f,
                                    filter: new Color(0.97f, 1.03f, 0.98f),
                                    grade: 1.05f, saturate: 0.94f, vignette: 1.15f, grain: 0.028f);
                    break;

                case LevelMood.Tomb:
                    // The strongest grade of the six. Firelight is the only light there is,
                    // so the bloom threshold drops to catch the torches and the filter is
                    // openly warm — the tomb should look like a different game to the wood.
                    stack.Configure(bloom: 1.20f, bloomThreshold: 0.80f,
                                    filter: new Color(1.12f, 0.96f, 0.80f),
                                    grade: 1.14f, saturate: 0.96f, vignette: 1.45f, grain: 0.050f);
                    break;

                case LevelMood.Jungle:
                    // Green-grey murk under a closed canopy. Heavy vignette, low saturation
                    // so the whole frame sits in one narrow band of colour, and the most
                    // grain of any level because there is almost nothing else in the dark.
                    stack.Configure(bloom: 0.95f, bloomThreshold: 0.90f,
                                    filter: new Color(0.90f, 1.05f, 0.94f),
                                    grade: 1.12f, saturate: 0.86f, vignette: 1.50f, grain: 0.052f);
                    break;

                default:
                    // The house: interior lamps, warm but restrained. This is the baseline
                    // the other five are tuned against.
                    stack.Configure(bloom: 0.85f, bloomThreshold: 1.05f,
                                    filter: new Color(1.04f, 1.00f, 0.96f),
                                    grade: 1.06f, saturate: 1.00f, vignette: 1.10f, grain: 0.035f);
                    break;
            }
        }

        private static void CreatePlaceholderMaterials()
        {
            CreateMaterial("floor", new Color(0.32f, 0.29f, 0.26f), 0.05f, 0f);
            CreateMaterial("wall", new Color(0.58f, 0.55f, 0.50f), 0.03f, 0f);
            CreateMaterial("flooralt", new Color(0.27f, 0.24f, 0.21f), 0.07f, 0f);
            CreateMaterial("trim", new Color(0.20f, 0.16f, 0.13f), 0.12f, 0f);
            CreateMaterial("wainscot", new Color(0.44f, 0.40f, 0.35f), 0.06f, 0f);

            // Forest palette.
            CreateMaterial("forestfloor", new Color(0.10f, 0.12f, 0.08f), 0.03f, 0f);
            CreateMaterial("bark", new Color(0.15f, 0.12f, 0.10f), 0.04f, 0f);
            CreateMaterial("foliage", new Color(0.07f, 0.13f, 0.08f), 0.05f, 0f);
            CreateMaterial("rock", new Color(0.19f, 0.20f, 0.21f), 0.08f, 0f);
            CreateMaterial("path", new Color(0.17f, 0.15f, 0.12f), 0.03f, 0f);
            CreateMaterial("bearfur", new Color(0.13f, 0.11f, 0.10f), 0.06f, 0f);
            CreateMaterial("beartooth", new Color(0.82f, 0.79f, 0.66f), 0.55f, 0f);
            CreateMaterial("survivorcoat", new Color(0.72f, 0.46f, 0.16f), 0.10f, 0f);

            // Old West palette.
            // Egypt palette.
            CreateMaterial("sandstone", new Color(0.62f, 0.52f, 0.35f), 0.04f, 0f);
            CreateMaterial("sandstonealt", new Color(0.54f, 0.44f, 0.29f), 0.04f, 0f);
            CreateMaterial("sand", new Color(0.68f, 0.58f, 0.38f), 0.02f, 0f);
            CreateMaterial("granite", new Color(0.26f, 0.22f, 0.22f), 0.22f, 0f);
            CreateMaterial("gold", new Color(0.82f, 0.66f, 0.22f), 0.72f, 0.95f);
            CreateMaterial("hieroglyph", new Color(0.40f, 0.32f, 0.20f), 0.06f, 0f);
            CreateMaterial("bandage", new Color(0.72f, 0.66f, 0.50f), 0.05f, 0f);
            CreateMaterial("bandagedark", new Color(0.46f, 0.40f, 0.29f), 0.05f, 0f);
            CreateMaterial("scarabshell", new Color(0.10f, 0.16f, 0.13f), 0.78f, 0.7f);
            CreateMaterial("scarablimb", new Color(0.13f, 0.12f, 0.10f), 0.20f, 0f);

            Material scarabEye = CreateMaterial("scarabeye", new Color(0.55f, 0.45f, 0.05f), 0.6f, 0f);
            ProtoMaterials.MakeEmissive(scarabEye, new Color(2.1f, 1.5f, 0.15f));
            EditorUtility.SetDirty(scarabEye);

            Material flame = CreateMaterial("flame", new Color(1f, 0.62f, 0.22f), 0.3f, 0f);
            ProtoMaterials.MakeEmissive(flame, new Color(2.6f, 1.35f, 0.35f));
            EditorUtility.SetDirty(flame);

            // Jungle palette. Every one of these has to exist as an asset, not just in
            // ProtoMaterials: a material built in memory does not survive being saved into
            // a prefab, and a prefab whose material reference is dangling renders magenta.
            // That is exactly what happened to the snakes, jaguars and monkeys — the
            // valley itself looked right, because its geometry is generated at Awake in
            // play mode where in-memory materials are perfectly fine.
            CreateMaterial("barkpale", new Color(0.34f, 0.30f, 0.24f), 0.06f, 0f);
            CreateMaterial("canopy", new Color(0.07f, 0.17f, 0.08f), 0.10f, 0f);
            CreateMaterial("frond", new Color(0.12f, 0.28f, 0.11f), 0.14f, 0f);
            CreateMaterial("fronddark", new Color(0.06f, 0.14f, 0.07f), 0.12f, 0f);
            CreateMaterial("undergrowth", new Color(0.10f, 0.20f, 0.09f), 0.08f, 0f);
            CreateMaterial("junglefloor", new Color(0.16f, 0.14f, 0.10f), 0.04f, 0f);
            CreateMaterial("mud", new Color(0.20f, 0.16f, 0.11f), 0.34f, 0f);
            CreateMaterial("water", new Color(0.08f, 0.16f, 0.15f), 0.92f, 0.2f);
            CreateMaterial("vine", new Color(0.15f, 0.22f, 0.10f), 0.16f, 0f);
            CreateMaterial("moss", new Color(0.13f, 0.24f, 0.12f), 0.05f, 0f);
            CreateMaterial("ruinstone", new Color(0.30f, 0.31f, 0.27f), 0.10f, 0f);

            // The snake: banded scales with a wet sheen.
            CreateMaterial("snakescale", new Color(0.13f, 0.20f, 0.11f), 0.62f, 0.25f);
            CreateMaterial("snakeband", new Color(0.30f, 0.24f, 0.06f), 0.58f, 0.25f);

            Material snakeEye = CreateMaterial("snakeeye", new Color(0.60f, 0.52f, 0.06f), 0.5f, 0f);
            ProtoMaterials.MakeEmissive(snakeEye, new Color(1.6f, 1.2f, 0.10f));
            EditorUtility.SetDirty(snakeEye);

            // The jaguar: tawny pelt, black rosettes, eyes that catch what light there is.
            CreateMaterial("jaguarpelt", new Color(0.42f, 0.31f, 0.14f), 0.08f, 0f);
            CreateMaterial("jaguarspot", new Color(0.09f, 0.07f, 0.05f), 0.10f, 0f);

            Material jaguarEye = CreateMaterial("jaguareye", new Color(0.68f, 0.62f, 0.20f), 0.55f, 0f);
            ProtoMaterials.MakeEmissive(jaguarEye, new Color(2.2f, 1.9f, 0.45f));
            EditorUtility.SetDirty(jaguarEye);

            // The monkeys: dark fur, bare face and hands.
            CreateMaterial("monkeyfur", new Color(0.17f, 0.13f, 0.11f), 0.06f, 0f);
            CreateMaterial("monkeyface", new Color(0.33f, 0.24f, 0.21f), 0.14f, 0f);

            // School palette.
            CreateMaterial("cardigan", new Color(0.42f, 0.30f, 0.36f), 0.05f, 0f);
            CreateMaterial("staffbadge", new Color(0.86f, 0.84f, 0.78f), 0.30f, 0f);
            CreateMaterial("kidshirt", new Color(0.24f, 0.46f, 0.68f), 0.06f, 0f);
            CreateMaterial("backpack", new Color(0.58f, 0.22f, 0.20f), 0.10f, 0f);
            CreateMaterial("coveralls", new Color(0.09f, 0.11f, 0.16f), 0.05f, 0f);
            CreateMaterial("mophead", new Color(0.52f, 0.50f, 0.44f), 0.08f, 0f);
            CreateMaterial("linoleum", new Color(0.44f, 0.43f, 0.39f), 0.30f, 0f);
            CreateMaterial("linoleumalt", new Color(0.37f, 0.37f, 0.34f), 0.30f, 0f);
            CreateMaterial("schoolwall", new Color(0.55f, 0.54f, 0.46f), 0.05f, 0f);
            CreateMaterial("locker", new Color(0.22f, 0.35f, 0.31f), 0.45f, 0.6f);
            CreateMaterial("chalkboard", new Color(0.09f, 0.14f, 0.11f), 0.06f, 0f);
            CreateMaterial("chalktray", new Color(0.42f, 0.33f, 0.22f), 0.12f, 0f);
            CreateMaterial("chalkdust", new Color(0.82f, 0.83f, 0.80f), 0.05f, 0f);
            CreateMaterial("whiteboard", new Color(0.88f, 0.88f, 0.85f), 0.55f, 0f);
            CreateMaterial("desktop", new Color(0.68f, 0.55f, 0.36f), 0.20f, 0f);
            CreateMaterial("gymfloor", new Color(0.60f, 0.44f, 0.25f), 0.35f, 0f);

            Material tube = CreateMaterial("fluorescent", new Color(0.90f, 0.94f, 0.98f), 0.4f, 0f);
            ProtoMaterials.MakeEmissive(tube, new Color(1.7f, 1.85f, 2.0f));
            EditorUtility.SetDirty(tube);

            CreateMaterial("beltcrate", new Color(0.13f, 0.46f, 0.19f), 0.30f, 0.25f);
            CreateMaterial("gunblack", new Color(0.055f, 0.058f, 0.065f), 0.62f, 0.85f);
            CreateMaterial("gunedge", new Color(0.30f, 0.31f, 0.34f), 0.80f, 0.95f);

            Material dot = CreateMaterial("sightdot", new Color(0.35f, 0.85f, 0.45f), 0.5f, 0f);
            ProtoMaterials.MakeEmissive(dot, new Color(0.6f, 2.1f, 0.9f));
            EditorUtility.SetDirty(dot);

            CreateMaterial("windowdark", new Color(0.05f, 0.06f, 0.08f), 0.72f, 0f);
            CreateMaterial("cactus", new Color(0.24f, 0.31f, 0.19f), 0.10f, 0f);
            CreateMaterial("cactusspine", new Color(0.68f, 0.64f, 0.44f), 0.35f, 0f);

            Material litGlass = CreateMaterial("windowlit", new Color(0.92f, 0.72f, 0.40f), 0.6f, 0f);
            ProtoMaterials.MakeEmissive(litGlass, new Color(1.5f, 1.02f, 0.45f));
            EditorUtility.SetDirty(litGlass);

            CreateMaterial("cellcase", new Color(0.20f, 0.24f, 0.28f), 0.30f, 0.6f);
            CreateMaterial("motorhousing", new Color(0.26f, 0.27f, 0.30f), 0.45f, 0.8f);

            Material stripe = CreateMaterial("cellstripe", new Color(0.85f, 0.62f, 0.10f), 0.5f, 0f);
            ProtoMaterials.MakeEmissive(stripe, new Color(1.9f, 1.15f, 0.15f));
            EditorUtility.SetDirty(stripe);

            CreateMaterial("hatleather", new Color(0.19f, 0.14f, 0.10f), 0.12f, 0f);
            CreateMaterial("bootleather", new Color(0.30f, 0.19f, 0.11f), 0.18f, 0f);
            CreateMaterial("denim", new Color(0.20f, 0.26f, 0.36f), 0.05f, 0f);
            CreateMaterial("adobe", new Color(0.52f, 0.42f, 0.31f), 0.04f, 0f);
            CreateMaterial("plank", new Color(0.36f, 0.26f, 0.17f), 0.08f, 0f);
            CreateMaterial("plankpale", new Color(0.46f, 0.36f, 0.26f), 0.08f, 0f);
            CreateMaterial("dust", new Color(0.33f, 0.27f, 0.20f), 0.02f, 0f);
            CreateMaterial("mesa", new Color(0.30f, 0.20f, 0.15f), 0.05f, 0f);
            CreateMaterial("horsehide", new Color(0.17f, 0.14f, 0.13f), 0.07f, 0f);
            CreateMaterial("horsemane", new Color(0.09f, 0.08f, 0.08f), 0.05f, 0f);
            CreateMaterial("horsehoof", new Color(0.55f, 0.57f, 0.60f), 0.80f, 0.95f);

            Material horseEye = CreateMaterial("horseeye", new Color(0.60f, 0.10f, 0.40f), 0.7f, 0f);
            ProtoMaterials.MakeEmissive(horseEye, new Color(2.8f, 0.35f, 1.7f));
            EditorUtility.SetDirty(horseEye);

            Material lantern = CreateMaterial("lantern", new Color(0.95f, 0.72f, 0.38f), 0.6f, 0f);
            ProtoMaterials.MakeEmissive(lantern, new Color(2.2f, 1.35f, 0.55f));
            EditorUtility.SetDirty(lantern);

            // Emission has to be switched on explicitly, exactly like the scope glass.
            Material eye = CreateMaterial("beareye", new Color(0.35f, 0.02f, 0.02f), 0.7f, 0f);
            ProtoMaterials.MakeEmissive(eye, new Color(2.6f, 0.10f, 0.06f));
            EditorUtility.SetDirty(eye);
            CreateMaterial("ground", new Color(0.14f, 0.16f, 0.13f), 0.02f, 0f);
            CreateMaterial("zombie", new Color(0.36f, 0.47f, 0.31f), 0.15f, 0f);
            CreateMaterial("zombiehead", new Color(0.52f, 0.55f, 0.38f), 0.20f, 0f);
            CreateMaterial("ammo", new Color(0.75f, 0.62f, 0.18f), 0.35f, 0.4f);
            CreateMaterial("medkit", new Color(0.78f, 0.16f, 0.18f), 0.35f, 0f);
            CreateMaterial("exit", new Color(0.20f, 0.85f, 0.45f), 0.50f, 0f);
            CreateMaterial("weapon", new Color(0.16f, 0.16f, 0.18f), 0.45f, 0.6f);
            CreateMaterial("wood", new Color(0.30f, 0.20f, 0.13f), 0.12f, 0f);
            CreateMaterial("fabric", new Color(0.26f, 0.28f, 0.32f), 0.04f, 0f);
            CreateMaterial("metal", new Color(0.42f, 0.44f, 0.47f), 0.55f, 0.7f);
            CreateMaterial("linen", new Color(0.62f, 0.60f, 0.56f), 0.08f, 0f);

            // Walker palette.
            CreateMaterial("skin", new Color(0.55f, 0.56f, 0.48f), 0.10f, 0f);
            CreateMaterial("shirt", new Color(0.21f, 0.20f, 0.18f), 0.04f, 0f);
            CreateMaterial("trousers", new Color(0.17f, 0.18f, 0.21f), 0.04f, 0f);
            CreateMaterial("gore", new Color(0.26f, 0.03f, 0.03f), 0.30f, 0f);
            CreateMaterial("hair", new Color(0.11f, 0.09f, 0.08f), 0.06f, 0f);
            CreateMaterial("blade", new Color(0.62f, 0.64f, 0.68f), 0.75f, 0.9f);

            // Scope glass: alpha blending has to be switched on explicitly, not just by
            // giving the colour an alpha below one.
            Material glass = CreateMaterial("glass", new Color(0.55f, 0.62f, 0.68f, 0.22f), 0.85f, 0f);
            ProtoMaterials.MakeTransparent(glass);
            EditorUtility.SetDirty(glass);

            AssetDatabase.SaveAssets();
        }

        private static Material CreateMaterial(string key, Color color, float smoothness, float metallic)
        {
            string path = MaterialsFolder + "/" + key + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");

            var material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static int EnsureLayer(string layerName)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning("[ZombieHouse] Could not open TagManager; falling back to the Default layer.");
                return 0;
            }

            var tagManager = new SerializedObject(assets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            for (int i = 0; i < layers.arraySize; i++)
                if (layers.GetArrayElementAtIndex(i).stringValue == layerName) return i;

            for (int i = 8; i < layers.arraySize; i++)   // 0-7 are Unity's built-ins
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue)) continue;

                slot.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                return i;
            }

            Debug.LogWarning("[ZombieHouse] No free layer slots for '" + layerName + "'.");
            return 0;
        }

        private static GameObject BuildZombiePrefab(int enemyLayer)
        {
            GameObject temp = ZombieFactory.Create("Zombie");
            SetLayerRecursively(temp, enemyLayer);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, ZombiePrefabPath);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        /// <summary>The town's dead: the same walker, in a hat and boots.</summary>
        private static GameObject BuildCowboyPrefab(int enemyLayer)
        {
            GameObject temp = ZombieFactory.Create("ZombieCowboy", ZombieOutfit.Cowboy);
            SetLayerRecursively(temp, enemyLayer);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, CowboyPrefabPath);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        /// <summary>The school's three, all the same humanoid in different clothes.</summary>
        private static GameObject BuildSchoolPrefab(int enemyLayer, ZombieOutfit outfit, string path)
        {
            GameObject temp = ZombieFactory.Create("Zombie" + outfit, outfit);
            SetLayerRecursively(temp, enemyLayer);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        private static GameObject BuildScarabPrefab(int enemyLayer)
        {
            GameObject temp = ScarabFactory.Create("Scarab");
            SetLayerRecursively(temp, enemyLayer);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, ScarabPrefabPath);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        /// <summary>
        /// The jungle's three. One builder because they differ only in which factory call
        /// makes the body — everything downstream of that is identical, and three
        /// near-identical methods is how the town ended up with a stale one.
        /// </summary>
        private static GameObject BuildJunglePrefab(int enemyLayer, ZombieKind kind)
        {
            GameObject temp;
            string path;

            switch (kind)
            {
                case ZombieKind.Snake:
                    temp = JungleFactory.CreateSnake("Snake");
                    path = SnakePrefabPath;
                    break;

                case ZombieKind.Monkey:
                    temp = JungleFactory.CreateMonkey("Monkey");
                    path = MonkeyPrefabPath;
                    break;

                default:
                    temp = JungleFactory.CreateJaguar("Jaguar");
                    path = JaguarPrefabPath;
                    break;
            }

            SetLayerRecursively(temp, enemyLayer);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        private static GameObject BuildHorsePrefab(int enemyLayer)
        {
            GameObject temp = ZombieHorseFactory.Create("ZombieHorse");
            SetLayerRecursively(temp, enemyLayer);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, HorsePrefabPath);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        // ---- scene ----------------------------------------------------------

        private static void ConfigureLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;

            // The roof shuts the moonlight out, so the interior leans on ambient plus the
            // point lights. Lifted from the open-roofed blockout to keep rooms readable.
            RenderSettings.ambientLight = new Color(0.24f, 0.25f, 0.29f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.06f, 0.07f, 0.09f);
            RenderSettings.fogDensity = 0.012f;

            var sunObject = new GameObject("Moonlight");
            sunObject.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.62f, 0.70f, 0.92f);
            sun.intensity = 0.35f;
            sun.shadows = LightShadows.Soft;

            RenderSettings.sun = sun;
        }

        private static GameObject BuildPlayerRig(int playerLayer, int enemyLayer)
        {
            var player = new GameObject("Player");
            player.tag = "Player";
            player.layer = playerLayer;
            player.transform.position = new Vector3(0f, 0.2f, 0f);

            var controller = player.AddComponent<CharacterController>();
            controller.radius = 0.34f;
            controller.height = 1.8f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.slopeLimit = 45f;
            controller.stepOffset = 0.4f;
            controller.skinWidth = 0.04f;

            player.AddComponent<PlayerController>();
            player.AddComponent<PlayerHealth>();
            player.AddComponent<ZombieHouse.Audio.PlayerAudio>();

            // --- camera ------------------------------------------------------
            var cameraObject = new GameObject("PlayerCamera");
            cameraObject.tag = "MainCamera";
            cameraObject.layer = playerLayer;
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.65f, 0f);

            int viewModelLayer = LayerMask.NameToLayer(ViewModelLayerName);
            if (viewModelLayer < 0) viewModelLayer = playerLayer;

            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 70f;
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 300f;

            // The world camera does not draw the weapon at all.
            camera.cullingMask &= ~(1 << viewModelLayer);

            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<MouseLook>();

            // --- view model camera -------------------------------------------
            // A second camera draws the weapon on top of the world at a FIXED field of
            // view. Aiming narrows the world camera to zoom in, and because the gun is no
            // longer rendered by that camera it keeps its normal size instead of swelling
            // to fill the screen. It also gets its own near plane, so the muzzle never
            // clips through a wall the player is standing against.
            // The post stack, on the world camera. Every level gets one; each level's
            // builder then dials in its own mood right afterwards, next to its lighting.
            cameraObject.AddComponent<ZombieHouse.Fx.PostProcessStack>();

            var weaponCameraObject = new GameObject("WeaponCamera");
            weaponCameraObject.transform.SetParent(cameraObject.transform, false);

            var weaponCamera = weaponCameraObject.AddComponent<Camera>();
            weaponCamera.clearFlags = CameraClearFlags.Depth;   // draw over the world, keep it
            weaponCamera.cullingMask = 1 << viewModelLayer;
            weaponCamera.depth = camera.depth + 1;
            weaponCamera.fieldOfView = 55f;
            weaponCamera.nearClipPlane = 0.01f;
            weaponCamera.farClipPlane = 12f;

            // --- weapons -----------------------------------------------------
            // Level geometry, live zombies, and corpses — bodies on the floor still stop
            // bullets rather than letting them pass through.
            LayerMask hitMask = (1 << 0) | (1 << enemyLayer);
            int corpseLayer = LayerMask.NameToLayer(CorpseLayerName);
            if (corpseLayer >= 0) hitMask |= 1 << corpseLayer;

            GameObject pistol = BuildPistol(cameraObject.transform, viewModelLayer, hitMask);
            GameObject rifle = BuildRifle(cameraObject.transform, viewModelLayer, hitMask);
            GameObject gatling = BuildGatling(cameraObject.transform, viewModelLayer, hitMask);

            // Slot 1 pistol, slot 2 rifle. The rifle is the quick-draw weapon: pressing
            // Space draws it and fires it, so it is usable without touching the switcher.
            GameObject uzi = BuildUzi(cameraObject.transform, viewModelLayer, hitMask);

            // Three weapons from the start now: sidearm, rifle and the gatling gun. The
            // gatling is no longer something you find — it is yours, it is belt-fed, and
            // what it costs you is the crates you have to route through to keep it alive.
            var switcher = cameraObject.AddComponent<WeaponSwitcher>();
            switcher.Configure(new[] { pistol, rifle, gatling }, 0, 1);

            // The Uzi takes over as the scavenged weapon: two a level, 200 rounds, and
            // when the last one is gone so is the gun.
            switcher.ConfigurePowerUp(uzi);

            cameraObject.AddComponent<ZombieHouse.Fx.CameraShake>();

            // The torch goes on the camera so the beam always points where you look.
            cameraObject.AddComponent<Flashlight>();

            BuildKatana(cameraObject.transform, camera, viewModelLayer, enemyLayer);

            // No ScopedWeaponHider any more: the weapon camera keeps the gun at a sane
            // size while scoped, so it can stay on screen.

            return player;
        }

        /// <summary>Shared rig for any gun: muzzle, flash, view model, effects and audio.</summary>
        private static GameObject BuildGunFrame(Transform cameraTransform, string name, int layer,
                                                Vector3 localPosition, Vector3 muzzleLocal,
                                                LayerMask hitMask, float flashIntensity)
        {
            var root = new GameObject(name);
            root.layer = layer;
            root.transform.SetParent(cameraTransform, false);
            root.transform.localPosition = localPosition;
            root.transform.localRotation = Quaternion.identity;

            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(root.transform, false);
            muzzle.transform.localPosition = muzzleLocal;

            var flashObject = new GameObject("MuzzleFlash");
            flashObject.transform.SetParent(muzzle.transform, false);

            var flash = flashObject.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.color = new Color(1f, 0.85f, 0.55f);
            flash.intensity = flashIntensity;
            flash.range = 11f;
            flash.shadows = LightShadows.None;
            flash.enabled = false;

            var weapon = root.AddComponent<Weapon>();
            weapon.ConfigureReferences(muzzle.transform, flash, hitMask);

            root.AddComponent<WeaponViewModel>();
            root.AddComponent<WeaponFx>();
            root.AddComponent<ZombieHouse.Audio.WeaponAudio>();

            return root;
        }

        private static GameObject BuildPistol(Transform cameraTransform, int layer, LayerMask hitMask)
        {
            GameObject root = BuildGunFrame(cameraTransform, "Pistol", layer,
                new Vector3(0.23f, -0.20f, 0.42f), new Vector3(0f, 0.015f, 0.30f), hitMask, 4.5f);

            BuildPistolBody(root.transform, layer);

            // The Desert Eagle .50. 125 a body shot puts a shambler down in one, and a
            // head shot (312) kills anything short of a brute outright — but seven rounds,
            // a 0.42 s cycle and a wrist-breaking kick mean you cannot simply hold it down.
            // The last figure is how far the shot is heard: a .50 carries 40 m, against the
            // old pistol's 5, so every shot in the town calls the street.
            var deagle = root.GetComponent<Weapon>();
            deagle.ConfigureStats(
                "Desert Eagle .50", 125f, 90f, 0.42f, 7, 42, 2.3f, 0.5f, 14f, 70f, 50f, 55f);

            // Hand cannon recoil: a big first shot, a steep climb, and a third of every
            // kick stays in your aim. Two shots is a pair; four is a prayer.
            deagle.ConfigureRecoil(vertical: 5.2f, horizontal: 1.35f, climbPerShot: 0.62f,
                                   maximumClimb: 3.2f, uncorrectedShare: 0.42f, settleSeconds: 0.75f);

            // A .50 is not a pistol report. Its own, much bigger sound, and it is heard
            // from 55 m — every shot you take is an announcement.
            var deagleAudio = root.GetComponentInChildren<ZombieHouse.Audio.WeaponAudio>();
            if (deagleAudio != null)
                deagleAudio.SetFireSound(ZombieHouse.Audio.Sfx.GunshotHeavy, 0.85f);

            var slide = root.AddComponent<PistolSlide>();
            slide.Configure(root.transform.Find("Slide"));

            return root;
        }

        /// <summary>
        /// A bolt-action rifle: twice the pistol's damage, far more accurate, and much
        /// slower between shots. It is the answer to a corridor, not to a room.
        /// </summary>
        private static GameObject BuildRifle(Transform cameraTransform, int layer, LayerMask hitMask)
        {
            GameObject root = BuildGunFrame(cameraTransform, "Rifle", layer,
                new Vector3(0.20f, -0.20f, 0.34f), new Vector3(0f, 0.03f, 0.62f), hitMask, 7f);

            BuildRifleBody(root.transform, layer);

            // Still exactly twice the pistol, a fifth of its rate of fire, and a much
            // tighter cone. At 100 it drops a runner in one body shot and anything short
            // of a brute in two.
            // Scoped aim drops to an 18 degree field of view — the magnification is what
            // makes the rifle a long-range weapon rather than just a harder-hitting pistol.
            var weapon = root.GetComponent<Weapon>();
            weapon.ConfigureStats("Rifle", 100f, 220f, 0.85f, 8, 40, 2.4f, 0.12f, 16f, 68f, 18f, 5f);

            // Heavier than the Deagle but slower to cycle, so the climb barely gets going;
            // a shouldered rifle also braces far better, which is the aimed multiplier.
            weapon.ConfigureRecoil(vertical: 2.6f, horizontal: 0.35f, climbPerShot: 0.2f,
                                   maximumClimb: 1.8f, uncorrectedShare: 0.2f, settleSeconds: 0.9f);
            weapon.Recoil.aimedMultiplier = 0.55f;
            weapon.SetFiresOnRifleKey(true);
            weapon.SetScoped(true);

            // Bears take double from rifle rounds; see ZombieArchetype.
            weapon.SetDamageKind(DamageKind.RifleRound);

            // Scoped, the rifle drops below the sight picture instead of sitting in the
            // middle of it — the scope overlay is what you aim with.
            root.GetComponent<WeaponViewModel>().ConfigureAimPose(new Vector3(0.02f, -0.30f, 0.20f));

            return root;
        }

        /// <summary>
        /// The Uzi: the scavenged weapon. Two a level, 200 rounds, and when the last one
        /// leaves the barrel the gun goes with it.
        ///
        /// It sits between the two things you already own rather than beating either. The
        /// Desert Eagle is 125 a shot at two and a half a second; the gatling gun is 680 a
        /// second but needs most of a second of spin-up and slows you down carrying it.
        /// The Uzi is **instant** — no wind-up, no weight — at 52 a shot and ten a second.
        /// It is what you want when something comes round a corner at four metres, which
        /// is the one situation the other two are both bad at.
        /// </summary>
        private static GameObject BuildUzi(Transform cameraTransform, int layer, LayerMask hitMask)
        {
            GameObject root = BuildGunFrame(cameraTransform, "Uzi", layer,
                new Vector3(0.20f, -0.19f, 0.34f), new Vector3(0f, 0.006f, 0.24f), hitMask, 4f);

            BuildUziBody(root.transform, layer);

            var weapon = root.GetComponent<Weapon>();
            weapon.ConfigureStats("Uzi", 52f, 60f, 0.1f, 32, 168, 1.6f, 0.9f, 4f, 72f, 60f, 30f);
            weapon.SetAutomatic(true);
            weapon.ConfigureAsPowerUp(200, true);

            // Small kick, steep climb: two-round bursts are free, a held trigger is not.
            weapon.ConfigureRecoil(vertical: 0.9f, horizontal: 0.55f, climbPerShot: 0.14f,
                                   maximumClimb: 4.5f, uncorrectedShare: 0.18f, settleSeconds: 0.3f);

            var audio = root.GetComponentInChildren<ZombieHouse.Audio.WeaponAudio>();
            if (audio != null) audio.SetFireSound(ZombieHouse.Audio.Sfx.GunshotSmg, 0.42f);

            return root;
        }

        /// <summary>
        /// A boxy stamped-steel receiver, the magazine through the pistol grip — the thing
        /// that actually makes an Uzi recognisable — a stubby barrel and a folded stock.
        /// </summary>
        private static void BuildUziBody(Transform root, int layer)
        {
            Material black = ProtoMaterials.GunBlack;
            Material edge = ProtoMaterials.GunEdge;

            var slide = new GameObject("Slide");
            slide.layer = layer;
            slide.transform.SetParent(root, false);
            slide.transform.localPosition = Vector3.zero;

            CreateWeaponPart(slide.transform, "Receiver", new Vector3(0f, 0.010f, 0.020f),
                             new Vector3(0.070f, 0.076f, 0.230f), layer, black);

            for (int i = 0; i < 5; i++)
            {
                CreateWeaponPart(slide.transform, "Rib_" + i,
                                 new Vector3(0f, 0.050f, -0.070f + i * 0.038f),
                                 new Vector3(0.072f, 0.008f, 0.012f), layer, edge);
            }

            CreateWeaponPart(slide.transform, "CockingKnob", new Vector3(0f, 0.056f, 0.004f),
                             new Vector3(0.030f, 0.016f, 0.050f), layer, edge);
            CreateWeaponPart(slide.transform, "Barrel", new Vector3(0f, 0.006f, 0.160f),
                             new Vector3(0.028f, 0.028f, 0.110f), layer, edge);
            CreateWeaponPart(slide.transform, "BarrelNut", new Vector3(0f, 0.006f, 0.118f),
                             new Vector3(0.048f, 0.048f, 0.024f), layer, black);
            CreateWeaponPart(slide.transform, "FrontSight", new Vector3(0f, 0.048f, 0.196f),
                             new Vector3(0.020f, 0.026f, 0.014f), layer, edge);
            CreateWeaponPart(slide.transform, "RearSight", new Vector3(0f, 0.050f, -0.086f),
                             new Vector3(0.024f, 0.022f, 0.012f), layer, edge);

            // The magazine runs down through the grip. Get that wrong and it is a box.
            var grip = CreateWeaponPart(root, "Grip", new Vector3(0f, -0.090f, 0.014f),
                                        new Vector3(0.056f, 0.120f, 0.070f), layer, black);
            grip.transform.localRotation = Quaternion.Euler(-6f, 0f, 0f);

            CreateWeaponPart(root, "Magazine", new Vector3(0f, -0.200f, 0.010f),
                             new Vector3(0.048f, 0.130f, 0.062f), layer, edge);
            CreateWeaponPart(root, "MagFloor", new Vector3(0f, -0.268f, 0.010f),
                             new Vector3(0.054f, 0.014f, 0.068f), layer, black);

            CreateWeaponPart(root, "GuardFront", new Vector3(0f, -0.052f, 0.070f),
                             new Vector3(0.014f, 0.046f, 0.012f), layer, black);
            CreateWeaponPart(root, "GuardBottom", new Vector3(0f, -0.072f, 0.042f),
                             new Vector3(0.014f, 0.012f, 0.070f), layer, black);
            CreateWeaponPart(root, "Trigger", new Vector3(0f, -0.044f, 0.040f),
                             new Vector3(0.010f, 0.026f, 0.012f), layer, edge);

            CreateWeaponPart(root, "StockBar", new Vector3(0.042f, -0.006f, -0.030f),
                             new Vector3(0.012f, 0.012f, 0.170f), layer, edge);
            CreateWeaponPart(root, "StockHinge", new Vector3(0.042f, -0.006f, -0.116f),
                             new Vector3(0.018f, 0.030f, 0.018f), layer, black);
        }

        /// <summary>
        /// The gatling gun: the power-up weapon, carried in one pair of hands because
        /// this is not a game about realism in that particular direction.
        ///
        /// It is realistic where it counts, which is the **spin-up**. The trigger does not
        /// fire it — it starts it, and there is most of a second of barrels winding up
        /// before a round comes out. Release and they coast down over a second more, so a
        /// burst you abandon costs you the spin-up all over again. Everything else in the
        /// game answers instantly; this is the one weapon you have to commit with, and
        /// that single second is its whole character.
        ///
        /// After that it is 1,200 rounds a minute of 34 damage — **680 a second**, against
        /// the Desert Eagle's 300 — with a cone you could drive through and a kick that
        /// climbs off the target in under two seconds. It is a room-clearing tool at eight
        /// metres and useless at twenty, and it is heavy enough that you walk slower
        /// holding it.
        /// </summary>
        private static GameObject BuildGatling(Transform cameraTransform, int layer, LayerMask hitMask)
        {
            GameObject root = BuildGunFrame(cameraTransform, "Gatling", layer,
                new Vector3(0.26f, -0.24f, 0.40f), new Vector3(0f, 0.02f, 0.52f), hitMask, 9f);

            BuildGatlingBody(root.transform, layer);

            var weapon = root.GetComponent<Weapon>();

            // 400 rounds: at twenty a second that is twenty seconds of fire, which is the
            // right length for something that should feel like a brief emergency measure.
            weapon.ConfigureStats("Gatling Gun", 34f, 55f, 0.05f, 100, 300, 3.4f, 1.6f, 3f, 74f, 66f, 45f);
            weapon.SetAutomatic(true);
            weapon.ConfigureRotary(spinUp: 0.85f, spinDown: 1.2f);

            // It reloads like anything else — R, from reserve — but *only* from the green
            // belt crates. Yellow boxes are small-arms ammunition and Weapon.AddAmmo turns
            // them away for anything belt-fed, so the two supplies never overlap: yellow
            // for the Deagle, the rifle and the Uzi; green for this.
            weapon.ConfigureResupply(roundsPerBox: 0, reserveCeiling: 800);

            // Yours from the start, and not thrown away when it runs dry — belt crates
            // are what keep it fed, and a gun you lose permanently in the first room
            // would make those crates worthless for the rest of the level.
            weapon.ConfigureAsPowerUp(400, false);

            // Almost no kick per round, but at twenty a second the climb is relentless —
            // it walks up a wall inside two seconds and most of it stays in your aim.
            weapon.ConfigureRecoil(vertical: 0.42f, horizontal: 0.5f, climbPerShot: 0.08f,
                                   maximumClimb: 6f, uncorrectedShare: 0.22f, settleSeconds: 0.5f);

            var audio = root.GetComponentInChildren<ZombieHouse.Audio.WeaponAudio>();
            if (audio != null) audio.SetFireSound(ZombieHouse.Audio.Sfx.GunshotSmg, 0.34f);

            root.AddComponent<BarrelSpinner>();
            return root;
        }

        /// <summary>
        /// Six barrels in a rotating cluster, a slab receiver, an ammunition drum on the
        /// side and a spade grip behind. The cluster is the silhouette — everything else
        /// exists to hold it up.
        /// </summary>
        private static void BuildGatlingBody(Transform root, int layer)
        {
            Material black = ProtoMaterials.GunBlack;
            Material edge = ProtoMaterials.GunEdge;

            var slide = new GameObject("Slide");
            slide.layer = layer;
            slide.transform.SetParent(root, false);
            slide.transform.localPosition = Vector3.zero;

            // Receiver: a heavy box, and the whole thing is visibly too big to be held.
            CreateWeaponPart(slide.transform, "Receiver", new Vector3(0f, 0.010f, 0.030f),
                             new Vector3(0.115f, 0.115f, 0.260f), layer, black);

            CreateWeaponPart(slide.transform, "Shroud", new Vector3(0f, 0.010f, 0.210f),
                             new Vector3(0.130f, 0.130f, 0.090f), layer, edge);

            // --- the rotating cluster -------------------------------------------
            // Its own object, spun by BarrelSpinner. Everything under it turns.
            var cluster = new GameObject("BarrelCluster");
            cluster.layer = layer;
            cluster.transform.SetParent(slide.transform, false);
            cluster.transform.localPosition = new Vector3(0f, 0.010f, 0.330f);

            for (int i = 0; i < 6; i++)
            {
                float angle = i * (360f / 6f) * Mathf.Deg2Rad;
                var offset = new Vector3(Mathf.Cos(angle) * 0.042f, Mathf.Sin(angle) * 0.042f, 0f);

                CreateWeaponPart(cluster.transform, "Barrel_" + i, offset,
                                 new Vector3(0.024f, 0.024f, 0.320f), layer, edge);
            }

            // The plate the barrels are set into, so the cluster reads as one assembly.
            CreateWeaponPart(cluster.transform, "ClusterPlate", new Vector3(0f, 0f, -0.145f),
                             new Vector3(0.105f, 0.105f, 0.030f), layer, black);
            CreateWeaponPart(cluster.transform, "ClusterNose", new Vector3(0f, 0f, 0.150f),
                             new Vector3(0.100f, 0.100f, 0.026f), layer, black);

            // A single dark bore in the middle so the front of it is not just a wheel.
            CreateWeaponPart(cluster.transform, "ClusterHub", new Vector3(0f, 0f, 0.160f),
                             new Vector3(0.034f, 0.034f, 0.014f), layer, ProtoMaterials.Gore);

            // --- feed and grip ---------------------------------------------------
            var drum = CreateWeaponPart(root, "AmmoDrum", new Vector3(-0.105f, -0.045f, -0.010f),
                                        new Vector3(0.150f, 0.150f, 0.110f), layer, edge);
            drum.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            CreateWeaponPart(root, "FeedChute", new Vector3(-0.055f, -0.010f, 0.020f),
                             new Vector3(0.070f, 0.055f, 0.070f), layer, black);

            // Spade grips: two handles behind the receiver, which is how you actually
            // hold one of these.
            for (int s = -1; s <= 1; s += 2)
            {
                var handle = CreateWeaponPart(root, "SpadeGrip_" + s,
                    new Vector3(0.062f * s, -0.055f, -0.115f),
                    new Vector3(0.030f, 0.115f, 0.030f), layer, black);

                handle.transform.localRotation = Quaternion.Euler(-18f, 0f, -7f * s);
            }

            CreateWeaponPart(root, "SpadeBar", new Vector3(0f, -0.008f, -0.120f),
                             new Vector3(0.150f, 0.026f, 0.026f), layer, edge);
            CreateWeaponPart(root, "Trigger", new Vector3(0.062f, -0.048f, -0.096f),
                             new Vector3(0.016f, 0.030f, 0.014f), layer, edge);

            // Carry handle over the top, which is the detail that sells its weight.
            CreateWeaponPart(root, "CarryHandle", new Vector3(0f, 0.086f, 0.020f),
                             new Vector3(0.026f, 0.026f, 0.170f), layer, edge);
            for (int s = -1; s <= 1; s += 2)
            {
                CreateWeaponPart(root, "HandleLeg_" + s, new Vector3(0f, 0.058f, 0.095f * s),
                                 new Vector3(0.024f, 0.062f, 0.024f), layer, edge);
            }
        }

        private static void BuildRifleBody(Transform root, int layer)
        {
            Material steel = ProtoMaterials.WeaponBody;
            Material wood = ProtoMaterials.Wood;

            // Receiver, then a long barrel — the silhouette is what distinguishes it.
            CreateWeaponPart(root, "Receiver", new Vector3(0f, 0.005f, 0.10f),
                             new Vector3(0.052f, 0.070f, 0.30f), layer, steel);
            CreateWeaponPart(root, "Barrel", new Vector3(0f, 0.020f, 0.42f),
                             new Vector3(0.028f, 0.028f, 0.42f), layer, steel);
            CreateWeaponPart(root, "MuzzleBrake", new Vector3(0f, 0.020f, 0.625f),
                             new Vector3(0.038f, 0.038f, 0.045f), layer, ProtoMaterials.Metal);

            // Furniture.
            CreateWeaponPart(root, "Handguard", new Vector3(0f, 0.005f, 0.30f),
                             new Vector3(0.050f, 0.048f, 0.24f), layer, wood);
            var stock = CreateWeaponPart(root, "Stock", new Vector3(0f, -0.028f, -0.15f),
                                         new Vector3(0.048f, 0.075f, 0.28f), layer, wood);
            stock.transform.localRotation = Quaternion.Euler(-4f, 0f, 0f);
            CreateWeaponPart(root, "Butt", new Vector3(0f, -0.045f, -0.29f),
                             new Vector3(0.050f, 0.115f, 0.035f), layer, wood);

            CreateWeaponPart(root, "Grip", new Vector3(0f, -0.105f, -0.045f),
                             new Vector3(0.042f, 0.130f, 0.060f), layer, wood)
                .transform.localRotation = Quaternion.Euler(-14f, 0f, 0f);

            CreateWeaponPart(root, "Magazine", new Vector3(0f, -0.075f, 0.075f),
                             new Vector3(0.044f, 0.100f, 0.070f), layer, steel);
            CreateWeaponPart(root, "Bolt", new Vector3(0.036f, 0.030f, 0.012f),
                             new Vector3(0.028f, 0.024f, 0.090f), layer, ProtoMaterials.Blade);

            CreateWeaponPart(root, "GuardBottom", new Vector3(0f, -0.068f, 0.005f),
                             new Vector3(0.013f, 0.012f, 0.075f), layer, steel);
            CreateWeaponPart(root, "Trigger", new Vector3(0f, -0.045f, 0.005f),
                             new Vector3(0.010f, 0.028f, 0.011f), layer, ProtoMaterials.Blade);

            // Scope on rings, high enough to sight over the receiver. The tube and bell are
            // glass rather than steel so looking down the sight does not black out the
            // middle of the screen — you can see the target through the optic.
            CreateWeaponPart(root, "ScopeRingFront", new Vector3(0f, 0.056f, 0.19f),
                             new Vector3(0.022f, 0.030f, 0.016f), layer, ProtoMaterials.Metal);
            CreateWeaponPart(root, "ScopeRingRear", new Vector3(0f, 0.056f, 0.04f),
                             new Vector3(0.022f, 0.030f, 0.016f), layer, ProtoMaterials.Metal);
            CreateWeaponPart(root, "ScopeTube", new Vector3(0f, 0.080f, 0.115f),
                             new Vector3(0.044f, 0.044f, 0.26f), layer, ProtoMaterials.Glass);
            CreateWeaponPart(root, "ScopeBell", new Vector3(0f, 0.080f, 0.255f),
                             new Vector3(0.056f, 0.056f, 0.045f), layer, ProtoMaterials.Glass);
        }

        /// <summary>
        /// A service pistol built from its actual parts rather than three boxes: a slide
        /// that cycles, a frame, an angled grip, a trigger inside a guard, a magazine
        /// floorplate, and front and rear sights you can line up.
        ///
        /// The gun points along +Z. The slide is named so PistolSlide can find and drive it.
        /// </summary>
        /// <summary>
        /// The Desert Eagle .50: a gas-operated pistol built like a rifle, and the
        /// silhouette is the point — a long slab slide with a ventilated rib along the
        /// top, a squared-off trigger guard, and a barrel that is visibly too big for the
        /// frame it is bolted to.
        /// </summary>
        /// <summary>
        /// The Desert Eagle .50, and it is meant to look like a threat rather than a tool.
        ///
        /// Four things do that work, in order of how much they matter at a glance:
        /// **it is black** — near-black hard chrome instead of the old mid grey, because a
        /// pale gun reads as a prop; **the muzzle is ported**, with cuts you can see down
        /// the top of the brake, which is the detail that says this thing is fighting its
        /// own recoil; **the slide is longer than the frame under it**, overhanging at the
        /// front so the barrel looks too big for what is holding it; and **it is covered
        /// in hard edges** — cocking serrations, a squared guard, a checkered grip, a
        /// scope rail — where the old one was mostly smooth boxes.
        ///
        /// The three tritium dots are the last touch, and the cheapest: two on the rear
        /// sight, one on the front, glowing faintly. In a dark corridor they are the first
        /// thing you see of your own weapon.
        /// </summary>
        private static void BuildPistolBody(Transform root, int layer)
        {
            Material black = ProtoMaterials.GunBlack;
            Material edge = ProtoMaterials.GunEdge;

            // --- slide: the part that reciprocates ---------------------------
            var slide = new GameObject("Slide");
            slide.layer = layer;
            slide.transform.SetParent(root, false);
            slide.transform.localPosition = Vector3.zero;

            // Longer and deeper again, and it now overhangs the frame at the front.
            CreateWeaponPart(slide.transform, "SlideBody", new Vector3(0f, 0.018f, 0.062f),
                             new Vector3(0.068f, 0.086f, 0.340f), layer, black);

            // Cocking serrations: eight thin ribs at the rear of the slide. Nothing says
            // "machined" faster than a row of parallel cuts catching the light.
            for (int i = 0; i < 8; i++)
            {
                CreateWeaponPart(slide.transform, "Serration_" + i,
                                 new Vector3(0f, 0.018f, -0.062f + i * 0.013f),
                                 new Vector3(0.071f, 0.062f, 0.005f), layer, edge);
            }

            // The rib along the top, with its slots.
            CreateWeaponPart(slide.transform, "TopRib", new Vector3(0f, 0.063f, 0.062f),
                             new Vector3(0.032f, 0.014f, 0.340f), layer, edge);
            for (int i = 0; i < 5; i++)
            {
                CreateWeaponPart(slide.transform, "RibSlot_" + i,
                                 new Vector3(0f, 0.070f, 0.000f + i * 0.046f),
                                 new Vector3(0.022f, 0.007f, 0.020f), layer, black);
            }

            // A scope rail over the chamber. A rail on a handgun is pure aggression.
            CreateWeaponPart(slide.transform, "Rail", new Vector3(0f, 0.076f, -0.010f),
                             new Vector3(0.026f, 0.010f, 0.140f), layer, black);
            for (int i = 0; i < 5; i++)
            {
                CreateWeaponPart(slide.transform, "RailNotch_" + i,
                                 new Vector3(0f, 0.082f, -0.060f + i * 0.026f),
                                 new Vector3(0.028f, 0.006f, 0.008f), layer, edge);
            }

            // Muzzle: a heavy barrel standing proud, and a brake with visible ports.
            CreateWeaponPart(slide.transform, "Barrel", new Vector3(0f, 0.014f, 0.232f),
                             new Vector3(0.056f, 0.066f, 0.090f), layer, black);

            var brake = CreateWeaponPart(slide.transform, "Brake", new Vector3(0f, 0.014f, 0.288f),
                                         new Vector3(0.064f, 0.070f, 0.046f), layer, edge);
            brake.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            // The ports themselves — three cuts across the top of the brake, and one
            // either side. These are what make the muzzle read as ported rather than blunt.
            for (int i = 0; i < 3; i++)
            {
                CreateWeaponPart(slide.transform, "BrakePortTop_" + i,
                                 new Vector3(0f, 0.050f, 0.272f + i * 0.016f),
                                 new Vector3(0.030f, 0.012f, 0.007f), layer, black);
            }

            for (int s = -1; s <= 1; s += 2)
            {
                CreateWeaponPart(slide.transform, "BrakePortSide_" + s,
                                 new Vector3(0.032f * s, 0.014f, 0.286f),
                                 new Vector3(0.008f, 0.030f, 0.020f), layer, black);
            }

            // The bore: a dark hole in the end of it, which is the whole point of pointing
            // a gun at something.
            CreateWeaponPart(slide.transform, "Bore", new Vector3(0f, 0.014f, 0.310f),
                             new Vector3(0.026f, 0.026f, 0.010f), layer, ProtoMaterials.Gore);

            CreateWeaponPart(slide.transform, "EjectionPort", new Vector3(0.034f, 0.034f, 0.090f),
                             new Vector3(0.014f, 0.034f, 0.105f), layer, edge);

            // --- sights, with tritium ----------------------------------------
            CreateWeaponPart(slide.transform, "FrontSight", new Vector3(0f, 0.080f, 0.214f),
                             new Vector3(0.010f, 0.030f, 0.012f), layer, edge);
            CreateWeaponPart(slide.transform, "FrontDot", new Vector3(0f, 0.088f, 0.209f),
                             new Vector3(0.006f, 0.006f, 0.004f), layer, ProtoMaterials.SightDot);

            for (int s = -1; s <= 1; s += 2)
            {
                CreateWeaponPart(slide.transform, "RearSight_" + s, new Vector3(0.018f * s, 0.080f, -0.070f),
                                 new Vector3(0.013f, 0.028f, 0.014f), layer, edge);
                CreateWeaponPart(slide.transform, "RearDot_" + s, new Vector3(0.018f * s, 0.086f, -0.064f),
                                 new Vector3(0.005f, 0.005f, 0.004f), layer, ProtoMaterials.SightDot);
            }

            // --- frame: everything that stays put ----------------------------
            CreateWeaponPart(root, "Frame", new Vector3(0f, -0.040f, 0.030f),
                             new Vector3(0.062f, 0.048f, 0.230f), layer, black);

            // A squared-off dust cover under the barrel, which is most of the bulk you
            // see from the side.
            CreateWeaponPart(root, "DustCover", new Vector3(0f, -0.038f, 0.140f),
                             new Vector3(0.058f, 0.040f, 0.120f), layer, black);

            // Grip, raked back the way a pistol grip actually sits in the hand.
            var grip = CreateWeaponPart(root, "Grip", new Vector3(0f, -0.140f, -0.052f),
                                        new Vector3(0.064f, 0.180f, 0.090f), layer, black);
            grip.transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);

            // Checkering: four raised panels down each side of the grip.
            for (int s = -1; s <= 1; s += 2)
            {
                for (int i = 0; i < 4; i++)
                {
                    var panel = CreateWeaponPart(root, $"GripCheck_{s}_{i}",
                        new Vector3(0.031f * s, -0.098f - i * 0.030f, -0.044f - i * 0.008f),
                        new Vector3(0.006f, 0.022f, 0.062f), layer, edge);
                    panel.transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);
                }
            }

            CreateWeaponPart(root, "Magazine", new Vector3(0f, -0.228f, -0.064f),
                             new Vector3(0.068f, 0.024f, 0.096f), layer, edge);

            // Squared trigger guard, with a hooked front face.
            CreateWeaponPart(root, "GuardFront", new Vector3(0f, -0.080f, 0.074f),
                             new Vector3(0.018f, 0.066f, 0.015f), layer, black);
            CreateWeaponPart(root, "GuardBottom", new Vector3(0f, -0.110f, 0.026f),
                             new Vector3(0.018f, 0.015f, 0.110f), layer, black);
            CreateWeaponPart(root, "GuardHook", new Vector3(0f, -0.112f, 0.078f),
                             new Vector3(0.018f, 0.020f, 0.024f), layer, black);
            CreateWeaponPart(root, "Trigger", new Vector3(0f, -0.064f, 0.020f),
                             new Vector3(0.011f, 0.032f, 0.013f), layer, edge);

            // A big exposed hammer, back and ready.
            var hammer = CreateWeaponPart(root, "Hammer", new Vector3(0f, 0.024f, -0.096f),
                                          new Vector3(0.016f, 0.044f, 0.018f), layer, edge);
            hammer.transform.localRotation = Quaternion.Euler(18f, 0f, 0f);

        }


        /// <summary>
        /// The offhand katana. The blade is a single generated mesh swept along a curve —
        /// one continuous surface, no segment joins.
        /// </summary>
        private static void BuildKatana(Transform cameraTransform, Camera camera, int playerLayer, int enemyLayer)
        {
            var katana = new GameObject("Katana");
            katana.layer = playerLayer;
            katana.transform.SetParent(cameraTransform, false);
            katana.transform.localPosition = new Vector3(-0.34f, -0.30f, 0.36f);
            katana.transform.localRotation = Quaternion.Euler(8f, 20f, -26f);

            Transform root = katana.transform;

            // Left forearm and fist, angled back towards the camera so it reads as "held".
            var forearm = CreateWeaponPart(root, "Forearm", new Vector3(-0.02f, -0.22f, -0.20f),
                                           new Vector3(0.085f, 0.085f, 0.30f), playerLayer, ProtoMaterials.Skin);
            forearm.transform.localRotation = Quaternion.Euler(28f, 0f, 0f);

            CreateWeaponPart(root, "Fist", new Vector3(0f, -0.05f, 0f),
                             new Vector3(0.08f, 0.10f, 0.085f), playerLayer, ProtoMaterials.Skin);

            // Tsuka (the long two-hand grip) and the round tsuba guard.
            CreateWeaponPart(root, "Tsuka", new Vector3(0f, 0.08f, 0.005f),
                             new Vector3(0.032f, 0.17f, 0.038f), playerLayer, ProtoMaterials.WeaponBody);
            CreateWeaponPart(root, "Tsuba", new Vector3(0f, 0.175f, 0.005f),
                             new Vector3(0.085f, 0.016f, 0.085f), playerLayer, ProtoMaterials.Metal);

            // One continuous swept mesh, saved as an asset so it survives a scene reload.
            var blade = new GameObject("Blade");
            blade.layer = playerLayer;
            blade.transform.SetParent(root, false);
            blade.transform.localPosition = new Vector3(0f, 0.185f, 0.004f);

            blade.AddComponent<MeshFilter>().sharedMesh = LoadOrCreateKatanaMesh();

            var bladeRenderer = blade.AddComponent<MeshRenderer>();
            bladeRenderer.sharedMaterial = ProtoMaterials.Blade;
            bladeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var melee = katana.AddComponent<MeleeWeapon>();

            // The blade bites walls and zombies, but not corpses — no re-killing the dead.
            LayerMask meleeMask = (1 << 0) | (1 << enemyLayer);
            melee.ConfigureReferences(camera, meleeMask);
        }

        /// <summary>
        /// Generates the katana blade mesh once and caches it as an asset. A mesh built
        /// purely at runtime would not survive saving the scene, leaving an empty filter.
        /// </summary>
        private static Mesh LoadOrCreateKatanaMesh()
        {
            const string path = MeshesFolder + "/KatanaBlade.asset";

            EnsureFolder(MeshesFolder);
            Mesh mesh = BladeMesh.CreateKatana();

            // Overwrite the existing asset's contents rather than returning it untouched:
            // the blade's length is a tuning number, and a cached asset would pin it to
            // whatever it was the first time this ran. CopySerialized keeps the same
            // asset identity, so every prefab and scene pointing at it follows along.
            // One mesh, one surface: if the blade were still segments this would report
            // several disconnected islands instead of a single closed shell. Logged on
            // every build, so a change to the blade's length is visible in the output.
            Bounds bounds = mesh.bounds;
            Debug.Log($"[Katana] Blade mesh: {mesh.vertexCount} vertices, " +
                      $"{mesh.triangles.Length / 3} triangles, " +
                      $"size {bounds.size.x:F3} x {bounds.size.y:F3} x {bounds.size.z:F3} m, " +
                      $"{CountIslands(mesh)} connected surface(s).");

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                return existing;
            }

            AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssets();
            return mesh;
        }

        /// <summary>
        /// Counts how many separate pieces a mesh is made of, by flooding across shared
        /// vertices. A continuous blade is exactly one; segments would be several.
        /// </summary>
        private static int CountIslands(Mesh mesh)
        {
            int[] triangles = mesh.triangles;
            var parent = new int[mesh.vertexCount];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;

            System.Func<int, int> find = null;
            find = x => parent[x] == x ? x : parent[x] = find(parent[x]);

            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = find(triangles[i]);
                int b = find(triangles[i + 1]);
                int c = find(triangles[i + 2]);
                if (a != b) parent[b] = a;
                if (a != c) parent[c] = a;
            }

            var roots = new HashSet<int>();
            for (int i = 0; i < triangles.Length; i++) roots.Add(find(triangles[i]));
            return roots.Count;
        }

        private static GameObject CreateWeaponPart(Transform parent, string name, Vector3 localPosition,
                                                   Vector3 scale, int layer, Material material = null)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.layer = layer;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = scale;
            part.GetComponent<MeshRenderer>().sharedMaterial = material != null ? material : ProtoMaterials.WeaponBody;

            // View-model geometry must never take part in physics or stop a bullet.
            var collider = part.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);

            return part;
        }

        private static void BuildManagers(GameObject zombiePrefab, GameObject player)
        {
            var managers = new GameObject("--- Managers ---");

            // Audio builds its clip library in Awake at execution order -200, so it is
            // ready before anything tries to play a sound.
            var audioObject = new GameObject("GameAudio");
            audioObject.transform.SetParent(managers.transform, false);
            var houseAudio = audioObject.AddComponent<ZombieHouse.Audio.GameAudio>();
            audioObject.AddComponent<ZombieHouse.Audio.StingerAudio>();
            audioObject.AddComponent<ZombieHouse.Audio.ThreatMeter>();
            audioObject.AddComponent<ZombieHouse.Fx.DreadDirector>();

            // Stated rather than inherited. Leaving this to the field default is exactly
            // how the town spent months playing the forest's wind down a dusty street.
            var houseAudioSo = new SerializedObject(houseAudio);
            houseAudioSo.FindProperty("musicTrack").enumValueIndex = (int)ZombieHouse.Audio.Sfx.Music;
            houseAudioSo.FindProperty("tensionTrack").enumValueIndex = (int)ZombieHouse.Audio.Sfx.TensionHouse;
            houseAudioSo.ApplyModifiedPropertiesWithoutUndo();

            var gameManagerObject = new GameObject("GameManager");
            gameManagerObject.transform.SetParent(managers.transform, false);
            gameManagerObject.AddComponent<GameManager>();
            gameManagerObject.AddComponent<HudController>();

            var impactObject = new GameObject("ImpactSystem");
            impactObject.transform.SetParent(managers.transform, false);
            impactObject.AddComponent<ZombieHouse.Fx.ImpactSystem>();

            var houseObject = new GameObject("House");
            var house = houseObject.AddComponent<HouseGenerator>();

            var houseSo = new SerializedObject(house);
            houseSo.FindProperty("doorLayer").intValue = EnsureLayer(DoorLayerName);
            houseSo.ApplyModifiedPropertiesWithoutUndo();

            var navMeshObject = new GameObject("NavMesh");
            navMeshObject.transform.SetParent(managers.transform, false);
            var baker = navMeshObject.AddComponent<RuntimeNavMeshBaker>();

            var spawnerObject = new GameObject("ZombieSpawner");
            spawnerObject.transform.SetParent(managers.transform, false);
            var spawner = spawnerObject.AddComponent<ZombieSpawner>();

            var directorObject = new GameObject("LevelDirector");
            directorObject.transform.SetParent(managers.transform, false);
            var director = directorObject.AddComponent<LevelDirector>();

            var so = new SerializedObject(director);
            AssignReference(so, "levelSourceBehaviour", house);
            AssignReference(so, "spawner", spawner);
            AssignReference(so, "navMeshBaker", baker);
            AssignReference(so, "player", player.transform);
            AssignReference(so, "zombiePrefab", zombiePrefab);
            // The boss — the house: the first thing the game taught you to kill, returned enormous.
            AssignReference(so, "bossPrefab", zombiePrefab);
            so.FindProperty("bossKind").enumValueIndex = (int)ZombieKind.BossZombie;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Show the layout in the editor immediately rather than only on Play.
            house.Generate();
        }

        private static void AssignReference(SerializedObject so, string propertyName, Object value)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning("[ZombieHouse] Missing serialized field '" + propertyName + "'.");
                return;
            }
            property.objectReferenceValue = value;
        }

        private static void AddSceneToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var entry in scenes)
                if (entry.path == path) return;

            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
        }
    }
}
