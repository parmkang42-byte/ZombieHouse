using UnityEngine;

namespace ZombieHouse.Core
{
    /// <summary>
    /// The first scene a player build opens, whose only job is to load the first level behind
    /// the loading screen.
    ///
    /// Without it the loading bar could never appear on the load players meet most: starting the
    /// game. Unity opens build index 0 before any script has run, so that scene cannot put a
    /// bar in front of its own loading. This scene is small enough to open instantly, and then
    /// loads the real first level the way every other load goes.
    ///
    /// Pressing Play on a level in the editor skips this entirely, as it should.
    /// </summary>
    public class Boot : MonoBehaviour
    {
        [Tooltip("Scene name of the level the game starts on. Written by the editor build.")]
        [SerializeField] private string firstScene = "Level1_House";

        public string FirstScene => firstScene;

        private void Start()
        {
            SceneLoader.Load(firstScene);
        }
    }
}
