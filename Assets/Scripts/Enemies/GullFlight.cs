using UnityEngine;
using ZombieHouse.Audio;
using ZombieHouse.Combat;
using ZombieHouse.Core;

namespace ZombieHouse.Enemies
{
    /// <summary>
    /// A zombie gull: sits on the rail, screams, comes down at you, goes back to the rail.
    ///
    /// The first creature in this game that does not use a NavMeshAgent at all. Everything
    /// else walks, and walking is what the NavMesh is for; a bird that perches 1.3 m up and
    /// crosses the deck through the air has no business on it. So this component owns the
    /// transform outright, which makes it the one creature that can get *permanently* wrong
    /// — a walker with a broken state machine stands still and is obviously broken, while a
    /// bird with a broken state machine hangs in the air and looks like it is circling.
    ///
    /// Three rules keep that from happening, and all three are tested:
    ///
    ///   **Home is where it started.** Not the nearest perch, not a perch looked up from the
    ///   level — the exact position the spawner put it at, captured in Awake. That position
    ///   is known-good by construction, so re-anchoring can never fail or need a fallback.
    ///
    ///   **There is a hard airborne budget**, checked every tick in every state rather than
    ///   at the end of each one. A bug inside a flight state cannot outlive it, because the
    ///   timeout does not live inside the flight states.
    ///
    ///   **Anchoring is one method and it always succeeds.** Timeout, arrival, being
    ///   disabled and dying all go through it. There is no path that leaves a live gull off
    ///   its perch and not flying.
    ///
    /// The dive is committed rather than homing, exactly like <see cref="LeapAttack"/>: it
    /// goes to where you were when it screamed. The scream is the warning and stepping aside
    /// is the answer, which is the only thing that makes a bird that attacks from above fair.
    ///
    /// It will not dive at a player on another deck. A gull is not going through the hull,
    /// and without that check it happily would — you would be two decks down and taking hits
    /// from something you cannot see or reach.
    /// </summary>
    public class GullFlight : MonoBehaviour
    {
        [Tooltip("How far it will notice you from. Gulls see a long way.")]
        [SerializeField] private float noticeRange = 20f;

        [Tooltip("Closer than this it does not bother — it is already on top of you.")]
        [SerializeField] private float minimumRange = 3.5f;

        [Tooltip("How far above or below the perch counts as the same deck. A gull is not "
                 + "flying through 2.9 m of steel to reach the hold.")]
        [SerializeField] private float sameDeckHeight = 2.2f;

        [Tooltip("The scream, and the lift off the rail. This is the player's warning.")]
        [SerializeField] private float alertSeconds = 0.65f;

        [Tooltip("Time spent coming down at you.")]
        [SerializeField] private float diveSeconds = 1.05f;

        [Tooltip("Time climbing back to the rail.")]
        [SerializeField] private float returnSeconds = 1.35f;

        [Tooltip("How high it goes over the top of the arc.")]
        [SerializeField] private float arcHeight = 2.8f;

        [Tooltip("Damage if you are still where it aimed. Small on purpose — the gulls are "
                 + "a pressure on your attention, not on your health bar. What they cost you "
                 + "is the second you spent looking up.")]
        [SerializeField] private float strikeDamage = 8f;

        [SerializeField] private float strikeRadius = 2.0f;

        [Tooltip("Rest on the rail between dives.")]
        [SerializeField] private float cooldownSeconds = 5.5f;

        [Tooltip("The hard stop. However the flight states misbehave, a gull that has been "
                 + "off its perch this long is put back on it. The dive and the return "
                 + "together are 2.4 s, so this is not a limit anything correct ever meets.")]
        [SerializeField] private float maxAirborneSeconds = 6f;

        private enum Phase { Perched, Alert, Diving, Returning, Cooling }

        private Phase _phase = Phase.Perched;
        private float _timer;
        private float _airborne;

        private Vector3 _from;
        private Vector3 _to;
        private Transform _player;
        private ZombieHealth _health;
        private Rigidbody _body;
        private ZombieRig _rig;

        /// <summary>The perch. Where it was put, and the only place it ever returns to.</summary>
        public Vector3 Home { get; private set; }

        public bool IsPerched => _phase == Phase.Perched || _phase == Phase.Cooling;
        public bool IsFlying => _phase == Phase.Diving || _phase == Phase.Returning;

        /// <summary>Completed dives. Read by the test.</summary>
        public int Dives { get; private set; }

        /// <summary>Seconds off the perch on the current trip. Read by the test.</summary>
        public float AirborneSeconds => _airborne;

        /// <summary>Times the hard timeout had to step in. Should be zero in a healthy run.</summary>
        public int Rescues { get; private set; }

        private void Awake() => Initialise();

        /// <summary>
        /// Captures the perch and wires the components.
        ///
        /// Public because Awake and OnEnable do not run in edit mode, so a gull built by a
        /// test has no Home at all — every re-anchor would send it to the world origin, and
        /// a test measuring "did it come back" would be measuring nothing. Safe to call more
        /// than once: the event is unsubscribed before it is subscribed.
        ///
        /// Home is taken from wherever the transform is at the moment this runs, which for a
        /// spawned gull is where the spawner put it. That is the entire re-anchor guarantee:
        /// a position that was already good, remembered rather than recomputed.
        /// </summary>
        public void Initialise()
        {
            Home = transform.position;

            _health = GetComponent<ZombieHealth>();
            _body = GetComponent<Rigidbody>();
            _rig = GetComponent<ZombieRig>();

            if (_health != null)
            {
                _health.Died -= OnDied;
                _health.Died += OnDied;
            }

            var playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) _player = playerObject.transform;
        }

        private void OnEnable()
        {
            if (_health != null)
            {
                _health.Died -= OnDied;
                _health.Died += OnDied;
            }
        }

        private void OnDisable()
        {
            if (_health != null) _health.Died -= OnDied;
        }

        /// <summary>
        /// Death drops it. The kinematic body goes dynamic and gravity does the rest, so a
        /// gull shot mid-dive falls onto the deck instead of stopping dead in the air —
        /// which is what it would do, because this component is the only thing moving it.
        /// </summary>
        private void OnDied(DamageInfo info)
        {
            enabled = false;

            if (_body == null) return;

            _body.isKinematic = false;
            _body.useGravity = true;
            _body.linearVelocity = (transform.forward * 1.5f + Vector3.up * 0.5f);
        }

        private void Update()
        {
            if (!GameManager.CombatAllowed) return;
            if (_player == null) return;

            Tick(Time.deltaTime, _player.position);
        }

        /// <summary>
        /// The whole bird. Public and taking its own delta and target so a test can fly one
        /// start to finish in edit mode, where Update never runs and Time does not advance.
        ///
        /// The airborne budget is spent and checked HERE, above the state machine, rather
        /// than inside the flight states. That is the point: a state that forgets to finish
        /// cannot also forget to time out.
        /// </summary>
        public void Tick(float deltaTime, Vector3 target)
        {
            if (IsFlying || _phase == Phase.Alert)
            {
                _airborne += deltaTime;

                if (_airborne > maxAirborneSeconds)
                {
                    Rescues++;
                    Anchor();
                    return;
                }
            }

            switch (_phase)
            {
                case Phase.Cooling:
                    _timer -= deltaTime;
                    if (_timer <= 0f) _phase = Phase.Perched;
                    return;

                case Phase.Perched:
                    TryStart(target);
                    return;

                case Phase.Alert:
                    _timer -= deltaTime;
                    if (_timer <= 0f) Launch(target);
                    return;

                case Phase.Diving:
                    Fly(deltaTime, diveSeconds, Strike);
                    return;

                case Phase.Returning:
                    Fly(deltaTime, returnSeconds, Anchor);
                    return;
            }
        }

        private void TryStart(Vector3 target)
        {
            float distance = Vector3.Distance(Home, target);
            if (distance > noticeRange || distance < minimumRange) return;

            // Not through the hull. Without this it dives at a player two decks down and
            // hits them through the plating, which reads as the game cheating rather than
            // as a bird.
            if (Mathf.Abs(target.y - Home.y) > sameDeckHeight) return;

            _phase = Phase.Alert;
            _timer = alertSeconds;
            _airborne = 0f;

            // The scream is the whole fairness of this attack, so it happens before anything
            // moves. Pitched well up: it is a bird, and nothing else in the game is.
            GameAudio.PlayAt(Sfx.ZombieAlert, transform.position, 1.9f, 0.06f);
            Noise.Emit(transform.position, 7f);
        }

        private void Launch(Vector3 target)
        {
            // Committed to where they were when it screamed, not to where they are now.
            _from = transform.position;
            _to = target + Vector3.up * 0.8f;

            _phase = Phase.Diving;
            _timer = 0f;
        }

        private void Fly(float deltaTime, float duration, System.Action arrive)
        {
            _timer += deltaTime;

            float t = Mathf.Clamp01(_timer / Mathf.Max(0.05f, duration));

            Vector3 at = Vector3.Lerp(_from, _to, t);
            at.y = Mathf.Lerp(_from.y, _to.y, t) + Mathf.Sin(t * Mathf.PI) * arcHeight;
            transform.position = at;

            Vector3 heading = _to - _from;
            heading.y = 0f;
            if (heading.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(heading.normalized, Vector3.up);

            Flap(true);

            if (t >= 1f) arrive();
        }

        private void Strike()
        {
            Dives++;

            GameAudio.PlayAt(Sfx.ZombieBite, transform.position, 1.6f, 0.07f);

            // Straight into the climb away, whether or not it connected. A gull that lands
            // on you and stays there is a different animal.
            _from = transform.position;
            _to = Home;
            _phase = Phase.Returning;
            _timer = 0f;

            var playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject == null) return;

            if (Vector3.Distance(playerObject.transform.position, transform.position) > strikeRadius)
                return;

            var health = playerObject.GetComponent<ZombieHouse.Player.PlayerHealth>();
            if (health == null) return;

            health.TakeDamage(new DamageInfo(strikeDamage, transform.position, Vector3.down,
                                             Vector3.down, gameObject, false));
        }

        /// <summary>
        /// Back on the rail. Every way out of the air goes through here — arrival, the hard
        /// timeout, and being switched off — so there is exactly one definition of "safe",
        /// and it cannot fail: Home is where the spawner put it, which was a real position.
        /// </summary>
        private void Anchor()
        {
            transform.position = Home;
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

            _phase = Phase.Cooling;
            _timer = cooldownSeconds;
            _airborne = 0f;

            Flap(false);
        }

        /// <summary>
        /// Wings out in flight, folded on the rail. Cosmetic, and driven from here rather
        /// than by ZombieVisuals because that animates a walk cycle and this thing has never
        /// walked anywhere.
        /// </summary>
        private void Flap(bool flying)
        {
            if (_rig == null || _rig.Bones == null) return;

            float beat = flying ? Mathf.Sin(Time.time * 18f) * 34f : 0f;
            float spread = flying ? 0f : 62f;

            if (_rig.Bones.ShoulderLeft != null)
                _rig.Bones.ShoulderLeft.localRotation = Quaternion.Euler(0f, 0f, -beat - spread);

            if (_rig.Bones.ShoulderRight != null)
                _rig.Bones.ShoulderRight.localRotation = Quaternion.Euler(0f, 0f, beat + spread);
        }
    }
}
