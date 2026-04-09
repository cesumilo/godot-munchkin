using System;

/// <summary>
/// Interface for all input device handlers (mouse, keyboard, gamepad, touch).
/// </summary>
/// <remarks>
/// Part of Phase 1: Core Input Abstraction.
/// Implementations translate device-specific input into abstract InputEvents.
/// </remarks>
public interface IInputHandler
{
    /// <summary>
    /// Event fired when an input action occurs.
    /// </summary>
    event Action<GameInputEvent> OnInput;

    /// <summary>
    /// Processes input for this device. Called each frame by InputManager.
    /// </summary>
    /// <param name="delta">Time since last frame.</param>
    void ProcessInput(double delta);

    /// <summary>
    /// Enables or disables this input handler.
    /// </summary>
    /// <param name="enabled">True to enable, false to disable.</param>
    void SetEnabled(bool enabled);

    /// <summary>
    /// Gets whether this handler is currently enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Gets the name of this input device for debugging.
    /// </summary>
    string DeviceName { get; }
}

/// <summary>
/// Types of input actions that can occur.
/// </summary>
public enum InputActionType
{
    /// <summary>Confirm current selection or target.</summary>
    Select,

    /// <summary>Cancel current action or go back.</summary>
    Cancel,

    /// <summary>Navigate up (D-pad up, W, stick up).</summary>
    NavigateUp,

    /// <summary>Navigate down (D-pad down, S, stick down).</summary>
    NavigateDown,

    /// <summary>Navigate left (D-pad left, A, stick left).</summary>
    NavigateLeft,

    /// <summary>Navigate right (D-pad right, D, stick right).</summary>
    NavigateRight,

    /// <summary>Open action/context menu.</summary>
    ActionMenu,

    /// <summary>Quick end turn action.</summary>
    EndTurn,

    /// <summary>Hold to inspect card (analog trigger value).</summary>
    Inspect,

    /// <summary>Pause menu.</summary>
    Pause,
}

/// <summary>
/// Represents a single input event from any device.
/// </summary>
public struct GameInputEvent
{
    /// <summary>
    /// The type of action that occurred.
    /// </summary>
    public InputActionType Action;

    /// <summary>
    /// True if the action is pressed/started, false if released.
    /// </summary>
    public bool IsPressed;

    /// <summary>
    /// Analog value for actions with variable intensity (0-1).
    /// Used for analog stick deflection, trigger pressure.
    /// </summary>
    public float Value;

    /// <summary>
    /// The input device that generated this event.
    /// </summary>
    public string DeviceName;

    /// <summary>
    /// Creates a simple button press event.
    /// </summary>
    public static GameInputEvent Press(InputActionType action, string device)
    {
        return new GameInputEvent
        {
            Action = action,
            IsPressed = true,
            Value = 1.0f,
            DeviceName = device,
        };
    }

    /// <summary>
    /// Creates a button release event.
    /// </summary>
    public static GameInputEvent Release(InputActionType action, string device)
    {
        return new GameInputEvent
        {
            Action = action,
            IsPressed = false,
            Value = 0.0f,
            DeviceName = device,
        };
    }

    /// <summary>
    /// Creates an analog event with variable value.
    /// </summary>
    public static GameInputEvent Analog(InputActionType action, float value, string device)
    {
        return new GameInputEvent
        {
            Action = action,
            IsPressed = value > 0.5f,
            Value = value,
            DeviceName = device,
        };
    }
}
