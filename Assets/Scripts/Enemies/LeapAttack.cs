using UnityEngine;
using UnityEngine.AI;
using ZombieHouse.Audio;
using ZombieHouse.Core;

namespace ZombieHouse.Enemies
{
    /// <summary>
    /// Dilly Dog's pounce.
    ///
    /// He is the tall one with arms a third again too long, and a leap is what those are
    /// for. Mechanically it exists to solve a specific problem: every enemy in this game
    /// closes distance at a constant rate, so the player learns one number — "how far can I
    /// back up before it reaches me" — and that number is the whole of the combat. A leap
    /// breaks it exactly once per encounter, at a distance the player had already written
    /// off as safe.
    ///
    /// **It is telegraphed and it is committed.** He crouches for a beat before he goes,
    /// which is the player's chance to move, and once airborne he travels to where they
    /// *were* rather than tracking them — so sidestepping works and standing still does not.
    /// A homing leap would be unfair and, worse, would make the crouch meaningless.
    ///
    /// **It does not teleport and it does not open a gap it could not walk.** The landing
    /// spot is sampled onto the NavMesh first, so he cannot vault a fence, cross a river or
    /// end up inside the funhouse wall.
    /// </summary>
    [RequireComponent(typeof(ZombieAI))]
    [RequireComponent(typeof(NavMeshAgent))]
    public class LeapAttack : MonoBehaviour
    {
        [Tooltip("Closest range he will bother leaping from. Inside this he simply walks "
                 + "into you, which is what the reach is already for.")]
        [SerializeField] private float minimumRange = 4.5f;

        [Tooltip("Furthest he will leap from. Beyond this it would read as a charge rather "
                 + "than a pounce.")]
        [SerializeField] private float maximumRange = 9f;

        [Tooltip("The crouch before the jump — the player's warning, and the reason this is "
                 + "not a cheap shot.")]
        [SerializeField] private float windupSeconds = 0.45f;

        [Tooltip("Time in the air.")]
        [SerializeField] private float flightSeconds = 0.62f;

        [Tooltip("Peak height of the arc above the ground.")]
        [SerializeField] private float arcHeight = 2.4f;

        [Tooltip("Seconds before he can leap again. Long: the leap is an event, not a gap "
                 + "closer, and one every few seconds would just be how he walks.")]
        [SerializeField] private float cooldownSeconds = 9f;

        [Tooltip("Damage on landing, if the player is still there.")]
        [SerializeField] private float landingDamage = 24f;

        [Tooltip("How close the landing has to be to hurt.")]
        [SerializeField] private float landingRadius = 2.6f;

        private enum Phase { Ready, Winding, Airborne, Cooling }

        private ZombieAI _ai;
        private NavMeshAgent _agent;
        private ZombieHealth _health;
        private Transform _player;

        private Phase _phase = Phase.Ready;
        private float _timer;
        private Vector3 _from, _to;

        /// <summary>True while he is off the ground. Read by the test.</summary>
        public bool IsAirborne => _phase == Phase.Airborne;

        /// <summary>How many times he has leapt. Read by the test.</summary>
        public int Leaps { get; private set; }

        private void Awake()
        {
            _ai = GetComponent<ZombieAI>();
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<ZombieHealth>();
        }

        private void Update()
        {
            if (!GameManager.GameplayActive) return;
            if (_health != null && !_health.IsAlive) return;

            if (_player == null)
            {
                var playerObject = GameObject.FindGameObjectWithTag("Player");
                if (playerObject != null) _player = playerObject.transform;
                if (_player == null) return;
            }

            Tick(Time.deltaTime, _player.position);
        }

        /// <summary>
        /// Drives the whole leap. Public and taking its own delta and target so a test can
        /// run one start to finish in edit mode, where Update never fires and Time does not
        /// advance — a leap that can only be exercised by playing the game is a leap nobody
        /// checks.
        /// </summary>
        public void Tick(float deltaTime, Vector3 target)
        {
            switch (_phase)
            {
                case Phase.Cooling:
                    _timer -= deltaTime;
                    if (_timer <= 0f) _phase = Phase.Ready;
                    return;

                case Phase.Ready:
                    TryStart(target);
                    return;

                case Phase.Winding:
                    _timer -= deltaTime;
                    if (_timer > 0f) return;
                    Launch();
                    return;

                case Phase.Airborne:
                    Fly(deltaTime);
                    return;
            }
        }

        private void TryStart(Vector3 target)
        {
            // Only when he is actually hunting. A wandering dog that pounces at a noise
            // would fire this constantly and it would stop meaning anything.
            if (_ai == null || _ai.State != ZombieState.Chase) return;

            float distance = Vector3.Distance(transform.position, target);
            if (distance < minimumRange || distance > maximumRange) return;

            // The landing has to be somewhere he could have walked to. Without this he
            // vaults fences and lands inside walls, which is the difference between a leap
            // and a teleport.
            Vector3 wanted = target;
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(wanted, out hit, 2f, NavMesh.AllAreas)) return;

            _from = transform.position;
            _to = hit.position;

            _phase = Phase.Winding;
            _timer = windupSeconds;

            // He drops into the crouch here, and the sound is the tell. A silent windup
            // behind the player is a cheap shot; this one they can hear coming.
            GameAudio.PlayAt(Sfx.ZombieAlert, transform.position, 1.1f, 0.05f);
        }

        private void Launch()
        {
            _phase = Phase.Airborne;
            _timer = 0f;
            Leaps++;

            // The agent has to stop steering, or it fights the arc the whole way across and
            // he lands short. Re-enabled on touchdown.
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh) _agent.isStopped = true;
        }

        private void Fly(float deltaTime)
        {
            _timer += deltaTime;

            float t = Mathf.Clamp01(_timer / Mathf.Max(0.05f, flightSeconds));

            Vector3 flat = Vector3.Lerp(_from, _to, t);

            // A parabola, not a lerp with a bump: sin gives a true arc that leaves and
            // arrives at ground level with the right vertical speed at each end.
            flat.y = Mathf.Lerp(_from.y, _to.y, t) + Mathf.Sin(t * Mathf.PI) * arcHeight;

            transform.position = flat;

            // Facing the way he is going, which a NavMeshAgent would normally handle.
            Vector3 heading = _to - _from;
            heading.y = 0f;
            if (heading.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(heading.normalized, Vector3.up);

            if (t < 1f) return;

            Land();
        }

        private void Land()
        {
            transform.position = _to;
            _phase = Phase.Cooling;
            _timer = cooldownSeconds;

            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            {
                // Warp rather than assign position: the agent has been off the mesh's
                // steering for half a second and needs telling where it actually is, or it
                // spends the next few frames walking back to where it thinks it left.
                _agent.Warp(_to);
                _agent.isStopped = false;
            }

            GameAudio.PlayAt(Sfx.ZombieBite, transform.position + Vector3.up, 1f, 0.08f);
            Noise.Emit(transform.position, 9f);

            // Damage on arrival, and only if they are still there — which is the entire
            // reason the windup exists.
            var playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject == null) return;

            if (Vector3.Distance(playerObject.transform.position, _to) > landingRadius) return;

            var health = playerObject.GetComponent<ZombieHouse.Player.PlayerHealth>();
            if (health == null) return;

            health.TakeDamage(new DamageInfo(landingDamage, _to, Vector3.up,
                                             Vector3.up, gameObject, false));
        }
    }
}
