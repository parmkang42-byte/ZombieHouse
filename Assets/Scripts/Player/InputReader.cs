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
    ///
    /// Keyboard, mouse and controller are all live at once and are merged here rather than
    /// switched between: holds and presses are OR'd, and the two analogue sources add. There
    /// is no controller mode to enter, so picking a pad up mid-game works and so does putting
    /// it down. See <see cref="PadInput"/> for the pad itself and for why a stick needs
    /// shaping that a mouse does not.
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
                Vector2 keys = Vector2.zero;
                if (Kb != null)
                {
                    keys.x = (Kb.dKey.isPressed ? 1f : 0f) - (Kb.aKey.isPressed ? 1f : 0f);
                    keys.y = (Kb.wKey.isPressed ? 1f : 0f) - (Kb.sKey.isPressed ? 1f : 0f);
                }

                return Vector2.ClampMagnitude(keys + PadInput.Move, 1f);
            }
        }

        public static Vector2 Look
        {
            get
            {
                Vector2 mouse = Ms == null ? Vector2.zero : Ms.delta.ReadValue() * LookScale;
                return mouse + PadInput.Look;
            }
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
        public static bool JumpPressed => (Kb != null && Kb.qKey.wasPressedThisFrame)
                                          || PadInput.JumpPressed;
        /// <summary>Hold to sprint: the up arrow, or left shift for the other hand.</summary>
        // Shift used to sprint here as well, and cannot any more: it reloads now, and a
        // reload discards the magazine. SprintHeld reads `isPressed` while ReloadPressed
        // reads `wasPressedThisFrame`, so sharing the key would throw away most of a
        // magazine on the first frame of every sprint.
        public static bool SprintHeld => (Kb != null && Kb.upArrowKey.isPressed)
                                         || PadInput.SprintHeld;

        // Right Ctrl swings the katana, so left Ctrl is free to crouch as usual.
        public static bool CrouchHeld => (Kb != null && (Kb.leftCtrlKey.isPressed || Kb.cKey.isPressed))
                                         || PadInput.CrouchHeld;
        public static bool FireHeld => (Ms != null && Ms.leftButton.isPressed) || PadInput.FireHeld;
        public static bool FirePressed => (Ms != null && Ms.leftButton.wasPressedThisFrame)
                                          || PadInput.FirePressed;

        // Katana: RIGHT Ctrl — the one beside the arrow keys, away from the movement hand
        // — or the forward ("advance") thumb button, button 5 on an MX Vertical.
        // Aiming is right mouse.
        public static bool MeleePressed => (Kb != null && Kb.rightCtrlKey.wasPressedThisFrame)
                                           || (Ms != null && Ms.forwardButton.wasPressedThisFrame)
                                           || PadInput.MeleePressed;

        public static bool MeleeHeld => (Kb != null && Kb.rightCtrlKey.isPressed)
                                        || (Ms != null && Ms.forwardButton.isPressed)
                                        || PadInput.MeleeHeld;

        public static bool AimHeld => (Ms != null && Ms.rightButton.isPressed) || PadInput.AimHeld;


        /// <summary>
        /// The rifle's own key. This was Space until Space became the action key for
        /// helping people up and fitting the power cell — one key, one job.
        /// </summary>
        public static bool RifleFirePressed => (Kb != null && Kb.eKey.wasPressedThisFrame)
                                               || PadInput.RifleFirePressed;
        public static bool RifleFireHeld => (Kb != null && Kb.eKey.isPressed) || PadInput.RifleFireHeld;

        /// <summary>Middle mouse swaps between the pistol and the rifle.</summary>
        public static bool SwitchWeaponPressed => (Ms != null && Ms.middleButton.wasPressedThisFrame)
                                                  || PadInput.SwitchWeaponPressed;

        // Direct slot keys, so weapon selection never depends on a mouse button that
        // vendor software may have remapped out from under the game.
        public static bool SelectSlotOnePressed => (Kb != null && Kb.digit1Key.wasPressedThisFrame)
                                                   || PadInput.SelectSlotOnePressed;
        public static bool SelectSlotTwoPressed => (Kb != null && Kb.digit2Key.wasPressedThisFrame)
                                                   || PadInput.SelectSlotTwoPressed;
        public static bool SelectSlotThreePressed => (Kb != null && Kb.digit3Key.wasPressedThisFrame)
                                                     || PadInput.SelectSlotThreePressed;

        /// <summary>The scavenged weapon: 4 on the keyboard, a back paddle on a pad.</summary>
        public static bool SelectPowerUpPressed => (Kb != null && Kb.digit4Key.wasPressedThisFrame)
                                                   || PadInput.SelectPowerUpPressed;
        public static bool ReloadPressed => (Kb != null && (Kb.rKey.wasPressedThisFrame
                                                        || Kb.leftShiftKey.wasPressedThisFrame
                                                        || Kb.rightShiftKey.wasPressedThisFrame))
                                            || PadInput.ReloadPressed;
        /// <summary>Space: cut someone loose, fit the power cell, start the motor.</summary>
        public static bool InteractPressed => (Kb != null && Kb.spaceKey.wasPressedThisFrame)
                                              || PadInput.InteractPressed;
        public static bool FlashlightPressed => (Kb != null && Kb.fKey.wasPressedThisFrame)
                                                || PadInput.FlashlightPressed;
        public static bool PausePressed => (Kb != null && Kb.escapeKey.wasPressedThisFrame)
                                           || PadInput.PausePressed;
        public static bool RestartPressed => (Kb != null && Kb.enterKey.wasPressedThisFrame)
                                             || PadInput.RestartPressed;
#else
        public static Vector2 Move
        {
            get
            {
                var keys = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
                return Vector2.ClampMagnitude(keys + PadInput.Move, 1f);
            }
        }

        public static Vector2 Look
        {
            get
            {
                var mouse = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
                return mouse + PadInput.Look;
            }
        }

        /// <summary>Wheel movement in notches. The legacy axis reports ~0.1 per notch.</summary>
        public static float ScrollDelta
        {
            get { return Input.GetAxis("Mouse ScrollWheel") * 10f; }
        }

        // Space is a second machete button, so jumping moved to Q.
        public static bool JumpPressed => Input.GetKeyDown(KeyCode.Q) || PadInput.JumpPressed;
        /// <summary>Hold to sprint: the up arrow, or left shift for the other hand.</summary>
        public static bool SprintHeld => Input.GetKey(KeyCode.UpArrow) || PadInput.SprintHeld;

        // Right Ctrl swings the katana, so left Ctrl is free to crouch as usual.
        public static bool CrouchHeld => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C)
                                         || PadInput.CrouchHeld;
        public static bool FireHeld => Input.GetMouseButton(0) || PadInput.FireHeld;
        public static bool FirePressed => Input.GetMouseButtonDown(0) || PadInput.FirePressed;

        // Katana: RIGHT Ctrl — the one beside the arrow keys, away from the movement hand
        // — or the forward ("advance") thumb button, index 4 in the legacy API.
        // Aiming is right mouse.
        public static bool MeleePressed => Input.GetKeyDown(KeyCode.RightControl)
                                           || Input.GetMouseButtonDown(4) || PadInput.MeleePressed;
        public static bool MeleeHeld => Input.GetKey(KeyCode.RightControl)
                                        || Input.GetMouseButton(4) || PadInput.MeleeHeld;
        public static bool AimHeld => Input.GetMouseButton(1) || PadInput.AimHeld;


        /// <summary>
        /// The rifle's own key. This was Space until Space became the action key for
        /// helping people up and fitting the power cell — one key, one job.
        /// </summary>
        public static bool RifleFirePressed => Input.GetKeyDown(KeyCode.E) || PadInput.RifleFirePressed;
        public static bool RifleFireHeld => Input.GetKey(KeyCode.E) || PadInput.RifleFireHeld;

        /// <summary>Middle mouse swaps between the pistol and the rifle.</summary>
        public static bool SwitchWeaponPressed => Input.GetMouseButtonDown(2)
                                                  || PadInput.SwitchWeaponPressed;

        // Direct slot keys, so weapon selection never depends on a mouse button that
        // vendor software may have remapped out from under the game.
        public static bool SelectSlotOnePressed => Input.GetKeyDown(KeyCode.Alpha1)
                                                   || PadInput.SelectSlotOnePressed;
        public static bool SelectSlotTwoPressed => Input.GetKeyDown(KeyCode.Alpha2)
                                                   || PadInput.SelectSlotTwoPressed;
        public static bool SelectSlotThreePressed => Input.GetKeyDown(KeyCode.Alpha3)
                                                     || PadInput.SelectSlotThreePressed;

        /// <summary>The scavenged weapon: 4 on the keyboard, a back paddle on a pad.</summary>
        public static bool SelectPowerUpPressed => Input.GetKeyDown(KeyCode.Alpha4)
                                                   || PadInput.SelectPowerUpPressed;
        public static bool ReloadPressed => Input.GetKeyDown(KeyCode.R)
                                         || Input.GetKeyDown(KeyCode.LeftShift)
                                         || Input.GetKeyDown(KeyCode.RightShift)
                                         || PadInput.ReloadPressed;
        /// <summary>Space: cut someone loose, fit the power cell, start the motor.</summary>
        public static bool InteractPressed => Input.GetKeyDown(KeyCode.Space)
                                              || PadInput.InteractPressed;
        public static bool FlashlightPressed => Input.GetKeyDown(KeyCode.F)
                                                || PadInput.FlashlightPressed;
        public static bool PausePressed => Input.GetKeyDown(KeyCode.Escape) || PadInput.PausePressed;
        public static bool RestartPressed => Input.GetKeyDown(KeyCode.Return)
                                             || PadInput.RestartPressed;
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
