# Input System Design Document

> **Status**: Design Phase  
> **Goal**: Universal input system supporting Mouse, Keyboard, Steam Deck, Xbox, and PS5 controllers  
> **Approach**: Selection-based interaction (not drag-and-drop)

---

## 1. Core Philosophy

**"Select, then Target, then Confirm"**

Instead of dragging cards with analog sticks or dealing with perspective distortion, players:
1. **Select** a card (mouse click / controller button)
2. **Choose** a target zone (mouse hover / controller navigate)
3. **Confirm** the action (mouse click / controller button)

This works identically for all input devices and eliminates precision issues.

---

## 2. Input Devices Supported

| Device | Primary Navigation | Select | Cancel | Quick Menu |
|--------|-------------------|--------|--------|------------|
| Mouse | Hover/Click | Left Click | Right Click | Middle Click |
| Keyboard | Arrow Keys/Tab | Enter | Escape | Space |
| Steam Deck | Touchscreen OR Analog Stick | A Button | B Button | X Button |
| Xbox | Left Analog/D-Pad | A | B | X |
| PS5 | Left Stick/D-Pad | Cross | Circle | Square |

---

## 3. Interaction State Machine

### States Overview

| State | Description | Valid Actions |
|-------|-------------|---------------|
| **IDLE** | Nothing selected, navigating cards/zones | Navigate, Select Card |
| **CARD_SELECTED** | Card lifted, valid targets highlighted | Navigate Targets, Cancel, Confirm |
| **TARGET_SELECTED** | Target chosen, waiting confirmation | Confirm, Cancel |
| **ANIMATING** | Card moving to target, inputs locked | (none) |

### State Transitions

```
IDLE --[Select Card]--> CARD_SELECTED --[Choose Target]--> TARGET_SELECTED
  ^                          |                                   |
  |                          | [Cancel]                          | [Confirm]
  |                          v                                   v
  +-------------------- IDLE <------------------------------ ANIMATING
```

---

## 4. Input Mappings

### 4.1 Mouse and Keyboard

| Action | Primary | Alternative |
|--------|---------|-------------|
| Select Card | Left Click | Enter |
| Cancel Selection | Right Click | Escape |
| Navigate | Arrow Keys | WASD |
| Quick Menu | Middle Click | Space |
| End Turn | Click Button | E key |

### 4.2 Xbox Controller

| Action | Button | Notes |
|--------|--------|-------|
| Select / Confirm | A | Primary action |
| Cancel / Back | B | Return to previous state |
| Navigate | Left Stick / D-Pad | Move focus between cards/zones |
| Action Menu | X | Open card options |
| End Turn | Y | Quick access |
| Inspect Card | RT (hold) | Show enlarged card view |

### 4.3 PlayStation 5 Controller

| Action | Button | Notes |
|--------|--------|-------|
| Select / Confirm | Cross | Primary action |
| Cancel / Back | Circle | Return to previous state |
| Navigate | Left Stick / D-Pad | Move focus between cards/zones |
| Action Menu | Square | Open card options |
| End Turn | Triangle | Quick access |
| Inspect Card | R2 (hold) | Show enlarged card view |

### 4.4 Steam Deck

| Action | Input | Notes |
|--------|-------|-------|
| Select / Confirm | A | Same as Xbox |
| Cancel / Back | B | Same as Xbox |
| Navigate | Left Stick / D-Pad | Same as Xbox |
| Touch Select | Touch Screen | Tap card to select |
| Touch Target | Touch Screen | Tap zone to target |

---

## 5. Visual Feedback System

### 5.1 Focus States

| Element | Idle | Focused | Selected |
|---------|------|---------|----------|
| Card in hand | Normal | Lifted +0.2Y, white glow | Lifted +0.5Y, gold glow, tilt 15 degrees |
| Drop Zone | Normal | Highlighted | Strong glow, ghost preview |
| UI Button | Normal | Border highlight | Pressed state |

### 5.2 Animation Durations

| Transition | Duration | Description |
|------------|----------|-------------|
| Focus Card | 0.15s | Lift up, glow on |
| Select Card | 0.2s | Higher lift, tilt, stronger glow |
| Ghost Preview | 0.1s | Semi-transparent card appears |
| Confirm Play | 0.3s | Card flies to target |
| Cancel Return | 0.2s | Card returns to hand |

---

## 6. Component Architecture

### Core Components

1. **InputManager** - Autoload singleton, coordinates all input
2. **IInputHandler** - Interface for device-specific handlers
3. **MouseKeyboardHandler** - Mouse and keyboard input
4. **GamepadHandler** - Xbox/PS5 controller input
5. **CardSelectionController** - Selection state machine
6. **FocusNavigator** - Navigation logic between cards/zones
7. **CardVisualState** - Visual feedback (lift, glow, tilt)
8. **TargetZoneHighlighter** - Zone highlighting and ghost preview

---

## 7. Implementation Phases

### Phase 1: Core Input Abstraction
- Create IInputHandler interface
- Implement MouseKeyboardHandler
- Implement GamepadHandler
- Create InputManager autoload
- Test: Input events fire correctly

### Phase 2: Selection State Machine
- Create CardSelectionController
- Implement state transitions
- Connect to InputManager
- Test: Can select/deselect cards

### Phase 3: Visual Feedback
- Create CardVisualState component
- Implement focus/selected animations
- Create TargetZoneHighlighter
- Test: Visual states change correctly

### Phase 4: Controller Navigation
- Implement focus order system
- Add navigation logic
- Test: Can navigate UI with controller

### Phase 5: Haptics and Polish
- Add haptic feedback events
- Platform-specific rumble
- Test on target devices

---

## 8. File Structure

```
Scripts/Input/
├── IInputHandler.cs
├── InputManager.cs
├── InputEvent.cs
├── MouseKeyboardHandler.cs
├── GamepadHandler.cs
├── CardSelectionController.cs
├── FocusNavigator.cs
├── CardVisualState.cs
└── TargetZoneHighlighter.cs
```

---

## 9. Next Steps

1. Review this design
2. Update GameBoard design doc with selection-based interaction
3. Implement Phase 1: Core Input Abstraction
4. Test and iterate
