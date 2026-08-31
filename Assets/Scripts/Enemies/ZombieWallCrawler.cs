using UnityEngine;
using UnityEngine.AI;
using ZombieHouse.Core;

namespace ZombieHouse.Enemies
{
    /// <summary>
    /// Lets a toddler leave the floor and come at you across the walls.
    ///
    /// While climbing it steps outside the NavMesh entirely and steers by raycast, sticking
    /// to whatever flat surface it found and orienting its up-vector to that surface's
    /// normal. That is the whole trick: the walls in this house are axis-aligned boxes, so
    /// following one flat plane is enough to read as wall-crawling without needing general
    /// surface traversal.
    ///
    /// It is deliberately hard to get stuck. Every climb has a hard timeout, and every exit
    /// path — finished, interrupted, or killed — funnels through Drop(), which puts the
    /// body back on the NavMesh. If anything unexpected happens it just falls off the wall
    /// and keeps walking.
    /// </summary>
    [RequireComponent(typeof(ZombieAI))]
    public class ZombieWallCrawler : MonoBehaviour
    {
        private enum Phase { Off, Ascending, Traversing, Dropping }

        [Header("When to climb")]
        [SerializeField] private Vector2 climbAttemptInterval = new Vector2(3.5f, 8f);
        [SerializeField] private float wallSearchRange = 2.2f;
        [SerializeField] private float minimumPlayerDistance = 5f;
        [SerializeField] private LayerMask climbableSurfaces = 1;   // Default = level geometry

        [Header("Climbing")]
        [SerializeField] private float climbSpeed = 3.4f;
        [SerializeField] private float traverseSpeed = 4.6f;
        [SerializeField] private float climbHeight = 2.4f;
        [SerializeField] private float surfaceOffset = 0.22f;
        [SerializeField] private float dropDistance = 3.2f;
        [SerializeField] private float maximumClimbSeconds = 9f;

        public bool IsCrawling => _phase != Phase.Off;

        private ZombieAI _ai;
        private ZombieHealth _health;
        private NavMeshAgent _agent;
        private Transform _player;

        private Phase _phase = Phase.Off;
        private Vector3 _surfaceNormal;
        private float _nextAttemptTime;
        private float _climbStartedAt;
        private bool _enabledForThisZombie;
        private bool _dead;

        private void Awake()
        {
            _ai = GetComponent<ZombieAI>();
            _health = GetComponent<ZombieHealth>();
            _agent = GetComponent<NavMeshAgent>();

            var profile = GetComponent<ZombieProfile>();
            _enabledForThisZombie = profile != null && profile.Archetype != null && profile.Archetype.ClimbsWalls;

            if (!_enabledForThisZombie) enabled = false;
        }

        private void OnEnable()
        {
            if (_health != null) _health.Died += OnDied;
            ScheduleNextAttempt();
        }

        private void OnDisable()
        {
            if (_health != null) _health.Died -= OnDied;
        }

        private void OnDied(DamageInfo info)
        {
            _dead = true;
            if (_phase != Phase.Off) EndClimb();
        }

        private void ScheduleNextAttempt()
        {
            _nextAttemptTime = Time.time + Random.Range(climbAttemptInterval.x, climbAttemptInterval.y);
        }

        private void Update()
        {
            if (_dead || !GameManager.GameplayActive) return;

            if (_player == null)
            {
                var playerObject = GameObject.FindGameObjectWithTag("Player");
                if (playerObject != null) _player = playerObject.transform;
                if (_player == null) return;
            }

            if (_phase == Phase.Off)
            {
                TryStartClimb();
                return;
            }

            // However it goes wrong, it comes down rather than hanging on a wall forever.
            if (Time.time - _climbStartedAt > maximumClimbSeconds)
            {
                EndClimb();
                return;
            }

            switch (_phase)
            {
                case Phase.Ascending: TickAscend(); break;
                case Phase.Traversing: TickTraverse(); break;
                case Phase.Dropping: TickDrop(); break;
            }
        }

        // ---- starting -------------------------------------------------------

        private void TryStartClimb()
        {
            if (Time.time < _nextAttemptTime) return;
            if (_ai == null || _ai.State != ZombieState.Chase) return;

            // No point scaling a wall once it is already on top of you.
            if (Vector3.Distance(transform.position, _player.position) < minimumPlayerDistance)
            {
                ScheduleNextAttempt();
                return;
            }

            if (!FindWall(out _surfaceNormal))
            {
                ScheduleNextAttempt();
                return;
            }

            BeginClimb();
        }

        /// <summary>Looks for a wall on any side, preferring one that faces the player.</summary>
        private bool FindWall(out Vector3 normal)
        {
            normal = Vector3.zero;

            Vector3 origin = transform.position + Vector3.up * 0.6f;
            float best = float.MaxValue;
            bool found = false;

            for (int i = 0; i < 8; i++)
            {
                Vector3 direction = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;

                RaycastHit hit;
                if (!Physics.Raycast(origin, direction, out hit, wallSearchRange,
                                     climbableSurfaces, QueryTriggerInteraction.Ignore))
                    continue;

                // Only near-vertical faces are climbable; floors and ceilings are not walls.
                if (Mathf.Abs(hit.normal.y) > 0.35f) continue;

                if (hit.distance >= best) continue;

                best = hit.distance;
                normal = hit.normal;
                found = true;
            }

            return found;
        }

        private void BeginClimb()
        {
            _phase = Phase.Ascending;
            _climbStartedAt = Time.time;

            // Off the NavMesh: the agent and the normal chase logic both stand down while
            // the crawler drives the transform directly.
            if (_agent != null && _agent.isActiveAndEnabled) _agent.enabled = false;
            if (_ai != null) _ai.enabled = false;
        }

        // ---- phases ---------------------------------------------------------

        private void TickAscend()
        {
            transform.position += Vector3.up * (climbSpeed * Time.deltaTime);
            StickToSurface();

            if (transform.position.y >= GroundHeight() + climbHeight)
                _phase = Phase.Traversing;
        }

        private void TickTraverse()
        {
            // Move across the wall face towards the player, staying in the surface plane.
            Vector3 toPlayer = _player.position - transform.position;
            Vector3 alongSurface = Vector3.ProjectOnPlane(toPlayer, _surfaceNormal);

            if (alongSurface.sqrMagnitude < 0.01f)
            {
                _phase = Phase.Dropping;
                return;
            }

            transform.position += alongSurface.normalized * (traverseSpeed * Time.deltaTime);

            // Losing the wall — an inside corner or a doorway — means it is time to drop.
            if (!StickToSurface())
            {
                _phase = Phase.Dropping;
                return;
            }

            // Close enough to drop on top of them.
            Vector3 flat = toPlayer;
            flat.y = 0f;
            if (flat.magnitude <= dropDistance) _phase = Phase.Dropping;
        }

        private void TickDrop()
        {
            transform.position += Vector3.down * (climbSpeed * 1.6f * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(Vector3.ProjectOnPlane(_player.position - transform.position, Vector3.up)),
                8f * Time.deltaTime);

            if (transform.position.y <= GroundHeight() + 0.15f) EndClimb();
        }

        /// <summary>
        /// Holds position a fixed distance off the wall and lies the body against it.
        /// Returns false when there is no longer a wall to cling to.
        /// </summary>
        private bool StickToSurface()
        {
            Vector3 origin = transform.position + _surfaceNormal * 0.5f;

            RaycastHit hit;
            if (!Physics.Raycast(origin, -_surfaceNormal, out hit, 1.5f,
                                 climbableSurfaces, QueryTriggerInteraction.Ignore))
                return false;

            transform.position = hit.point + hit.normal * surfaceOffset;
            _surfaceNormal = hit.normal;

            // Up-vector along the wall normal is what makes it read as clinging rather
            // than hovering: the body lies flat against the surface.
            Vector3 forward = Vector3.ProjectOnPlane(_player.position - transform.position, hit.normal);
            if (forward.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(forward, hit.normal);

            return true;
        }

        private float GroundHeight()
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.down, out hit, 30f,
                                climbableSurfaces, QueryTriggerInteraction.Ignore))
                return hit.point.y;

            return 0f;
        }

        /// <summary>Puts it back on the floor and hands control back to the normal AI.</summary>
        private void EndClimb()
        {
            _phase = Phase.Off;
            ScheduleNextAttempt();

            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 6f, NavMesh.AllAreas))
                transform.position = hit.position;

            if (_dead) return;

            if (_agent != null && !_agent.enabled) _agent.enabled = true;
            if (_ai != null) _ai.enabled = true;
        }
    }
}
