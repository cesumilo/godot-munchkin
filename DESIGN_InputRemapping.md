# Input Remapping System

> **Goal**: Allow players to customize keyboard/mouse input bindings  
> **Requirement**: Essential for desktop game accessibility

---

## 1. Features

### Core Requirements
- [ ] Remap any keyboard key to any action
- [ ] Remap mouse buttons to actions
- [ ] Save/load mappings to user config
- [ ] Reset to defaults
- [ ] Conflict detection (prevent duplicate bindings)
- [ ] Visual binding display in UI

---

## 2. Action Categories

### Gameplay Actions (Remappable)
| Action | Default Key | Default Mouse | Description |
|--------|-------------|---------------|-------------|
| Select | Enter | Left Click | Confirm selection |
| Cancel | Escape | Right Click | Go back/cancel |
| Navigate Up | Up Arrow | - | Move focus up |
| Navigate Down | Down Arrow | - | Move focus down |
| Navigate Left | Left Arrow | - | Move focus left |
| Navigate Right | Right Arrow | - | Move focus right |
| Action Menu | E | Middle Click | Open card menu |
| End Turn | Tab | - | End turn |
| Inspect | I | - | Inspect card |
| Pause | Escape | - | Pause menu |

### System Actions (Not Remappable)
- Screenshot (F12)
- Console (`)
- Fullscreen (Alt+Enter)

---

## 3. Configuration File Format

```json
{
  "version": "1.0",
  "bindings": {
    "Select": {
      "keyboard": "Enter",
      "mouse": "LeftButton"
    },
    "Cancel": {
      "keyboard": "Escape",
      "mouse": "RightButton"
    },
    "NavigateUp": {
      "keyboard": "Up",
      "mouse": null
    },
    "ActionMenu": {
      "keyboard": "E",
      "mouse": "MiddleButton"
    }
  }
}
```

---

## 4. UI Design

### Remapping Menu Layout
```
+------------------------------------------+
|  INPUT SETTINGS                          |
+------------------------------------------+
|                                          |
|  Gameplay Actions                        |
|  +------------------------------------+  |
|  | Select          [Enter] [L-Click]  |  |
|  | Cancel          [Esc]   [R-Click]  |  |
|  | Action Menu     [E]     [M-Click]  |  |
|  | End Turn        [Tab]    [-]       |  |
|  | Inspect         [I]      [-]       |  |
|  | ...                                |  |
|  +------------------------------------+  |
|                                          |
|  Navigation                            |
|  +------------------------------------+  |
|  | Up              [Up]     [-]       |  |
|  | Down            [Down]   [-]       |  |
|  | Left            [Left]   [-]       |  |
|  | Right           [Right]  [-]       |  |
|  +------------------------------------+  |
|                                          |
|  [Reset to Defaults]  [Apply]  [Cancel] |
+------------------------------------------+
```

### Remapping Flow
```
1. Player clicks on a binding row
2. UI shows "Press key or mouse button..."
3. Player presses desired input
4. System checks for conflicts
5. If conflict: Show warning, ask to rebind or cancel
6. If no conflict: Save new binding
7. Update display
```

---

## 5. Implementation Components

### InputBindingConfig.cs
```csharp
public class InputBindingConfig
{
    public string ActionName { get; set; }
    public Key? KeyboardKey { get; set; }
    public MouseButton? MouseButton { get; set; }
    public string DisplayName { get; set; }
    public bool IsRemappable { get; set; } = true;
}
```

### InputRemappingManager.cs (extends InputManager)
```csharp
public partial class InputRemappingManager : InputManager
{
    private Dictionary<string, InputBindingConfig> _bindings;
    private string _configPath = "user://input_config.json";
    
    // Load saved bindings on init
    // Save bindings when changed
    // Apply bindings to handlers
    // Handle conflict detection
}
```

### RemappingUI.cs
```csharp
public partial class RemappingUI : Control
{
    // Display current bindings
    // Handle user input for remapping
    // Show conflict warnings
    // Save/reset functionality
}
```

---

## 6. Conflict Resolution

### Conflict Types
1. **Same action, same input** - No change needed
2. **Different action, same input** - Must resolve
3. **Reserved system key** - Block with message

### Resolution UI
```
+----------------------------------+
|  KEY CONFLICT                    |
+----------------------------------+
|                                  |
|  'Space' is already bound to:    |
|  [Select]                        |
|                                  |
|  Do you want to:                 |
|                                  |
|  [Rebind Select to None]        |
|  [Keep Select, Cancel this]     |
|  [Cancel]                        |
+----------------------------------+
```

---

## 7. Persistence

### Save Location
- Windows: `%APPDATA%/Godot/munchkin/input_config.json`
- macOS: `~/Library/Application Support/Godot/munchkin/input_config.json`
- Linux: `~/.local/share/godot/munchkin/input_config.json`

### Save Triggers
- On "Apply" button press in settings
- On game exit (if changes made)
- Auto-save after each successful remap (optional)

---

## 8. Integration Steps

1. **Modify MouseKeyboardHandler** to read from config instead of hardcoded keys
2. **Create InputRemappingManager** extending InputManager
3. **Create RemappingUI** scene for settings menu
4. **Add to Settings menu** as "Input" tab
5. **Test all default mappings still work**
6. **Test remapping saves and loads correctly**

---

## 9. Testing Checklist

### Default Mappings
- [ ] All gameplay actions work with defaults
- [ ] Navigation works with arrow keys
- [ ] Mouse clicks work correctly

### Remapping
- [ ] Can rebind keyboard key
- [ ] Can rebind mouse button
- [ ] Can clear a binding (set to none)
- [ ] Conflict detection works
- [ ] Changes persist after restart
- [ ] Reset to defaults works

### Edge Cases
- [ ] Rebind to same key (no-op)
- [ ] Rebind to reserved key (blocked)
- [ ] Config file corruption (use defaults)
- [ ] Config version mismatch (migrate or reset)

---

**Ready to implement remapping system!**
