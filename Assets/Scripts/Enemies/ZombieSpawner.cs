using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ZombieHouse.Core;
using ZombieHouse.Level;

namespace ZombieHouse.Enemies
{
    /// <summary>
    /// Populates the house and trickles in reinforcements. Spawn points come from the
    /// 'Z' markers in the house layout; the spawner refuses to use one the player can
    /// currently see or is standing near, so zombies never pop in front of you.
    /// </summary>
    public class ZombieSpawner : MonoBehaviour
    {
        [Header("Prefab")]
        [SerializeField] private GameObject zombiePrefab;

        [Header("Beasts")]
        [Tooltip("The level's large quadruped, if it has one: bears in the wood, horses "
                 + "in the town. Anything not the mutant has this chance of being one.")]
        [SerializeField] private GameObject beastPrefab;
        [Range(0f, 1f)] [SerializeField] private float beastShare;
        [SerializeField] private ZombieKind beastKind = ZombieKind.Bear;

        [Header("Lurkers")]
        [Tooltip("A second special type, placed at positions the level hands over rather "
                 + "than drawn from the spawn markers. The jungle's snakes use it: they "
                 + "are lying in particular fern beds and particular reeds, and one that "
                 + "wandered in from a marker would not be a snake.")]
        [SerializeField] private GameObject lurkerPrefab;
        [SerializeField] private ZombieKind lurkerKind = ZombieKind.Snake;
        [SerializeField] private List<Vector3> lurkerPositions = new List<Vector3>();

        [Tooltip("Lifts each lurker this far above the walkable ground it was matched to. "
                 + "Zero for anything that lies on the floor, which is every lurker before "
                 + "the gulls; the ship sets it so they end up on the rail rather than "
                 + "standing on the deck under it.")]
        [SerializeField] private float lurkerHoverHeight;

        [Header("Packs")]
        [Tooltip("A type that arrives in groups rather than singly. The school's children "
                 + "use this: a classroom door opening produces three of them at once, "
                 + "which is a different problem from three of them spread down a corridor.")]
        [SerializeField] private GameObject packPrefab;
        [Range(0f, 1f)] [SerializeField] private float packShare;
        [SerializeField] private Vector2Int packSize = new Vector2Int(2, 4);
        [SerializeField] private ZombieKind packKind = ZombieKind.Kid;
        [Tooltip("How far apart a pack's members are placed, in metres.")]
        [SerializeField] private float packSpread = 1.6f;

        [Header("Ambush")]
        [Tooltip("Chance a zombie spawning near a door waits behind it rather than "
                 + "wandering. Kept low: the trick works because most doors are empty.")]
        [Range(0f, 1f)] [SerializeField] private float ambushShare = 0.55f;

        [Tooltip("Share of the zombies that do not get a door to hide behind which lie down "
                 + "among the corpses instead. Kept low on purpose: the scare works because "
                 + "most bodies really are just bodies, and a level where a third of the "
                 + "floor sits up is not frightening, it is a mechanic.")]
        [Range(0f, 1f)] [SerializeField] private float playDeadShare = 0.16f;

        [Tooltip("How close to a door a spawn has to be to become an ambush.")]
        [SerializeField] private float ambushRadius = 3.2f;

        [Header("Population")]
        [Tooltip("Place one sleeper at every marker at level start, instead of trickling " +
                 "waves in. The house is populated once and then left alone.")]
        [SerializeField] private bool populateHouseAtStart = true;

        [SerializeField] private int totalZombies = 46;
        [SerializeField] private int initialPopulation = 12;
        [SerializeField] private int maxAliveAtOnce = 18;
        [SerializeField] private float spawnInterval = 2.6f;

        [Header("Mutant")]
        [Tooltip("Exactly one mutant is placed per level; it is never rolled at random.")]
        [SerializeField] private bool spawnMutant = true;
        [Tooltip("Which zombie of the run is the mutant, counted from zero.")]
        [SerializeField] private int mutantSpawnOrdinal = 4;
        [SerializeField] private float firstReinforcementDelay = 8f;

        [Header("Safe start")]
        [Tooltip("Nothing is placed within this distance of the player's start. The trickle "
                 + "spawner already respected a minimum, but PopulateHouse — which is what "
                 + "every level actually uses — placed one at every marker regardless, so a "
                 + "marker near the start put a zombie on top of you before you had moved.")]
        [SerializeField] private float safeStartRadius = 13f;

        [Header("Spawn placement")]
        [SerializeField] private float minDistanceFromPlayer = 14f;
        [SerializeField] private float absoluteMinDistance = 8f;
        [SerializeField] private LayerMask sightBlockers = 1;

        [Header("Spawn points")]
        [SerializeField] private List<Transform> spawnPoints = new List<Transform>();

        public int Spawned { get; private set; }
        public int Remaining => Mathf.Max(0, totalZombies - Spawned);

        private Transform _player;
        private readonly List<GameObject> _live = new List<GameObject>();

        public void Configure(GameObject prefab, IEnumerable<Vector3> points)
        {
            Configure(prefab, points, null);
        }

        /// <summary>
        /// Marked spawn points are placed first and always used; hiding spots fill the
        /// rest of the population up to <see cref="totalZombies"/>, shuffled so no two
        /// runs put the ambushes in the same places.
        /// </summary>
        /// <summary>Adds a bear prefab and the share of spawns that should use it.</summary>
        /// <summary>
        /// Sets the type that spawns in groups. Called by the scene setup; a level that
        /// never calls it simply has no packs, which is every level before the school.
        /// </summary>
        public void ConfigurePacks(GameObject prefab, float share, ZombieKind kind,
                                   int minimum = 2, int maximum = 4)
        {
            packPrefab = prefab;
            packShare = Mathf.Clamp01(share);
            packKind = kind;
            packSize = new Vector2Int(Mathf.Max(1, minimum), Mathf.Max(1, maximum));
        }

        public void ConfigureBeasts(GameObject prefab, float share, ZombieKind kind)
        {
            beastPrefab = prefab;
            beastShare = Mathf.Clamp01(share);
            beastKind = kind;
        }

        /// <summary>
        /// Sets a type placed exactly where the level says, rather than rolled against the
        /// spawn markers. These are additional to the population — a level with twelve
        /// markers and nine lurkers has twenty-one things in it.
        /// </summary>
        public void ConfigureLurkers(GameObject prefab, ZombieKind kind, IEnumerable<Vector3> positions,
                                    float hoverHeight = 0f)
        {
            lurkerPrefab = prefab;
            lurkerKind = kind;
            lurkerHoverHeight = hoverHeight;

            SetLurkerPositions(positions);
        }

        /// <summary>
        /// Where this run's lurkers are lying. Split from ConfigureLurkers because the
        /// prefab is chosen when the scene is built and the positions are not known until
        /// the level has generated itself.
        /// </summary>
        public void SetLurkerPositions(IEnumerable<Vector3> positions)
        {
            lurkerPositions.Clear();
            if (positions != null) lurkerPositions.AddRange(positions);
        }

        public void Configure(GameObject prefab, IEnumerable<Vector3> points, IEnumerable<Pose> hidingSpots)
        {
            zombiePrefab = prefab;
            spawnPoints.Clear();

            int index = 0;
            foreach (Vector3 p in points)
            {
                var marker = new GameObject("ZombieSpawn_" + index++);
                marker.transform.SetParent(transform, false);
                marker.transform.position = p;
                spawnPoints.Add(marker.transform);
            }

            if (hidingSpots == null) return;

            var shuffled = new List<Pose>(hidingSpots);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }

            foreach (Pose pose in shuffled)
            {
                if (spawnPoints.Count >= totalZombies) break;

                var marker = new GameObject("ZombieHide_" + index++);
                marker.transform.SetParent(transform, false);
                marker.transform.SetPositionAndRotation(pose.position, pose.rotation);
                spawnPoints.Add(marker.transform);
            }
        }

        private void Start()
        {
            var playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) _player = playerObject.transform;

            StartCoroutine(SpawnLoop());
        }

        private IEnumerator SpawnLoop()
        {
            if (zombiePrefab == null || spawnPoints.Count == 0)
            {
                Debug.LogWarning("[ZombieSpawner] No prefab or no spawn points — nothing will spawn.");
                if (GameManager.Instance != null) GameManager.Instance.ReportSpawningFinished();
                yield break;
            }

            // Let the NavMesh finish baking before the first zombie asks for a path.
            yield return null;
            yield return new WaitForSeconds(0.25f);

            PlaceLurkers();

            if (populateHouseAtStart)
            {
                yield return PopulateHouse();
                yield break;
            }

            int initial = Mathf.Min(initialPopulation, totalZombies);
            for (int i = 0; i < initial; i++)
            {
                SpawnOne();
                yield return null;
            }

            yield return new WaitForSeconds(firstReinforcementDelay);

            while (Spawned < totalZombies)
            {
                PruneDead();

                if (_live.Count < maxAliveAtOnce)
                    SpawnOne();

                if (GameManager.Instance != null) GameManager.Instance.ReportSpawnQueue(Remaining);
                yield return new WaitForSeconds(spawnInterval);
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.ReportSpawnQueue(0);
                GameManager.Instance.ReportSpawningFinished();
            }
        }

        /// <summary>
        /// Puts exactly one sleeper on every marker and then stops. There are no waves and
        /// no reinforcements: what is in the house at the start is all there is, which is
        /// what lets the player learn a route through it.
        /// </summary>
        private System.Collections.IEnumerator PopulateHouse()
        {
            Vector3 start = _player != null ? _player.position : Vector3.zero;
            int skipped = 0;

            for (int i = 0; i < spawnPoints.Count; i++)
            {
                // The safe start. A marker inside the radius is left empty rather than
                // moved: relocating it would put the zombie somewhere the level designer
                // did not choose, and an empty marker near the door is a much smaller loss
                // than an ambush the player could not have seen coming.
                if (_player != null && spawnPoints[i] != null &&
                    Vector3.Distance(spawnPoints[i].position, start) < safeStartRadius)
                {
                    skipped++;
                    continue;
                }

                SpawnAt(spawnPoints[i], i);

                // Spread the work over a few frames; a whole house at once hitches.
                if (i % 4 == 3) yield return null;
            }

            if (skipped > 0)
            {
                Debug.Log($"[ZombieSpawner] {skipped} spawn(s) left empty inside the " +
                          $"{safeStartRadius:0} m safe start.");
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.ReportSpawnQueue(0);
                GameManager.Instance.ReportSpawningFinished();
            }
        }

        /// <summary>
        /// Drops the lurkers onto the NavMesh where the level chose. They are placed once,
        /// at the start, and never reinforced: a snake in a particular fern bed is a fact
        /// about the level, not a wave.
        /// </summary>
        private void PlaceLurkers()
        {
            if (lurkerPrefab == null) return;

            for (int i = 0; i < lurkerPositions.Count; i++)
            {
                NavMeshHit hit;

                // Nowhere walkable near it: skip rather than strand one inside a trunk.
                if (!NavMesh.SamplePosition(lurkerPositions[i], out hit, 4f, NavMesh.AllAreas))
                    continue;

                // Snakes obey the safe start too — one lying in the first fern bed you
                // walk past is exactly the ambush this whole change exists to remove.
                if (_player != null &&
                    Vector3.Distance(hit.position, _player.position) < safeStartRadius) continue;

                ZombieProfile.NextKindOverride = lurkerKind;

                // Sampled onto the mesh to prove there is deck under it, then lifted back
                // up. The sample is the safety check — it is what stops a perch position
                // over the sea from becoming a gull hovering over the sea — and the lift is
                // what puts the bird on the rail instead of standing beneath it.
                Vector3 placeAt = hit.position + Vector3.up * lurkerHoverHeight;

                var lurker = Instantiate(lurkerPrefab, placeAt,
                    Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                lurker.name = lurkerKind + "_" + i;

                _live.Add(lurker);
            }
        }

        /// <summary>
        /// Several of one type around a single marker. They are placed on the NavMesh
        /// individually, so a pack at a doorway spills into the room rather than stacking
        /// three bodies in the same square metre.
        /// </summary>
        private void SpawnPack(Vector3 centre, int ordinal)
        {
            int count = Random.Range(packSize.x, packSize.y + 1);

            for (int i = 0; i < count; i++)
            {
                Vector2 offset = Random.insideUnitCircle * packSpread;
                Vector3 wanted = centre + new Vector3(offset.x, 0f, offset.y);

                NavMeshHit hit;
                Vector3 at = NavMesh.SamplePosition(wanted, out hit, 3f, NavMesh.AllAreas)
                    ? hit.position
                    : centre;

                ZombieProfile.NextKindOverride = packKind;

                var member = Instantiate(packPrefab, at,
                    Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                member.name = packKind + "_" + ordinal + "_" + i;

                _live.Add(member);
                Spawned++;
            }
        }

        private void SpawnAt(Transform point, int ordinal)
        {
            if (point == null) return;

            Vector3 position = point.position;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(position, out hit, 4f, NavMesh.AllAreas))
                position = hit.position;

            bool isMutant = spawnMutant && ordinal == Mathf.Clamp(mutantSpawnOrdinal, 0, spawnPoints.Count - 1);

            // The beasts take a share of everything that is not the mutant.
            bool isBeast = !isMutant && beastPrefab != null && Random.value < beastShare;

            // Packs take a share of what is left. One marker becomes several bodies, so
            // the level's population is higher than its marker count — which is the point.
            if (!isMutant && !isBeast && packPrefab != null && Random.value < packShare)
            {
                SpawnPack(position, ordinal);
                return;
            }

            if (isMutant) ZombieProfile.NextKindOverride = ZombieKind.Mutant;
            else if (isBeast) ZombieProfile.NextKindOverride = beastKind;

            GameObject prefab = isBeast ? beastPrefab : zombiePrefab;
            if (prefab == null) prefab = zombiePrefab;

            // A hiding spot carries the direction it should be facing — out from its cover.
            // A plain marker does not, so those get a random heading.
            bool hasFacing = point.name.StartsWith("ZombieHide");
            Quaternion rotation = hasFacing
                ? point.rotation
                : Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            var zombie = Instantiate(prefab, position, rotation);
            zombie.name = isMutant ? "Zombie_Mutant"
                        : isBeast ? beastKind + "_" + ordinal
                        : "Zombie_" + ordinal;

            if (!TrySetAmbush(zombie, position, isMutant || isBeast))
                TrySetPlayDead(zombie, isMutant || isBeast);

            _live.Add(zombie);
            Spawned++;
        }

        /// <summary>
        /// A zombie that happens to spawn beside a door crouches behind it instead of
        /// wandering, and gets up when the door opens.
        ///
        /// Derived from where the markers already are rather than from a new spawn list,
        /// which means every level with doors gets ambushes for free and a level designer
        /// moving a spawn point moves the ambush with it. The mutant and the beasts are
        /// excluded: a bear folded behind a classroom door is comedy, not menace, and the
        /// mutant is a landmark you are supposed to see coming.
        /// </summary>
        private bool TrySetAmbush(GameObject zombie, Vector3 position, bool excluded)
        {
            if (excluded || zombie == null) return false;
            if (Random.value > ambushShare) return false;

            Door nearest = null;
            float best = ambushRadius * ambushRadius;

            foreach (Door door in Door.All)
            {
                if (door == null) continue;

                float distance = (door.transform.position - position).sqrMagnitude;
                if (distance >= best) continue;

                best = distance;
                nearest = door;
            }

            if (nearest == null) return false;

            var ambush = zombie.AddComponent<DoorAmbush>();
            ambush.Crouch();
            nearest.Register(ambush);

            var ai = zombie.GetComponent<ZombieAI>();
            if (ai != null) ai.HoldDormant();

            zombie.name += "_Ambush";
            return true;
        }

        /// <summary>
        /// A zombie with no door to hide behind lies down among the corpses instead.
        ///
        /// Only ever reached when TrySetAmbush declined, which is why that method now
        /// reports whether it claimed the zombie. The two are mutually exclusive and the
        /// combination is nonsense — a body lying flat behind a door is hidden by the door,
        /// so the door scare and the corpse scare cancel each other out and you get neither.
        ///
        /// Excluded on the same terms as the ambush, and for the same reason: the mutant is
        /// a landmark the player is meant to see coming, and a bear playing dead in a wood
        /// full of no other bears is not camouflage.
        /// </summary>
        private void TrySetPlayDead(GameObject zombie, bool excluded)
        {
            if (excluded || zombie == null) return;
            if (Random.value > playDeadShare) return;

            var playDead = zombie.AddComponent<PlayDead>();

            // If it declines — it already has a door to hide behind — take the component
            // straight back off. Leaving an inert one attached would make every later count
            // of "how many are playing dead" wrong.
            if (!playDead.Lie())
            {
                DestroyImmediate(playDead);
                return;
            }

            var ai = zombie.GetComponent<ZombieAI>();
            if (ai != null) ai.HoldDormant();

            zombie.name += "_PlayDead";
        }

        private void PruneDead()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                if (_live[i] == null) _live.RemoveAt(i);
                else
                {
                    var health = _live[i].GetComponent<ZombieHealth>();
                    if (health != null && !health.IsAlive) _live.RemoveAt(i);
                }
            }
        }

        private void SpawnOne()
        {
            Transform point = PickSpawnPoint();
            if (point == null) return;

            Vector3 position = point.position;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(position, out hit, 4f, NavMesh.AllAreas))
                position = hit.position;

            // The type must be chosen before the prefab wakes up, so it is handed over
            // just ahead of Instantiate rather than assigned afterwards.
            bool isMutant = spawnMutant && Spawned == mutantSpawnOrdinal;
            if (isMutant) ZombieProfile.NextKindOverride = ZombieKind.Mutant;

            var zombie = Instantiate(zombiePrefab, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            zombie.name = isMutant ? "Zombie_Mutant" : "Zombie_" + Spawned;
            _live.Add(zombie);
            Spawned++;
        }

        private Transform PickSpawnPoint()
        {
            if (spawnPoints.Count == 0) return null;

            var preferred = new List<Transform>();
            var acceptable = new List<Transform>();

            foreach (Transform point in spawnPoints)
            {
                if (point == null) continue;

                if (_player == null)
                {
                    preferred.Add(point);
                    continue;
                }

                float distance = Vector3.Distance(point.position, _player.position);
                if (distance < absoluteMinDistance) continue;

                if (distance >= minDistanceFromPlayer || !HasLineOfSightToPlayer(point.position))
                    preferred.Add(point);
                else
                    acceptable.Add(point);
            }

            if (preferred.Count > 0) return preferred[Random.Range(0, preferred.Count)];
            if (acceptable.Count > 0) return acceptable[Random.Range(0, acceptable.Count)];
            return spawnPoints[Random.Range(0, spawnPoints.Count)];
        }

        private bool HasLineOfSightToPlayer(Vector3 position)
        {
            if (_player == null) return false;

            Vector3 from = position + Vector3.up * 1.5f;
            Vector3 to = _player.position + Vector3.up * 1.2f;
            Vector3 direction = to - from;
            float distance = direction.magnitude;

            return !Physics.Raycast(from, direction.normalized, distance - 0.1f, sightBlockers, QueryTriggerInteraction.Ignore);
        }
    }
}
