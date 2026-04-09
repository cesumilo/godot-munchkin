using System;
using Godot;

/// <summary>
/// Handles gamepad/controller input (Xbox, PS5, Steam Deck).
/// </summary>
/// <remarks>
/// Part of Phase 1: Core Input Abstraction.
/// Uses Godot's built-in joypad detection for cross-platform support.
/// </remarks>
public partial class GamepadHandler : Node, IInputHandler
{
    /// <summary>
    /// Event fired when an input action occurs.
    /// </summary>
    public event Action<GameInputEvent> OnInput;

    /// <summary>
    /// Gets the device name for debugging.
    /// </summary>
    public string DeviceName => "Gamepad";

    /// <summary>
    /// Gets whether this handler is enabled.
    /// </summary>
    public bool IsEnabled { get; private set; } = true;

    // Joypad device ID (usually 0 for first controller)
    private int _deviceId = 0;

    // Track previous states
    private bool _wasSelectPressed = false;
    private bool _wasCancelPressed = false;
    private bool _wasActionMenuPressed = false;
    private bool _wasEndTurnPressed = false;

    // Navigation
    private double _navigateRepeatTimer = 0.0;
    private const double NavigateRepeatDelay = 0.3;
    private const double NavigateRepeatRate = 0.15;
    private Vector2 _lastNavigateDirection = Vector2.Zero;

    // Analog stick deadzone
    private const float Deadzone = 0.3f;

    /// <summary>
    /// Sets whether this handler is enabled.
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
    }

    /// <summary>
    /// Processes gamepad input.
    /// </summary>
    public void ProcessInput(double delta)
    {
        if (!IsEnabled)
            return;

        // Check if a controller is connected
        if (!IsControllerConnected())
            return;

        ProcessButtonInput();
        ProcessAnalogNavigation(delta);
    }

    /// <summary>
    /// Checks if a controller is connected.
    /// </summary>
    private bool IsControllerConnected()
    {
        // Godot 4: Input.GetConnectedJoypads()
        var joypads = Input.GetConnectedJoypads();
        return joypads.Count > 0;
    }

    /// <summary>
    /// Processes button inputs.
    /// </summary>
    private void ProcessButtonInput()
    {
        // A/Cross - Select (Joy Button 0 on Xbox, Cross on PS)
        bool selectPressed = Input.IsJoyButtonPressed(_deviceId, JoyButton.A);
        if (selectPressed && !_wasSelectPressed)
        {
            FireEvent(GameInputEvent.Press(InputActionType.Select, DeviceName));
        }
        _wasSelectPressed = selectPressed;

        // B/Circle - Cancel (Joy Button 1)
        bool cancelPressed = Input.IsJoyButtonPressed(_deviceId, JoyButton.B);
        if (cancelPressed && !_wasCancelPressed)
        {
            FireEvent(GameInputEvent.Press(InputActionType.Cancel, DeviceName));
        }
        _wasCancelPressed = cancelPressed;

        // X/Square - Action Menu (Joy Button 2)
        bool actionMenuPressed = Input.IsJoyButtonPressed(_deviceId, JoyButton.X);
        if (actionMenuPressed && !_wasActionMenuPressed)
        {
            FireEvent(GameInputEvent.Press(InputActionType.ActionMenu, DeviceName));
        }
        _wasActionMenuPressed = actionMenuPressed;

        // Y/Triangle - End Turn (Joy Button 3)
        bool endTurnPressed = Input.IsJoyButtonPressed(_deviceId, JoyButton.Y);
        if (endTurnPressed && !_wasEndTurnPressed)
        {
            FireEvent(GameInputEvent.Press(InputActionType.EndTurn, DeviceName));
        }
        _wasEndTurnPressed = endTurnPressed;

        // Start/Menu - Pause (Joy Button 6)
        if (Input.IsJoyButtonPressed(_deviceId, JoyButton.Start))
        {
            FireEvent(GameInputEvent.Press(InputActionType.Pause, DeviceName));
        }
    }

    /// <summary>
    /// Processes analog stick navigation with deadzone and repeat.
    /// </summary>
    private void ProcessAnalogNavigation(double delta)
    {
        // Get left stick input
        float x = Input.GetJoyAxis(_deviceId, JoyAxis.LeftX);
        float y = Input.GetJoyAxis(_deviceId, JoyAxis.LeftY);

        Vector2 input = new Vector2(x, y);

        // Apply deadzone
        if (input.Length() < Deadzone)
        {
            input = Vector2.Zero;
        }

        // Check for navigation
        bool hasInput = input != Vector2.Zero;

        if (hasInput)
        {
            _navigateRepeatTimer += delta;

            // Determine primary direction
            InputActionType? direction = null;

            if (Mathf.Abs(input.Y) > Mathf.Abs(input.X))
            {
                // Vertical dominant
                if (input.Y < -0.5f)
                    direction = InputActionType.NavigateUp;
                else if (input.Y > 0.5f)
                    direction = InputActionType.NavigateDown;
            }
            else
            {
                // Horizontal dominant
                if (input.X < -0.5f)
                    direction = InputActionType.NavigateLeft;
                else if (input.X > 0.5f)
                    direction = InputActionType.NavigateRight;
            }

            // Fire event
            if (direction.HasValue)
            {
                bool changedDirection =
                    GetDirectionVector(direction.Value) != _lastNavigateDirection;

                if (changedDirection || _navigateRepeatTimer >= NavigateRepeatRate)
                {
                    FireEvent(GameInputEvent.Analog(direction.Value, input.Length(), DeviceName));
                    _navigateRepeatTimer = 0;
                    _lastNavigateDirection = GetDirectionVector(direction.Value);
                }
            }
        }
        else
        {
            _navigateRepeatTimer = 0;
            _lastNavigateDirection = Vector2.Zero;
        }

        // Also check D-pad for digital navigation
        ProcessDpadNavigation();
    }

    /// <summary>
    /// Processes D-pad input as digital navigation.
    /// </summary>
    private void ProcessDpadNavigation()
    {
        // D-pad is typically joy buttons 11-14 or axis 6-7
        // Godot maps D-pad differently per platform, check both

        // Try button-based D-pad first
        if (Input.IsJoyButtonPressed(_deviceId, JoyButton.DpadUp))
        {
            FireEvent(GameInputEvent.Press(InputActionType.NavigateUp, DeviceName));
        }
        if (Input.IsJoyButtonPressed(_deviceId, JoyButton.DpadDown))
        {
            FireEvent(GameInputEvent.Press(InputActionType.NavigateDown, DeviceName));
        }
        if (Input.IsJoyButtonPressed(_deviceId, JoyButton.DpadLeft))
        {
            FireEvent(GameInputEvent.Press(InputActionType.NavigateLeft, DeviceName));
        }
        if (Input.IsJoyButtonPressed(_deviceId, JoyButton.DpadRight))
        {
            FireEvent(GameInputEvent.Press(InputActionType.NavigateRight, DeviceName));
        }
    }

    /// <summary>
    /// Converts action type to direction vector.
    /// </summary>
    private Vector2 GetDirectionVector(InputActionType action)
    {
        return action switch
        {
            InputActionType.NavigateUp => new Vector2(0, -1),
            InputActionType.NavigateDown => new Vector2(0, 1),
            InputActionType.NavigateLeft => new Vector2(-1, 0),
            InputActionType.NavigateRight => new Vector2(1, 0),
            _ => Vector2.Zero,
        };
    }

    /// <summary>
    /// Fires an input event.
    /// </summary>
    private void FireEvent(GameInputEvent inputEvent)
    {
        OnInput?.Invoke(inputEvent);
    }
}
