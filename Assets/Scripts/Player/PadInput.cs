using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace ZombieHouse.Player
{
    /// <summary>
    /// A wireless controller, read the same way on either input backend.
    ///
    /// Separate from <see cref="InputReader"/> rather than folded into it, because the two
    /// answer different questions. InputReader says what the player is asking for; this says
    /// what one particular device is doing, and a stick is not a mouse — it reports a
    /// position where a mouse reports a movement, so everything here exists to turn the
    /// first into the second. Nothing outside InputReader should read this directly.
    ///
    /// Sticks are the whole difficulty. Three things have to be right or a controller feels
    /// broken in a way players describe as "floaty" and cannot be more specific about:
    ///
    ///   - the deadzone must be RADIAL, not per-axis. Unity's own `dead` field on a joystick
    ///     axis is per-axis, which cuts a square hole out of a round stick: push diagonally
    ///     and both axes clear their deadzone at once, so the diagonal is the easiest
    ///     direction to move in and straight lines are the hardest. The axes below are
    ///     therefore declared with dead: 0 and the deadzone is applied here, on the vector.
    ///
    ///   - it must be CONTINUOUS at the edge. Zeroing everything under 0.18 and passing the
    ///     rest through unchanged means the stick jumps from nothing to 18% the instant it
    ///     crosses. The magnitude is rescaled from the deadzone edge instead, so it starts
    ///     at zero and reaches one at full deflection.
    ///
    ///   - look must be scaled by TIME, not by frame. A stick held half over is a request to
    ///     keep turning at a rate; a mouse delta is a distance already travelled. Returning
    ///     the raw stick would make aiming depend on frame rate.
    /// </summary>
    public static class PadInput
    {
        /// <summary>Below this the stick is treated as centred. Sticks do not rest at zero.</summary>
        private const float MoveDeadzone = 0.14f;
        private const float LookDeadzone = 0.16f;

        /// <summary>How far a trigger must be pulled to count as a press.</summary>
        private const float TriggerThreshold = 0.35f;

        /// <summary>Degrees per second at full deflection, before sensitivity.</summary>
        private const float YawRate = 235f;
        private const float PitchRate = 175f;

        /// <summary>
        /// MouseLook multiplies whatever Look returns by its sensitivity, which defaults to
        /// this. Dividing it back out here means the rates above are the real turn rates at
        /// the default setting — and that the player's sensitivity slider moves the stick
        /// and the mouse together, which is one setting rather than two.
        /// </summary>
        private const float NominalSensitivity = 2.2f;

        /// <summary>
        /// The axes this reads, and the names that must exist in the Input Manager.
        ///
        /// Public because `Input.GetAxisRaw` on a name that is not defined throws rather than
        /// returning zero — it would take the whole game down on the first frame, for
        /// keyboard players too. `Test Gamepad` cross-checks this list against
        /// ProjectSettings/InputManager.asset so the two cannot drift apart.
        /// </summary>
        public static readonly string[] AxisNames =
        {
            "PadMoveX", "PadMoveY", "PadLookX", "PadLookY",
            "PadTriggers", "PadDpadX", "PadDpadY"
        };

        // ---- edge detection -------------------------------------------------
        // Buttons get wasPressedThisFrame for free; axes do not. These sample once per
        // frame so that several reads in one frame agree with each other.

        private static int _polledFrame = -1;
        private static bool _fireWas, _fireIs;
        private static bool _aimWas, _aimIs;
        private static bool _slotLeftWas, _slotLeftIs;
        private static bool _slotUpWas, _slotUpIs;
        private static bool _slotRightWas, _slotRightIs;
        private static bool _torchWas, _torchIs;

        private static void Poll()
        {
            if (Time.frameCount == _polledFrame) return;
            _polledFrame = Time.frameCount;

            _fireWas = _fireIs;
            _aimWas = _aimIs;
            _slotLeftWas = _slotLeftIs;
            _slotUpWas = _slotUpIs;
            _slotRightWas = _slotRightIs;
            _torchWas = _torchIs;

            _fireIs = RightTrigger > TriggerThreshold;
            _aimIs = LeftTrigger > TriggerThreshold;

            Vector2 dpad = Dpad;
            _slotLeftIs = dpad.x < -0.5f;
            _slotRightIs = dpad.x > 0.5f;
            _slotUpIs = dpad.y > 0.5f;
            _torchIs = dpad.y < -0.5f;
        }

        // ---- the shaping, which is the part worth testing -------------------

        /// <summary>
        /// A stick vector with its deadzone removed, radially and without a step at the edge.
        ///
        /// Returns zero inside the deadzone, then rises continuously from zero to one as the
        /// stick travels from the deadzone edge to full deflection. Direction is preserved
        /// exactly: only the magnitude is rescaled.
        /// </summary>
        public static Vector2 ApplyDeadzone(Vector2 raw, float deadzone)
        {
            float magnitude = raw.magnitude;
            if (magnitude <= deadzone) return Vector2.zero;

            float scaled = Mathf.Min((magnitude - deadzone) / (1f - deadzone), 1f);
            return raw * (scaled / magnitude);
        }

        /// <summary>
        /// The look stick, shaped for aiming: deadzoned, then squared.
        ///
        /// Squaring the magnitude buys precision where it is needed. Most aiming happens with
        /// the stick barely off centre, and a linear stick spends nearly all of its travel
        /// on speeds too fast to place a shot with. This gives fine control near the middle
        /// and keeps the full rate at the edge.
        /// </summary>
        public static Vector2 ShapeLook(Vector2 raw)
        {
            Vector2 stick = ApplyDeadzone(raw, LookDeadzone);

            float magnitude = stick.magnitude;
            if (magnitude <= 0f) return Vector2.zero;

            return stick * (magnitude);   // magnitude is already <= 1, so this squares it
        }

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        // ---- Input System ---------------------------------------------------
        // Gamepad.current is whichever pad was used last, wired or wireless, and covers
        // Xbox, DualShock, DualSense and Switch Pro without any per-device work.

        private static Gamepad Pad => Gamepad.current;

        public static bool Connected => Pad != null;

        private static Vector2 RawMove => Pad == null ? Vector2.zero : Pad.leftStick.ReadValue();
        private static Vector2 RawLook => Pad == null ? Vector2.zero : Pad.rightStick.ReadValue();
        private static Vector2 Dpad => Pad == null ? Vector2.zero : Pad.dpad.ReadValue();

        private static float LeftTrigger => Pad == null ? 0f : Pad.leftTrigger.ReadValue();
        private static float RightTrigger => Pad == null ? 0f : Pad.rightTrigger.ReadValue();

        private static bool South => Pad != null && Pad.buttonSouth.wasPressedThisFrame;
        private static bool East => Pad != null && Pad.buttonEast.isPressed;
        private static bool West => Pad != null && Pad.buttonWest.wasPressedThisFrame;
        private static bool North => Pad != null && Pad.buttonNorth.wasPressedThisFrame;

        private static bool LeftShoulderDown => Pad != null && Pad.leftShoulder.wasPressedThisFrame;
        private static bool LeftShoulderHeld => Pad != null && Pad.leftShoulder.isPressed;
        private static bool RightShoulderDown => Pad != null && Pad.rightShoulder.wasPressedThisFrame;
        private static bool RightShoulderHeld => Pad != null && Pad.rightShoulder.isPressed;

        private static bool LeftStickHeld => Pad != null && Pad.leftStickButton.isPressed;
        private static bool RightStickDown => Pad != null && Pad.rightStickButton.wasPressedThisFrame;
        private static bool StartDown => Pad != null && Pad.startButton.wasPressedThisFrame;
        private static bool SelectDown => Pad != null && Pad.selectButton.wasPressedThisFrame;
#else
        // ---- legacy Input Manager -------------------------------------------
        // Axis indices are XInput's layout on Windows, which is what a wireless Xbox pad
        // presents over Bluetooth. The third axis carries BOTH triggers on this backend —
        // left positive, right negative — which is why they are read off one axis and why
        // pulling both at once cancels them out. That is a limitation of this backend, not
        // a bug here; the Input System path above reads them separately.
        //
        // Button numbers are XInput's too: 0 A, 1 B, 2 X, 3 Y, 4 LB, 5 RB, 6 View, 7 Menu,
        // 8 left stick, 9 right stick. KeyCode.JoystickButtonN needs no Input Manager entry,
        // which is why only the axes are declared there.

        public static bool Connected
        {
            get
            {
                string[] names = Input.GetJoystickNames();
                foreach (string name in names)
                    if (!string.IsNullOrEmpty(name)) return true;

                return false;
            }
        }

        private static Vector2 RawMove =>
            new Vector2(Input.GetAxisRaw("PadMoveX"), Input.GetAxisRaw("PadMoveY"));

        private static Vector2 RawLook =>
            new Vector2(Input.GetAxisRaw("PadLookX"), Input.GetAxisRaw("PadLookY"));

        private static Vector2 Dpad =>
            new Vector2(Input.GetAxisRaw("PadDpadX"), Input.GetAxisRaw("PadDpadY"));

        private static float LeftTrigger => Mathf.Max(0f, Input.GetAxisRaw("PadTriggers"));
        private static float RightTrigger => Mathf.Max(0f, -Input.GetAxisRaw("PadTriggers"));

        private static bool South => Input.GetKeyDown(KeyCode.JoystickButton0);
        private static bool East => Input.GetKey(KeyCode.JoystickButton1);
        private static bool West => Input.GetKeyDown(KeyCode.JoystickButton2);
        private static bool North => Input.GetKeyDown(KeyCode.JoystickButton3);

        private static bool LeftShoulderDown => Input.GetKeyDown(KeyCode.JoystickButton4);
        private static bool LeftShoulderHeld => Input.GetKey(KeyCode.JoystickButton4);
        private static bool RightShoulderDown => Input.GetKeyDown(KeyCode.JoystickButton5);
        private static bool RightShoulderHeld => Input.GetKey(KeyCode.JoystickButton5);

        private static bool LeftStickHeld => Input.GetKey(KeyCode.JoystickButton8);
        private static bool RightStickDown => Input.GetKeyDown(KeyCode.JoystickButton9);
        private static bool StartDown => Input.GetKeyDown(KeyCode.JoystickButton7);
        private static bool SelectDown => Input.GetKeyDown(KeyCode.JoystickButton6);
#endif

        // ---- what InputReader asks for --------------------------------------
        //
        // The layout is the one every console shooter has trained people to expect, so
        // nobody has to learn it: triggers shoot and aim, bumpers are the other two weapons,
        // face buttons are jump / use / reload / crouch, and the sticks click for sprint and
        // weapon swap.

        public static Vector2 Move => ApplyDeadzone(RawMove, MoveDeadzone);

        /// <summary>
        /// The look stick as a per-frame delta, so it can be added to a mouse delta.
        ///
        /// Unscaled time on purpose: if the game ever slows down, the view should not.
        /// </summary>
        public static Vector2 Look
        {
            get
            {
                Vector2 shaped = ShapeLook(RawLook);
                if (shaped == Vector2.zero) return Vector2.zero;

                return new Vector2(shaped.x * YawRate, shaped.y * PitchRate)
                       * (Time.unscaledDeltaTime / NominalSensitivity);
            }
        }

        public static bool FireHeld { get { Poll(); return _fireIs; } }
        public static bool FirePressed { get { Poll(); return _fireIs && !_fireWas; } }
        public static bool AimHeld { get { Poll(); return _aimIs; } }

        /// <summary>Right bumper: the katana. Left bumper: the rifle.</summary>
        public static bool MeleePressed => RightShoulderDown;
        public static bool MeleeHeld => RightShoulderHeld;
        public static bool RifleFirePressed => LeftShoulderDown;
        public static bool RifleFireHeld => LeftShoulderHeld;

        public static bool JumpPressed => South;
        public static bool InteractPressed => West;
        public static bool ReloadPressed => North;
        public static bool CrouchHeld => East;

        /// <summary>Click the left stick to sprint — held, like the up arrow.</summary>
        public static bool SprintHeld => LeftStickHeld;

        /// <summary>Click the right stick to swap weapons, where the middle mouse button is.</summary>
        public static bool SwitchWeaponPressed => RightStickDown;

        // The d-pad picks a weapon directly and turns the torch on, which keeps those off
        // the face buttons where they would fight with jumping and using things.
        public static bool SelectSlotOnePressed { get { Poll(); return _slotLeftIs && !_slotLeftWas; } }
        public static bool SelectSlotTwoPressed { get { Poll(); return _slotUpIs && !_slotUpWas; } }
        public static bool SelectSlotThreePressed { get { Poll(); return _slotRightIs && !_slotRightWas; } }
        public static bool FlashlightPressed { get { Poll(); return _torchIs && !_torchWas; } }

        public static bool PausePressed => StartDown;
        public static bool RestartPressed => SelectDown;
    }
}
