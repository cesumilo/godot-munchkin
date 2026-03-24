# MVP Implementation Plan: First Gameplay Loop

## Goal

A player can complete one full turn cycle on screen, driven by mock server messages via WebSocket protocol. The flow: Login → Lobby → Start Game → Game Board → Open Door → Combat (if monster) or Loot Room → Charity → End Turn → Next player's turn.

---

## Inventory: What Exists Today

| System | Status | Key Files |
|--------|--------|-----------|
| Auth (login, JWT) | Done | `Scripts/UI/Main.cs`, `Scenes/Main/Main.tscn` |
| Lobby (list/create/join/start, WS connect) | Done | `Scripts/UI/Lobby.cs`, `Scenes/Lobby/Lobby.tscn` |
| WebSocketClient | Done | `Scripts/Networking/WebSocketClient.cs` — poll, send, receive, reconnect, message queue |
| NetworkManager (autoload) | Done | `Scripts/Networking/NetworkManager.cs` — singleton, `ConnectToLobby()`, `SendPlayerAction()`, `SendPlayCard()` |
| MessageProtocol | Done | `Scripts/Networking/MessageProtocol.cs` — all message types, builders, parsers, `WebSocketMessage.ToJson()` |
| Card data model | Done | `Scripts/Cards/CardData.cs`, `ItemCardData.cs`, `MonsterCardData.cs`, `CurseCardData.cs`, `RaceCardData.cs`, `ClassCardData.cs`, `ActionCardData.cs`, `Enums.cs` |
| CardFactory (autoload) | Done | `Scripts/Cards/CardFactory.cs` — loads `.tres` from `Resources/Cards/Definitions/`, lookup by ID/type |
| CardVisual (simple 3D) | Done | `Scripts/Cards/CardVisual.cs` — color-coded mesh + labels |
| PlayerState | Done | `Scripts/GameState/PlayerState.cs` — level, race, class, equip/unequip, hand, slot validation |
| GameStateMachine | Done | `Scripts/GameState/GameStateMachine.cs` — phase enum, transitions, combat state, player management |
| GameStateManager (autoload) | Done | `Scripts/GameState/GameStateManager.cs` — bridges WS messages → state machine, parses `GAME_STATE`/`TURN_PHASE_CHANGE`/`COMBAT_START`/`ERROR` |
| EquipmentPanel + DragDrop | Done | `Scripts/UI/EquipmentPanel.cs`, `Scripts/UI/DragDropHandler.cs` — 3D equipment drag-and-drop |
| Card3D plugin (GDScript) | Available | `addons/card_3d/` — Card3D, CardCollection3D, DragController, layouts (Line/Fan/Pile) |
| Sample .tres cards | 12 cards | 2 monsters, 2 items, 1 curse, 2 races, 2 classes, 2 actions, 1 test card |
| Game Board scene | Missing | No `Scenes/Game/` directory exists |

## Card3D Plugin API (GDScript — called from C# via Godot node API)

### Card3D (`addons/card_3d/scenes/card_3d.tscn`)
- Scene tree: `Card3D(Node3D)` > `CardMesh(Node3D)` > `CardBackMesh(MeshInstance3D)` + `CardFrontMesh(MeshInstance3D)` + `StaticBody3D` > `CollisionShape3D`
- Card size: `PlaneMesh(2.5, 3.5)`
- Key methods: `animate_to_position(pos, duration)`, `set_hovered()`, `remove_hovered()`, `disable_collision()`, `enable_collision()`
- Property: `face_down: bool` — sets `$CardMesh.rotation.y = PI`
- Set front material: `$CardMesh/CardFrontMesh.set_surface_override_material(0, material)`
- Signals: `card_3d_mouse_down`, `card_3d_mouse_up`, `card_3d_mouse_over`, `card_3d_mouse_exit`

### CardCollection3D (`addons/card_3d/scenes/card_collection_3d.tscn`)
- Methods: `append_card(card)`, `insert_card(card, idx)`, `remove_card(idx)` → `Card3D`, `pop_card()`, `remove_all()`, `move_card(card, new_idx)`, `apply_card_layout()`
- Properties: `card_layout_strategy` (LineCardLayout/FanCardLayout/PileCardLayout), `drag_strategy`, `cards: Array[Card3D]`, `card_indicies: Dictionary`
- Signals: `card_clicked(card)`, `card_selected(card)`, `card_added(card)`, `card_moved(card, from, to)`

### DragController (`addons/card_3d/scripts/drag_controller.gd`)
- Parent of CardCollection3D children. Auto-discovers them in `_ready()`.
- Methods: `add_card_collection(collection)`, `remove_card_collection(collection)`
- Signals: `drag_started(card)`, `drag_stopped(card)`, `card_moved(card, from_coll, to_coll, from_idx, to_idx)`
- Property: `card_drag_plane: Plane` — default `Plane(Vector3(0,0,1), 1.5)` — the XY plane at Z=1.5

### How to extend Card3D (from `example_battle/`)
Create a `.tscn` that instances `card_3d.tscn`, override the script, add child nodes to `CardMesh` (e.g., `Label3D`). Set properties via `@export` setters that manipulate `$CardMesh/CardFrontMesh` material and labels.

### How cards are created dynamically
```gdscript
var scene = load("res://path/to/card.tscn")
var card = scene.instantiate()
card.Set("property", value)
collection.append_card(card)
```

---

## Architecture Decision: C# ↔ GDScript Bridge

The Card3D plugin is GDScript. Our game code is C#. We cannot inherit from Card3D in C#. The approach:

1. Create `Scenes/Game/MunchkinCard3D.tscn` — a scene that instances `card_3d.tscn` and overrides the script with a GDScript file `Scripts/Cards/munchkin_card_3d.gd` (following the BattleCard3D pattern).
2. Create `Scripts/Cards/MunchkinCard3D.cs` — a C# helper class (not a node script) with static methods to instantiate and configure MunchkinCard3D scenes. Interacts with the GDScript node via `node.Set()`, `node.Call()`, `node.GetNode()`.

This is exactly how `example_battle/` works: `BattleCard3D.gd` extends `Card3D`, `battle.gd` creates instances and sets properties.

---

## Step 1: MunchkinCard3D — Card Visual for Munchkin

### Purpose
A reusable card visual that displays any Munchkin card (monster, item, curse, race, class, action) using the Card3D plugin, with color-coded backgrounds and text labels.

### Files to Create

#### `Scripts/Cards/munchkin_card_3d.gd` (GDScript — extends Card3D)
```gdscript
class_name MunchkinCard3D
extends Card3D

@export var card_id: String = ""
@export var card_name: String = "":
    set(v):
        card_name = v
        if has_node("CardMesh/CardNameLabel"):
            $CardMesh/CardNameLabel.text = v

@export var card_type: String = "":  # "MONSTER", "ITEM", "CURSE", "RACE", "CLASS", "ACTION"
    set(v):
        card_type = v
        _update_card_color()

@export var card_info: String = "":
    set(v):
        card_info = v
        if has_node("CardMesh/CardInfoLabel"):
            $CardMesh/CardInfoLabel.text = v

func _update_card_color():
    # Create StandardMaterial3D with color based on card_type
    # Set on $CardMesh/CardFrontMesh via set_surface_override_material(0, mat)
    # Colors: Monster=red(0.9,0.3,0.3), Item=green(0.3,0.8,0.3), Curse=purple(0.7,0.2,0.7),
    #         Race=blue(0.3,0.5,0.9), Class=yellow(0.9,0.8,0.3), Action=orange(0.9,0.6,0.2)
```

#### `Scenes/Game/MunchkinCard3D.tscn` (scene file — instances `card_3d.tscn`)
- Root: instance of `res://addons/card_3d/scenes/card_3d.tscn`
- Override script to `res://Scripts/Cards/munchkin_card_3d.gd`
- Add child `Label3D` "CardNameLabel" under `CardMesh`:
  - Position: `(0, 0.5, 0.001)` (top area of card front)
  - `font_size = 40`, `modulate = Color(1,1,1)`, `autowrap_mode = 3`, `width = 350`
- Add child `Label3D` "CardInfoLabel" under `CardMesh`:
  - Position: `(0, -0.5, 0.001)` (bottom area of card front)
  - `font_size = 30`, `modulate = Color(1,1,1)`, `autowrap_mode = 3`, `width = 350`

#### `Scripts/Cards/MunchkinCardHelper.cs` (C# static helper)
```csharp
public static class MunchkinCardHelper
{
    private static PackedScene _cardScene;
    
    public static Node3D CreateCard(CardData cardData)
    {
        // Load scene once: "res://Scenes/Game/MunchkinCard3D.tscn"
        // Instantiate
        // Set properties via node.Set("card_id", cardData.Id), etc.
        // Build card_info string based on card type:
        //   Monster: "Level {level}\n{treasures} Treasures"
        //   Item: "+{bonus}\n{slot}\n{goldValue}g"
        //   Curse: "{effect}"
        //   Race: "{race}"
        //   Class: "{class}"
        //   Action: "{playableWhen}"
        // Set card_type to cardData.Type.ToString().ToUpper()
        // Set card_name to cardData.Name
        // Return the Node3D
    }
    
    public static Node3D CreateCardById(string cardId)
    {
        // Lookup via CardFactory.Instance.GetCardById(cardId)
        // Call CreateCard(cardData)
    }
    
    public static Node3D CreateFaceDownCard()
    {
        // Instantiate card scene
        // Set face_down = true via node.Set("face_down", true)
        // Return
    }
    
    // Extract card_id from a MunchkinCard3D node
    public static string GetCardId(Node3D cardNode)
    {
        return (string)cardNode.Get("card_id");
    }
}
```

### Test Procedure
1. `dotnet build` from project root — 0 errors
2. `dotnet csharpier format .`
3. Create test scene `Scenes/Test/CardVisualTest.tscn`:
   - `Camera3D` at `(0, 0, 9)`, looking at origin
   - `DirectionalLight3D`
   - C# script that creates cards using `MunchkinCardHelper.CreateCard()` for each card type (goblin, broad_sword, lose_headgear, elf, warrior, potion_studliness) and positions them in a row
4. Run scene → see 6 cards with correct colors and text labels
5. Verify face-down card shows card back

---

## Step 2: MockServer — Client-Side Game Logic Simulator

### Purpose
Simulate server WebSocket responses so we can develop the full UI without the real server. Intercepts outgoing messages from NetworkManager and injects responses into WebSocketClient's message handler.

### Architecture
MockServer is a C# class (not a Node) that:
1. Gets called when `NetworkManager.UseMockServer == true`
2. Intercepts client messages (`PLAYER_ACTION`, `COMBAT_RESPONSE`, `PLAY_CARD`, etc.)
3. Maintains a simplified game state: players, deck, phase, combat
4. Generates server response messages (`GAME_STATE`, `TURN_PHASE_CHANGE`, `COMBAT_START`, `COMBAT_RESOLUTION`)
5. Feeds responses back through `WebSocketClient.MessageReceived` event (simulating server messages)

### Files to Create

#### `Scripts/Networking/MockServer.cs`
```csharp
public partial class MockServer
{
    // State
    private List<PlayerState> _players = new();
    private List<string> _dungeonDeck = new();
    private List<string> _treasureDeck = new();
    private int _activePlayerIndex = 0;
    private string _currentPhase = "OPEN_DOOR";
    private string _drawnCardId = null;  // card drawn during Open Door
    private bool _combatActive = false;
    private string _combatMonsterId = null;
    
    // Events — MockServer fires these, NetworkManager routes them
    public event Action<string, Godot.Collections.Dictionary> OnServerMessage;
    
    // Initialize with 3 mock players and full decks
    public void Initialize(string localPlayerId)
    {
        // Create 3 players (local player + 2 bots)
        // Build dungeon deck from all CardFactory dungeon cards (repeat IDs to fill 95 cards)
        // Build treasure deck from all CardFactory treasure cards (repeat IDs to fill 73 cards)
        // Shuffle both decks
        // Deal 4 dungeon + 4 treasure to each player
        // Set phase to OPEN_DOOR, active player = 0
        // Emit initial GAME_STATE
    }
    
    // Process incoming client message
    public void ProcessMessage(string messageType, Godot.Collections.Dictionary data)
    {
        switch (messageType)
        {
            case "JOIN_GAME":
                HandleJoinGame(data);
                break;
            case "PLAYER_ACTION":
                HandlePlayerAction(data);
                break;
            case "COMBAT_RESPONSE":
                HandleCombatResponse(data);
                break;
            case "PLAY_CARD":
                HandlePlayCard(data);
                break;
        }
    }
    
    private void HandleJoinGame(data)
    {
        // Emit GAME_STATE with full state
    }
    
    private void HandlePlayerAction(data)
    {
        string action = (string)data["action"];
        switch (action)
        {
            case "OPEN_DOOR":
                // Draw top card from dungeon deck
                // If monster: emit TURN_PHASE_CHANGE with drawn_card + combat_triggered=true,
                //             then emit COMBAT_START
                // If curse: apply effect, emit TURN_PHASE_CHANGE with drawn_card + combat_triggered=false
                // If other: add to hand, emit TURN_PHASE_CHANGE with drawn_card + combat_triggered=false
                break;
            case "LOOK_FOR_TROUBLE":
                // Client must also send PLAY_CARD with monster card_id
                // For now: emit ERROR if no monster card provided
                break;
            case "LOOT_ROOM":
                // Draw top dungeon card face-down (add to hand)
                // Emit TURN_PHASE_CHANGE with phase=CHARITY
                // Emit PLAYER_UPDATE with new hand
                break;
            case "END_TURN":
                // Advance to next player
                // Emit GAME_STATE with new active player and phase=OPEN_DOOR
                break;
        }
    }
    
    private void HandleCombatResponse(data)
    {
        string response = (string)data["response"];
        switch (response)
        {
            case "FLEE":
                // Roll d6 (random 1-6)
                // >= 5: flee success → emit COMBAT_RESOLUTION with result=DEFEAT + no penalty
                // < 5: flee fail → emit COMBAT_RESOLUTION with result=DEFEAT + penalty from monster
                break;
            default:
                // For MVP: auto-resolve combat
                // Compare player force vs monster level
                // player force = player.Level + sum of equipped item bonuses
                // If player force > monster level: VICTORY
                // Else: DEFEAT (auto-flee)
                // Emit COMBAT_RESOLUTION
                // Then advance phase to CHARITY
                break;
        }
    }
    
    // Helper: build GAME_STATE dictionary matching PROTOCOL.md format
    private Godot.Collections.Dictionary BuildGameState() { ... }
    
    // Helper: build TURN_PHASE_CHANGE dictionary
    private Godot.Collections.Dictionary BuildTurnPhaseChange(
        string playerId, string phase, string drawnCard, bool combatTriggered) { ... }
    
    // Helper: build COMBAT_START dictionary
    private Godot.Collections.Dictionary BuildCombatStart(
        string monsterCardId, int playerForce) { ... }
    
    // Helper: build COMBAT_RESOLUTION dictionary
    private Godot.Collections.Dictionary BuildCombatResolution(
        string result, int playerForce, int monsterForce,
        Godot.Collections.Dictionary rewards,
        Godot.Collections.Dictionary penalty) { ... }
    
    // Helper: emit message via event
    private void EmitMessage(string type, Godot.Collections.Dictionary data)
    {
        OnServerMessage?.Invoke(type, data);
    }
}
```

### Files to Modify

#### `Scripts/Networking/NetworkManager.cs` — Add mock mode:
```csharp
// New field
[Export]
public bool UseMockServer { get; set; } = false;
private MockServer _mockServer;

// In _Ready(), after WebSocketClient init:
if (UseMockServer)
{
    _mockServer = new MockServer();
    _mockServer.OnServerMessage += OnMockServerMessage;
    GD.Print("[NetworkManager] Mock server enabled");
}

// New method: route mock messages to WebSocketClient's MessageReceived event
private void OnMockServerMessage(string type, Godot.Collections.Dictionary data)
{
    // Call WebSocketClient.MessageReceived directly
    // This simulates receiving a message from the real server
    WebSocketClient.InjectMessage(type, data);
}

// Override SendPlayerAction to route through mock:
public bool SendPlayerAction(MessageProtocol.PlayerActionType action)
{
    if (UseMockServer && _mockServer != null)
    {
        var data = new Godot.Collections.Dictionary
        {
            ["action"] = action.ToString(),
        };
        _mockServer.ProcessMessage("PLAYER_ACTION", data);
        return true;
    }
    // ... existing real send logic
}

// Similarly override SendPlayCard, add SendCombatResponse:
public bool SendCombatResponse(MessageProtocol.CombatResponseType response, string cardId = null)
{
    if (UseMockServer && _mockServer != null)
    {
        var data = new Godot.Collections.Dictionary { ["response"] = response.ToString() };
        if (cardId != null) data["card_id"] = cardId;
        _mockServer.ProcessMessage("COMBAT_RESPONSE", data);
        return true;
    }
    var message = MessageProtocol.CreateCombatResponse(response, cardId);
    return WebSocketClient.SendMessage(message.ToJson());
}

// Initialize mock server when game starts
public void InitializeMockGame()
{
    if (UseMockServer && _mockServer != null)
    {
        _mockServer.Initialize(Main.PlayerId);
    }
}
```

#### `Scripts/Networking/WebSocketClient.cs` — Add message injection for mock:
```csharp
// New public method to inject messages (called by NetworkManager for mock)
public void InjectMessage(string messageType, Godot.Collections.Dictionary data)
{
    GD.Print($"[WebSocketClient] Injected mock message: {messageType}");
    MessageReceived?.Invoke(messageType, data);
}
```

### Message Format Compliance
All mock messages must match `PROTOCOL.md` exactly. Key formats:

#### GAME_STATE (emitted on initialize and END_TURN):
```json
{
  "type": "GAME_STATE",
  "data": {
    "game_id": "mock-game",
    "players": [
      {"id": "player1", "name": "You", "level": 1, "race": null, "class": null,
       "sex": "MALE", "hand": ["card_id_1", ...], "equipment": [], "is_dead": false},
      ...
    ],
    "current_turn": {"player_id": "player1", "phase": "OPEN_DOOR"},
    "combat": null,
    "decks": {"dungeon_remaining": 83, "treasure_remaining": 61},
    "winner": null
  }
}
```

#### TURN_PHASE_CHANGE (emitted on OPEN_DOOR action):
```json
{
  "type": "TURN_PHASE_CHANGE",
  "data": {
    "player_id": "player1",
    "phase": "OPEN_DOOR",
    "result": {"drawn_card": "monster_goblin_001", "combat_triggered": true}
  }
}
```

#### COMBAT_START (emitted when monster drawn or Look for Trouble):
```json
{
  "type": "COMBAT_START",
  "data": {
    "monster": {"card_id": "monster_goblin_001", "level": 1, "treasures": 1, "levels_gained": 1},
    "player_force": 1,
    "interaction_window_duration": 30
  }
}
```

#### COMBAT_RESOLUTION (emitted after fight/flee):
```json
{
  "type": "COMBAT_RESOLUTION",
  "data": {
    "result": "VICTORY",
    "player_force": 5,
    "monster_force": 1,
    "rewards": {"treasures": ["item_broad_sword_001"], "levels_gained": 1},
    "penalty": null
  }
}
```

### Test Procedure
1. `dotnet build` — 0 errors
2. `dotnet csharpier format .`
3. In Godot, select the NetworkManager autoload node → set `UseMockServer = true` in inspector
4. Create a minimal test: In `GameStateManager.TestWithMockData()`, instead of building mock data manually, call `NetworkManager.Instance.InitializeMockGame()`
5. Verify in console: `[WebSocketClient] Injected mock message: GAME_STATE` with full player data
6. Verify: `[GameStateManager] Updated state: 3 players, phase: OpenDoor`

---

## Step 3: GameBoard Scene — Main Game View

### Purpose
The central game scene that players see after starting a game. Contains the card table, player hand, play area, UI overlay with action buttons, combat panel, and player info.

### Scene Tree Structure
```
GameBoard (Node3D, script: GameBoard.cs)
├── DirectionalLight3D (position: 0, 0, 40)
├── Camera3D (position: 0, -2, 12, rotation: ~15° down toward table)
├── Table (MeshInstance3D — PlaneMesh 30x30, green material, rotated to XY plane)
├── DragController (DragController.gd)
│   ├── PlayerHand (CardCollection3D, fan layout, position: 0, -4, 3)
│   │   └── [cards added dynamically]
│   └── PlayArea (CardCollection3D, line layout, position: 0, 1, 3)
│       └── [drawn card shown here]
├── DungeonDeck (Node3D, position: -4, 2, 3)
│   └── [visual face-down card stack]
├── TreasureDeck (Node3D, position: 4, 2, 3)
│   └── [visual face-down card stack]
└── UI (CanvasLayer)
    ├── PhaseLabel (Label — top center, shows current phase)
    ├── TurnIndicator (Label — top center below phase, shows "Your turn" or "Player X's turn")
    ├── ActionPanel (VBoxContainer — right side)
    │   ├── OpenDoorButton (Button)
    │   ├── LookForTroubleButton (Button)
    │   ├── LootRoomButton (Button)
    │   └── EndTurnButton (Button)
    ├── CombatPanel (PanelContainer — center, hidden by default)
    │   ├── MonsterNameLabel (Label)
    │   ├── MonsterLevelLabel (Label)
    │   ├── PlayerForceLabel (Label)
    │   ├── VsLabel (Label — "VS")
    │   ├── MonsterForceLabel (Label)
    │   ├── FightButton (Button)
    │   ├── FleeButton (Button)
    │   └── ResultLabel (Label — hidden, shows VICTORY/DEFEAT)
    ├── PlayersPanel (VBoxContainer — left side)
    │   └── [PlayerInfoRow per player: name, level, race, class, hand count]
    └── GameLog (RichTextLabel — bottom, last 10 messages)
```

### Camera Setup
Following the Card3D plugin's example (`example_battle/battle.tscn`), the camera looks along -Z toward Z=0. Cards exist in the XY plane at various Z values. The `DragController.card_drag_plane` is set to `Plane(Vector3(0,0,1), z_value)` — cards move in XY while Z determines stacking.

However, our project uses Z as vertical (per AGENTS.md discovery). The Card3D example camera is at `(0, 0, 9)` looking down the -Z axis, with cards at Z ~2-3. We need to either:
- **(A)** Match the example's coordinate system for the game board (camera at Z=12, cards at Z=3, drag plane `Plane(0,0,1, 3)`)
- **(B)** Adapt to our Z-up system

**Decision**: Use option **(A)** for the game board — match the Card3D plugin's coordinate expectations exactly. The Card3D plugin layouts use X/Y axes for card positioning and Z for stacking. The existing equipment panel's Z-up system is a separate concern that doesn't conflict since it's a different scene.

### Files to Create

#### `Scripts/UI/GameBoard.cs`
```csharp
public partial class GameBoard : Node3D
{
    // Node references (found in _Ready via GetNode)
    private Label _phaseLabel;
    private Label _turnIndicator;
    private Button _openDoorButton;
    private Button _lookForTroubleButton;
    private Button _lootRoomButton;
    private Button _endTurnButton;
    private Control _combatPanel;
    private Label _monsterNameLabel;
    private Label _monsterLevelLabel;
    private Label _playerForceLabel;
    private Label _monsterForceLabel;
    private Button _fightButton;
    private Button _fleeButton;
    private Label _resultLabel;
    private VBoxContainer _playersPanel;
    private RichTextLabel _gameLog;
    
    // Card3D collections (GDScript nodes, accessed via Godot API)
    private Node3D _playerHandCollection;  // CardCollection3D
    private Node3D _playAreaCollection;    // CardCollection3D
    
    // State
    private GameStateManager _gsm;
    private NetworkManager _net;
    private bool _combatActive = false;
    private string _drawnCardId = null;
    
    // Card tracking
    private Dictionary<string, Node3D> _handCardNodes = new();  // card_id → MunchkinCard3D node
    
    public override void _Ready()
    {
        // 1. Find all UI nodes
        // 2. Find CardCollection3D nodes under DragController
        // 3. Connect button signals: _openDoorButton.Pressed += OnOpenDoorPressed, etc.
        // 4. Connect to GameStateManager events:
        //    _gsm.OnGameStateUpdated += OnGameStateUpdated
        //    _gsm.StateMachine.OnPhaseChanged += OnPhaseChanged
        //    _gsm.StateMachine.OnCombatPhaseChanged += OnCombatPhaseChanged
        //    _gsm.StateMachine.OnActivePlayerChanged += OnActivePlayerChanged
        // 5. Subscribe to specific WS message types for combat/phase details:
        //    _net.WebSocketClient.MessageReceived += OnNetworkMessage
        // 6. Initialize mock server if enabled:
        //    _net.InitializeMockGame()
        // 7. Update UI from current state
    }
    
    // === Network Message Handler (for detailed data beyond GameStateManager) ===
    private void OnNetworkMessage(string type, Godot.Collections.Dictionary data)
    {
        switch (type)
        {
            case MessageProtocol.TURN_PHASE_CHANGE:
                HandleTurnPhaseChange(data);
                break;
            case MessageProtocol.COMBAT_START:
                HandleCombatStart(data);
                break;
            case MessageProtocol.COMBAT_RESOLUTION:
                HandleCombatResolution(data);
                break;
        }
    }
    
    // === Phase Change Handler ===
    private void HandleTurnPhaseChange(Godot.Collections.Dictionary data)
    {
        // Parse result.drawn_card — if not null, create MunchkinCard3D and add to PlayArea
        // Parse result.combat_triggered — if true, wait for COMBAT_START
        // Update phase label
        // Update action buttons visibility
        // Add to game log
    }
    
    // === Combat Handlers ===
    private void HandleCombatStart(Godot.Collections.Dictionary data)
    {
        // Show combat panel
        // Parse monster info: card_id, level, treasures
        // Show monster name/level (lookup via CardFactory)
        // Show player force vs monster level
        // Enable Fight/Flee buttons
        // Add to game log: "A {monster_name} (Level {level}) appears!"
    }
    
    private void HandleCombatResolution(Godot.Collections.Dictionary data)
    {
        // Parse result: VICTORY or DEFEAT
        // If VICTORY: show "Victory!" + rewards (levels gained, treasures)
        // If DEFEAT: show penalty info
        // Hide Fight/Flee buttons, show result
        // After 2s delay: hide combat panel, update action buttons for post-combat phase
        // Add to game log
    }
    
    // === Action Button Handlers ===
    private void OnOpenDoorPressed()
    {
        _net.SendPlayerAction(MessageProtocol.PlayerActionType.OPEN_DOOR);
        _openDoorButton.Disabled = true;
    }
    
    private void OnLookForTroublePressed()
    {
        // For MVP: just send the action
        // Full implementation: player selects a monster from hand first
        _net.SendPlayerAction(MessageProtocol.PlayerActionType.LOOK_FOR_TROUBLE);
    }
    
    private void OnLootRoomPressed()
    {
        _net.SendPlayerAction(MessageProtocol.PlayerActionType.LOOT_ROOM);
    }
    
    private void OnEndTurnPressed()
    {
        _net.SendPlayerAction(MessageProtocol.PlayerActionType.END_TURN);
    }
    
    private void OnFightPressed()
    {
        // Send combat response — server resolves
        _net.SendCombatResponse(MessageProtocol.CombatResponseType.PLAY_CARD);
        // For MVP: PLAY_CARD with no card = "just fight with current force"
        _fightButton.Disabled = true;
        _fleeButton.Disabled = true;
    }
    
    private void OnFleePressed()
    {
        _net.SendCombatResponse(MessageProtocol.CombatResponseType.FLEE);
        _fightButton.Disabled = true;
        _fleeButton.Disabled = true;
    }
    
    // === UI Update Methods ===
    private void UpdateActionButtons()
    {
        var phase = _gsm.StateMachine.CurrentPhase;
        bool isMyTurn = IsLocalPlayerActive();
        
        _openDoorButton.Visible = isMyTurn && phase == GameStateMachine.MainGamePhase.OpenDoor;
        _lookForTroubleButton.Visible = isMyTurn && phase == GameStateMachine.MainGamePhase.LookForTrouble;
        _lootRoomButton.Visible = isMyTurn &&
            (phase == GameStateMachine.MainGamePhase.LookForTrouble ||
             phase == GameStateMachine.MainGamePhase.LootRoom);
        _endTurnButton.Visible = isMyTurn &&
            (phase == GameStateMachine.MainGamePhase.Charity ||
             phase == GameStateMachine.MainGamePhase.TurnEnd);
        
        // Reset disabled state
        _openDoorButton.Disabled = false;
        _lookForTroubleButton.Disabled = false;
        _lootRoomButton.Disabled = false;
        _endTurnButton.Disabled = false;
    }
    
    private void UpdatePhaseLabel()
    {
        _phaseLabel.Text = _gsm.StateMachine.CurrentPhase.ToString();
    }
    
    private void UpdateTurnIndicator()
    {
        var active = _gsm.StateMachine.GetActivePlayer();
        if (active == null) return;
        
        _turnIndicator.Text = IsLocalPlayerActive()
            ? "Your Turn"
            : $"{active.PlayerName}'s Turn";
    }
    
    private void UpdatePlayersPanel()
    {
        // Clear and rebuild player info rows
        // For each player in StateMachine.Players:
        //   Show: Name | Level {n} | {Race} | {Class} | Hand: {count} cards
        //   Highlight active player
    }
    
    private void SyncHandCards()
    {
        // Compare _gsm.LocalPlayer.HandCardIds with _handCardNodes keys
        // Add new cards: MunchkinCardHelper.CreateCardById(id) → _playerHandCollection.Call("append_card", node)
        // Remove gone cards: find index in collection, call remove_card(idx), queue_free
        // This keeps the visual hand in sync with the state
    }
    
    private bool IsLocalPlayerActive()
    {
        var active = _gsm.StateMachine.GetActivePlayer();
        return active != null && active.PlayerId == _gsm.LocalPlayerId;
    }
    
    private void AddToGameLog(string message)
    {
        _gameLog.AppendText(message + "\n");
    }
}
```

#### `Scenes/Game/GameBoard.tscn` — Created in Godot editor with the scene tree described above. Key configuration:
- Camera3D: position `(0, -2, 12)`, rotation `(-15, 0, 0)` degrees (slightly tilted down)
- DragController: `card_drag_plane = Plane(0, 0, 1, 3.5)` (cards at Z~3)
- PlayerHand (CardCollection3D): position `(0, -4, 3)`, layout = `FanCardLayout` (`arc_angle_deg=60`, `arc_radius=7`)
- PlayArea (CardCollection3D): position `(0, 1, 3)`, layout = `LineCardLayout` (`max_width=10`, `card_width=2.5`)
- Both collections need `DragStrategy` with `can_select = false` initially (we don't want drag between collections for MVP — drag is future work)

### Files to Modify

#### `Scripts/UI/Lobby.cs` — Update `TransitionToGame()` at line 452:
```csharp
private void TransitionToGame()
{
    try
    {
        ShowStatus("Transitioning to game...");
        var gameScene = GD.Load<PackedScene>("res://Scenes/Game/GameBoard.tscn");
        if (gameScene != null)
        {
            GetTree().ChangeSceneToPacked(gameScene);
            GD.Print("[Lobby] Transitioning to GameBoard scene...");
        }
        else
        {
            GD.PrintErr("[Lobby] Failed to load GameBoard scene!");
            ShowStatus("Failed to load game. Check game files.", true);
        }
    }
    catch (Exception ex)
    {
        GD.PrintErr($"[Lobby] Error transitioning to game: {ex.Message}");
        ShowStatus($"Error: {ex.Message}", true);
    }
}
```

Also update `HandleStartGameResponse()` (line 748) to call `TransitionToGame()`:
```csharp
private void HandleStartGameResponse(string responseBody)
{
    GD.Print($"[Lobby] Start game response: {responseBody}");
    ShowStatus("Game started!", false);
    TransitionToGame();
}
```

And the `OnWebSocketConnectionStateChanged` handler (line 784): When in the lobby and connected, if the host presses start, `HandleStartGameResponse` triggers the transition. For non-host players, the server should send a `GAME_STATE` that also triggers transition — add this to `HandleGameStateMessage`:
```csharp
private void HandleGameStateMessage(Godot.Collections.Dictionary data)
{
    GD.Print("[Lobby] Received GAME_STATE — transitioning to game");
    TransitionToGame();
}
```

### GameStateMachine Modification
`Scripts/GameState/GameStateMachine.cs` — Relax `IsValidTransition` to accept server-driven phase jumps. The mock server might set phases directly. Change `TransitionToPhase` to use `SetPhase` when the phase comes from the server (via GameStateManager). The existing `SetPhase()` method already bypasses validation — this is fine for server-driven updates.

Add missing transition: OpenDoor → Charity (for when OPEN_DOOR draws a non-monster, non-combat card and player skips Look for Trouble, the server may jump directly to Charity):
```csharp
case MainGamePhase.OpenDoor:
    return to == MainGamePhase.Combat
        || to == MainGamePhase.LookForTrouble
        || to == MainGamePhase.LootRoom
        || to == MainGamePhase.Charity;  // ADD THIS
```

### GameStateManager Modifications
`Scripts/GameState/GameStateManager.cs` — Add events for combat:
```csharp
// New events (add after existing events at line 26)
public event Action<Godot.Collections.Dictionary> OnCombatStarted;
public event Action<Godot.Collections.Dictionary> OnCombatResolved;
public event Action<Godot.Collections.Dictionary> OnTurnPhaseChanged;

// Update HandleTurnPhaseChangeMessage (line 354) to emit event:
private void HandleTurnPhaseChangeMessage(Godot.Collections.Dictionary data)
{
    // ... existing parsing logic ...
    OnTurnPhaseChanged?.Invoke(data);  // ADD: emit raw data for GameBoard
    OnGameStateUpdated?.Invoke();
}

// Update HandleCombatStartMessage (line 376) to emit event:
private void HandleCombatStartMessage(Godot.Collections.Dictionary data)
{
    // ... existing logic ...
    OnCombatStarted?.Invoke(data);  // ADD
    OnGameStateUpdated?.Invoke();
}

// Update HandleCombatResolutionMessage (line 392) to parse and emit:
private void HandleCombatResolutionMessage(Godot.Collections.Dictionary data)
{
    GD.Print("[GameStateManager] Combat resolved");
    // Parse result
    if (data.ContainsKey("result"))
    {
        string result = (string)data["result"];
        // If VICTORY: update player level from rewards
        if (result == "VICTORY" && data.ContainsKey("rewards"))
        {
            var rewards = data["rewards"].AsGodotDictionary();
            if (rewards.ContainsKey("levels_gained"))
            {
                int levels = (int)(long)rewards["levels_gained"];
                var active = StateMachine.GetActivePlayer();
                if (active != null)
                    active.Level = Math.Min(active.Level + levels, 10);
            }
            // Add treasure card IDs to player's hand
            if (rewards.ContainsKey("treasures"))
            {
                var treasures = rewards["treasures"].AsGodotArray();
                var active = StateMachine.GetActivePlayer();
                foreach (var t in treasures)
                    active?.AddToHand((string)t);
            }
        }
    }
    // Transition out of combat
    StateMachine.SetPhase(GameStateMachine.MainGamePhase.Charity);
    OnCombatResolved?.Invoke(data);
    OnGameStateUpdated?.Invoke();
    OnLocalPlayerUpdated?.Invoke(LocalPlayer);
}
```

Also set `LocalPlayerId` from `Main.PlayerId` in `_Ready()`:
```csharp
// In _Ready(), after getting NetworkManager (line 39):
LocalPlayerId = Main.PlayerId;
```

### Test Procedure
1. `dotnet build` — 0 errors
2. `dotnet csharpier format .`
3. Set `NetworkManager.UseMockServer = true` in Godot inspector
4. Run the project (F5)
5. Login with any credentials (login still goes to real server)
6. Create/join lobby, start game
7. Should transition to GameBoard scene
8. Should see: empty hand area, play area, phase label "OpenDoor", "Your Turn" indicator, "Open Door" button
9. Console should show: `[GameStateManager] Updated state: 3 players, phase: OpenDoor`

---

## Step 4: Hand Cards Display

### Purpose
Show the local player's hand as Card3D fan at the bottom of the screen, updating whenever hand contents change.

### Implementation in GameBoard.cs
The `SyncHandCards()` method (described in Step 3) is the core. It's called:
- On `OnGameStateUpdated` — full state refresh
- On `OnLocalPlayerUpdated` — local player hand changed
- On `OnTurnPhaseChanged` — after drawing cards

Card creation: Use `MunchkinCardHelper.CreateCardById(cardId)` which returns a `Node3D` that's a `MunchkinCard3D` scene instance. Then call `_playerHandCollection.Call("append_card", cardNode)`.

Card removal: Iterate `_handCardNodes`, find IDs no longer in `LocalPlayer.HandCardIds`. For each, get index from collection via `collection.Get("card_indicies")` dictionary, call `collection.Call("remove_card", index)`, then `cardNode.QueueFree()`.

Ordering: Since `CardCollection3D` handles layout automatically via `apply_card_layout()`, we just need to add/remove cards and the fan layout repositions everything.

Face-down cards: Cards in hand are only visible to the local player. Other players' hand cards are not displayed as visuals (just the count in the info panel). All local hand cards are face-up.

### Test Procedure
1. `dotnet build` — 0 errors
2. `dotnet csharpier format .`
3. Run with `UseMockServer = true`
4. After game starts, should see 8 cards in a fan at the bottom (4 dungeon + 4 treasure from mock deal)
5. Each card shows name, type color, and info text
6. Cards should have hover animation (Card3D built-in)

---

## Step 5: Turn Flow — Phase Actions

### Purpose
Wire action buttons to send messages, handle responses, update the board.

### Flow: Open Door
1. Player clicks "Open Door" button
2. `GameBoard.OnOpenDoorPressed()` → `NetworkManager.SendPlayerAction(OPEN_DOOR)`
3. MockServer draws a card from dungeon deck
4. MockServer emits `TURN_PHASE_CHANGE` with `{drawn_card: "card_id", combat_triggered: true/false}`
5. `GameBoard.HandleTurnPhaseChange(data)`:
   - Parse `result.drawn_card` — look up via `CardFactory.Instance.GetCardById(id)`
   - Create `MunchkinCard3D` for the drawn card
   - Add to PlayArea collection: `_playAreaCollection.Call("append_card", cardNode)`
   - If `combat_triggered == true`: wait for `COMBAT_START` message
   - If `combat_triggered == false`:
     - If drawn card was a curse: log `"Curse: {effect}!"` (already applied by mock server)
     - Show "Look for Trouble" and "Loot Room" buttons
   - Update phase label

### Flow: Loot Room
1. Player clicks "Loot Room"
2. MockServer draws a dungeon card face-down, adds to player's hand
3. MockServer emits `TURN_PHASE_CHANGE` with `{phase: "CHARITY"}`
4. GameBoard:
   - `SyncHandCards()` picks up the new card
   - Clear PlayArea (remove drawn card visual)
   - If hand ≤ 5: auto-advance to END_TURN
   - If hand > 5: show "End Turn" button (charity auto-handled by mock for now — just discards excess)

### Flow: Look for Trouble (simplified for MVP)
1. Player clicks "Look for Trouble"
2. For MVP: just send `PLAYER_ACTION {LOOK_FOR_TROUBLE}`
3. MockServer: if player has a monster in hand, auto-pick the first one; otherwise send `ERROR`
4. Full implementation (future): player clicks a monster card in hand, which triggers a `PLAY_CARD` message
5. If monster played: MockServer emits `COMBAT_START`

### Flow: End Turn
1. Player clicks "End Turn"
2. MockServer:
   - If hand > 5: auto-discard to 5 (simplified charity for MVP)
   - Advance active player index
   - Emit `GAME_STATE` with next player as active, phase = `OPEN_DOOR`
3. GameBoard:
   - `OnGameStateUpdated` fires
   - Clear PlayArea
   - Update all UI: phase label, turn indicator, action buttons
   - `SyncHandCards()` for potentially updated hand
   - If it's a bot's turn: MockServer auto-plays bot turn after a delay, then emits `GAME_STATE` again with next player

### Bot Auto-Play in MockServer
For the MVP, when it's a non-local player's turn, MockServer should auto-play:
1. Open Door → draw card
2. If monster → auto-fight (compare forces, resolve)
3. If no monster → Loot Room
4. End Turn → advance to next player
5. Emit `GAME_STATE` after each bot turn

This should happen with a small delay (e.g., 1-2 seconds) so the UI can show "Player X's Turn" before it changes. Implement via `SceneTree.CreateTimer()` or `Task.Delay()` routed back to the main thread.

### Test Procedure
1. `dotnet build` — 0 errors
2. `dotnet csharpier format .`
3. Run with mock server
4. Click "Open Door" → drawn card appears in play area, console logs the card
5. If monster drawn: combat panel appears (tested in Step 6)
6. If non-monster drawn: "Look for Trouble" and "Loot Room" buttons appear
7. Click "Loot Room" → hand gains a card, "End Turn" button appears
8. Click "End Turn" → turn advances to next player, bot auto-plays, eventually returns to your turn
9. Verify game log shows all events

---

## Step 6: Combat UI

### Purpose
Show combat panel when a monster is encountered, allow Fight or Flee, display resolution.

### Combat Panel Behavior
On `COMBAT_START` received (`GameBoard.HandleCombatStart`):
1. Show `_combatPanel`
2. Parse monster data from message: `data["monster"]` → `card_id`, `level`, `treasures`, `levels_gained`
3. Look up monster name from `CardFactory.Instance.GetCardById<MonsterCardData>(cardId)`
4. Display: `_monsterNameLabel.Text = monster.Name`
5. Display: `_monsterLevelLabel.Text = $"Level: {monster.Level}"`
6. Display: `_playerForceLabel.Text = $"Your Force: {data["player_force"]}"`
7. Display: `_monsterForceLabel.Text = $"Monster Force: {monster.Level}"`
8. Enable Fight + Flee buttons
9. Hide result label
10. Add to game log: `"A {name} (Level {level}) appears! {treasures} treasures await."`

On Fight clicked:
1. Send `COMBAT_RESPONSE {response: "PLAY_CARD"}` (MVP: "fight" = submit with current force)
2. Disable both buttons

On Flee clicked:
1. Send `COMBAT_RESPONSE {response: "FLEE"}`
2. Disable both buttons

On `COMBAT_RESOLUTION` received (`GameBoard.HandleCombatResolution`):
1. Parse result: "VICTORY" or "DEFEAT"
2. Parse `player_force`, `monster_force`
3. If VICTORY:
   - `_resultLabel.Text = "VICTORY!"`
   - Parse `rewards.levels_gained` → log `"You gain {n} level(s)!"`
   - Parse `rewards.treasures` → log `"You receive {n} treasure(s)!"`
   - (Level update + hand update handled by GameStateManager)
4. If DEFEAT:
   - Parse penalty: `type` + `details`
   - `_resultLabel.Text = "DEFEAT! {penalty.details}"`
   - Log penalty
5. Show `_resultLabel`
6. After 2-second delay:
   - Hide combat panel
   - Clear PlayArea (remove monster card)
   - Update action buttons for post-combat (Charity/End Turn)
   - `SyncHandCards()` for any new treasure cards

### MockServer Combat Resolution Logic
```csharp
private void ResolveCombat(string response)
{
    var activePlayer = _players[_activePlayerIndex];
    var monster = CardFactory.Instance.GetCardById<MonsterCardData>(_combatMonsterId);
    int playerForce = activePlayer.Level + activePlayer.GetEquipmentBonus();
    int monsterForce = monster?.Level ?? 1;
    
    if (response == "FLEE")
    {
        int roll = new Random().Next(1, 7);  // 1-6
        if (roll >= 5)
        {
            // Flee success — no penalty
            EmitCombatResolution("DEFEAT", playerForce, monsterForce, null, null);
        }
        else
        {
            // Flee failure — apply penalty
            var penalty = new Godot.Collections.Dictionary
            {
                ["type"] = monster.FleePenalty.ToString().ToUpper(),
                ["details"] = $"Flee failed (rolled {roll}). {monster.FleePenalty}!"
            };
            EmitCombatResolution("DEFEAT", playerForce, monsterForce, null, penalty);
            // Apply penalty to player state
            ApplyFleePenalty(activePlayer, monster);
        }
    }
    else
    {
        // Fight
        if (playerForce > monsterForce)
        {
            // Victory (strictly greater per §8.4)
            int levelsGained = monster?.LevelsGained ?? 1;
            activePlayer.Level = Math.Min(activePlayer.Level + levelsGained, 10);
            
            // Draw treasure cards
            var treasureIds = new Godot.Collections.Array();
            int treasureCount = monster?.Treasures ?? 1;
            for (int i = 0; i < treasureCount && _treasureDeck.Count > 0; i++)
            {
                string tid = _treasureDeck[0];
                _treasureDeck.RemoveAt(0);
                activePlayer.AddToHand(tid);
                treasureIds.Add(tid);
            }
            
            var rewards = new Godot.Collections.Dictionary
            {
                ["levels_gained"] = levelsGained,
                ["treasures"] = treasureIds
            };
            EmitCombatResolution("VICTORY", playerForce, monsterForce, rewards, null);
            
            // Check win condition (§2)
            if (activePlayer.Level >= 10)
            {
                EmitGameOver(activePlayer.PlayerId);
                return;
            }
        }
        else
        {
            // Defeat (equal or less per §8.4) — forced flee
            int roll = new Random().Next(1, 7);
            if (roll >= 5)
            {
                EmitCombatResolution("DEFEAT", playerForce, monsterForce, null, null);
            }
            else
            {
                var penalty = new Godot.Collections.Dictionary
                {
                    ["type"] = monster.FleePenalty.ToString().ToUpper(),
                    ["details"] = $"Defeated and fled failed (rolled {roll}). {monster.FleePenalty}!"
                };
                EmitCombatResolution("DEFEAT", playerForce, monsterForce, null, penalty);
                ApplyFleePenalty(activePlayer, monster);
            }
        }
    }
    
    _combatActive = false;
    _combatMonsterId = null;
    // Advance phase to CHARITY
    _currentPhase = "CHARITY";
    EmitTurnPhaseChange(activePlayer.PlayerId, "CHARITY", null, false);
}
```

### Test Procedure
1. `dotnet build` — 0 errors
2. `dotnet csharpier format .`
3. Run with mock server
4. Click "Open Door" — if a monster is drawn (check console for card type):
   - Combat panel appears with monster name, level, forces
   - Fight and Flee buttons are enabled
5. Click "Fight":
   - If your force > monster level: "VICTORY!" shown, level increases, treasure cards added to hand
   - If your force ≤ monster level: "DEFEAT!" shown with flee result
6. Click "Flee" (restart if needed):
   - Random outcome shown (success or penalty)
7. After 2s: combat panel hides, phase advances
8. Verify game log shows all combat events
9. Verify level displayed in players panel updates after victory

---

## Step 7: End-to-End Polish + Multi-Turn Test

### Purpose
Ensure the full loop works across multiple turns, bot turns auto-play, and edge cases are handled.

### Tasks
1. **Bot auto-play**: When MockServer advances to a bot player's turn, auto-play their turn after a 1.5s delay:
   - Draw a card (Open Door)
   - If monster: auto-fight
   - If no monster: Loot Room
   - End Turn
   - Emit all intermediate messages so GameBoard shows "Bot Player's Turn" briefly
   - Then emit `GAME_STATE` with next player

2. **Charity phase handling**:
   - When phase is `CHARITY` and hand > 5: MockServer auto-discards excess cards (removes last cards from hand) for MVP
   - GameBoard shows "Discarding excess cards..." in log
   - Future: player picks which cards to discard

3. **PlayArea cleanup**: Clear play area cards when:
   - Turn ends (`END_TURN`)
   - Combat resolves
   - New turn starts

4. **Game Over detection**:
   - If any player reaches level 10 via combat victory, MockServer emits `GAME_STATE` with `winner` field set
   - GameBoard shows "GAME OVER! {player} wins!" overlay
   - All buttons disabled

5. **Deck empty handling**:
   - MockServer: if dungeon/treasure deck is empty, reshuffle the discard pile (per §13.2)
   - For MVP: just refill with repeated card IDs

6. **Reconnection note**: When `UseMockServer = false` and connecting to real server, the flow should work identically — GameStateManager already handles `GAME_STATE` messages the same way whether they come from mock or real server.

### Additional Card Definitions Needed
The mock server needs enough cards to fill 8-card hands for 3 players (24 cards minimum). Current `.tres` files only have 12 cards. We need to either:
- **(A)** Create more `.tres` card definition files (10+ more)
- **(B)** Have MockServer generate card IDs that don't exist in CardFactory and handle gracefully (show "Unknown Card" in visuals)

**Decision**: Option **(A)** — create at least 12 more card definitions to have ~24 total. Focus on items and monsters since those are the most gameplay-relevant:
- 4 more monsters (various levels 1-8)
- 6 more items (various slots, bonuses, some Big, some with restrictions)
- 2 more curses

These go in `Resources/Cards/Definitions/Monsters/`, `Items/`, `Curses/` as `.tres` files referencing the appropriate CardData subclass.

### Test Procedure (Full End-to-End)
1. `dotnet build` — 0 errors
2. `dotnet csharpier format .`
3. Run with `UseMockServer = true`
4. Login → Lobby → Create → Start
5. Turn 1: Click Open Door → handle result → Loot Room or Fight → End Turn
6. Bot turns: Watch bot players auto-play (visible in game log and turn indicator)
7. Turn 2: Your turn again → Open Door → different card drawn
8. Verify: Hand card count changes, levels update, player panel shows correct info
9. Combat test: Keep playing until you draw a monster. Fight it. Verify level up.
10. Multiple turns: Play 5+ turns without errors or crashes
11. Edge case: Level 9 player kills a monster that gives 1 level → should win at 10

---

## Dependency Order

1. **Step 1: MunchkinCard3D** (no dependencies)
2. **Step 2: MockServer** (depends on: CardFactory, PlayerState, MessageProtocol)
3. **Step 3: GameBoard Scene** (depends on: Step 1, Step 2, Card3D plugin)
4. **Step 4: Hand Cards Display** (depends on: Step 1, Step 3)
5. **Step 5: Turn Flow** (depends on: Step 2, Step 3, Step 4)
6. **Step 6: Combat UI** (depends on: Step 2, Step 3, Step 5)
7. **Step 7: Polish + Multi-Turn** (depends on: all above)

Steps 1 and 2 can be done in parallel. Steps 3 depends on both. Steps 4-7 are sequential.

---

## Files Summary

### New Files (7)
| File | Type | Purpose |
|------|------|---------|
| `Scripts/Cards/munchkin_card_3d.gd` | GDScript | Card3D extension with Munchkin card properties |
| `Scenes/Game/MunchkinCard3D.tscn` | Scene | Inherited scene from `card_3d.tscn` with labels |
| `Scripts/Cards/MunchkinCardHelper.cs` | C# | Static helper to create/configure card nodes from C# |
| `Scripts/Networking/MockServer.cs` | C# | Client-side game logic simulator |
| `Scenes/Game/GameBoard.tscn` | Scene | Main game board with table, collections, UI overlay |
| `Scripts/UI/GameBoard.cs` | C# | Game board controller |
| `Scenes/Test/CardVisualTest.tscn` | Scene | Test scene for MunchkinCard3D visuals |

### Modified Files (6)
| File | Change |
|------|--------|
| `Scripts/Networking/NetworkManager.cs` | Add `UseMockServer` export, MockServer field, mock routing, `InitializeMockGame()`, `SendCombatResponse()` |
| `Scripts/Networking/WebSocketClient.cs` | Add `InjectMessage()` public method |
| `Scripts/UI/Lobby.cs` | Update `TransitionToGame()` to load GameBoard, update `HandleStartGameResponse()` and `HandleGameStateMessage()` |
| `Scripts/GameState/GameStateManager.cs` | Add combat/phase events, set `LocalPlayerId` from `Main.PlayerId`, parse combat resolution |
| `Scripts/GameState/GameStateMachine.cs` | Add OpenDoor → Charity transition, relax validation for server-driven jumps |
| `Resources/Cards/Definitions/` | Add ~12 more `.tres` card definitions (monsters, items, curses) |

### Unchanged Files
All existing files not listed above remain untouched: `Main.cs`, `CardData.cs`, `ItemCardData.cs`, `MonsterCardData.cs`, `CurseCardData.cs`, `RaceCardData.cs`, `ClassCardData.cs`, `ActionCardData.cs`, `Enums.cs`, `CardFactory.cs`, `CardVisual.cs`, `PlayerState.cs`, `EquipmentPanel.cs`, `DragDropHandler.cs`, `MessageProtocol.cs`, `PROTOCOL.md`.