using UnityEngine;
using ZombieHouse.Audio;
using ZombieHouse.Core;

namespace ZombieHouse.Enemies
{
    /// <summary>
    /// Gives each zombie a voice. The groans come from its own AudioSource so they track
    /// the body as it moves; one-shot impacts go through the shared pool.
    ///
    /// Every zombie gets a slightly different pitch so a group sounds like a crowd
    /// rather than one sound played several times.
    /// </summary>
    [RequireComponent(typeof(ZombieAI))]
    public class ZombieAudio : MonoBehaviour
    {
        [Header("Groan timing (seconds between)")]
        [SerializeField] private Vector2 idleInterval = new Vector2(4.5f, 10f);
        [SerializeField] private Vector2 huntingInterval = new Vector2(2f, 4.5f);

        [Header("Mix")]
        [SerializeField] private float voiceVolume = 0.5f;
        [SerializeField] private float footstepVolume = 0.28f;
        [SerializeField] private float pitchSpread = 0.24f;

        [Header("Footsteps")]
        [Tooltip("Shortest gap between this one's own footsteps. The stride phase is a "
                 + "*visual* tuning knob and some creatures cycle their legs very fast; "
                 + "a scarab at 1.3 cycles per metre and 6.6 m/s crosses the sign of its "
                 + "stride seventeen times a second, and playing a footstep for each is "
                 + "how a pack of four turns into static.")]
        [SerializeField] private float minimumInterval = 0.26f;

        [Tooltip("Beyond this, no footstep at all. A footstep is a proximity cue — it "
                 + "tells you something is near you. At thirty metres it is not a cue, "
                 + "it is noise, and there may be forty of them out there.")]
        [SerializeField] private float audibleRange = 19f;

        [Tooltip("Footsteps the whole level may play per second, between all of them. "
                 + "Running through a level wakes a lot of things at once and they all "
                 + "converge on you; without a ceiling the mix is nothing but taps.")]
        [SerializeField] private int levelFootstepsPerSecond = 14;

        private ZombieAI _ai;
        private ZombieHealth _health;
        private ZombieVisuals _visuals;
        private AudioSource _voice;

        private float _nextGroanTime;
        private float _personalPitch = 1f;
        private bool _dead;
        private float _nextFootstepTime;

        // Shared across every zombie in the level. Static because the budget is a
        // property of the mix, not of any one creature — the point is precisely that no
        // single zombie can know how many others are stepping.
        private static float _budgetWindowEnds;
        private static int _budgetSpent;

        private ZombieProfile _profile;

        private void Awake()
        {
            _ai = GetComponent<ZombieAI>();
            _health = GetComponent<ZombieHealth>();
            _visuals = GetComponent<ZombieVisuals>();
            _profile = GetComponent<ZombieProfile>();

            _personalPitch = 1f + Random.Range(-pitchSpread, pitchSpread);

            _voice = GetComponent<AudioSource>();
            if (_voice == null) _voice = GameAudio.AttachSource(gameObject, voiceVolume);
            _voice.pitch = _personalPitch;

            ScheduleNextGroan();
        }

        private void OnEnable()
        {
            if (_ai != null)
            {
                _ai.StateChanged += OnStateChanged;
                _ai.AttackStarted += OnAttackStarted;
                _ai.AttackConnected += OnAttackConnected;
            }
            if (_health != null)
            {
                _health.Damaged += OnDamaged;
                _health.Died += OnDied;
            }
            if (_visuals != null) _visuals.Footstep += OnFootstep;
        }

        private void OnDisable()
        {
            if (_ai != null)
            {
                _ai.StateChanged -= OnStateChanged;
                _ai.AttackStarted -= OnAttackStarted;
                _ai.AttackConnected -= OnAttackConnected;
            }
            if (_health != null)
            {
                _health.Damaged -= OnDamaged;
                _health.Died -= OnDied;
            }
            if (_visuals != null) _visuals.Footstep -= OnFootstep;
        }

        private void Update()
        {
            if (_dead || !GameManager.GameplayActive) return;
            if (Time.time < _nextGroanTime) return;

            // Do not talk over a bigger cue.
            if (!_voice.isPlaying) PlayVoice(Sfx.ZombieIdle);
            ScheduleNextGroan();
        }

        private void ScheduleNextGroan()
        {
            bool hunting = _ai != null && (_ai.State == ZombieState.Chase || _ai.State == ZombieState.Attack);
            Vector2 range = hunting ? huntingInterval : idleInterval;
            _nextGroanTime = Time.time + Random.Range(range.x, range.y);
        }

        /// <summary>
        /// Swaps the standard zombie voice for this creature's own, where it has one.
        ///
        /// Routed on the archetype rather than on a flag per prefab, so a mascot spawned by
        /// any path sounds right and nothing has to remember to set anything. Creatures with
        /// no entry here fall through to the ordinary voice, which is every zombie in the
        /// first six levels.
        /// </summary>
        private Sfx VoiceFor(Sfx standard)
        {
            if (_profile == null || _profile.Archetype == null) return standard;

            switch (_profile.Archetype.Kind)
            {
                case ZombieKind.MascotMouse:
                case ZombieKind.MascotDog:
                case ZombieKind.BossMascot:
                    // Every vocalisation is the same buried groan. A suit cannot shout, and
                    // it certainly cannot make a different noise when it is angry.
                    return Sfx.MascotGroan;

                case ZombieKind.MascotBowMouse:
                    // The squeaker instead — a toy, not a voice, which is worse.
                    return Sfx.MascotSqueak;

                case ZombieKind.StorybookPrincess:
                    // Only when she is calling. Her hurt and death noises stay human, and
                    // the contrast is the point: she performs at you and then she does not.
                    return standard == Sfx.ZombieAlert || standard == Sfx.ZombieIdle
                        ? Sfx.PrincessCall
                        : standard;
            }

            return standard;
        }

        private void PlayVoice(Sfx sfx, float volumeScale = 1f)
        {
            // Substituted here rather than at each of the five call sites, so a creature
            // with its own voice cannot leak the standard one through a path somebody
            // forgot to update.
            AudioClip clip = GameAudio.Get(VoiceFor(sfx));
            if (clip == null || _voice == null) return;

            _voice.pitch = _personalPitch + Random.Range(-0.05f, 0.05f);
            _voice.PlayOneShot(clip, voiceVolume * volumeScale);
        }

        private void OnStateChanged(ZombieState state)
        {
            if (_dead) return;

            if (state == ZombieState.Chase && !_voice.isPlaying)
            {
                PlayVoice(Sfx.ZombieAlert, 1.1f);
                ScheduleNextGroan();
            }
        }

        private void OnAttackStarted()
        {
            if (_dead) return;
            PlayVoice(Sfx.ZombieAttack, 1.15f);
        }

        /// <summary>
        /// The bite only sounds when teeth actually reach you. Playing it on every swing
        /// would train you to ignore it; this way the sound means you have been hurt.
        /// </summary>
        private void OnAttackConnected()
        {
            if (_dead) return;

            // Loud and close: it is happening at your throat, not across the room.
            GameAudio.PlayAt(Sfx.ZombieBite, transform.position + Vector3.up * 1.5f, 1f, 0.09f);
        }

        private void OnDamaged(DamageInfo info)
        {
            if (_dead) return;

            // The bullet impact itself is played by the weapon; this is the reaction.
            PlayVoice(Sfx.ZombieHurt);
        }

        private void OnDied(DamageInfo info)
        {
            _dead = true;
            if (_voice != null) _voice.Stop();
            PlayVoice(Sfx.ZombieDeath, 1.1f);
        }

        /// <summary>
        /// A footstep, if this one is close enough, has not just taken one, and the level
        /// has not already spent its budget this second.
        ///
        /// The three gates do different jobs and all three are needed. The interval stops
        /// a fast-legged creature machine-gunning on its own; the range stops the far half
        /// of the level being audible at all; the budget stops thirty things that each
        /// pass the first two from adding up to a wall of noise anyway.
        /// </summary>
        private void OnFootstep()
        {
            if (_dead || footstepVolume <= 0f) return;
            if (Time.time < _nextFootstepTime) return;

            Vector3 position = transform.position;

            // Distance to the ear, not to the player object — they are the same thing
            // here, and AudioListener is what actually decides whether this is audible.
            AudioListener listener = GameAudio.Listener;
            if (listener != null &&
                (listener.transform.position - position).sqrMagnitude > audibleRange * audibleRange)
            {
                // Still consume the interval: a distant one that walks into range should
                // not arrive mid-stride with a backlog of steps owed.
                _nextFootstepTime = Time.time + minimumInterval;
                return;
            }

            if (!TakeFromBudget()) return;

            _nextFootstepTime = Time.time + minimumInterval;
            GameAudio.PlayAt(Sfx.Footstep, position, footstepVolume, 0.18f);
        }

        private bool TakeFromBudget()
        {
            if (Time.time >= _budgetWindowEnds)
            {
                _budgetWindowEnds = Time.time + 1f;
                _budgetSpent = 0;
            }

            if (_budgetSpent >= levelFootstepsPerSecond) return false;

            _budgetSpent++;
            return true;
        }

        /// <summary>
        /// Silences this one's footsteps for good. A snake has no feet, and a soft tap
        /// under it was the single most obvious wrong sound in the valley.
        /// </summary>
        public void SilenceFootsteps()
        {
            footstepVolume = 0f;
        }
    }
}
