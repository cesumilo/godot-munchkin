using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

/// <summary>
/// Manages game state by integrating network messages with GameStateMachine
/// Bridges between server messages and client-side state representation
/// </summary>
public partial class GameStateManager : Node
{
    // Singleton instance
    private static GameStateManager _instance;
    public static GameStateManager Instance => _instance;

    // Core state
    public GameStateMachine StateMachine { get; private set; }

    // Local player reference
    public PlayerState LocalPlayer { get; private set; }
    public string LocalPlayerId { get; private set; } = string.Empty;

    // Events
    public event Action OnGameStateUpdated;
    public event Action<PlayerState> OnLocalPlayerUpdated;
    public event Action<string> OnError;

    // Network integration
    private NetworkManager _networkManager;
    private WebSocketClient _webSocketClient;

    // Called when the node enters the scene tree
    public override void _Ready()
    {
        _instance = this;
        StateMachine = new GameStateMachine();

        // Get network manager
        _networkManager = GetNode<NetworkManager>("/root/NetworkManager");

        if (_networkManager == null)
        {
            GD.PrintErr("[GameStateManager] NetworkManager not found in autoloads!");
            return;
        }

        // Get WebSocket client from NetworkManager
        _webSocketClient = _networkManager.WebSocketClient;

        if (_webSocketClient == null)
        {
            GD.PrintErr("[GameStateManager] WebSocketClient not found!");
            return;
        }

        // Subscribe to WebSocket events
        _webSocketClient.MessageReceived += HandleNetworkMessage;
        _webSocketClient.ErrorOccurred += HandleConnectionError;

        GD.Print("[GameStateManager] Initialized");
    }

    // Clean up
    public override void _ExitTree()
    {
        if (_webSocketClient != null)
        {
            _webSocketClient.MessageReceived -= HandleNetworkMessage;
            _webSocketClient.ErrorOccurred -= HandleConnectionError;
        }

        _instance = null;
    }

    // Network message handler
    private void HandleNetworkMessage(string messageType, Godot.Collections.Dictionary data)
    {
        GD.Print($"[GameStateManager] Received message: {messageType}");

        switch (messageType)
        {
            case MessageProtocol.GAME_STATE:
                HandleGameStateMessage(data);
                break;

            case MessageProtocol.PLAYER_UPDATE:
                HandlePlayerUpdateMessage(data);
                break;

            case MessageProtocol.TURN_PHASE_CHANGE:
                HandleTurnPhaseChangeMessage(data);
                break;

            case MessageProtocol.COMBAT_START:
                HandleCombatStartMessage(data);
                break;

            case MessageProtocol.COMBAT_RESOLUTION:
                HandleCombatResolutionMessage(data);
                break;

            case MessageProtocol.ERROR:
                HandleErrorMessage(data);
                break;

            default:
                // Ignore other message types
                break;
        }
    }

    // Handle GAME_STATE message - full state sync
    private void HandleGameStateMessage(Godot.Collections.Dictionary data)
    {
        try
        {
            GD.Print("[GameStateManager] Processing GAME_STATE message");

            // Parse using MessageProtocol
            if (MessageProtocol.TryParseGameState(data, out var gameState))
            {
                UpdateFromServerGameState(gameState);
                OnGameStateUpdated?.Invoke();
            }
            else
            {
                GD.PrintErr("[GameStateManager] Failed to parse GAME_STATE message");
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GameStateManager] Error handling GAME_STATE: {ex.Message}");
        }
    }

    // Update state from server game state
    private void UpdateFromServerGameState(MessageProtocol.GameStateMessage serverState)
    {
        // Clear existing players
        StateMachine.Players.Clear();

        // Parse players from server data
        if (serverState.Players != null)
        {
            foreach (var playerData in serverState.Players)
            {
                if (playerData.VariantType == Variant.Type.Dictionary)
                {
                    var playerDict = playerData.AsGodotDictionary();
                    var player = ParsePlayerFromServer(playerDict);

                    if (player != null)
                    {
                        StateMachine.Players.Add(player);

                        // Check if this is the local player
                        // TODO: Get local player ID from authentication
                        if (LocalPlayerId == string.Empty || player.PlayerId == LocalPlayerId)
                        {
                            LocalPlayer = player;
                            LocalPlayerId = player.PlayerId;
                            OnLocalPlayerUpdated?.Invoke(player);
                        }
                    }
                }
            }
        }

        // Parse current turn
        if (serverState.CurrentTurn != null)
        {
            // TODO: Parse turn phase and active player
            if (serverState.CurrentTurn.ContainsKey("player_id"))
            {
                string activePlayerId = (string)serverState.CurrentTurn["player_id"];
                var activePlayer = StateMachine.Players.Find(p => p.PlayerId == activePlayerId);
                if (activePlayer != null)
                {
                    int index = StateMachine.Players.IndexOf(activePlayer);
                    StateMachine.SetActivePlayer(index);
                }
            }

            if (serverState.CurrentTurn.ContainsKey("phase"))
            {
                string phaseStr = (string)serverState.CurrentTurn["phase"];
                if (Enum.TryParse<MessageProtocol.TurnPhase>(phaseStr, out var turnPhase))
                {
                    // Map TurnPhase to MainGamePhase
                    var mainPhase = MapTurnPhaseToMainPhase(turnPhase);
                    StateMachine.SetPhase(mainPhase);
                }
            }
        }

        // Parse combat state
        if (serverState.Combat != null)
        {
            // TODO: Parse combat state
            GD.Print("[GameStateManager] Combat state received (not yet implemented)");
        }

        GD.Print(
            $"[GameStateManager] Updated state: {StateMachine.Players.Count} players, phase: {StateMachine.CurrentPhase}"
        );
    }

    // Parse player data from server format
    private PlayerState ParsePlayerFromServer(Godot.Collections.Dictionary playerData)
    {
        try
        {
            var player = new PlayerState();

            // Basic fields
            if (playerData.ContainsKey("id"))
                player.PlayerId = (string)playerData["id"];

            if (playerData.ContainsKey("name"))
                player.PlayerName = (string)playerData["name"];

            if (playerData.ContainsKey("level"))
                player.Level = (int)playerData["level"];

            // Race
            if (playerData.ContainsKey("race"))
            {
                string raceStr = (string)playerData["race"];
                if (Enum.TryParse<RaceType>(raceStr, true, out var race))
                    player.PrimaryRace = race;
            }

            // Class
            if (playerData.ContainsKey("class"))
            {
                string classStr = (string)playerData["class"];
                if (Enum.TryParse<ClassType>(classStr, true, out var playerClass))
                    player.PrimaryClass = playerClass;
            }

            // Sex
            if (playerData.ContainsKey("sex"))
            {
                string sexStr = (string)playerData["sex"];
                if (Enum.TryParse<SexType>(sexStr, true, out var sex))
                    player.Sex = sex;
            }

            // Hand cards
            if (
                playerData.ContainsKey("hand")
                && playerData["hand"].VariantType == Variant.Type.Array
            )
            {
                var handArray = playerData["hand"].AsGodotArray();
                foreach (var cardId in handArray)
                {
                    if (cardId.VariantType == Variant.Type.String)
                        player.AddToHand((string)cardId);
                }
            }

            // Equipment (simplified - server might send as arrays of card IDs)
            if (
                playerData.ContainsKey("equipment")
                && playerData["equipment"].VariantType == Variant.Type.Array
            )
            {
                var equipArray = playerData["equipment"].AsGodotArray();
                foreach (var cardId in equipArray)
                {
                    if (cardId.VariantType == Variant.Type.String)
                        player.EquipItem((string)cardId);
                }
            }

            // Status
            if (playerData.ContainsKey("is_dead"))
                player.IsDead = (bool)playerData["is_dead"];

            return player;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GameStateManager] Error parsing player data: {ex.Message}");
            return null;
        }
    }

    // Map server turn phase to our main game phase
    private GameStateMachine.MainGamePhase MapTurnPhaseToMainPhase(
        MessageProtocol.TurnPhase turnPhase
    )
    {
        return turnPhase switch
        {
            MessageProtocol.TurnPhase.OPEN_DOOR => GameStateMachine.MainGamePhase.OpenDoor,
            MessageProtocol.TurnPhase.LOOK_FOR_TROUBLE => GameStateMachine
                .MainGamePhase
                .LookForTrouble,
            MessageProtocol.TurnPhase.LOOT_ROOM => GameStateMachine.MainGamePhase.LootRoom,
            MessageProtocol.TurnPhase.CHARITY => GameStateMachine.MainGamePhase.Charity,
            MessageProtocol.TurnPhase.TURN_END => GameStateMachine.MainGamePhase.TurnEnd,
            _ => GameStateMachine.MainGamePhase.TurnStart,
        };
    }

    // Handle PLAYER_UPDATE message - individual player update
    private void HandlePlayerUpdateMessage(Godot.Collections.Dictionary data)
    {
        try
        {
            GD.Print("[GameStateManager] Processing PLAYER_UPDATE message");

            // Parse player data
            if (data.ContainsKey("player"))
            {
                var playerDict = data["player"].AsGodotDictionary();
                var updatedPlayer = ParsePlayerFromServer(playerDict);

                if (updatedPlayer != null)
                {
                    // Update or add player
                    var existingIndex = StateMachine.Players.FindIndex(p =>
                        p.PlayerId == updatedPlayer.PlayerId
                    );
                    if (existingIndex >= 0)
                    {
                        StateMachine.Players[existingIndex] = updatedPlayer;

                        // Update local player reference if needed
                        if (updatedPlayer.PlayerId == LocalPlayerId)
                        {
                            LocalPlayer = updatedPlayer;
                            OnLocalPlayerUpdated?.Invoke(updatedPlayer);
                        }
                    }
                    else
                    {
                        StateMachine.Players.Add(updatedPlayer);
                    }

                    GD.Print($"[GameStateManager] Updated player: {updatedPlayer.PlayerName}");
                }
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GameStateManager] Error handling PLAYER_UPDATE: {ex.Message}");
        }
    }

    // Handle TURN_PHASE_CHANGE message
    private void HandleTurnPhaseChangeMessage(Godot.Collections.Dictionary data)
    {
        try
        {
            if (MessageProtocol.TryParseTurnPhaseChange(data, out var phaseChange))
            {
                if (Enum.TryParse<MessageProtocol.TurnPhase>(phaseChange.Phase, out var turnPhase))
                {
                    var mainPhase = MapTurnPhaseToMainPhase(turnPhase);
                    StateMachine.TransitionToPhase(mainPhase);

                    GD.Print($"[GameStateManager] Turn phase changed to: {mainPhase}");
                }
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GameStateManager] Error handling TURN_PHASE_CHANGE: {ex.Message}");
        }
    }

    // Handle COMBAT_START message
    private void HandleCombatStartMessage(Godot.Collections.Dictionary data)
    {
        try
        {
            if (MessageProtocol.TryParseCombatStart(data, out var combatStart))
            {
                StateMachine.TransitionToPhase(GameStateMachine.MainGamePhase.Combat);
                GD.Print("[GameStateManager] Combat started");
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GameStateManager] Error handling COMBAT_START: {ex.Message}");
        }
    }

    // Handle COMBAT_RESOLUTION message
    private void HandleCombatResolutionMessage(Godot.Collections.Dictionary data)
    {
        try
        {
            GD.Print("[GameStateManager] Combat resolved");
            // TODO: Parse combat result and update players
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GameStateManager] Error handling COMBAT_RESOLUTION: {ex.Message}");
        }
    }

    // Handle ERROR message
    private void HandleErrorMessage(Godot.Collections.Dictionary data)
    {
        try
        {
            if (MessageProtocol.TryParseError(data, out var error))
            {
                GD.PrintErr($"[GameStateManager] Server error: {error.Code} - {error.Message}");
                OnError?.Invoke(error.Message);
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GameStateManager] Error handling ERROR message: {ex.Message}");
        }
    }

    // Handle connection errors
    private void HandleConnectionError(string error)
    {
        GD.PrintErr($"[GameStateManager] Connection error: {error}");
        OnError?.Invoke($"Connection error: {error}");
    }

    // Test method: Simulate receiving a game state for testing
    public void TestWithMockData()
    {
        GD.Print("[GameStateManager] Testing with mock data...");

        // Create mock player data matching server format
        var mockPlayers = new Godot.Collections.Array
        {
            new Godot.Collections.Dictionary
            {
                { "id", "player1" },
                { "name", "Test Player 1" },
                { "level", 1 },
                { "race", "Human" },
                { "class", "Warrior" },
                { "sex", "Male" },
                {
                    "hand",
                    new Godot.Collections.Array { "card1", "card2", "card3" }
                },
                {
                    "equipment",
                    new Godot.Collections.Array { "sword1" }
                },
                { "is_dead", false },
            },
            new Godot.Collections.Dictionary
            {
                { "id", "player2" },
                { "name", "Test Player 2" },
                { "level", 1 },
                { "race", "Elf" },
                { "class", "Thief" },
                { "sex", "Female" },
                {
                    "hand",
                    new Godot.Collections.Array { "card4", "card5" }
                },
                { "equipment", new Godot.Collections.Array() },
                { "is_dead", false },
            },
        };

        var mockCurrentTurn = new Godot.Collections.Dictionary
        {
            { "player_id", "player1" },
            { "phase", "OPEN_DOOR" },
        };

        var mockGameState = new Godot.Collections.Dictionary
        {
            { "players", mockPlayers },
            { "current_turn", mockCurrentTurn },
        };

        // Process the mock state
        HandleGameStateMessage(mockGameState);

        GD.Print($"[GameStateManager] Test complete. State: {StateMachine}");
    }
}
