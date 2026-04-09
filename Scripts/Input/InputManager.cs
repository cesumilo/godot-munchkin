using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Autoload singleton that coordinates all input from various devices.
/// Routes input events to the appropriate handlers.
/// </summary>
/// <remarks>
/// Part of Phase 1: Core Input Abstraction.
/// Must be configured as autoload in Godot project settings.
/// </remarks>
public partial class InputManager : Node
{
    private static InputManager _instance;

    /// <summary>
    /// Gets the singleton instance of InputManager.
    /// </summary>
    public static InputManager Instance => _instance;

    /// <summary>
    /// Event fired when any input action occurs from any device.
    /// Uses Godot Dictionary for signal compatibility.
    /// </summary>
    [Signal]
    public delegate void InputReceivedEventHandler(Godot.Collections.Dictionary eventData);

    /// <summary>
    /// Event fired when navigation input occurs.
    /// </summary>
    [Signal]
    public delegate void NavigateEventHandler(Vector2 direction);

    /// <summary>
    /// Event fired when Select is pressed.
    /// </summary>
    [Signal]
    public delegate void SelectPressedEventHandler();

    /// <summary>
    /// Event fired when Cancel is pressed.
    /// </summary>
    [Signal]
    public delegate void CancelPressedEventHandler();

    /// <summary>
    /// Event fired when Action Menu is requested.
    /// </summary>
    [Signal]
    public delegate void ActionMenuEventHandler();

    /// <summary>
    /// Event fired when End Turn is requested.
    /// </summary>
    [Signal]
    public delegate void EndTurnEventHandler();

    /// <summary>
    /// Event fired when Pause is requested.
    /// </summary>
    [Signal]
    public delegate void PauseEventHandler();

    private List<IInputHandler> _handlers = new();
    private bool _isProcessing = true;

    /// <summary>
    /// Initializes the singleton and default handlers.
    /// </summary>
    public override void _Ready()
    {
        if (_instance != null && _instance != this)
        {
            QueueFree();
            return;
        }

        _instance = this;
        ProcessMode = ProcessModeEnum.Always;

        // Register default handlers
        InitializeDefaultHandlers();

        GD.Print("[InputManager] Initialized with " + _handlers.Count + " handlers");
    }

    /// <summary>
    /// Creates and registers the default input handlers.
    /// </summary>
    private void InitializeDefaultHandlers()
    {
        // Wait for InputRemappingManager to be ready
        if (InputRemappingManager.Instance == null)
        {
            GD.Print("[InputManager] Waiting for InputRemappingManager...");
            // Try again next frame
            CallDeferred(nameof(InitializeDefaultHandlers));
            return;
        }

        // Mouse and keyboard handler
        var mouseKeyboard = new MouseKeyboardHandler();
        RegisterHandler(mouseKeyboard);

        // Gamepad handler (Xbox, PS5, Steam Deck controller)
        var gamepad = new GamepadHandler();
        RegisterHandler(gamepad);

        GD.Print("[InputManager] Default handlers registered");
    }

    /// <summary>
    /// Registers an input handler.
    /// </summary>
    /// <param name="handler">The handler to register.</param>
    public void RegisterHandler(IInputHandler handler)
    {
        if (handler == null)
            return;

        _handlers.Add(handler);
        handler.OnInput += OnHandlerInput;

        GD.Print($"[InputManager] Registered handler: {handler.DeviceName}");
    }

    /// <summary>
    /// Unregisters an input handler.
    /// </summary>
    /// <param name="handler">The handler to unregister.</param>
    public void UnregisterHandler(IInputHandler handler)
    {
        if (handler == null)
            return;

        handler.OnInput -= OnHandlerInput;
        _handlers.Remove(handler);

        GD.Print($"[InputManager] Unregistered handler: {handler.DeviceName}");
    }

    /// <summary>
    /// Processes input from all registered handlers.
    /// </summary>
    public override void _Process(double delta)
    {
        if (!_isProcessing)
            return;

        foreach (var handler in _handlers)
        {
            if (handler.IsEnabled)
            {
                handler.ProcessInput(delta);
            }
        }
    }

    /// <summary>
    /// Handles input events from registered handlers.
    /// </summary>
    /// <param name="inputEvent">The input event.</param>
    private void OnHandlerInput(GameInputEvent inputEvent)
    {
        // Emit general input event as Dictionary for Godot signal compatibility
        var eventData = new Godot.Collections.Dictionary
        {
            ["action"] = (int)inputEvent.Action,
            ["is_pressed"] = inputEvent.IsPressed,
            ["value"] = inputEvent.Value,
            ["device"] = inputEvent.DeviceName,
        };
        EmitSignal(SignalName.InputReceived, eventData);

        // Emit specific events based on action type
        switch (inputEvent.Action)
        {
            case InputActionType.Select when inputEvent.IsPressed:
                EmitSignal(SignalName.SelectPressed);
                break;

            case InputActionType.Cancel when inputEvent.IsPressed:
                EmitSignal(SignalName.CancelPressed);
                break;

            case InputActionType.ActionMenu when inputEvent.IsPressed:
                EmitSignal(SignalName.ActionMenu);
                break;

            case InputActionType.EndTurn when inputEvent.IsPressed:
                EmitSignal(SignalName.EndTurn);
                break;

            case InputActionType.Pause when inputEvent.IsPressed:
                EmitSignal(SignalName.Pause);
                break;

            case InputActionType.NavigateUp:
            case InputActionType.NavigateDown:
            case InputActionType.NavigateLeft:
            case InputActionType.NavigateRight:
                EmitNavigationSignal(inputEvent);
                break;
        }
    }

    /// <summary>
    /// Emits navigation signal with direction vector.
    /// </summary>
    private void EmitNavigationSignal(GameInputEvent inputEvent)
    {
        Vector2 direction = Vector2.Zero;

        switch (inputEvent.Action)
        {
            case InputActionType.NavigateUp:
                direction = new Vector2(0, -1);
                break;
            case InputActionType.NavigateDown:
                direction = new Vector2(0, 1);
                break;
            case InputActionType.NavigateLeft:
                direction = new Vector2(-1, 0);
                break;
            case InputActionType.NavigateRight:
                direction = new Vector2(1, 0);
                break;
        }

        // Scale by analog value
        direction *= inputEvent.Value;

        EmitSignal(SignalName.Navigate, direction);
    }

    /// <summary>
    /// Enables or disables all input processing.
    /// </summary>
    /// <param name="enabled">True to enable, false to disable.</param>
    public void SetProcessingEnabled(bool enabled)
    {
        _isProcessing = enabled;
        GD.Print($"[InputManager] Processing {(enabled ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// Gets whether input processing is enabled.
    /// </summary>
    public bool IsProcessingEnabled => _isProcessing;

    /// <summary>
    /// Enables or disables a specific device type.
    /// </summary>
    /// <param name="deviceName">The device name to toggle.</param>
    /// <param name="enabled">True to enable, false to disable.</param>
    public void SetDeviceEnabled(string deviceName, bool enabled)
    {
        foreach (var handler in _handlers)
        {
            if (handler.DeviceName == deviceName)
            {
                handler.SetEnabled(enabled);
                GD.Print($"[InputManager] {deviceName} {(enabled ? "enabled" : "disabled")}");
                return;
            }
        }
    }

    /// <summary>
    /// Cleans up on exit.
    /// </summary>
    public override void _ExitTree()
    {
        if (_instance == this)
        {
            _instance = null;
        }

        foreach (var handler in _handlers)
        {
            handler.OnInput -= OnHandlerInput;
        }

        _handlers.Clear();
    }
}
