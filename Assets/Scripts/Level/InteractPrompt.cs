using UnityEngine;

namespace ZombieHouse.Level
{
    /// <summary>
    /// One place for "pressing Space here would do something".
    ///
    /// Several unrelated things can be the thing under your nose — a hostage, the power
    /// cell on the floor, the motor beside the door — and the HUD should not have to know
    /// about any of them. Each one asks for the prompt while the player is in reach, and
    /// the HUD asks whether anyone asked this frame.
    ///
    /// Frame-stamped rather than a flag, so nothing has to remember to clear it when the
    /// player walks away, and nothing has to be reset between scenes.
    /// </summary>
    public static class InteractPrompt
    {
        private static int _frame = -10;

        /// <summary>True while something within reach could be acted on.</summary>
        public static bool Active => Time.frameCount - _frame <= 1;

        /// <summary>What that something is, in the imperative.</summary>
        public static string Label { get; private set; } = string.Empty;

        /// <summary>Called every frame by whatever is in reach.</summary>
        public static void Request(string label)
        {
            _frame = Time.frameCount;
            Label = label;
        }
    }
}
