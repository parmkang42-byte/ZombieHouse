using System;
using UnityEngine;
using UnityEngine.AI;
using ZombieHouse.Core;

namespace ZombieHouse.Enemies
{
    public enum ZombieState { Dormant, Idle, Wander, Investigate, Chase, Attack, Stagger, Dead }

    /// <summary>
    /// Shambling melee zombie. Sees in a cone with line of sight, hears gunshots and
    /// footsteps through walls, remembers where you were, and gives up after a while.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class ZombieAI : MonoBehaviour
    {
        [Header("Senses")]
        [SerializeField] private float sightRange = 18f;
        [SerializeField] private float fieldOfView = 110f;
        [SerializeField] private float peripheralRange = 3.5f;   // notices you regardless of angle this close
        [SerializeField] private float eyeHeight = 1.6f;
        [SerializeField] private LayerMask sightBlockers = 1;    // Default layer = level geometry
        [SerializeField] private float memorySeconds = 7f;
        [Tooltip("Scales how far it hears. Near zero means gunfire will not draw it.")]
        [SerializeField] private float hearingMultiplier = 1f;

        [Header("Movement")]
        [SerializeField] private float wanderSpeed = 0.9f;
        [SerializeField] private float investigateSpeed = 2.0f;
        [SerializeField] private float chaseSpeed = 3.3f;
        [SerializeField] private float turnSpeedDegrees = 220f;
        [SerializeField] private float wanderRadius = 8f;
        [SerializeField] private Vector2 idlePauseRange = new Vector2(1.5f, 4f);

        [Header("Attack")]
        [SerializeField] private float attackRange = 1.8f;
        [SerializeField] private float attackWindup = 0.45f;
        [SerializeField] private float attackCooldown = 1.3f;
        [SerializeField] private float attackDamage = 14f;
        [SerializeField] private float attackAngle = 70f;

        [Header("Reactions")]
        [SerializeField] private float alertRepathInterval = 0.25f;
        [Tooltip("Chance a hit fails to interrupt it. Set by the archetype.")]
        [Range(0f, 1f)] [SerializeField] private float staggerResistance = 0.35f;

        [Header("Ambush")]
        [Tooltip("Waits where it was placed until disturbed, instead of roaming.")]
        [SerializeField] private bool startDormant = true;
        [Tooltip("How close you have to get before it notices you and stirs.")]
        [SerializeField] private float wakeDistance = 7f;
        [Tooltip("A noise this close will wake it even if it cannot see you.")]
        [SerializeField] private float wakeNoiseDistance = 5f;

        [Header("Pack behaviour")]
        [Tooltip("How far its cry carries when it spots you. Small values keep fights local.")]
        [SerializeField] private float hordeCallRadius = 8f;
        [Tooltip("How far off to one side it aims while closing, so a pack surrounds you.")]
        [SerializeField] private float flankDistance = 2.6f;
        [SerializeField] private float flankRepickSeconds = 2.5f;

        [Header("Hunting")]
        [Tooltip("Seconds ahead it aims, so it cuts you off instead of trailing behind.")]
        [SerializeField] private float leadTime = 0.45f;
        [Tooltip("Places it checks around your last known position before giving up.")]
        [SerializeField] private int searchPoints = 3;
        [SerializeField] private float searchRadius = 6f;

        public ZombieState State { get; private set; } = ZombieState.Idle;
        public bool CanSeePlayer { get; private set; }

        /// <summary>Seconds between the swing starting and the damage landing — animation timing.</summary>
        public float AttackWindup => attackWindup;

        /// <summary>Current planar speed, used to drive the walk cycle.</summary>
        public float PlanarSpeed
        {
            get
            {
                if (_agent == null || !_agent.isActiveAndEnabled) return 0f;
                Vector3 v = _agent.velocity;
                v.y = 0f;
                return v.magnitude;
            }
        }

        public event Action<ZombieState> StateChanged;
        public event Action AttackStarted;
        public event Action AttackLanded;

        /// <summary>Raised only when the bite actually reaches you, not when it swings and misses.</summary>
        public event Action AttackConnected;

        private NavMeshAgent _agent;
        private Transform _player;

        private bool _heldDormant;
        private Vector3 _lastKnownPlayerPos;
        private Vector3 _homePosition;
        private float _lastSeenTime = -999f;
        private float _nextRepathTime;
        private float _idleUntil;
        private float _staggerUntil;
        private float _nextAttackTime;
        private float _attackLandsAt = -1f;

        private Player.PlayerController _playerMovement;
        private float _flankAngle;
        private float _nextFlankPick;
        private int _searchesRemaining;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _homePosition = transform.position;

            _agent.speed = wanderSpeed;
            _agent.angularSpeed = turnSpeedDegrees;
            _agent.stoppingDistance = attackRange * 0.75f;
            _agent.autoBraking = true;
        }

        private void OnEnable()
        {
            Noise.Emitted += OnNoise;
            ZombieComms.HordeCalled += OnHordeCalled;
        }

        private void OnDisable()
        {
            Noise.Emitted -= OnNoise;
            ZombieComms.HordeCalled -= OnHordeCalled;
        }

        private void Start()
        {
            var playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                _player = playerObject.transform;
                _playerMovement = playerObject.GetComponent<Player.PlayerController>();
            }

            _flankAngle = UnityEngine.Random.Range(-70f, 70f);

            if (startDormant) EnterDormant();
            else EnterIdle();
        }

        /// <summary>
        /// Waits where it was placed. A dormant walker does not roam, does not scan, and
        /// does not answer another's cry — it is part of the room until you get close
        /// enough to disturb it. That is what makes exploring the house tense rather than
        /// a running battle with a horde that all noticed you at once.
        /// </summary>
        private void EnterDormant()
        {
            SetState(ZombieState.Dormant);

            if (_agent != null && _agent.isActiveAndEnabled && _agent.isOnNavMesh)
            {
                _agent.ResetPath();
                _agent.isStopped = true;
            }
        }

        private void TickDormant()
        {
            if (_player == null) return;

            // An ambusher stays down no matter how close you get. Without this the whole
            // trick collapses: zombies wake on proximity, so one crouched behind a door
            // would stand up while you were still walking towards it and the door would
            // open on something already coming at you rather than on a surprise.
            if (_heldDormant) return;

            // Nothing stirs during the opening grace. Without this the structural fix — a
            // clear radius around the start — would still be undone by anything that
            // wandered in during the first second.
            if (!GameManager.CombatAllowed) return;

            if (Vector3.Distance(transform.position, _player.position) <= wakeDistance)
                Wake();
        }

        /// <summary>
        /// Keeps it asleep until something wakes it by hand. Used by the door ambush, which
        /// owns the moment it gets up.
        /// </summary>
        public void HoldDormant()
        {
            _heldDormant = true;
            if (State != ZombieState.Dormant) EnterDormant();
        }

        /// <summary>Rouses it and sends it after you. Safe to call when already awake.</summary>
        public void Wake()
        {
            if (State != ZombieState.Dormant) return;

            // An explicit wake overrides the hold — that is what the door pulls.
            _heldDormant = false;

            if (_agent != null && _agent.isActiveAndEnabled && _agent.isOnNavMesh)
                _agent.isStopped = false;

            _lastKnownPlayerPos = _player != null ? _player.position : transform.position;
            _homePosition = transform.position;
            EnterChase();
        }

        /// <summary>Copies a type's numbers in. Called by ZombieProfile before Awake ordering matters.</summary>
        public void ApplyArchetype(ZombieArchetype archetype)
        {
            if (archetype == null) return;

            wanderSpeed = archetype.WanderSpeed;
            investigateSpeed = archetype.InvestigateSpeed;
            chaseSpeed = archetype.ChaseSpeed;
            turnSpeedDegrees = archetype.TurnSpeed;

            attackDamage = archetype.AttackDamage;
            attackCooldown = archetype.AttackCooldown;
            attackWindup = archetype.AttackWindup;

            // Reach is per-type now. Zero means "keep the default", so every archetype
            // written before the janitor carries on behaving exactly as it did.
            if (archetype.AttackRange > 0f) attackRange = archetype.AttackRange;

            sightRange = archetype.SightRange;
            fieldOfView = archetype.FieldOfView;
            memorySeconds = archetype.MemorySeconds;
            hearingMultiplier = archetype.HearingMultiplier;

            staggerResistance = archetype.StaggerResistance;
        }

        private void Update()
        {
            if (State == ZombieState.Dead) return;
            if (!GameManager.GameplayActive)
            {
                if (_agent.isActiveAndEnabled && _agent.isOnNavMesh) _agent.isStopped = true;
                return;
            }

            if (_agent.isActiveAndEnabled && _agent.isOnNavMesh) _agent.isStopped = false;

            // A dormant walker is not looking for you; only proximity, noise or a bullet
            // will rouse it, so its senses stay switched off until then.
            if (State == ZombieState.Dormant)
            {
                TickDormant();
                return;
            }

            UpdateSenses();

            switch (State)
            {
                case ZombieState.Idle: TickIdle(); break;
                case ZombieState.Wander: TickWander(); break;
                case ZombieState.Investigate: TickInvestigate(); break;
                case ZombieState.Chase: TickChase(); break;
                case ZombieState.Attack: TickAttack(); break;
                case ZombieState.Stagger: TickStagger(); break;
            }
        }

        // ---- senses ---------------------------------------------------------

        private void UpdateSenses()
        {
            CanSeePlayer = false;
            if (_player == null) return;

            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Vector3 targetPoint = _player.position + Vector3.up * 1.2f;
            Vector3 toPlayer = targetPoint - eye;
            float distance = toPlayer.magnitude;

            if (distance > sightRange) return;

            float angle = Vector3.Angle(transform.forward, new Vector3(toPlayer.x, 0f, toPlayer.z));
            bool withinCone = angle <= fieldOfView * 0.5f || distance <= peripheralRange;
            if (!withinCone) return;

            if (Physics.Raycast(eye, toPlayer.normalized, distance - 0.1f, sightBlockers, QueryTriggerInteraction.Ignore))
                return;

            CanSeePlayer = true;
            _lastSeenTime = Time.time;
            _lastKnownPlayerPos = _player.position;

            if (State != ZombieState.Chase && State != ZombieState.Attack && State != ZombieState.Stagger)
                EnterChase();
        }

        private void OnNoise(Vector3 position, float radius)
        {
            if (State == ZombieState.Dead || State == ZombieState.Chase || State == ZombieState.Attack) return;

            float distance = Vector3.Distance(transform.position, position);

            // Asleep: only something happening almost on top of it will do.
            if (State == ZombieState.Dormant)
            {
                if (distance <= wakeNoiseDistance * hearingMultiplier)
                {
                    _lastKnownPlayerPos = position;
                    Wake();
                }
                return;
            }

            if (distance > radius * hearingMultiplier) return;

            _lastKnownPlayerPos = position;
            EnterInvestigate();
        }

        /// <summary>
        /// Another walker has seen the player and cried out. Anything in earshot converges,
        /// which turns one bad sightline into the whole floor coming for you.
        /// </summary>
        private void OnHordeCalled(Vector3 playerPosition, Vector3 callerPosition, float radius)
        {
            if (State == ZombieState.Dead || State == ZombieState.Chase || State == ZombieState.Attack) return;

            // Sleepers ignore the call. Otherwise one scream would empty the whole house
            // at you, which is exactly the horde behaviour the ambush layout replaces.
            if (State == ZombieState.Dormant) return;

            if (Vector3.Distance(transform.position, callerPosition) > radius) return;

            _lastKnownPlayerPos = playerPosition;

            // Close enough to join the hunt directly; otherwise head over and look.
            if (Vector3.Distance(transform.position, playerPosition) <= sightRange * 1.4f) EnterChase();
            else EnterInvestigate();
        }

        private bool RemembersPlayer => Time.time - _lastSeenTime <= memorySeconds;

        // ---- states ---------------------------------------------------------

        /// <summary>Single funnel for transitions so listeners (audio, animation) never miss one.</summary>
        private void SetState(ZombieState next)
        {
            if (State == next) return;
            State = next;
            StateChanged?.Invoke(next);
        }

        private void EnterIdle()
        {
            SetState(ZombieState.Idle);
            _agent.speed = wanderSpeed;
            // Qualified: this file imports System, so bare Random is ambiguous.
            _idleUntil = Time.time + UnityEngine.Random.Range(idlePauseRange.x, idlePauseRange.y);
            if (_agent.isOnNavMesh) _agent.ResetPath();
        }

        private void TickIdle()
        {
            if (Time.time >= _idleUntil) EnterWander();
        }

        private void EnterWander()
        {
            SetState(ZombieState.Wander);
            _agent.speed = wanderSpeed;

            Vector3 target;
            if (TryFindPointNear(_homePosition, wanderRadius, out target))
                SetDestination(target);
            else
                EnterIdle();
        }

        private void TickWander()
        {
            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.2f)
                EnterIdle();
        }

        private void EnterInvestigate()
        {
            if (State != ZombieState.Investigate) _searchesRemaining = searchPoints;

            SetState(ZombieState.Investigate);
            _agent.speed = investigateSpeed;
            SetDestination(_lastKnownPlayerPos);
        }

        /// <summary>
        /// Reaching the last known position is not the end of it. A few more spots get
        /// checked nearby before it gives up, so breaking line of sight and standing still
        /// in the next room is no longer enough — you have to actually leave.
        /// </summary>
        private void TickInvestigate()
        {
            bool arrived = !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.3f;
            if (!arrived) return;

            if (_searchesRemaining <= 0)
            {
                _homePosition = transform.position;
                EnterIdle();
                return;
            }

            _searchesRemaining--;

            Vector3 next;
            if (TryFindPointNear(_lastKnownPlayerPos, searchRadius, out next)) SetDestination(next);
            else EnterIdle();
        }

        private void EnterChase()
        {
            bool wasHunting = State == ZombieState.Chase || State == ZombieState.Attack;

            SetState(ZombieState.Chase);
            _agent.speed = chaseSpeed;
            _nextRepathTime = 0f;
            _searchesRemaining = searchPoints;

            // Cry out the first time it picks you up, not on every re-entry from an attack.
            if (!wasHunting && _player != null)
                ZombieComms.CallHorde(_player.position, transform.position, hordeCallRadius);
        }

        private void TickChase()
        {
            if (_player == null) { EnterIdle(); return; }

            float distance = Vector3.Distance(transform.position, _player.position);

            if (CanSeePlayer && distance <= attackRange && Time.time >= _nextAttackTime
                && GameManager.CombatAllowed)
            {
                EnterAttack();
                return;
            }

            if (Time.time >= _nextRepathTime)
            {
                _nextRepathTime = Time.time + alertRepathInterval;
                SetDestination(ChaseDestination(distance));
            }

            if (!CanSeePlayer && !RemembersPlayer)
            {
                // Lost the trail — sweep the last known spot, then search around it.
                _homePosition = transform.position;
                EnterInvestigate();
            }
        }

        /// <summary>
        /// Where to actually run to. Two things stop a pack behaving like a queue: it aims
        /// where the player is going rather than where they are, and each one approaches
        /// from its own angle so they arrive spread out instead of single file.
        /// </summary>
        private Vector3 ChaseDestination(float distance)
        {
            if (!CanSeePlayer) return _lastKnownPlayerPos;

            Vector3 target = _player.position;

            // Lead the target. Cutting the corner matters most for the fast ones.
            if (_playerMovement != null)
                target += _playerMovement.Velocity * leadTime;

            // Close in properly for the kill; only spread out on the approach.
            if (distance <= attackRange * 1.8f) return target;

            if (Time.time >= _nextFlankPick)
            {
                _nextFlankPick = Time.time + flankRepickSeconds;
                _flankAngle = UnityEngine.Random.Range(-70f, 70f);
            }

            Vector3 approach = (target - transform.position).normalized;
            Vector3 offset = Quaternion.Euler(0f, _flankAngle, 0f) * approach;
            return target - offset * flankDistance;
        }

        private void EnterAttack()
        {
            SetState(ZombieState.Attack);
            _attackLandsAt = Time.time + attackWindup;
            AttackStarted?.Invoke();
            if (_agent.isOnNavMesh)
            {
                _agent.ResetPath();
                _agent.velocity = Vector3.zero;
            }
        }

        private void TickAttack()
        {
            if (_player == null) { EnterIdle(); return; }

            FaceTarget(_player.position);

            if (_attackLandsAt < 0f || Time.time < _attackLandsAt) return;

            // The swing lands now — it only connects if you are still close and in front.
            Vector3 toPlayer = _player.position - transform.position;
            float distance = toPlayer.magnitude;
            float angle = Vector3.Angle(transform.forward, new Vector3(toPlayer.x, 0f, toPlayer.z));

            if (distance <= attackRange + 0.35f && angle <= attackAngle)
            {
                var target = _player.GetComponentInParent<IDamageable>();
                if (target != null && target.IsAlive)
                {
                    target.TakeDamage(new DamageInfo(
                        attackDamage,
                        _player.position,
                        -toPlayer.normalized,
                        toPlayer.normalized,
                        gameObject));

                    AttackConnected?.Invoke();
                }
            }

            _attackLandsAt = -1f;
            _nextAttackTime = Time.time + attackCooldown;
            AttackLanded?.Invoke();
            EnterChase();
        }

        private void TickStagger()
        {
            if (Time.time >= _staggerUntil)
            {
                if (RemembersPlayer) EnterChase();
                else EnterInvestigate();
            }
        }

        // ---- events from ZombieHealth ---------------------------------------

        public void OnDamaged(DamageInfo info, float staggerDuration)
        {
            if (State == ZombieState.Dead) return;

            // Being shot always wakes it, however deeply it was sleeping.
            if (State == ZombieState.Dormant) Wake();

            // Being shot tells it exactly where you are, even from behind — and it tells
            // everything else nearby too.
            if (info.Source != null)
            {
                _lastKnownPlayerPos = info.Source.transform.position;
                _lastSeenTime = Time.time;
                ZombieComms.CallHorde(_lastKnownPlayerPos, transform.position, hordeCallRadius * 0.6f);
            }

            // Tough ones shrug the hit off and keep coming instead of being stun-locked.
            if (UnityEngine.Random.value < staggerResistance)
            {
                if (State != ZombieState.Attack) EnterChase();
                return;
            }

            SetState(ZombieState.Stagger);
            _staggerUntil = Time.time + staggerDuration;
            if (_agent.isActiveAndEnabled && _agent.isOnNavMesh)
            {
                _agent.ResetPath();
                _agent.velocity *= 0.2f;
            }
        }

        public void OnDied()
        {
            SetState(ZombieState.Dead);
            Noise.Emitted -= OnNoise;
            enabled = false;
        }

        // ---- helpers --------------------------------------------------------

        private void SetDestination(Vector3 worldPosition)
        {
            if (!_agent.isActiveAndEnabled || !_agent.isOnNavMesh) return;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(worldPosition, out hit, 3f, NavMesh.AllAreas))
                _agent.SetDestination(hit.position);
        }

        private bool TryFindPointNear(Vector3 center, float radius, out Vector3 result)
        {
            for (int attempt = 0; attempt < 12; attempt++)
            {
                Vector2 offset = UnityEngine.Random.insideUnitCircle * radius;
                Vector3 candidate = center + new Vector3(offset.x, 0f, offset.y);

                NavMeshHit hit;
                if (NavMesh.SamplePosition(candidate, out hit, 2.5f, NavMesh.AllAreas))
                {
                    result = hit.position;
                    return true;
                }
            }

            result = center;
            return false;
        }

        private void FaceTarget(Vector3 worldPosition)
        {
            Vector3 direction = worldPosition - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) return;

            Quaternion target = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeedDegrees * Time.deltaTime);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, sightRange);
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
