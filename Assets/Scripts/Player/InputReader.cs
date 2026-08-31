using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace ZombieHouse.Player
{
    /// <summary>
    /// Single place the game asks "what is the player pressing".
    /// Compiles against either input backend so the project works whichever
    /// Active Input Handling the editor is set to.
    /// </summary>
    public static class InputReader
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        // Input System reports raw pixel deltas; legacy axes are pre-scaled.
        // This keeps mouse sensitivity numbers meaningful across both paths.
        private const float LookScale = 0.06f;

        private static Keyboard Kb => Keyboard.current;
        private static Mouse Ms => Mouse.current;

        public static Vector2 Move
        {
            get
            {
                if (Kb == null) return Vector2.zero;
                float x = (Kb.dKey.isPressed ? 1f : 0f) - (Kb.aKey.isPressed ? 1f : 0f);
                float y = (Kb.wKey.isPressed ? 1f : 0f) - (Kb.sKey.isPressed ? 1f : 0f);
                return new Vector2(x, y);
            }
        }

        public static Vector2 Look
        {
            get { return Ms == null ? Vector2.zero : Ms.delta.ReadValue() * LookScale; }
        }

        /// <summary>
        /// Wheel movement in notches: +1 per click forward, -1 back. The Input System
        /// reports 120 per notch on Windows, so it is divided down to match the legacy
        /// axis and keep the tuning values in PlayerController meaningful on both paths.
        /// </summary>
        public static float ScrollDelta
        {
            get { return Ms == null ? 0f : Ms.scroll.ReadValue().y / 120f; }
        }

        // Space is a second machete button, so jumping moved to Q.
        public static bool JumpPressed => Kb != null && Kb.qKey.wasPressedThisFrame;
        /// <summary>Hold to sprint: the up arrow, or left shift for the other hand.</summary>
        public static bool SprintHeld => Kb != null && (Kb.upArrowKey.isPressed || Kb.leftShiftKey.isPressed);

        // Right Ctrl swings the katana, so left Ctrl is free to crouch as usual.
        public static bool CrouchHeld => Kb != null && (Kb.leftCtrlKey.isPressed || Kb.cKey.isPressed);
        public static bool FireHeld => Ms != null && Ms.leftButton.isPressed;
        public static bool FirePressed => Ms != null && Ms.leftButton.wasPressedThisFrame;

        // Katana: RIGHT Ctrl — the one beside the arrow keys, away from the movement hand
        // — or the forward ("advance") thumb button, button 5 on an MX Vertical.
        // Aiming is right mouse.
        public static bool MeleePressed => (Kb != null && Kb.rightCtrlKey.wasPressedThisFrame)
                                           || (Ms != null && Ms.forwardButton.wasPressedThisFrame);

        public static bool MeleeHeld => (Kb != null && Kb.rightCtrlKey.isPressed)
                                        || (Ms != null && Ms.forwardButton.isPressed);

        public static bool AimHeld => Ms != null && Ms.rightButton.isPressed;


        /// <summary>
        /// The rifle's own key. This was Space until Space became the action key for
        /// helping people up and fitting the power cell — one key, one job.
        /// </summary>
        public static bool RifleFirePressed => Kb != null && Kb.eKey.wasPressedThisFrame;
        public static bool RifleFireHeld => Kb != null && Kb.eKey.isPressed;

        /// <summary>Middle mouse swaps between the pistol and the rifle.</summary>
        public static bool SwitchWeaponPressed => Ms != null && Ms.middleButton.wasPressedThisFrame;

        // Direct slot keys, so weapon selection never depends on a mouse button that
        // vendor software may have remapped out from under the game.
        public static bool SelectSlotOnePressed => Kb != null && Kb.digit1Key.wasPressedThisFrame;
        public static bool SelectSlotTwoPressed => Kb != null && Kb.digit2Key.wasPressedThisFrame;
        public static bool SelectSlotThreePressed => Kb != null && Kb.digit3Key.wasPressedThisFrame;
        public static bool ReloadPressed => Kb != null && Kb.rKey.wasPressedThisFrame;
        /// <summary>Space: cut someone loose, fit the power cell, start the motor.</summary>
        public static bool InteractPressed => Kb != null && Kb.spaceKey.wasPressedThisFrame;
        public static bool FlashlightPressed => Kb != null && Kb.fKey.wasPressedThisFrame;
        public static bool PausePressed => Kb != null && Kb.escapeKey.wasPressedThisFrame;
        public static bool RestartPressed => Kb != null && Kb.enterKey.wasPressedThisFrame;
#else
        public static Vector2 Move
        {
            get { return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")); }
        }

        public static Vector2 Look
        {
            get { return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")); }
        }

        /// <summary>Wheel movement in notches. The legacy axis reports ~0.1 per notch.</summary>
        public static float ScrollDelta
        {
            get { return Input.GetAxis("Mouse ScrollWheel") * 10f; }
        }

        // Space is a second machete button, so jumping moved to Q.
        public static bool JumpPressed => Input.GetKeyDown(KeyCode.Q);
        /// <summary>Hold to sprint: the up arrow, or left shift for the other hand.</summary>
        public static bool SprintHeld => Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.LeftShift);

        // Right Ctrl swings the katana, so left Ctrl is free to crouch as usual.
        public static bool CrouchHeld => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
        public static bool FireHeld => Input.GetMouseButton(0);
        public static bool FirePressed => Input.GetMouseButtonDown(0);

        // Katana: RIGHT Ctrl — the one beside the arrow keys, away from the movement hand
        // — or the forward ("advance") thumb button, index 4 in the legacy API.
        // Aiming is right mouse.
        public static bool MeleePressed => Input.GetKeyDown(KeyCode.RightControl) || Input.GetMouseButtonDown(4);
        public static bool MeleeHeld => Input.GetKey(KeyCode.RightControl) || Input.GetMouseButton(4);
        public static bool AimHeld => Input.GetMouseButton(1);


        /// <summary>
        /// The rifle's own key. This was Space until Space became the action key for
        /// helping people up and fitting the power cell — one key, one job.
        /// </summary>
        public static bool RifleFirePressed => Input.GetKeyDown(KeyCode.E);
        public static bool RifleFireHeld => Input.GetKey(KeyCode.E);

        /// <summary>Middle mouse swaps between the pistol and the rifle.</summary>
        public static bool SwitchWeaponPressed => Input.GetMouseButtonDown(2);

        // Direct slot keys, so weapon selection never depends on a mouse button that
        // vendor software may have remapped out from under the game.
        public static bool SelectSlotOnePressed => Input.GetKeyDown(KeyCode.Alpha1);
        public static bool SelectSlotTwoPressed => Input.GetKeyDown(KeyCode.Alpha2);
        public static bool SelectSlotThreePressed => Input.GetKeyDown(KeyCode.Alpha3);
        public static bool ReloadPressed => Input.GetKeyDown(KeyCode.R);
        /// <summary>Space: cut someone loose, fit the power cell, start the motor.</summary>
        public static bool InteractPressed => Input.GetKeyDown(KeyCode.Space);
        public static bool FlashlightPressed => Input.GetKeyDown(KeyCode.F);
        public static bool PausePressed => Input.GetKeyDown(KeyCode.Escape);
        public static bool RestartPressed => Input.GetKeyDown(KeyCode.Return);
#endif

        /// <summary>Move input clamped to length 1 so diagonals aren't faster.</summary>
        public static Vector2 MoveNormalized
        {
            get
            {
                Vector2 m = Move;
                return m.sqrMagnitude > 1f ? m.normalized : m;
            }
        }
    }
}
