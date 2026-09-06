using UnityEngine;
using ZombieHouse.Audio;
using ZombieHouse.Core;
using ZombieHouse.UI;

namespace ZombieHouse.Enemies
{
    /// <summary>
    /// The thing standing between you and the way out.
    ///
    /// One per level, and always a giant of that level's own creature — the house has a
    /// shambler two and a half metres tall, the wood has a bear, the tomb has a scarab the
    /// size of a car. That is deliberate rather than lazy: a monster from nowhere at the end
    /// of a level is a spectacle, but a giant version of the thing you have spent twenty
    /// minutes learning to fight is an *exam*. Everything the level taught you still applies
    /// and none of it is quite enough, which is the only interesting thing a boss can be.
    ///
    /// **It waits.** The boss is dormant at the exit until the level's other conditions are
    /// met — quota, survivors, motor. So it is not something you blunder into halfway
    /// through; it is what happens when you think you have finished. You will usually have
    /// walked past it in the dark on the way to something else, which is the point.
    ///
    /// **It is a fourth exit condition.** <see cref="GameManager"/> will not open the door
    /// while a boss is alive, so there is no running past it.
    /// </summary>
    [RequireComponent(typeof(ZombieAI))]
    [RequireComponent(typeof(ZombieHealth))]
    public class LevelBoss : MonoBehaviour
    {
        [Tooltip("How close you have to come before it stirs, once the kill quota is met. "
                 + "Generous — a boss that wakes across a whole level is a chase, and this "
                 + "is meant to be a fight in a place — but wide enough that walking the "
                 + "main route past it is enough.")]
        [SerializeField] private float wakeRange = 34f;

        /// <summary>The one in this level, or null. Read by GameManager to gate the exit.</summary>
        public static LevelBoss Current { get; private set; }

        /// <summary>True once it is down. The exit will not open until it is.</summary>
        public static bool Defeated { get; private set; }

        /// <summary>True once it has woken, so the HUD and the music can react.</summary>
        public static bool Engaged { get; private set; }

        [Tooltip("Health fraction at which it turns. Below this it stops flinching almost "
                 + "entirely and moves faster.")]
        [Range(0f, 1f)] [SerializeField] private float secondPhaseAt = 0.45f;

        [Tooltip("How much of its remaining stagger it loses in the second phase.")]
        [Range(0f, 1f)] [SerializeField] private float secondPhaseResistance = 0.6f;

        [Tooltip("Chase speed multiplier once it turns. Small on purpose — see the note "
                 + "on why this is not larger.")]
        [SerializeField] private float secondPhaseSpeed = 1.12f;

        private ZombieAI _ai;
        private ZombieHealth _health;
        private Transform _player;
        private bool _turned;

        private void OnEnable()
        {
            Current = this;
            Defeated = false;
            Engaged = false;

            _ai = GetComponent<ZombieAI>();
            _health = GetComponent<ZombieHealth>();

            if (_health != null)
            {
                _health.Died += OnDied;
                _health.Damaged += OnDamaged;
            }

            // The bar builds itself now and stays hidden until the fight starts, so it can
            // measure the boss's real rendered height while everything is still assembled.
            if (GetComponent<BossHealthBar>() == null)
                gameObject.AddComponent<BossHealthBar>().Initialise();

            // Asleep until the level is otherwise finished. HoldDormant is the same lever
            // the door ambushes use — it stops the ordinary proximity wake, so walking past
            // this thing early does nothing at all.
            if (_ai != null) _ai.HoldDormant();
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.Died -= OnDied;
                _health.Damaged -= OnDamaged;
            }
            if (Current == this) Current = null;
        }

        private void Update()
        {
            if (Engaged || !GameManager.GameplayActive) return;

            GameManager game = GameManager.Instance;
            if (game == null) return;

            // The kill quota alone, not the whole exit condition.
            //
            // This used to wait for quota AND survivors AND the motor, which meant the boss
            // stirred only once literally everything else was finished — in the park that is
            // twenty-one kills plus four rescues plus carrying the power cell the length of
            // the level, and a player who explored in a different order simply never met it.
            // A boss nobody reaches is not a climax, it is a rumour.
            //
            // Quota on its own still keeps the thing that made this good: it is asleep while
            // you walk past it early, so the first time you see it move you have already been
            // near it in the dark. It just no longer waits for the very last errand.
            //
            // Deliberately still not ExitUnlocked — the boss is part of that now, so waiting
            // on it would mean waiting on itself and it would never wake at all.
            if (game.ZombiesNeededForExit > 0) return;

            if (_player == null)
            {
                var playerObject = GameObject.FindGameObjectWithTag("Player");
                if (playerObject != null) _player = playerObject.transform;
                if (_player == null) return;
            }

            if (Vector3.Distance(transform.position, _player.position) > wakeRange) return;

            Engage();
        }

        /// <summary>Wakes it. Public so a test can start the fight without walking there.</summary>
        public void Engage()
        {
            if (Engaged) return;

            Engaged = true;
            if (_ai != null) _ai.Wake();

            // The bed changes the moment it moves, which is the only announcement this
            // fight gets. Crossfaded rather than cut — see GameAudio.SwitchMusic.
            GameAudio.SwitchMusic(Sfx.MusicBoss, Sfx.TensionBoss);
        }

        /// <summary>
        /// Watches for the turn: below <see cref="secondPhaseAt"/> it stops flinching and
        /// picks up speed.
        ///
        /// This is what "harder to kill" should mean beyond a bigger number. A boss with
        /// twice the health is twice as long; a boss that changes at the halfway mark is a
        /// fight with a shape, and — more usefully — it punishes the player for settling into
        /// whatever rhythm got them through the first half.
        ///
        /// **The speed multiplier is deliberately small.** 1.12x keeps every boss under the
        /// player's 6.8 m/s sprint, which is the one invariant this project has already
        /// broken once: an enraged boss that outruns you does not make the fight harder, it
        /// removes the fight and replaces it with a coin flip. Test Boss checks the enraged
        /// speed, not just the base one.
        /// </summary>
        private void OnDamaged(DamageInfo info)
        {
            if (_turned || _health == null || _health.Max <= 0f) return;
            if (_health.Current / _health.Max > secondPhaseAt) return;

            _turned = true;

            var profile = GetComponent<ZombieProfile>();
            if (profile == null || profile.Archetype == null) return;

            // CLONE first. Catalogue entries are shared static objects read by every zombie
            // of that kind, so enraging the entry rather than a copy would permanently
            // enrage every boss of that type for the rest of the session — and compound, so
            // the second one starts where the first finished. Nothing errors; the difficulty
            // just drifts upward and no test would notice.
            ZombieArchetype enraged = profile.Archetype.Clone();

            enraged.StaggerResistance =
                Mathf.Clamp01(enraged.StaggerResistance +
                              (1f - enraged.StaggerResistance) * secondPhaseResistance);

            enraged.ChaseSpeed *= secondPhaseSpeed;

            if (_ai != null) _ai.ApplyArchetype(enraged);

            // A second, louder announcement. Same bed — restarting it here would be a cut,
            // and the point is that the thing changed, not that the level did.
            GameAudio.PlayAt(Sfx.ZombieRise, transform.position, 1f, 0f);
            Noise.Emit(transform.position, 20f);
        }

        private void OnDied(DamageInfo info)
        {
            Defeated = true;

            // Tell the manager to look again — killing the boss is usually the last
            // condition, and without a nudge the exit would not open until something else
            // happened to trigger a re-evaluation.
            if (GameManager.Instance != null) GameManager.Instance.ReEvaluateExit();
        }
    }
}
