using UnityEngine;
using ZombieHouse.Core;

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
        [Tooltip("How close you have to come before it stirs, if the level's other "
                 + "conditions are already met. Generous — a boss that wakes across a level "
                 + "is a chase, and this is meant to be a fight in a place.")]
        [SerializeField] private float wakeRange = 22f;

        /// <summary>The one in this level, or null. Read by GameManager to gate the exit.</summary>
        public static LevelBoss Current { get; private set; }

        /// <summary>True once it is down. The exit will not open until it is.</summary>
        public static bool Defeated { get; private set; }

        /// <summary>True once it has woken, so the HUD and the music can react.</summary>
        public static bool Engaged { get; private set; }

        private ZombieAI _ai;
        private ZombieHealth _health;
        private Transform _player;

        private void OnEnable()
        {
            Current = this;
            Defeated = false;
            Engaged = false;

            _ai = GetComponent<ZombieAI>();
            _health = GetComponent<ZombieHealth>();

            if (_health != null) _health.Died += OnDied;

            // Asleep until the level is otherwise finished. HoldDormant is the same lever
            // the door ambushes use — it stops the ordinary proximity wake, so walking past
            // this thing early does nothing at all.
            if (_ai != null) _ai.HoldDormant();
        }

        private void OnDisable()
        {
            if (_health != null) _health.Died -= OnDied;
            if (Current == this) Current = null;
        }

        private void Update()
        {
            if (Engaged || !GameManager.GameplayActive) return;

            GameManager game = GameManager.Instance;
            if (game == null) return;

            // Everything except the boss itself. Deliberately not ExitUnlocked, which the
            // boss is now part of — that would never become true and it would never wake.
            bool levelOtherwiseDone = game.ZombiesNeededForExit <= 0
                                   && game.AllSurvivorsRescued
                                   && (!game.RequiresMotor || game.MotorPowered);

            if (!levelOtherwiseDone) return;

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
