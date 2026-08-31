using UnityEngine;
using ZombieHouse.Core;

namespace ZombieHouse.Audio
{
    /// <summary>
    /// The three moments that deserve music: the house going quiet, getting out,
    /// and not getting out.
    /// </summary>
    public class StingerAudio : MonoBehaviour
    {
        [SerializeField] private float stingerVolume = 0.75f;

        private GameManager _game;

        private void Start()
        {
            _game = GameManager.Instance;
            if (_game == null) return;

            _game.StateChanged += OnStateChanged;
            _game.ExitUnlockedChanged += OnHouseCleared;
        }

        private void OnDestroy()
        {
            if (_game == null) return;

            _game.StateChanged -= OnStateChanged;
            _game.ExitUnlockedChanged -= OnHouseCleared;
        }

        private void OnHouseCleared()
        {
            GameAudio.Play2D(Sfx.ExitOpen, stingerVolume);
        }

        private void OnStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.Won:
                    GameAudio.Play2D(Sfx.Victory, stingerVolume);
                    break;
                case GameState.Lost:
                    GameAudio.Play2D(Sfx.Defeat, stingerVolume);
                    break;
            }
        }
    }
}
