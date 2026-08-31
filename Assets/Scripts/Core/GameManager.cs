using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZombieHouse.Core
{
    public enum GameState { Playing, Paused, Won, Lost }

    /// <summary>
    /// Owns the run: who is alive, whether the house is cleared, pause/cursor state,
    /// and the win/lose transitions. Everything else talks to it, not to each other.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Flow")]
        [SerializeField] private bool lockCursorWhilePlaying = true;

        [Tooltip("Share of the level that must be put down before the way out will open. "
             + "Two thirds: enough that you have to fight through the place, not so much "
             + "that the last stragglers have to be hunted down one at a time.")]
        [Range(0f, 1f)] [SerializeField] private float requiredKillFraction = 2f / 3f;

        public GameState State { get; private set; } = GameState.Playing;
        public int ZombiesAlive => _alive.Count;
        public int ZombiesKilled { get; private set; }
        public int ZombiesRemainingToSpawn { get; private set; }

        /// <summary>True once every wave has spawned and every zombie is down.</summary>
        public bool HouseCleared { get; private set; }

        /// <summary>Everything that was ever in the house, alive, dead or still to arrive.</summary>
        public int ZombiesTotal => ZombiesKilled + ZombiesAlive + ZombiesRemainingToSpawn;

        public float KillFraction => ZombiesTotal <= 0 ? 0f : (float)ZombiesKilled / ZombiesTotal;

        /// <summary>The door stays shut until enough of the house is dead.</summary>
        public bool ExitUnlocked { get; private set; }

        public int SurvivorsTotal { get; private set; }
        public int SurvivorsRescued { get; private set; }
        public int SurvivorsRemaining => Mathf.Max(0, SurvivorsTotal - SurvivorsRescued);

        /// <summary>Nobody left behind. True from the start on a level with none.</summary>
        public bool AllSurvivorsRescued => SurvivorsRemaining == 0;

        /// <summary>True once a motor exists in the level and has been given its cell.</summary>
        public bool MotorPowered { get; private set; }

        /// <summary>False on a level with no motor, in which case it holds nothing up.</summary>
        public bool RequiresMotor { get; private set; }

        /// <summary>True while the player is carrying the cell to it.</summary>
        public bool CarryingPowerCell => Level.PowerCell.Carried != null;

        /// <summary>How many more have to go down before the door will open.</summary>
        public int ZombiesNeededForExit =>
            Mathf.Max(0, Mathf.CeilToInt(ZombiesTotal * requiredKillFraction) - ZombiesKilled);

        public event Action<GameState> StateChanged;
        public event Action HouseClearedChanged;
        public event Action ExitUnlockedChanged;
        public event Action SurvivorsChanged;
        public event Action MotorPoweredChanged;

        private readonly HashSet<GameObject> _alive = new HashSet<GameObject>();
        private bool _spawningFinished;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Static events survive scene reloads; clear them before anything subscribes.
            Noise.Reset();
            Enemies.ZombieComms.Reset();
        }

        private void Start()
        {
            SetState(GameState.Playing);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (Player.InputReader.PausePressed)
            {
                if (State == GameState.Playing) SetState(GameState.Paused);
                else if (State == GameState.Paused) SetState(GameState.Playing);
            }

            // Restart is available from any end state.
            if ((State == GameState.Won || State == GameState.Lost) && Player.InputReader.RestartPressed)
            {
                Restart();
            }
        }

        // ---- registry -------------------------------------------------------

        public void RegisterZombie(GameObject zombie)
        {
            _alive.Add(zombie);
        }

        public void ReportZombieKilled(GameObject zombie)
        {
            if (_alive.Remove(zombie)) ZombiesKilled++;
            EvaluateClear();
        }

        /// <summary>
        /// Called from Survivor.Start, so the total is however many the level actually
        /// put out rather than a number configured somewhere that could drift from it.
        /// </summary>
        public void RegisterSurvivor(Level.Survivor survivor)
        {
            SurvivorsTotal++;
            SurvivorsChanged?.Invoke();
        }

        public void ReportSurvivorRescued(Level.Survivor survivor)
        {
            SurvivorsRescued++;
            SurvivorsChanged?.Invoke();
            EvaluateClear();
        }

        /// <summary>Called from DoorMotor.Start, so a level without one simply has none.</summary>
        public void RegisterMotor(Level.DoorMotor motor)
        {
            RequiresMotor = true;
        }

        public void ReportMotorPowered(Level.DoorMotor motor)
        {
            if (MotorPowered) return;

            MotorPowered = true;
            MotorPoweredChanged?.Invoke();
            EvaluateClear();
        }

        public void ReportSpawnQueue(int remaining)
        {
            ZombiesRemainingToSpawn = remaining;
        }

        /// <summary>Called by the spawner when the last wave has been released.</summary>
        public void ReportSpawningFinished()
        {
            _spawningFinished = true;
            EvaluateClear();
        }

        private void EvaluateClear()
        {
            // Two conditions, and they ask for different things. The kill quota is a
            // share of the house rather than all of it — the last few can be hiding
            // anywhere across two storeys and hunting them is tedious rather than tense.
            // Two thirds rather than nine tenths: at 90% the tail was most of the level,
            // and the last stretch of every run was walking the map looking for one
            // shambler in a wardrobe.
            // The survivors are the opposite: every one, no forgiveness, because the
            // whole point of them is that you have to search the place.
            // Three conditions now, and they ask for three different things: clear the
            // level, search it, and then carry something heavy back across it.
            bool motorReady = !RequiresMotor || MotorPowered;

            if (!ExitUnlocked && _spawningFinished && ZombiesNeededForExit <= 0
                && AllSurvivorsRescued && motorReady)
            {
                ExitUnlocked = true;
                ExitUnlockedChanged?.Invoke();
            }

            if (HouseCleared || !_spawningFinished || _alive.Count > 0) return;
            HouseCleared = true;
            HouseClearedChanged?.Invoke();
        }

        // ---- outcomes -------------------------------------------------------

        public void PlayerDied()
        {
            if (State == GameState.Won || State == GameState.Lost) return;
            SetState(GameState.Lost);
        }

        public void PlayerEscaped()
        {
            if (State == GameState.Won || State == GameState.Lost) return;
            SetState(GameState.Won);
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void SetState(GameState next)
        {
            State = next;
            bool playing = next == GameState.Playing;

            Time.timeScale = playing ? 1f : 0f;

            bool grabCursor = playing && lockCursorWhilePlaying;
            Cursor.lockState = grabCursor ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !grabCursor;

            StateChanged?.Invoke(next);
        }

        /// <summary>Gameplay scripts check this before acting so pause/death freezes them cleanly.</summary>
        public static bool GameplayActive
        {
            get { return Instance == null || Instance.State == GameState.Playing; }
        }
    }
}
