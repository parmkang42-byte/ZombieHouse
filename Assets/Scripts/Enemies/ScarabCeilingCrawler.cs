using UnityEngine;
using UnityEngine.AI;
using ZombieHouse.Core;

namespace ZombieHouse.Enemies
{
    /// <summary>
    /// Scarabs go up the walls and across the ceiling, and drop on you from above.
    ///
    /// This is the sibling of <see cref="ZombieWallCrawler"/> — the toddlers' component —
    /// but the behaviour is deliberately different in the one way that matters: a toddler
    /// scrambles up a wall *fast* to get around you, and a scarab climbs **slowly**, hangs
    /// over the room, and waits. A thing moving quickly overhead is a chase; a thing
    /// creeping across the ceiling above your head is dread, and dread is what a tomb is
    /// supposed to sell.
    ///
    /// Every exit from the climb — arrived, timed out, lost the surface, killed — funnels
    /// through <see cref="LetGo"/>, which drops it and hands control back to the NavMesh
    /// agent. That single funnel is why a failed climb means a beetle falling off a wall
    /// rather than a beetle stuck inside one.
    /// </summary>
    public class ScarabCeilingCrawler : MonoBehaviour
    {
        private enum Phase { Off, Ascending, OnCeiling, Falling }

        [Header("When to climb")]
        [SerializeField] private Vector2 attemptInterval = new Vector2(4f, 9f);
        [SerializeField] private float wallSearchRange = 1.8f;

        [Tooltip("It will not start a climb closer than this to the player: the point is "
                 + "to appear overhead, not to scale a wall in front of you.")]
        [SerializeField] private float minimumPlayerDistance = 7f;
        [SerializeField] private LayerMask climbableSurfaces = 1;

        [Header("Climbing — slow on purpose")]
        [SerializeField] private float climbSpeed = 1.1f;
        [SerializeField] private float ceilingSpeed = 1.5f;
        [SerializeField] private float surfaceOffset = 0.22f;
        [SerializeField] private float maximumCeilingHeight = 6f;

        [Header("Dropping")]
        [Tooltip("How close overhead it has to be before it lets go.")]
        [SerializeField] private float dropRadius = 2.2f;
        [SerializeField] private float fallSpeed = 7f;
        [SerializeField] private float maximumClimbSeconds = 14f;

        public bool IsClimbing => _phase != Phase.Off;
        public bool IsOnCeiling => _phase == Phase.OnCeiling;

        private ZombieAI _ai;
        private ZombieHealth _health;
        private NavMeshAgent _agent;
        private Transform _player;

        private Phase _phase = Phase.Off;
        private Vector3 _wallNormal;
        private float _ceilingHeight;
        private float _nextAttemptAt;
        private float _startedAt;
        private bool _dead;

        private void Awake()
        {
            _ai = GetComponent<ZombieAI>();
            _health = GetComponent<ZombieHealth>();
            _agent = GetComponent<NavMeshAgent>();

            var playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) _player = playerObject.transform;

            ScheduleNextAttempt();
        }

        private void OnEnable()
        {
            if (_health != null) _health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (_health != null) _health.Died -= OnDied;
        }

        /// <summary>
        /// Killed mid-climb. Put it back on the ground before the ragdoll takes over, or
        /// the corpse hangs in the air where the ceiling was.
        /// </summary>
        private void OnDied(DamageInfo info)
        {
            _dead = true;
            if (_phase != Phase.Off) LetGo();
        }

        private void ScheduleNextAttempt()
        {
            _nextAttemptAt = Time.time + Random.Range(attemptInterval.x, attemptInterval.y);
        }

        private void Update()
        {
            if (_dead || !GameManager.GameplayActive || _player == null) return;

            if (_phase != Phase.Off && Time.time - _startedAt > maximumClimbSeconds)
            {
                LetGo();
                return;
            }

            switch (_phase)
            {
                case Phase.Off: TryStartClimb(); break;
                case Phase.Ascending: TickAscend(); break;
                case Phase.OnCeiling: TickCeiling(); break;
                case Phase.Falling: TickFall(); break;
            }
        }

        private void TryStartClimb()
        {
            if (Time.time < _nextAttemptAt) return;
            ScheduleNextAttempt();

            if (_ai != null && _ai.State == ZombieState.Dead) return;
            if (Vector3.Distance(_player.position, transform.position) < minimumPlayerDistance) return;

            // A wall to go up, and a ceiling above it to get onto.
            if (!FindWall(out _wallNormal)) return;
            if (!FindCeiling(out _ceilingHeight)) return;

            _phase = Phase.Ascending;
            _startedAt = Time.time;

            // The agent has to let go of the transform while we drive it by hand.
            if (_agent != null && _agent.enabled) _agent.enabled = false;
        }

        private bool FindWall(out Vector3 normal)
        {
            normal = Vector3.zero;

            for (int i = 0; i < 8; i++)
            {
                Vector3 direction = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;

                if (!Physics.Raycast(transform.position + Vector3.up * 0.3f, direction,
                                     out RaycastHit hit, wallSearchRange,
                                     climbableSurfaces, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                // Only near-vertical faces are climbable; a ramp is not a wall.
                if (Mathf.Abs(hit.normal.y) > 0.35f) continue;

                normal = hit.normal;
                return true;
            }

            return false;
        }

        private bool FindCeiling(out float height)
        {
            height = 0f;

            if (!Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.up,
                                 out RaycastHit hit, maximumCeilingHeight,
                                 climbableSurfaces, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            height = hit.point.y;
            return true;
        }

        private void TickAscend()
        {
            transform.position += Vector3.up * (climbSpeed * Time.deltaTime);

            // Lie against the wall, feet towards it, which is what an insect climbing
            // actually looks like from below.
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(Vector3.up, _wallNormal), 6f * Time.deltaTime);

            if (transform.position.y < _ceilingHeight - surfaceOffset - 0.1f) return;

            _phase = Phase.OnCeiling;
        }

        private void TickCeiling()
        {
            // Upside down, and held just under the stone.
            Vector3 position = transform.position;
            position.y = _ceilingHeight - surfaceOffset;

            Vector3 toPlayer = _player.position - position;
            Vector3 flat = new Vector3(toPlayer.x, 0f, toPlayer.z);

            if (flat.sqrMagnitude > 0.01f)
                position += flat.normalized * (ceilingSpeed * Time.deltaTime);

            transform.position = position;

            // Hanging: its own up-vector points at the floor.
            Quaternion hanging = Quaternion.LookRotation(
                flat.sqrMagnitude > 0.01f ? flat.normalized : transform.forward, Vector3.down);

            transform.rotation = Quaternion.Slerp(transform.rotation, hanging, 5f * Time.deltaTime);

            // Directly overhead: let go.
            if (flat.magnitude <= dropRadius) _phase = Phase.Falling;
        }

        private void TickFall()
        {
            transform.position += Vector3.down * (fallSpeed * Time.deltaTime);

            // Right itself on the way down so it lands on its feet.
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(
                    Vector3.ProjectOnPlane(_player.position - transform.position, Vector3.up).normalized
                        + Vector3.forward * 0.001f,
                    Vector3.up),
                10f * Time.deltaTime);

            if (transform.position.y <= GroundHeight() + 0.1f) LetGo();
        }

        /// <summary>
        /// The single way out of a climb. Puts the scarab on the navigable floor, stands
        /// it up, and hands it back to the agent — whatever phase it was in and whatever
        /// interrupted it.
        /// </summary>
        private void LetGo()
        {
            _phase = Phase.Off;
            ScheduleNextAttempt();

            Vector3 at = transform.position;
            at.y = GroundHeight();

            if (NavMesh.SamplePosition(at, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                at = hit.position;

            transform.position = at;

            Vector3 facing = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            transform.rotation = facing.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(facing.normalized, Vector3.up)
                : Quaternion.identity;

            if (_dead || _agent == null) return;

            _agent.enabled = true;
            if (_agent.isOnNavMesh) _agent.Warp(at);
        }

        private float GroundHeight()
        {
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down,
                                out RaycastHit hit, 40f, climbableSurfaces,
                                QueryTriggerInteraction.Ignore))
            {
                return hit.point.y;
            }

            return 0f;
        }
    }
}
