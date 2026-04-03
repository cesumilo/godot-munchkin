using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// Simulates server WebSocket responses for local development and testing.
/// Maintains simplified game state and generates server messages matching PROTOCOL.md.
/// </summary>
/// <remarks>
/// Per MVP_PLAN.md Step 2: MockServer intercepts outgoing messages when UseMockServer is true
/// and injects responses via WebSocketClient.MessageReceived event.
/// </remarks>
public class MockServer
{
    // ============ GAME STATE ============

    /// <summary>
    /// List of mock players in the game.
    /// </summary>
    private List<MockPlayer> _players = new();

    /// <summary>
    /// Dungeon deck card IDs.
    /// </summary>
    private List<string> _dungeonDeck = new();

    /// <summary>
    /// Treasure deck card IDs.
    /// </summary>
    private List<string> _treasureDeck = new();

    /// <summary>
    /// Index of the currently active player.
    /// </summary>
    private int _activePlayerIndex = 0;

    /// <summary>
    /// Current game phase.
    /// </summary>
    private MessageProtocol.TurnPhase _currentPhase = MessageProtocol.TurnPhase.OPEN_DOOR;

    /// <summary>
    /// Whether a combat is currently active.
    /// </summary>
    private bool _combatActive = false;

    /// <summary>
    /// Current combat monster card ID.
    /// </summary>
    private string _combatMonsterId = null;

    /// <summary>
    /// Whether the game has started.
    /// </summary>
    private bool _gameStarted = false;

    /// <summary>
    /// Random number generator for dice rolls and shuffling.
    /// </summary>
    private Random _random = new();

    // ============ LOBBY STATE ============

    /// <summary>
    /// Current lobby state.
    /// </summary>
    private MessageProtocol.LobbyStateData _lobbyState = null;

    /// <summary>
    /// Local player ID for the mock session.
    /// </summary>
    private string _localPlayerId = null;

    // ============ EVENTS ============

    /// <summary>
    /// Event fired when mock server generates a message.
    /// NetworkManager subscribes to this and routes messages to WebSocketClient.
    /// </summary>
    public event Action<string, Godot.Collections.Dictionary> OnServerMessage;

    // ============ INITIALIZATION ============

    /// <summary>
    /// Initializes the mock lobby with players.
    /// </summary>
    /// <param name="lobbyId">The lobby identifier.</param>
    /// <param name="hostId">The host player ID.</param>
    /// <param name="localPlayerId">The local player ID.</param>
    /// <param name="players">List of players in the lobby.</param>
    public void InitializeLobby(
        string lobbyId,
        string hostId,
        string localPlayerId,
        List<MessageProtocol.LobbyPlayerData> players
    )
    {
        _localPlayerId = localPlayerId;
        _lobbyState = new MessageProtocol.LobbyStateData
        {
            LobbyId = lobbyId,
            HostId = hostId,
            Players = players,
            GameInProgress = false,
            MaxPlayers = 6,
            CurrentPlayers = players.Count,
            Settings = new MessageProtocol.LobbySettingsData
            {
                TimerEnabled = false,
                TurnTimeLimit = 120,
                CombatInteractionTime = 30,
            },
        };
        _gameStarted = false;

        EmitLobbyState();
    }

    /// <summary>
    /// Starts the game from lobby.
    /// </summary>
    public void StartGame()
    {
        if (_gameStarted)
            return;

        EmitGameStarting(5);

        // Use Godot timer for countdown simulation
        var timer = new Timer();
        timer.WaitTime = 5.0f;
        timer.OneShot = true;
        timer.Timeout += () =>
        {
            InitializeGame();
            timer.QueueFree();
        };

        // We need a scene tree to add the timer - this will be called from NetworkManager
        var sceneTree = Engine.GetMainLoop() as SceneTree;
        if (sceneTree != null)
        {
            sceneTree.Root.AddChild(timer);
            timer.Start();
        }
        else
        {
            // Fallback: start immediately
            InitializeGame();
        }
    }

    /// <summary>
    /// Initializes the game with mock players and decks.
    /// </summary>
    /// <param name="localPlayerId">The local player ID.</param>
    public void InitializeGame(string localPlayerId = null)
    {
        if (localPlayerId != null)
            _localPlayerId = localPlayerId;

        _gameStarted = true;

        // Build decks from available cards
        BuildDecks();

        // Create 3 mock players (local + 2 bots)
        CreateMockPlayers();

        // Deal initial hands: 4 dungeon + 4 treasure
        DealInitialHands();

        _activePlayerIndex = 0;
        _currentPhase = MessageProtocol.TurnPhase.OPEN_DOOR;
        _combatActive = false;
        _combatMonsterId = null;

        EmitGameStarted();
    }

    /// <summary>
    /// Builds the dungeon and treasure decks from available cards.
    /// </summary>
    private void BuildDecks()
    {
        _dungeonDeck.Clear();
        _treasureDeck.Clear();

        // Get cards from CardFactory (needs to be initialized)
        if (CardFactory.Instance != null)
        {
            var dungeonCards = CardFactory.Instance.GetCardsByDeckType(CardData.DeckType.Dungeon);
            var treasureCards = CardFactory.Instance.GetCardsByDeckType(CardData.DeckType.Treasure);

            // Add dungeon cards (repeat to fill ~95 cards as per rules)
            for (int i = 0; i < 15; i++) // Repeat to get enough cards
            {
                foreach (var card in dungeonCards)
                {
                    _dungeonDeck.Add(card.Id);
                }
            }

            // Add treasure cards (repeat to fill ~73 cards)
            for (int i = 0; i < 12; i++)
            {
                foreach (var card in treasureCards)
                {
                    _treasureDeck.Add(card.Id);
                }
            }
        }
        else
        {
            // Fallback: use hardcoded IDs if CardFactory not available
            _dungeonDeck.AddRange(
                new[]
                {
                    "monster_goblin_001",
                    "monster_potted_plant_001",
                    "curse_lose_headgear_001",
                    "race_elf_001",
                    "race_dwarf_001",
                    "class_warrior_001",
                    "class_thief_001",
                }
            );

            _treasureDeck.AddRange(
                new[]
                {
                    "item_helm_of_courage_001",
                    "item_broad_sword_001",
                    "action_potion_studliness_001",
                    "action_duck_of_doom_001",
                }
            );
        }

        // Shuffle decks
        ShuffleDeck(_dungeonDeck);
        ShuffleDeck(_treasureDeck);

        GD.Print(
            $"[MockServer] Built decks: {_dungeonDeck.Count} dungeon, {_treasureDeck.Count} treasure"
        );
    }

    /// <summary>
    /// Shuffles a deck of card IDs using Fisher-Yates algorithm.
    /// </summary>
    /// <param name="deck">The deck to shuffle.</param>
    private void ShuffleDeck(List<string> deck)
    {
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }
    }

    /// <summary>
    /// Creates mock players for the game.
    /// </summary>
    private void CreateMockPlayers()
    {
        _players.Clear();

        // Local player
        _players.Add(
            new MockPlayer
            {
                Id = _localPlayerId ?? "player_local",
                Name = "You",
                Level = 1,
                Race = null,
                Class = null,
                Sex = "MALE",
                Hand = new List<string>(),
                Equipment = new List<MockEquipment>(),
                IsDead = false,
            }
        );

        // Bot players
        _players.Add(
            new MockPlayer
            {
                Id = "player_bot_1",
                Name = "Bot 1",
                Level = 1,
                Race = null,
                Class = null,
                Sex = "FEMALE",
                Hand = new List<string>(),
                Equipment = new List<MockEquipment>(),
                IsDead = false,
            }
        );

        _players.Add(
            new MockPlayer
            {
                Id = "player_bot_2",
                Name = "Bot 2",
                Level = 1,
                Race = null,
                Class = null,
                Sex = "MALE",
                Hand = new List<string>(),
                Equipment = new List<MockEquipment>(),
                IsDead = false,
            }
        );
    }

    /// <summary>
    /// Deals initial hands to all players (4 dungeon + 4 treasure).
    /// </summary>
    private void DealInitialHands()
    {
        foreach (var player in _players)
        {
            player.Hand.Clear();

            // Deal 4 dungeon cards
            for (int i = 0; i < 4 && _dungeonDeck.Count > 0; i++)
            {
                player.Hand.Add(_dungeonDeck[0]);
                _dungeonDeck.RemoveAt(0);
            }

            // Deal 4 treasure cards
            for (int i = 0; i < 4 && _treasureDeck.Count > 0; i++)
            {
                player.Hand.Add(_treasureDeck[0]);
                _treasureDeck.RemoveAt(0);
            }
        }

        GD.Print($"[MockServer] Dealt initial hands to {_players.Count} players");
    }

    // ============ MESSAGE PROCESSING ============

    /// <summary>
    /// Processes an incoming client message.
    /// </summary>
    /// <param name="messageType">The message type.</param>
    /// <param name="data">The message data.</param>
    public void ProcessMessage(string messageType, Godot.Collections.Dictionary data)
    {
        GD.Print($"[MockServer] Received: {messageType}");

        if (!_gameStarted)
        {
            // Lobby phase messages
            ProcessLobbyMessage(messageType, data);
        }
        else
        {
            // Game phase messages
            ProcessGameMessage(messageType, data);
        }
    }

    /// <summary>
    /// Processes lobby-related messages.
    /// </summary>
    /// <param name="messageType">The message type.</param>
    /// <param name="data">The message data.</param>
    private void ProcessLobbyMessage(string messageType, Godot.Collections.Dictionary data)
    {
        switch (messageType)
        {
            case "SET_READY":
                HandleSetReady(data);
                break;
            case "START_GAME":
                HandleStartGame(data);
                break;
            case "LOBBY_CHAT":
                HandleLobbyChat(data);
                break;
        }
    }

    /// <summary>
    /// Processes game-related messages.
    /// </summary>
    /// <param name="messageType">The message type.</param>
    /// <param name="data">The message data.</param>
    private void ProcessGameMessage(string messageType, Godot.Collections.Dictionary data)
    {
        switch (messageType)
        {
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

    // ============ LOBBY HANDLERS ============

    /// <summary>
    /// Handles SET_READY message.
    /// </summary>
    /// <param name="data">The message data.</param>
    private void HandleSetReady(Godot.Collections.Dictionary data)
    {
        bool isReady = data.ContainsKey("is_ready") && (bool)data["is_ready"];

        // Update local player ready status
        var localPlayer = _lobbyState?.Players.FirstOrDefault(p => p.Id == _localPlayerId);
        if (localPlayer != null)
        {
            localPlayer.IsReady = isReady;
            EmitPlayerReadyChange(_localPlayerId, isReady);
        }
    }

    /// <summary>
    /// Handles START_GAME message.
    /// </summary>
    /// <param name="data">The message data.</param>
    private void HandleStartGame(Godot.Collections.Dictionary data)
    {
        bool forceStart = data.ContainsKey("force_start") && (bool)data["force_start"];
        StartGame();
    }

    /// <summary>
    /// Handles LOBBY_CHAT message.
    /// </summary>
    /// <param name="data">The message data.</param>
    private void HandleLobbyChat(Godot.Collections.Dictionary data)
    {
        string message = data.ContainsKey("message") ? (string)data["message"] : "";

        var chatData = new Godot.Collections.Dictionary
        {
            ["player_id"] = _localPlayerId,
            ["player_name"] = "You",
            ["message"] = message,
            ["timestamp"] = DateTime.UtcNow.ToString("o"),
        };

        EmitMessage("LOBBY_CHAT_MESSAGE", chatData);
    }

    // ============ GAME HANDLERS ============

    /// <summary>
    /// Handles PLAYER_ACTION message.
    /// </summary>
    /// <param name="data">The message data.</param>
    private void HandlePlayerAction(Godot.Collections.Dictionary data)
    {
        string action = data.ContainsKey("action") ? (string)data["action"] : "";
        var activePlayer = _players[_activePlayerIndex];

        switch (action)
        {
            case "OPEN_DOOR":
                HandleOpenDoorAction(activePlayer);
                break;
            case "LOOK_FOR_TROUBLE":
                HandleLookForTroubleAction(activePlayer);
                break;
            case "LOOT_ROOM":
                HandleLootRoomAction(activePlayer);
                break;
            case "END_TURN":
                HandleEndTurnAction();
                break;
        }
    }

    /// <summary>
    /// Handles OPEN_DOOR action.
    /// </summary>
    /// <param name="player">The active player.</param>
    private void HandleOpenDoorAction(MockPlayer player)
    {
        if (_dungeonDeck.Count == 0)
        {
            // Reshuffle discard back into deck (simplified)
            EmitError("NO_CARDS", "Dungeon deck is empty");
            return;
        }

        string drawnCardId = _dungeonDeck[0];
        _dungeonDeck.RemoveAt(0);

        var card = CardFactory.Instance?.GetCardById(drawnCardId);
        bool isMonster = card?.Type == CardData.CardType.Monster;

        var result = new Godot.Collections.Dictionary
        {
            ["drawn_card"] = drawnCardId,
            ["combat_triggered"] = isMonster,
        };

        EmitTurnPhaseChange(player.Id, MessageProtocol.TurnPhase.OPEN_DOOR, result);

        if (isMonster)
        {
            _combatActive = true;
            _combatMonsterId = drawnCardId;
            EmitCombatStart(drawnCardId, player);
        }
        else
        {
            // Non-monster cards go to hand
            player.Hand.Add(drawnCardId);
            _currentPhase = MessageProtocol.TurnPhase.LOOK_FOR_TROUBLE;
        }
    }

    /// <summary>
    /// Handles LOOK_FOR_TROUBLE action.
    /// </summary>
    /// <param name="player">The active player.</param>
    private void HandleLookForTroubleAction(MockPlayer player)
    {
        // For MVP: find first monster in hand, or emit error
        string monsterCardId = player.Hand.FirstOrDefault(id =>
        {
            var card = CardFactory.Instance?.GetCardById(id);
            return card?.Type == CardData.CardType.Monster;
        });

        if (monsterCardId == null)
            monsterCardId = "monster_goblin_001"; // Fallback for MVP

        player.Hand.Remove(monsterCardId);
        _combatActive = true;
        _combatMonsterId = monsterCardId;

        var result = new Godot.Collections.Dictionary
        {
            ["drawn_card"] = monsterCardId,
            ["combat_triggered"] = true,
        };

        EmitTurnPhaseChange(player.Id, MessageProtocol.TurnPhase.LOOK_FOR_TROUBLE, result);
        EmitCombatStart(monsterCardId, player);
    }

    /// <summary>
    /// Handles LOOT_ROOM action.
    /// </summary>
    /// <param name="player">The active player.</param>
    private void HandleLootRoomAction(MockPlayer player)
    {
        if (_dungeonDeck.Count == 0)
        {
            EmitError("NO_CARDS", "Dungeon deck is empty");
            return;
        }

        string drawnCardId = _dungeonDeck[0];
        _dungeonDeck.RemoveAt(0);
        player.Hand.Add(drawnCardId);

        _currentPhase = MessageProtocol.TurnPhase.CHARITY;

        var result = new Godot.Collections.Dictionary
        {
            ["drawn_card"] = drawnCardId,
            ["combat_triggered"] = false,
        };

        EmitTurnPhaseChange(player.Id, MessageProtocol.TurnPhase.LOOT_ROOM, result);

        // Auto-handle charity if needed
        HandleCharityPhase(player);
    }

    /// <summary>
    /// Handles END_TURN action.
    /// </summary>
    private void HandleEndTurnAction()
    {
        _combatActive = false;
        _combatMonsterId = null;
        _currentPhase = MessageProtocol.TurnPhase.OPEN_DOOR;
        _activePlayerIndex = (_activePlayerIndex + 1) % _players.Count;

        EmitGameState();

        // If next player is a bot, auto-play their turn
        if (_activePlayerIndex != 0)
        {
            SimulateBotTurn();
        }
    }

    /// <summary>
    /// Handles charity phase (auto-discard excess cards for MVP).
    /// </summary>
    /// <param name="player">The active player.</param>
    private void HandleCharityPhase(MockPlayer player)
    {
        while (player.Hand.Count > 5)
        {
            // Auto-discard to deck (simplified charity)
            player.Hand.RemoveAt(player.Hand.Count - 1);
        }
    }

    /// <summary>
    /// Handles COMBAT_RESPONSE message.
    /// </summary>
    /// <param name="data">The message data.</param>
    private void HandleCombatResponse(Godot.Collections.Dictionary data)
    {
        string response = data.ContainsKey("response") ? (string)data["response"] : "";
        var player = _players[_activePlayerIndex];

        if (response == "FLEE")
        {
            HandleFleeAttempt(player);
        }
        else
        {
            HandleCombatResolution(player);
        }
    }

    /// <summary>
    /// Handles flee attempt.
    /// </summary>
    /// <param name="player">The active player.</param>
    private void HandleFleeAttempt(MockPlayer player)
    {
        int roll = _random.Next(1, 7); // 1-6
        bool success = roll >= 5;

        var result = success ? "FLEE_SUCCESS" : "FLEE_FAILED";
        var penalty = success
            ? null
            : new Godot.Collections.Dictionary
            {
                ["type"] = "LOSE_LEVEL",
                ["details"] = "Flee failed",
            };

        var resolutionData = new Godot.Collections.Dictionary
        {
            ["result"] = "DEFEAT",
            ["player_force"] = CalculatePlayerForce(player),
            ["monster_force"] = GetMonsterLevel(_combatMonsterId),
            ["rewards"] = new Godot.Collections.Dictionary(),
            ["penalty"] = penalty,
            ["flee_result"] = result,
        };

        EmitMessage("COMBAT_RESOLUTION", resolutionData);

        if (!success)
        {
            player.Level = Math.Max(1, player.Level - 1);
        }

        _combatActive = false;
        _currentPhase = MessageProtocol.TurnPhase.CHARITY;
    }

    /// <summary>
    /// Handles combat resolution (fight).
    /// </summary>
    /// <param name="player">The active player.</param>
    private void HandleCombatResolution(MockPlayer player)
    {
        int playerForce = CalculatePlayerForce(player);
        int monsterForce = GetMonsterLevel(_combatMonsterId);
        bool victory = playerForce > monsterForce; // Strictly greater per §8.4

        var resolutionData = new Godot.Collections.Dictionary();

        if (victory)
        {
            var monsterCard = CardFactory.Instance?.GetCardById<MonsterCardData>(_combatMonsterId);
            int levelsGained = monsterCard?.LevelsGained ?? 1;
            int treasures = monsterCard?.Treasures ?? 1;

            player.Level = Math.Min(player.Level + levelsGained, 10);

            // Award treasures
            var treasureCards = new Godot.Collections.Array();
            for (int i = 0; i < treasures && _treasureDeck.Count > 0; i++)
            {
                treasureCards.Add(_treasureDeck[0]);
                player.Hand.Add(_treasureDeck[0]);
                _treasureDeck.RemoveAt(0);
            }

            resolutionData["result"] = "VICTORY";
            resolutionData["player_force"] = playerForce;
            resolutionData["monster_force"] = monsterForce;
            resolutionData["rewards"] = new Godot.Collections.Dictionary
            {
                ["treasures"] = treasureCards,
                ["levels_gained"] = levelsGained,
                ["ally_levels_gained"] = 0,
            };
            resolutionData["penalty"] = new Godot.Collections.Dictionary();
        }
        else
        {
            resolutionData["result"] = "DEFEAT";
            resolutionData["player_force"] = playerForce;
            resolutionData["monster_force"] = monsterForce;
            resolutionData["rewards"] = new Godot.Collections.Dictionary();
            resolutionData["penalty"] = new Godot.Collections.Dictionary
            {
                ["type"] = "LOSE_LEVEL",
                ["details"] = "Combat defeat",
            };

            player.Level = Math.Max(1, player.Level - 1);
        }

        EmitMessage("COMBAT_RESOLUTION", resolutionData);

        _combatActive = false;
        _currentPhase = MessageProtocol.TurnPhase.CHARITY;
    }

    /// <summary>
    /// Handles PLAY_CARD message.
    /// </summary>
    /// <param name="data">The message data.</param>
    private void HandlePlayCard(Godot.Collections.Dictionary data)
    {
        string cardId = data.ContainsKey("card_id") ? (string)data["card_id"] : "";
        var player = _players[_activePlayerIndex];

        if (!player.Hand.Contains(cardId))
        {
            EmitError("NOT_IN_HAND", "Card not in player's hand");
            return;
        }

        var card = CardFactory.Instance?.GetCardById(cardId);
        if (card == null)
        {
            EmitError("CARD_NOT_FOUND", "Card not found");
            return;
        }

        // Remove from hand and process based on type
        player.Hand.Remove(cardId);

        var resultData = new Godot.Collections.Dictionary
        {
            ["player_id"] = player.Id,
            ["card_id"] = cardId,
            ["success"] = true,
            ["effect"] = "CARD_PLAYED",
            ["validation_error"] = "",
        };

        EmitMessage("CARD_PLAY_RESULT", resultData);
    }

    // ============ BOT SIMULATION ============

    /// <summary>
    /// Simulates a bot player's turn.
    /// </summary>
    private void SimulateBotTurn()
    {
        var timer = new Timer();
        timer.WaitTime = 1.5f; // Delay between bot actions
        timer.OneShot = true;

        int step = 0;
        timer.Timeout += () =>
        {
            var bot = _players[_activePlayerIndex];

            switch (step)
            {
                case 0: // Open door
                    HandleOpenDoorAction(bot);
                    step++;
                    timer.WaitTime = 1.0f;
                    timer.Start();
                    break;

                case 1: // If no combat, loot room
                    if (!_combatActive)
                    {
                        HandleLootRoomAction(bot);
                        step++;
                        timer.WaitTime = 0.5f;
                        timer.Start();
                    }
                    else
                    {
                        // Auto-fight combat
                        HandleCombatResolution(bot);
                        step += 2;
                        timer.WaitTime = 0.5f;
                        timer.Start();
                    }
                    break;

                case 2: // End turn
                    HandleEndTurnAction();
                    timer.QueueFree();
                    break;
            }
        };

        var sceneTree = Engine.GetMainLoop() as SceneTree;
        if (sceneTree != null)
        {
            sceneTree.Root.AddChild(timer);
            timer.Start();
        }
        else
        {
            // Fallback: do actions immediately
            HandleOpenDoorAction(_players[_activePlayerIndex]);
            if (!_combatActive)
            {
                HandleLootRoomAction(_players[_activePlayerIndex]);
            }
            else
            {
                HandleCombatResolution(_players[_activePlayerIndex]);
            }
            HandleEndTurnAction();
        }
    }

    // ============ HELPERS ============

    /// <summary>
    /// Calculates a player's combat force.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The combat force.</returns>
    private int CalculatePlayerForce(MockPlayer player)
    {
        int force = player.Level;

        foreach (var equip in player.Equipment.Where(e => e.IsEquipped))
        {
            var itemCard = CardFactory.Instance?.GetCardById<ItemCardData>(equip.CardId);
            if (itemCard != null)
            {
                force += itemCard.Bonus;
            }
        }

        return force;
    }

    /// <summary>
    /// Gets a monster's level.
    /// </summary>
    /// <param name="monsterId">The monster card ID.</param>
    /// <returns>The monster level.</returns>
    private int GetMonsterLevel(string monsterId)
    {
        var card = CardFactory.Instance?.GetCardById<MonsterCardData>(monsterId);
        return card?.Level ?? 1;
    }

    // ============ MESSAGE BUILDERS ============

    /// <summary>
    /// Builds a GAME_STATE message.
    /// </summary>
    /// <returns>The game state dictionary.</returns>
    private Godot.Collections.Dictionary BuildGameState()
    {
        var playersArray = new Godot.Collections.Array();
        foreach (var player in _players)
        {
            var equipmentArray = new Godot.Collections.Array();
            foreach (var equip in player.Equipment)
            {
                equipmentArray.Add(
                    new Godot.Collections.Dictionary
                    {
                        ["card_id"] = equip.CardId,
                        ["slot"] = equip.Slot,
                        ["is_equipped"] = equip.IsEquipped,
                    }
                );
            }

            // Convert hand List<string> to Godot Array
            var handArray = new Godot.Collections.Array();
            foreach (var cardId in player.Hand)
            {
                handArray.Add(cardId);
            }

            playersArray.Add(
                new Godot.Collections.Dictionary
                {
                    ["id"] = player.Id,
                    ["name"] = player.Name,
                    ["level"] = player.Level,
                    ["race"] = player.Race ?? "",
                    ["class"] = player.Class ?? "",
                    ["sex"] = player.Sex,
                    ["has_hybrid_race"] = false,
                    ["has_hybrid_class"] = false,
                    ["equipment"] = equipmentArray,
                    ["hand"] = handArray,
                    ["is_dead"] = player.IsDead,
                }
            );
        }

        Godot.Collections.Dictionary combatData;
        if (_combatActive)
        {
            combatData = new Godot.Collections.Dictionary
            {
                ["active"] = true,
                ["monsters"] = new Godot.Collections.Array
                {
                    new Godot.Collections.Dictionary
                    {
                        ["card_id"] = _combatMonsterId,
                        ["level"] = GetMonsterLevel(_combatMonsterId),
                    },
                },
                ["player_force"] = CalculatePlayerForce(_players[_activePlayerIndex]),
                ["monster_force"] = GetMonsterLevel(_combatMonsterId),
                ["ally"] = "",
                ["interaction_window_open"] = true,
            };
        }
        else
        {
            combatData = new Godot.Collections.Dictionary();
        }

        return new Godot.Collections.Dictionary
        {
            ["game_id"] = "mock-game",
            ["players"] = playersArray,
            ["current_turn"] = new Godot.Collections.Dictionary
            {
                ["player_id"] = _players[_activePlayerIndex].Id,
                ["phase"] = _currentPhase.ToString(),
            },
            ["combat"] = combatData,
            ["decks"] = new Godot.Collections.Dictionary
            {
                ["dungeon_remaining"] = _dungeonDeck.Count,
                ["treasure_remaining"] = _treasureDeck.Count,
                ["dungeon_discard"] = new Godot.Collections.Array(),
                ["treasure_discard"] = new Godot.Collections.Array(),
            },
            ["winner"] = _players.Any(p => p.Level >= 10)
                ? _players.First(p => p.Level >= 10).Id
                : null,
        };
    }

    // ============ MESSAGE EMITTERS ============

    /// <summary>
    /// Emits a server message.
    /// </summary>
    /// <param name="type">The message type.</param>
    /// <param name="data">The message data.</param>
    private void EmitMessage(string type, Godot.Collections.Dictionary data)
    {
        GD.Print($"[MockServer] Emitting: {type}");
        OnServerMessage?.Invoke(type, data);
    }

    /// <summary>
    /// Emits LOBBY_STATE message.
    /// </summary>
    private void EmitLobbyState()
    {
        var playersArray = new Godot.Collections.Array();
        foreach (var player in _lobbyState.Players)
        {
            playersArray.Add(
                new Godot.Collections.Dictionary
                {
                    ["id"] = player.Id,
                    ["name"] = player.Name,
                    ["is_host"] = player.IsHost,
                    ["is_ready"] = player.IsReady,
                    ["avatar"] = player.Avatar ?? "",
                }
            );
        }

        var data = new Godot.Collections.Dictionary
        {
            ["lobby_id"] = _lobbyState.LobbyId,
            ["host_id"] = _lobbyState.HostId,
            ["players"] = playersArray,
            ["game_in_progress"] = _lobbyState.GameInProgress,
            ["max_players"] = _lobbyState.MaxPlayers,
            ["current_players"] = _lobbyState.CurrentPlayers,
            ["settings"] = new Godot.Collections.Dictionary
            {
                ["timer_enabled"] = _lobbyState.Settings.TimerEnabled,
                ["turn_time_limit"] = _lobbyState.Settings.TurnTimeLimit,
                ["combat_interaction_time"] = _lobbyState.Settings.CombatInteractionTime,
            },
        };

        EmitMessage("LOBBY_STATE", data);
    }

    /// <summary>
    /// Emits PLAYER_READY_CHANGE message.
    /// </summary>
    /// <param name="playerId">The player ID.</param>
    /// <param name="isReady">The ready status.</param>
    private void EmitPlayerReadyChange(string playerId, bool isReady)
    {
        EmitMessage(
            "PLAYER_READY_CHANGE",
            new Godot.Collections.Dictionary { ["player_id"] = playerId, ["is_ready"] = isReady }
        );
    }

    /// <summary>
    /// Emits GAME_STARTING message.
    /// </summary>
    /// <param name="countdown">The countdown seconds.</param>
    private void EmitGameStarting(int countdown)
    {
        EmitMessage(
            "GAME_STARTING",
            new Godot.Collections.Dictionary { ["countdown"] = countdown, ["reason"] = "ALL_READY" }
        );
    }

    /// <summary>
    /// Emits GAME_STARTED message.
    /// </summary>
    private void EmitGameStarted()
    {
        var data = new Godot.Collections.Dictionary
        {
            ["first_player_id"] = _players[0].Id,
            ["initial_hand_size"] = 8,
            ["initial_state"] = BuildGameState(),
        };

        EmitMessage("GAME_STARTED", data);
    }

    /// <summary>
    /// Emits GAME_STATE message.
    /// </summary>
    private void EmitGameState()
    {
        EmitMessage("GAME_STATE", BuildGameState());
    }

    /// <summary>
    /// Emits TURN_PHASE_CHANGE message.
    /// </summary>
    /// <param name="playerId">The player ID.</param>
    /// <param name="phase">The new phase.</param>
    /// <param name="result">The phase result data.</param>
    private void EmitTurnPhaseChange(
        string playerId,
        MessageProtocol.TurnPhase phase,
        Godot.Collections.Dictionary result
    )
    {
        EmitMessage(
            "TURN_PHASE_CHANGE",
            new Godot.Collections.Dictionary
            {
                ["player_id"] = playerId,
                ["phase"] = phase.ToString(),
                ["result"] = result,
            }
        );
    }

    /// <summary>
    /// Emits COMBAT_START message.
    /// </summary>
    /// <param name="monsterId">The monster card ID.</param>
    /// <param name="player">The player in combat.</param>
    private void EmitCombatStart(string monsterId, MockPlayer player)
    {
        var monsterCard = CardFactory.Instance?.GetCardById<MonsterCardData>(monsterId);

        var data = new Godot.Collections.Dictionary
        {
            ["monster"] = new Godot.Collections.Dictionary
            {
                ["card_id"] = monsterId,
                ["level"] = monsterCard?.Level ?? 1,
                ["treasures"] = monsterCard?.Treasures ?? 1,
                ["levels_gained"] = monsterCard?.LevelsGained ?? 1,
            },
            ["player_force"] = CalculatePlayerForce(player),
            ["interaction_window_duration"] = 30,
        };

        EmitMessage("COMBAT_START", data);
    }

    /// <summary>
    /// Emits ERROR message.
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <param name="message">The error message.</param>
    private void EmitError(string code, string message)
    {
        EmitMessage(
            "ERROR",
            new Godot.Collections.Dictionary
            {
                ["code"] = code,
                ["message"] = message,
                ["recoverable"] = true,
                ["suggested_action"] = "RETRY",
            }
        );
    }

    // ============ INNER CLASSES ============

    /// <summary>
    /// Represents a mock player in the game.
    /// </summary>
    private class MockPlayer
    {
        /// <summary>
        /// Player unique identifier.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Player display name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Player level (1-10).
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// Player race card ID, or null for Human.
        /// </summary>
        public string Race { get; set; }

        /// <summary>
        /// Player class card ID, or null for none.
        /// </summary>
        public string Class { get; set; }

        /// <summary>
        /// Player sex (MALE/FEMALE).
        /// </summary>
        public string Sex { get; set; }

        /// <summary>
        /// Cards in hand.
        /// </summary>
        public List<string> Hand { get; set; }

        /// <summary>
        /// Equipped items.
        /// </summary>
        public List<MockEquipment> Equipment { get; set; }

        /// <summary>
        /// Whether the player is dead.
        /// </summary>
        public bool IsDead { get; set; }
    }

    /// <summary>
    /// Represents a piece of equipment on a player.
    /// </summary>
    private class MockEquipment
    {
        /// <summary>
        /// The item card ID.
        /// </summary>
        public string CardId { get; set; }

        /// <summary>
        /// The equipment slot.
        /// </summary>
        public string Slot { get; set; }

        /// <summary>
        /// Whether the item is currently equipped (providing bonuses).
        /// </summary>
        public bool IsEquipped { get; set; }

        /// <summary>
        /// Whether this is a big item.
        /// </summary>
        public bool IsBigItem { get; set; }
    }
}
