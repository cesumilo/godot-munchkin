# GameBoard Scene Design Document

> **Status**: Design review  
> **Goal**: Define complete architecture before implementation  
> **Dependencies**: MockServer (done), Card3D plugin (available), GameStateManager (done)

---

## 1. Overview

The GameBoard is the main gameplay scene that displays:
- **3D play area**: Table, decks, cards in play
- **Player hand**: Fan of cards at bottom of screen
- **UI overlay**: Turn info, action buttons, combat panel
- **Opponent views**: Side panels showing other players' states

**Entry point**: Transitioned to from Lobby when `GAME_STARTED` message received.

---

## 2. Input System (NEW)

**Pattern**: Selection-based interaction (not drag-and-drop)

**Flow**: Select Card → Choose Target → Confirm

**Supported Devices**:
- Mouse: Hover + Click
- Keyboard: Arrow keys + Enter/Escape
- Xbox/PS5: D-Pad/Stick + A/Cross (confirm), B/Circle (cancel)
- Steam Deck: Touch OR controller

**See**: `DESIGN_InputSystem.md` for complete specification

---

## 3. Scene Hierarchy

```
GameBoard (Node3D) - root
├── Camera3D
│   └── Position: (0, 5, 5), Looking at (0, 0, 0)
│   └── Projection: Perspective, FOV 60°
│
├── Environment (Node3D)
│   ├── Table (StaticBody3D)
│   │   └── Mesh: PlaneMesh(20, 15) - dark wood texture
│   │   └── Collision: for raycast detection
│   │
│   ├── TableEdge (StaticBody3D) - invisible walls to prevent cards falling off
│   │
│   └── Lighting
│       ├── DirectionalLight3D (sun) - from above at angle
│       └── Ambient light for visibility
│
├── DeckAreas (Node3D) - markers for card positions
│   ├── DungeonDeck (Marker3D) at (-4, 0.05, -2)
│   ├── DungeonDiscard (Marker3D) at (-2.5, 0.05, -2)
│   ├── TreasureDeck (Marker3D) at (4, 0.05, -2)
│   └── TreasureDiscard (Marker3D) at (2.5, 0.05, -2)
│
├── PlayAreas (Node3D)
│   ├── DoorCardSlot (Marker3D) at (0, 0.1, -1) - for opened door cards
│   ├── CombatArea (Marker3D) at (0, 0.1, -0.5) - for monster cards
│   └── EquipmentArea (Marker3D) per player - for equipped items
│
├── CardCollections (Node3D) - Card3D plugin containers
│   ├── PlayerHand (CardCollection3D) - fan layout at bottom
│   ├── DoorCardStack (CardCollection3D) - single card display
│   ├── CombatMonsters (CardCollection3D) - monster cards in combat
│   └── PlayerEquipment (CardCollection3D) - equipped items
│
├── DragController (Node) - from card_3d plugin
│   └── Manages all CardCollection3D children
│
└── CanvasLayer (UI Overlay - 2D on top of 3D)
    ├── GameUI (Control) - full screen
    │   ├── TopBar (HBoxContainer)
    │   │   ├── CurrentTurnLabel: "Player 1's Turn"
    │   │   ├── PhaseLabel: "OPEN_DOOR"
    │   │   └── TimerLabel: "1:45" (if enabled)
    │   │
    │   ├── LeftPanel (VBoxContainer)
    │   │   └── PlayerList
    │   │       └── PlayerStatusPanel[] (for each opponent)
│   │   │           ├── NameLabel: "Bot 1"
│   │   │           ├── LevelLabel: "Level 3"
│   │   │           └── CardCountLabel: "5 cards"
│   │   │
│   │   ├── RightPanel (VBoxContainer)
│   │   │   ├── CombatPanel (visible only during combat)
│   │   │   │   ├── MonsterCardDisplay (texture rect)
│   │   │   │   ├── ForceComparison (HBoxContainer)
│   │   │   │   │   ├── PlayerForce: "5"
│   │   │   │   │   ├── VSLabel: "vs"
│   │   │   │   │   └── MonsterForce: "3"
│   │   │   │   └── CombatActionButtons (HBoxContainer)
│   │   │   │       ├── FightButton
│   │   │   │       ├── FleeButton
│   │   │   │       └── AskHelpButton
│   │   │   │
│   │   │   └── AllyPanel (visible if you have an ally)
│   │   │
│   │   └── BottomBar (HBoxContainer)
│   │       ├── PlayerInfoSection
│   │       │   ├── PlayerName: "You"
│   │       │   ├── LevelBadge: "Level 1"
│   │       │   └── CombatBonus: "+0"
│   │       │
│   │       ├── ActionButtons (HBoxContainer)
│   │       │   ├── OpenDoorButton (active in OPEN_DOOR phase)
│   │       │   ├── LookForTroubleButton (active in LOOK_FOR_TROUBLE phase)
│   │       │   ├── LootRoomButton (active in LOOT_ROOM phase)
│   │       │   ├── EndTurnButton (active in CHARITY phase)
│   │       │   └── HelpButton (context-sensitive)
│   │       │
│   │       └── HandInfoLabel: "5 cards"
│   │
│   ├── MessageLog (RichTextLabel) - bottom left, fades after 5 seconds
│   │
│   └── PopupLayer (Control) - modals
│       ├── CardDetailPopup - enlarged card view on hover/click
│       ├── EquipmentChoicePopup - when equipping items
│       └── CharityPopup - when giving away cards
```

---

## 3. Component Responsibilities

### 3.1 GameBoardController.cs (Main controller)
**Attached to**: `GameBoard` (root Node3D)

**Responsibilities**:
- Initialize scene on `GAME_STARTED` message
- Subscribe to `GameStateManager.OnStateChanged` for updates
- Route game state changes to child managers
- Handle scene transitions (back to lobby on disconnect)

**Key Methods**:
```csharp
void InitializeFromGameState(Godot.Collections.Dictionary gameStateData);
void OnGameStateUpdate(GameStateMessage state);
void OnTurnPhaseChange(TurnPhaseChangeMessage phaseChange);
void OnCombatStart(CombatStartMessage combat);
void OnCombatResolution(CombatResolutionMessage resolution);
```

### 3.2 PlayerHandManager.cs
**Attached to**: `PlayerHand` CardCollection3D node

**Responsibilities**:
- Instantiate card visuals from hand card IDs
- Update hand when cards added/removed
- Handle card play (drag to play area or click)
- Sort/filter cards by type (monsters, items, actions)

**Key Methods**:
```csharp
void SetHand(List<string> cardIds);
void AddCard(string cardId);
void RemoveCard(string cardId);
void OnCardClicked(Card3D card);
void OnCardPlayed(string cardId, string targetSlot = null);
```

### 3.3 PlayAreaManager.cs
**Attached to**: `PlayAreas` Node3D

**Responsibilities**:
- Display door card when opened
- Display monster cards in combat
- Show played action cards temporarily
- Manage equipment areas for all players

**Key Methods**:
```csharp
void ShowDoorCard(string cardId);
void ShowCombatMonster(string cardId);
void ClearDoorCard();
void ClearCombatArea();
```

### 3.4 GameUIManager.cs
**Attached to**: `GameUI` Control node

**Responsibilities**:
- Update all UI labels (turn, phase, levels)
- Show/hide action buttons based on phase
- Manage combat panel visibility
- Display message log
- Update opponent status panels

**Key Methods**:
```csharp
void SetCurrentPlayer(string playerName);
void SetPhase(TurnPhase phase);
void UpdateActionButtons(TurnPhase phase, bool isMyTurn);
void ShowCombatPanel(bool show);
void ShowMessage(string message, float duration = 5f);
void UpdatePlayerList(List<PlayerState> players);
```

### 3.5 CombatPanelController.cs
**Attached to**: `CombatPanel` Control node

**Responsibilities**:
- Display current monster
- Show force comparison (player vs monster)
- Handle combat action buttons (Fight, Flee, Ask Help)
- Show alliance offers
- **Trigger dice roll animation for flee attempts**

**Key Methods**:
```csharp
void SetMonster(string cardId, int monsterForce);
void SetPlayerForce(int force);
void SetAlly(string allyName, int allyForce);
void OnFightClicked();
void OnFleeClicked(); // -> triggers DiceRoller.Roll()
void OnAskHelpClicked();
void OnFleeResult(int roll, bool success); // callback from dice
```

### 3.6 CardInteractionController.cs (NEW)
**Attached to**: GameBoard root or dedicated node

**Responsibilities**:
- Manage selection state machine (IDLE, CARD_SELECTED, TARGET_SELECTED, ANIMATING)
- Handle input from InputManager
- Coordinate between hand, play areas, and UI
- Trigger visual state changes on cards

**Key Methods**:
```csharp
void OnNavigate(Vector2 direction);        // D-pad/arrow keys
void OnSelectPressed();                     // Confirm
void OnCancelPressed();                     // Cancel
void FocusCard(Card3D card);               // Hover/focus
void SelectCard(Card3D card);              // Lift card, show targets
void FocusTarget(PlayZone zone);           // Highlight target
void ConfirmPlay(PlayZone zone);           // Animate and play
void ClearSelection();                      // Return to IDLE
```

### 3.7 CardVisualState.cs (NEW)
**Attached to**: Each Card3D instance

**Responsibilities**:
- Apply visual states: Idle, Focused, Selected
- Handle animations: lift, tilt, glow
- Manage ghost preview

**Key Methods**:
```csharp
void SetState(CardVisualState state);
void SetFocus(bool focused);
void SetSelected(bool selected);
void SetGhostPreview(bool visible);
```

### 3.8 PlayZoneHighlighter.cs (NEW)
**Attached to**: Each valid drop zone (Door slot, Combat area, Equipment slots)

**Responsibilities**:
- Show/hide highlight when card selected
- Display ghost preview of card
- Indicate valid/invalid target

**Key Methods**:
```csharp
void SetHighlighted(bool highlighted);
void SetGhostCard(string cardId);
void SetValid(bool valid);
```

---

## 4. Data Flow

### 4.1 Game Start Sequence
```
1. GAME_STARTED message received
   ↓
2. GameBoardController.InitializeFromGameState()
   - Parse players array
   - Parse current_turn (who's active, what phase)
   - Parse combat state (if any)
   - Parse decks
   ↓
3. Initialize child managers:
   - PlayerHandManager.SetHand(localPlayer.hand)
   - PlayAreaManager.ClearAll()
   - GameUIManager.SetCurrentPlayer(activePlayer.name)
   - GameUIManager.SetPhase(currentPhase)
   - GameUIManager.UpdatePlayerList(allPlayers)
   ↓
4. Scene ready for interaction
```

### 4.2 Turn Phase Change Sequence
```
1. TURN_PHASE_CHANGE message received
   ↓
2. GameBoardController.OnTurnPhaseChange()
   ↓
3. If phase == OPEN_DOOR and result.drawn_card:
   - PlayAreaManager.ShowDoorCard(cardId)
   - GameUIManager.ShowMessage("Drew: " + cardName)
   ↓
4. If phase == COMBAT:
   - CombatPanelController.SetMonster(...)
   - GameUIManager.ShowCombatPanel(true)
   ↓
5. GameUIManager.SetPhase(newPhase)
   - Update available action buttons
```

### 4.3 Card Play Sequence (Selection-Based)
```
1. Player navigates to card (mouse hover / controller D-pad / keyboard arrows)
   ↓
2. [Visual] Card lifts slightly (Focused state)
   ↓
3. Player presses Select (mouse click / A button / Enter)
   ↓
4. [State] CARD_SELECTED
   [Visual] Card lifts higher, valid target zones glow
   ↓
5. Player navigates to target zone (mouse hover / controller)
   ↓
6. [Visual] Ghost preview appears at target zone
   ↓
7. Player presses Select again to confirm
   ↓
8. [State] ANIMATING
   [Visual] Card animates from hand to target
   ↓
9. NetworkManager.SendPlayCard(cardId, targetZone)
   ↓
10. CARD_PLAY_RESULT received
   ↓
11. If success:
   - PlayerHandManager.RemoveCard(cardId)
   - PlayAreaManager.ShowCardInPlay(cardId)
   - GameUIManager.ShowMessage("Card played!")
   ↓
7. If fail:
   - GameUIManager.ShowMessage("Cannot play: " + error)
```

### 4.4 Combat Resolution Sequence
```
1. Player clicks "Fight" or "Flee" button
   ↓
2. CombatPanelController.OnFightClicked() / OnFleeClicked()
   ↓
3. NetworkManager.SendCombatResponse(FLEE or PLAY_CARD)
   ↓
4. (Server resolves combat)
   ↓
5. COMBAT_RESOLUTION received
   ↓
6. GameBoardController.OnCombatResolution()
   - If VICTORY: Show rewards, level up animation
   - If DEFEAT: Show penalty, flee animation
   ↓
7. GameUIManager.ShowCombatPanel(false)
8. Update player stats
```

---

## 5. Integration Points

### 5.1 Required Autoloads
- `NetworkManager` - send actions, receive messages
- `GameStateManager` - current game state
- `CardFactory` - card data lookup
- `GameLogger` - logging

### 5.2 Events to Subscribe
```csharp
// In _Ready():
GameStateManager.Instance.OnStateChanged += OnGameStateUpdate;
NetworkManager.Instance.WebSocketClient.MessageReceived += OnWebSocketMessage;

// Handle:
// - GAME_STATE
// - TURN_PHASE_CHANGE
// - COMBAT_START
// - COMBAT_RESOLUTION
// - CARD_PLAY_RESULT
// - PLAYER_UPDATE
// - ERROR
```

### 3.6 DiceRoller.cs
**Attached to**: Separate Node3D (e.g., `DiceAnchor` at table center)

**Responsibilities**:
- Instantiate 3D dice model when flee is attempted
- Animate dice roll with physics
- Determine result (1-6)
- Callback with result to CombatPanelController
- Clean up dice after animation

**Key Methods**:
```csharp
void Roll(System.Action<int> onComplete);
void ShowRollAnimation(int result);
void ClearDice();
```

### 3.7 OpponentEquipmentManager.cs
**Attached to**: `LeftPanel` or separate equipment areas

**Responsibilities**:
- Display equipped items for each opponent (small card visuals)
- Update when PLAYER_UPDATE message received
- Show count of carried items

**Key Methods**:
```csharp
void SetOpponentEquipment(string playerId, List<string> equippedCardIds);
void ClearOpponentEquipment(string playerId);
void UpdateFromPlayerState(PlayerState state);
```

### 3.8 MessageLogController.cs
**Attached to**: `MessageLog` RichTextLabel

**Responsibilities**:
- Append messages with timestamps
- Keep last 50 messages (scrollable)
- Auto-scroll to bottom on new message
- Fade old messages (optional)

**Key Methods**:
```csharp
void AddMessage(string message, string type = "info");
void Clear();
```

---

### 5.3 Card3D Plugin Integration
```csharp
// Load Card3D scene
var cardScene = GD.Load<PackedScene>("res://addons/card_3d/scenes/card_3d.tscn");
var card = cardScene.Instantiate<Card3D>();

// Set card data via method call (GDScript from C#)
card.Call("set_card_data", cardName, cardDescription, cardImage);

// Add to collection
cardCollection.Call("append_card", card);
```

---

## 6. UI State Machine

### 6.1 Action Button States
| Phase | isMyTurn | Buttons Active |
|-------|----------|----------------|
| OPEN_DOOR | true | Open Door |
| OPEN_DOOR | false | (none) |
| COMBAT (interaction) | true | Play Card, Ask Help |
| COMBAT (interaction) | false | Play Card, Offer Help |
| LOOK_FOR_TROUBLE | true | Look for Trouble, Loot Room |
| LOOT_ROOM | true | Loot Room |
| CHARITY | true | End Turn (when hand ≤ 5) |

### 6.2 Combat Panel States
| State | Visible Elements |
|-------|------------------|
| Hidden | (combat not active) |
| Player Turn | Monster, Forces, Fight, Flee, Ask Help |
| Interaction Window | + timer countdown |
| With Ally | + ally info, negotiated split |
| Resolving | Disable buttons, show "Resolving..." |

---

## 7. Implementation Steps (Suggested Order)

### Phase 1: Basic Layout (no interaction)
1. Create `Scenes/Game/GameBoard.tscn`
2. Add Camera3D at correct position
3. Add table mesh and lighting
4. Add DeckAreas markers
5. Add basic UI overlay (CanvasLayer with placeholder labels)
6. Test: Scene loads without errors

### Phase 2: Game State Integration
1. Create `GameBoardController.cs`
2. Handle GAME_STARTED message
3. Parse game state and log to console
4. Update UI labels from state
5. Test: Enter from lobby, see correct player name/phase

### Phase 3: Player Hand Display
1. Create `PlayerHandManager.cs`
2. Integrate Card3D plugin
3. Instantiate cards from hand data
4. Position in fan layout
5. Test: See 8 cards dealt at game start

### Phase 4: Play Area
1. Create `PlayAreaManager.cs`
2. Show door card when opened
3. Show/hide door card
4. Test: Click Open Door button, see card appear

### Phase 5: Action Buttons
1. Create `GameUIManager.cs`
2. Implement all action buttons
3. Enable/disable based on phase
4. Hook up to NetworkManager
5. Test: Full turn cycle with mock server

### Phase 6: Combat UI + Dice Roller
1. Create `CombatPanelController.cs`
2. Combat panel visibility
3. Force comparison display
4. Fight/Flee buttons
5. **Create `DiceRoller.cs` with 3D dice model**
6. **Trigger dice animation on flee attempt**
7. Show dice result and combat outcome
8. Test: Enter combat, flee, see dice roll, see result

### Phase 7: Opponent Display + Message Log
1. **Create `OpponentEquipmentManager.cs`**
2. **Show opponent equipped items in side panel**
3. **Create `MessageLogController.cs`**
4. **Implement scrollable message history (last 50)**
5. Test: See all opponents' gear, review message history

### Phase 8: Selection System Polish
1. **Create `CardInteractionController.cs`**
2. **Create `CardVisualState.cs`**
3. **Create `PlayZoneHighlighter.cs`**
4. Smooth animations between states
5. Haptic feedback integration
6. Sound effects (optional)

---

## 8. Design Decisions (Resolved)

| Question | Decision | Implementation Note |
|----------|----------|---------------------|
| Card3D Material | **Use plugin defaults** | No custom materials for MVP; rely on Card3D's built-in appearance |
| Hand Layout | **Dynamic based on card count** | Fan arc adjusts angle/spacing as cards are added/removed |
| Card Interaction | **Selection-based** | Select card → choose target → confirm (works for mouse AND controllers) |
| Combat Visualization | **3D dice roll animation** | Instantiate Dice3D model, roll physics, show result, then fade |
| Opponent Equipment | **Show equipped items visually** | Small equipment area per opponent showing their equipped cards |
| Message Log | **Scrollable history** | RichTextLabel with scroll, keep last 50 messages, timestamps |

---

## 9. File Checklist

### New Files to Create

#### Core GameBoard
- [ ] `Scenes/Game/GameBoard.tscn`
- [ ] `Scripts/UI/GameBoardController.cs`
- [ ] `Scripts/UI/PlayerHandManager.cs`
- [ ] `Scripts/UI/PlayAreaManager.cs`
- [ ] `Scripts/UI/GameUIManager.cs`
- [ ] `Scripts/UI/CombatPanelController.cs`
- [ ] `Scripts/UI/DiceRoller.cs`
- [ ] `Scenes/Game/Dice3D.tscn` (or use primitive Cube)
- [ ] `Scripts/UI/OpponentEquipmentManager.cs`
- [ ] `Scripts/UI/MessageLogController.cs`

#### Input System (NEW - Selection-based)
- [ ] `Scripts/Input/IInputHandler.cs`
- [ ] `Scripts/Input/InputManager.cs` (autoload)
- [ ] `Scripts/Input/MouseKeyboardHandler.cs`
- [ ] `Scripts/Input/GamepadHandler.cs`
- [ ] `Scripts/Input/CardInteractionController.cs`
- [ ] `Scripts/Input/CardVisualState.cs`
- [ ] `Scripts/Input/PlayZoneHighlighter.cs`

### Existing Files to Modify
- [ ] `Scripts/GameState/GameStateManager.cs` - ensure GAME_STARTED triggers scene change
- [ ] `Scripts/UI/Lobby.cs` - fix TransitionToGame() to load GameBoard

---

## 10. Testing Plan

### Test Case 1: Basic Load
1. Enable mock server
2. Login, create lobby, start game
3. **Expected**: GameBoard scene loads, shows table, UI visible

### Test Case 2: Hand Display
1. Start game
2. **Expected**: 8 cards visible in hand area

### Test Case 3: Open Door
1. Click "Open Door" button
2. **Expected**: Card appears in door slot, phase changes

### Test Case 4: Combat
1. Open door draws monster
2. **Expected**: Combat panel appears, monster visible

### Test Case 5: Bot Turn
1. End turn
2. **Expected**: Bot plays turn automatically, turn returns

---

## Decision Log

| Date | Decision | Rationale |
|------|----------|-----------|
| 2025-04-03 | Use Card3D plugin vs custom CardVisual | Plugin provides drag/drop, animations, layouts out of box |
| 2025-04-03 | CanvasLayer for UI vs 3D UI | CanvasLayer simpler for buttons/labels, pixel-perfect |
| 2025-04-03 | Static table mesh vs generated | Static simpler, can texture later |

---

**Ready for review!** Once approved, we proceed with Phase 1 implementation.
