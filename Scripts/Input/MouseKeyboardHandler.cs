using System;
using Godot;

/// <summary>
/// Handles mouse and keyboard input devices with remappable bindings.
/// </summary>
/// <remarks>
/// Uses InputRemappingManager for configurable bindings.
/// Supports keyboard keys and mouse buttons.
/// </remarks>
public partial class MouseKeyboardHandler : Node, IInputHandler
{
    /// <summary>
    /// Event fired when an input action occurs.
    /// </summary>
    public event Action<GameInputEvent> OnInput;

    /// <summary>
    /// Gets the device name for debugging.
    /// </summary>
    public string DeviceName => "Mouse/Keyboard";

    /// <summary>
    /// Gets whether this handler is enabled.
    /// </summary>
    public bool IsEnabled { get; private set; } = true;

    // Track previous states for edge detection
    private bool _wasSelectPressed = false;
    private bool _wasCancelPressed = false;
    private bool _wasActionMenuPressed = false;
    private bool _wasEndTurnPressed = false;
    private bool _wasPausePressed = false;

    // Navigation repeat timing
    private double _navigateRepeatTimer = 0.0;
    private const double NavigateRepeatDelay = 0.3;
    private const double NavigateRepeatRate = 0.1;
    private bool _isNavigating = false;

    /// <summary>
    /// Sets whether this handler is enabled.
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
    }

    /// <summary>
    /// Processes mouse and keyboard input.
    /// </summary>
    public void ProcessInput(double delta)
    {
        if (!IsEnabled)
            return;

        ProcessMouseInput();
        ProcessKeyboardInput(delta);
    }

    /// <summary>
    /// Processes mouse button input using remappable bindings.
    /// </summary>
    private void ProcessMouseInput()
    {
        var remapping = InputRemappingManager.Instance;
        if (remapping == null)
            return;

        // Check each action's mouse binding
        foreach (var kvp in remapping.Bindings)
        {
            if (!kvp.Value.MouseButton.HasValue)
                continue;

            var button = kvp.Value.MouseButton.Value;
            bool isPressed = Input.IsMouseButtonPressed(button);
            var actionType = ParseActionType(kvp.Key);

            if (actionType.HasValue)
            {
                ProcessActionButton(actionType.Value, isPressed);
            }
        }
    }

    /// <summary>
    /// Processes keyboard input using remappable bindings.
    /// </summary>
    private void ProcessKeyboardInput(double delta)
    {
        var remapping = InputRemappingManager.Instance;
        if (remapping == null)
            return;

        // Check each action's keyboard binding
        foreach (var kvp in remapping.Bindings)
        {
            if (!kvp.Value.KeyboardKey.HasValue)
                continue;

            var key = kvp.Value.KeyboardKey.Value;
            bool isPressed = Input.IsKeyPressed(key);
            var actionType = ParseActionType(kvp.Key);

            if (actionType.HasValue && !IsNavigationAction(actionType.Value))
            {
                ProcessActionButton(actionType.Value, isPressed);
            }
        }

        // Process navigation separately for repeat handling
        ProcessNavigation(delta);
    }

    /// <summary>
    /// Processes a single action button state.
    /// </summary>
    private void ProcessActionButton(InputActionType action, bool isPressed)
    {
        bool wasPressed = GetWasPressed(action);

        if (isPressed && !wasPressed)
        {
            FireEvent(GameInputEvent.Press(action, DeviceName));
            SetWasPressed(action, true);
        }
        else if (!isPressed && wasPressed)
        {
            FireEvent(GameInputEvent.Release(action, DeviceName));
            SetWasPressed(action, false);
        }
    }

    /// <summary>
    /// Processes navigation input with repeat handling.
    /// </summary>
    private void ProcessNavigation(double delta)
    {
        var remapping = InputRemappingManager.Instance;
        if (remapping == null)
            return;

        // Get navigation bindings
        var upBinding = remapping.GetBinding("NavigateUp");
        var downBinding = remapping.GetBinding("NavigateDown");
        var leftBinding = remapping.GetBinding("NavigateLeft");
        var rightBinding = remapping.GetBinding("NavigateRight");

        bool up =
            upBinding?.KeyboardKey.HasValue == true
            && Input.IsKeyPressed(upBinding.KeyboardKey.Value);
        bool down =
            downBinding?.KeyboardKey.HasValue == true
            && Input.IsKeyPressed(downBinding.KeyboardKey.Value);
        bool left =
            leftBinding?.KeyboardKey.HasValue == true
            && Input.IsKeyPressed(leftBinding.KeyboardKey.Value);
        bool right =
            rightBinding?.KeyboardKey.HasValue == true
            && Input.IsKeyPressed(rightBinding.KeyboardKey.Value);

        bool anyNav = up || down || left || right;

        if (anyNav)
        {
            _navigateRepeatTimer += delta;

            if (!_isNavigating || _navigateRepeatTimer >= NavigateRepeatRate)
            {
                if (up)
                    FireEvent(GameInputEvent.Press(InputActionType.NavigateUp, DeviceName));
                if (down)
                    FireEvent(GameInputEvent.Press(InputActionType.NavigateDown, DeviceName));
                if (left)
                    FireEvent(GameInputEvent.Press(InputActionType.NavigateLeft, DeviceName));
                if (right)
                    FireEvent(GameInputEvent.Press(InputActionType.NavigateRight, DeviceName));

                _navigateRepeatTimer = 0;
            }

            _isNavigating = true;
        }
        else
        {
            _isNavigating = false;
            _navigateRepeatTimer = NavigateRepeatDelay;
        }
    }

    /// <summary>
    /// Checks if an action is a navigation action.
    /// </summary>
    private bool IsNavigationAction(InputActionType action)
    {
        return action == InputActionType.NavigateUp
            || action == InputActionType.NavigateDown
            || action == InputActionType.NavigateLeft
            || action == InputActionType.NavigateRight;
    }

    /// <summary>
    /// Gets the previous pressed state for an action.
    /// </summary>
    private bool GetWasPressed(InputActionType action)
    {
        return action switch
        {
            InputActionType.Select => _wasSelectPressed,
            InputActionType.Cancel => _wasCancelPressed,
            InputActionType.ActionMenu => _wasActionMenuPressed,
            InputActionType.EndTurn => _wasEndTurnPressed,
            InputActionType.Pause => _wasPausePressed,
            _ => false,
        };
    }

    /// <summary>
    /// Sets the previous pressed state for an action.
    /// </summary>
    private void SetWasPressed(InputActionType action, bool value)
    {
        switch (action)
        {
            case InputActionType.Select:
                _wasSelectPressed = value;
                break;
            case InputActionType.Cancel:
                _wasCancelPressed = value;
                break;
            case InputActionType.ActionMenu:
                _wasActionMenuPressed = value;
                break;
            case InputActionType.EndTurn:
                _wasEndTurnPressed = value;
                break;
            case InputActionType.Pause:
                _wasPausePressed = value;
                break;
        }
    }

    /// <summary>
    /// Parses action name string to enum.
    /// </summary>
    private InputActionType? ParseActionType(string actionName)
    {
        return actionName switch
        {
            "Select" => InputActionType.Select,
            "Cancel" => InputActionType.Cancel,
            "ActionMenu" => InputActionType.ActionMenu,
            "EndTurn" => InputActionType.EndTurn,
            "Inspect" => InputActionType.Inspect,
            "Pause" => InputActionType.Pause,
            "NavigateUp" => InputActionType.NavigateUp,
            "NavigateDown" => InputActionType.NavigateDown,
            "NavigateLeft" => InputActionType.NavigateLeft,
            "NavigateRight" => InputActionType.NavigateRight,
            _ => null,
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
