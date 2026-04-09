using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Manages customizable input bindings for keyboard and mouse.
/// Extends InputManager with persistence and remapping capabilities.
/// </summary>
/// <remarks>
/// Part of Input Remapping feature. Saves bindings to user://input_config.json
/// </remarks>
public partial class InputRemappingManager : Node
{
    private static InputRemappingManager _instance;
    public static InputRemappingManager Instance => _instance;

    /// <summary>
    /// Event fired when bindings are changed.
    /// </summary>
    [Signal]
    public delegate void BindingsChangedEventHandler();

    private const string ConfigPath = "user://input_config.json";
    private const string ConfigVersion = "1.0";

    /// <summary>
    /// Dictionary of action names to their bindings.
    /// </summary>
    private Dictionary<string, InputBinding> _bindings = new();

    /// <summary>
    /// Gets all current bindings.
    /// </summary>
    public IReadOnlyDictionary<string, InputBinding> Bindings => _bindings;

    /// <summary>
    /// Default bindings used for reset.
    /// </summary>
    private readonly Dictionary<string, InputBinding> _defaultBindings = new()
    {
        ["Select"] = new InputBinding
        {
            ActionName = "Select",
            KeyboardKey = Key.Enter,
            MouseButton = MouseButton.Left,
        },
        ["Cancel"] = new InputBinding
        {
            ActionName = "Cancel",
            KeyboardKey = Key.Escape,
            MouseButton = MouseButton.Right,
        },
        ["NavigateUp"] = new InputBinding { ActionName = "NavigateUp", KeyboardKey = Key.Up },
        ["NavigateDown"] = new InputBinding { ActionName = "NavigateDown", KeyboardKey = Key.Down },
        ["NavigateLeft"] = new InputBinding { ActionName = "NavigateLeft", KeyboardKey = Key.Left },
        ["NavigateRight"] = new InputBinding
        {
            ActionName = "NavigateRight",
            KeyboardKey = Key.Right,
        },
        ["ActionMenu"] = new InputBinding
        {
            ActionName = "ActionMenu",
            KeyboardKey = Key.E,
            MouseButton = MouseButton.Middle,
        },
        ["EndTurn"] = new InputBinding { ActionName = "EndTurn", KeyboardKey = Key.Tab },
        ["Inspect"] = new InputBinding { ActionName = "Inspect", KeyboardKey = Key.I },
        ["Pause"] = new InputBinding { ActionName = "Pause", KeyboardKey = Key.Escape },
    };

    public override void _Ready()
    {
        if (_instance != null && _instance != this)
        {
            QueueFree();
            return;
        }

        _instance = this;

        LoadBindings();

        GD.Print("[InputRemappingManager] Initialized with " + _bindings.Count + " bindings");
    }

    /// <summary>
    /// Loads bindings from config file or uses defaults.
    /// </summary>
    public void LoadBindings()
    {
        if (!FileAccess.FileExists(ConfigPath))
        {
            GD.Print("[InputRemappingManager] No config file found, using defaults");
            ResetToDefaults();
            return;
        }

        try
        {
            using var file = FileAccess.Open(ConfigPath, FileAccess.ModeFlags.Read);
            string json = file.GetAsText();
            var data = Json.ParseString(json).AsGodotDictionary();

            string version = data.GetValueOrDefault("version", "").ToString();
            if (version != ConfigVersion)
            {
                GD.Print(
                    $"[InputRemappingManager] Config version mismatch ({version} vs {ConfigVersion}), using defaults"
                );
                ResetToDefaults();
                return;
            }

            _bindings.Clear();
            var bindingsData = data.GetValueOrDefault(
                    "bindings",
                    new Godot.Collections.Dictionary()
                )
                .AsGodotDictionary();

            foreach (var action in bindingsData.Keys)
            {
                string actionName = action.ToString();
                var bindingData = bindingsData[action].AsGodotDictionary();

                _bindings[actionName] = new InputBinding
                {
                    ActionName = actionName,
                    KeyboardKey = ParseKey(
                        bindingData.GetValueOrDefault("keyboard", "").ToString()
                    ),
                    MouseButton = ParseMouseButton(
                        bindingData.GetValueOrDefault("mouse", "").ToString()
                    ),
                };
            }

            GD.Print("[InputRemappingManager] Loaded " + _bindings.Count + " bindings from config");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[InputRemappingManager] Failed to load config: {ex.Message}");
            ResetToDefaults();
        }
    }

    /// <summary>
    /// Saves current bindings to config file.
    /// </summary>
    public void SaveBindings()
    {
        try
        {
            var bindingsData = new Godot.Collections.Dictionary();

            foreach (var kvp in _bindings)
            {
                var binding = kvp.Value;
                bindingsData[kvp.Key] = new Godot.Collections.Dictionary
                {
                    ["keyboard"] = binding.KeyboardKey?.ToString() ?? "",
                    ["mouse"] = binding.MouseButton?.ToString() ?? "",
                };
            }

            var data = new Godot.Collections.Dictionary
            {
                ["version"] = ConfigVersion,
                ["bindings"] = bindingsData,
            };

            using var file = FileAccess.Open(ConfigPath, FileAccess.ModeFlags.Write);
            file.StoreString(data.ToString());

            GD.Print("[InputRemappingManager] Saved bindings to " + ConfigPath);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[InputRemappingManager] Failed to save config: {ex.Message}");
        }
    }

    /// <summary>
    /// Resets all bindings to defaults.
    /// </summary>
    public void ResetToDefaults()
    {
        _bindings.Clear();
        foreach (var kvp in _defaultBindings)
        {
            _bindings[kvp.Key] = kvp.Value.Clone();
        }

        SaveBindings();
        EmitSignal(SignalName.BindingsChanged);

        GD.Print("[InputRemappingManager] Reset to defaults");
    }

    /// <summary>
    /// Sets a keyboard binding for an action.
    /// </summary>
    /// <param name="actionName">The action to bind.</param>
    /// <param name="key">The key to bind, or null to clear.</param>
    /// <returns>True if successful, false if there's a conflict.</returns>
    public bool SetKeyboardBinding(string actionName, Key? key)
    {
        if (!_bindings.ContainsKey(actionName))
            return false;

        // Check for conflicts
        if (key.HasValue)
        {
            foreach (var kvp in _bindings)
            {
                if (kvp.Key != actionName && kvp.Value.KeyboardKey == key)
                {
                    GD.Print($"[InputRemappingManager] Conflict: {key} already bound to {kvp.Key}");
                    return false;
                }
            }
        }

        _bindings[actionName].KeyboardKey = key;
        SaveBindings();
        EmitSignal(SignalName.BindingsChanged);

        GD.Print($"[InputRemappingManager] Set {actionName} keyboard binding to {key}");
        return true;
    }

    /// <summary>
    /// Sets a mouse binding for an action.
    /// </summary>
    /// <param name="actionName">The action to bind.</param>
    /// <param name="button">The mouse button to bind, or null to clear.</param>
    /// <returns>True if successful, false if there's a conflict.</returns>
    public bool SetMouseBinding(string actionName, MouseButton? button)
    {
        if (!_bindings.ContainsKey(actionName))
            return false;

        // Check for conflicts
        if (button.HasValue)
        {
            foreach (var kvp in _bindings)
            {
                if (kvp.Key != actionName && kvp.Value.MouseButton == button)
                {
                    GD.Print(
                        $"[InputRemappingManager] Conflict: {button} already bound to {kvp.Key}"
                    );
                    return false;
                }
            }
        }

        _bindings[actionName].MouseButton = button;
        SaveBindings();
        EmitSignal(SignalName.BindingsChanged);

        GD.Print($"[InputRemappingManager] Set {actionName} mouse binding to {button}");
        return true;
    }

    /// <summary>
    /// Gets the binding for an action.
    /// </summary>
    public InputBinding GetBinding(string actionName)
    {
        return _bindings.TryGetValue(actionName, out var binding) ? binding : null;
    }

    /// <summary>
    /// Parses a key string to Key enum.
    /// </summary>
    private Key? ParseKey(string keyName)
    {
        if (string.IsNullOrEmpty(keyName))
            return null;

        if (Enum.TryParse<Key>(keyName, out var key))
            return key;

        return null;
    }

    /// <summary>
    /// Parses a mouse button string to MouseButton enum.
    /// </summary>
    private MouseButton? ParseMouseButton(string buttonName)
    {
        if (string.IsNullOrEmpty(buttonName))
            return null;

        if (Enum.TryParse<MouseButton>(buttonName, out var button))
            return button;

        return null;
    }

    public override void _ExitTree()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }
}

/// <summary>
/// Represents a single input binding configuration.
/// </summary>
public class InputBinding
{
    public string ActionName { get; set; }
    public Key? KeyboardKey { get; set; }
    public MouseButton? MouseButton { get; set; }

    public InputBinding Clone()
    {
        return new InputBinding
        {
            ActionName = ActionName,
            KeyboardKey = KeyboardKey,
            MouseButton = MouseButton,
        };
    }

    /// <summary>
    /// Gets a display string for the keyboard binding.
    /// </summary>
    public string GetKeyboardDisplay()
    {
        return KeyboardKey?.ToString() ?? "-";
    }

    /// <summary>
    /// Gets a display string for the mouse binding.
    /// </summary>
    public string GetMouseDisplay()
    {
        return MouseButton switch
        {
            Godot.MouseButton.Left => "L-Click",
            Godot.MouseButton.Right => "R-Click",
            Godot.MouseButton.Middle => "M-Click",
            Godot.MouseButton.Xbutton1 => "MB4",
            Godot.MouseButton.Xbutton2 => "MB5",
            null => "-",
            _ => MouseButton.ToString(),
        };
    }
}
