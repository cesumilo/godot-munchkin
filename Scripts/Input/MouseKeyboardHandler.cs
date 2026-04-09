using System;
using System.Collections.Generic;
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

    // Track pressed state per action (combines mouse + keyboard)
    private HashSet<InputActionType> _pressedActions = new();

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

        // Build current state from both mouse and keyboard
        var currentState = new HashSet<InputActionType>();

        CollectMouseInput(currentState);
        CollectKeyboardInput(currentState, delta);

        // Process state changes
        ProcessStateChanges(currentState);

        // Update stored state
        _pressedActions = currentState;
    }

    /// <summary>
    /// Collects mouse input into the current state set.
    /// </summary>
    private void CollectMouseInput(HashSet<InputActionType> currentState)
    {
        var remapping = InputRemappingManager.Instance;
        if (remapping == null)
            return;

        foreach (var kvp in remapping.Bindings)
        {
            if (!kvp.Value.MouseButton.HasValue)
                continue;

            var button = kvp.Value.MouseButton.Value;
            if (Input.IsMouseButtonPressed(button))
            {
                var actionType = ParseActionType(kvp.Key);
                if (actionType.HasValue)
                {
                    currentState.Add(actionType.Value);
                }
            }
        }
    }

    /// <summary>
    /// Collects keyboard input into the current state set.
    /// </summary>
    private void CollectKeyboardInput(HashSet<InputActionType> currentState, double delta)
    {
        var remapping = InputRemappingManager.Instance;
        if (remapping == null)
            return;

        // Check action keys
        foreach (var kvp in remapping.Bindings)
        {
            if (!kvp.Value.KeyboardKey.HasValue)
                continue;

            var key = kvp.Value.KeyboardKey.Value;
            if (Input.IsKeyPressed(key))
            {
                var actionType = ParseActionType(kvp.Key);
                if (actionType.HasValue && !IsNavigationAction(actionType.Value))
                {
                    currentState.Add(actionType.Value);
                }
            }
        }

        // Process navigation with repeat
        ProcessNavigation(currentState, delta);
    }

    /// <summary>
    /// Processes state changes and fires events.
    /// </summary>
    private void ProcessStateChanges(HashSet<InputActionType> currentState)
    {
        // Actions that are newly pressed
        foreach (var action in currentState)
        {
            if (!_pressedActions.Contains(action))
            {
                FireEvent(GameInputEvent.Press(action, DeviceName));
            }
        }

        // Actions that are newly released
        foreach (var action in _pressedActions)
        {
            if (!currentState.Contains(action))
            {
                FireEvent(GameInputEvent.Release(action, DeviceName));
            }
        }
    }

    /// <summary>
    /// Processes navigation input with repeat handling.
    /// </summary>
    private void ProcessNavigation(HashSet<InputActionType> currentState, double delta)
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
                    currentState.Add(InputActionType.NavigateUp);
                if (down)
                    currentState.Add(InputActionType.NavigateDown);
                if (left)
                    currentState.Add(InputActionType.NavigateLeft);
                if (right)
                    currentState.Add(InputActionType.NavigateRight);

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
