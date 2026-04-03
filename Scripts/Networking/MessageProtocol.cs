using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Defines the WebSocket message protocol for Munchkin client-server communication.
/// Based on the networking architecture specification with JSON-based message format.
/// </summary>
/// <remarks>
/// Per AGENTS.md architecture: Uses typed JSON messages for real-time gameplay.
/// All cards referenced by unique IDs; full card data maintained locally by both client and server.
/// </remarks>
public static class MessageProtocol
{
    // ============== CLIENT MESSAGE TYPES ==============

    // Note: JOIN_GAME is not needed - player joining is implicit via WebSocket connection + JWT
    /// <summary>
    /// Client message type for player actions during their turn.
    /// </summary>
    public const string PLAYER_ACTION = "PLAYER_ACTION";

    /// <summary>
    /// Client message type for playing a card from hand.
    /// </summary>
    public const string PLAY_CARD = "PLAY_CARD";

    /// <summary>
    /// Client message type for combat responses (flee, accept alliance, etc.).
    /// </summary>
    public const string COMBAT_RESPONSE = "COMBAT_RESPONSE";

    /// <summary>
    /// Client message type for negotiation actions (offers, counter-offers, accept/reject).
    /// </summary>
    public const string NEGOTIATION = "NEGOTIATION";

    /// <summary>
    /// Client message type for using class abilities.
    /// </summary>
    public const string USE_ABILITY = "USE_ABILITY";

    // ============== SERVER MESSAGE TYPES ==============

    /// <summary>
    /// Server message type for full game state updates.
    /// </summary>
    public const string GAME_STATE = "GAME_STATE";

    /// <summary>
    /// Server message type for turn phase change notifications.
    /// </summary>
    public const string TURN_PHASE_CHANGE = "TURN_PHASE_CHANGE";

    /// <summary>
    /// Server message type when combat begins.
    /// </summary>
    public const string COMBAT_START = "COMBAT_START";

    /// <summary>
    /// Server message type for combat resolution results.
    /// </summary>
    public const string COMBAT_RESOLUTION = "COMBAT_RESOLUTION";

    /// <summary>
    /// Server message type for card play results.
    /// </summary>
    public const string CARD_PLAY_RESULT = "CARD_PLAY_RESULT";

    /// <summary>
    /// Server message type for individual player state updates.
    /// </summary>
    public const string PLAYER_UPDATE = "PLAYER_UPDATE";

    /// <summary>
    /// Server message type for error notifications.
    /// </summary>
    public const string ERROR = "ERROR";

    // ============== ENUMERATIONS ==============

    /// <summary>
    /// Defines player actions available during their turn per §7.
    /// </summary>
    /// <remarks>
    /// Per §7: Actions are phase-dependent. OPEN_DOOR is automatic, others are player choices.
    /// </remarks>
    public enum PlayerActionType
    {
        /// <summary>
        /// Per §7.1: Open top card of Donjon deck (automatic at turn start).
        /// </summary>
        OPEN_DOOR,

        /// <summary>
        /// Per §7.2: Play a monster from hand if no combat occurred.
        /// </summary>
        LOOK_FOR_TROUBLE,

        /// <summary>
        /// Per §7.3: Draw face-down Donjon card if no combat occurred.
        /// </summary>
        LOOT_ROOM,

        /// <summary>
        /// Per §7.4: End turn after Charity phase.
        /// </summary>
        END_TURN,
    }

    /// <summary>
    /// Defines combat response options per §8.
    /// </summary>
    public enum CombatResponseType
    {
        /// <summary>
        /// Per §8.2: Accept an offer of alliance from another player.
        /// </summary>
        ACCEPT_ALLIANCE,

        /// <summary>
        /// Per §8.2: Decline an offer of alliance.
        /// </summary>
        DECLINE_ALLIANCE,

        /// <summary>
        /// Per §8.6: Attempt to flee from combat.
        /// </summary>
        FLEE,

        /// <summary>
        /// Per §8.2: Play a card during combat interaction window.
        /// </summary>
        PLAY_CARD,
    }

    /// <summary>
    /// Defines negotiation action types for alliance offers.
    /// </summary>
    /// <remarks>
    /// Per §8.2: Players negotiate treasure split before alliance is confirmed.
    /// </remarks>
    public enum NegotiationActionType
    {
        /// <summary>
        /// Initial offer to help in combat.
        /// </summary>
        OFFER,

        /// <summary>
        /// Accept the proposed terms.
        /// </summary>
        ACCEPT,

        /// <summary>
        /// Reject the proposed terms.
        /// </summary>
        REJECT,

        /// <summary>
        /// Counter with modified terms.
        /// </summary>
        COUNTER_OFFER,
    }

    /// <summary>
    /// Defines class abilities that can be activated per §5.2.
    /// </summary>
    public enum AbilityType
    {
        /// <summary>
        /// Per §12.3: Thief attempts to steal an item from another player.
        /// </summary>
        THIEF_STEAL,

        /// <summary>
        /// Per §5.2: Warrior discards cards for +1 bonus each (max 3).
        /// </summary>
        WARRIOR_DISCARD,

        /// <summary>
        /// Per §5.2: Mage discards cards for charm effects.
        /// </summary>
        MAGE_CHARM,

        /// <summary>
        /// Per §5.2 and §11: Cleric discards hand to resurrect a dead player.
        /// </summary>
        CLERIC_RESURRECT,
    }

    /// <summary>
    /// Defines turn phases per §7.
    /// </summary>
    /// <remarks>
    /// Per §7 and §16: Ordered phases that make up a player's turn.
    /// </remarks>
    public enum TurnPhase
    {
        /// <summary>
        /// Per §7.1: Open top Donjon card face-up.
        /// </summary>
        OPEN_DOOR,

        /// <summary>
        /// Per §7.2: Optionally play monster from hand.
        /// </summary>
        LOOK_FOR_TROUBLE,

        /// <summary>
        /// Per §7.3: Draw face-down Donjon card.
        /// </summary>
        LOOT_ROOM,

        /// <summary>
        /// Per §7.4: Give away excess cards (hand limit 5).
        /// </summary>
        CHARITY,

        /// <summary>
        /// End of turn, transitions to next player.
        /// </summary>
        TURN_END,
    }

    /// <summary>
    /// Defines combat resolution results.
    /// </summary>
    public enum CombatResult
    {
        /// <summary>
        /// Per §8.5: Player force > monster force.
        /// </summary>
        VICTORY,

        /// <summary>
        /// Per §8.4: Player force ≤ monster force, must flee or accept punishment.
        /// </summary>
        DEFEAT,
    }

    /// <summary>
    /// Defines error codes for server error responses.
    /// </summary>
    public enum ErrorCode
    {
        /// <summary>
        /// The requested action is invalid.
        /// </summary>
        INVALID_ACTION,

        /// <summary>
        /// Action attempted when not the player's turn.
        /// </summary>
        NOT_YOUR_TURN,

        /// <summary>
        /// Action not valid in current game phase.
        /// </summary>
        INVALID_PHASE,

        /// <summary>
        /// Network communication error.
        /// </summary>
        NETWORK_ERROR,
    }

    // ============== DATA STRUCTURES ==============

    /// <summary>
    /// Base structure for WebSocket messages.
    /// </summary>
    public class WebSocketMessage
    {
        /// <summary>
        /// Gets or sets the message type identifier.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets the message data payload.
        /// </summary>
        public Godot.Collections.Dictionary Data { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="WebSocketMessage"/> class.
        /// </summary>
        /// <param name="type">The message type.</param>
        /// <param name="data">The message data.</param>
        public WebSocketMessage(string type, Godot.Collections.Dictionary data)
        {
            Type = type;
            Data = data;
        }

        /// <summary>
        /// Converts the message to JSON format for transmission.
        /// </summary>
        /// <returns>The JSON string representation.</returns>
        public string ToJson()
        {
            var message = new Godot.Collections.Dictionary { ["type"] = Type, ["data"] = Data };

            return Json.Stringify(message);
        }
    }

    // ============== CLIENT MESSAGE BUILDERS ==============

    // Note: CreateJoinGame removed - player joining is implicit via WebSocket connection + JWT

    /// <summary>
    /// Creates a PLAYER_ACTION message.
    /// </summary>
    /// <param name="action">The action type to perform.</param>
    /// <returns>A new WebSocketMessage ready for transmission.</returns>
    /// <remarks>
    /// Per §7: Sent when player chooses an action during their turn.
    /// </remarks>
    public static WebSocketMessage CreatePlayerAction(PlayerActionType action)
    {
        var data = new Godot.Collections.Dictionary
        {
            ["action"] = action.ToString(),
            ["timestamp"] = DateTime.UtcNow.ToString("o"),
        };

        return new WebSocketMessage(PLAYER_ACTION, data);
    }

    /// <summary>
    /// Creates a PLAY_CARD message.
    /// </summary>
    /// <param name="cardId">The unique identifier of the card to play.</param>
    /// <param name="targetPlayerId">Optional target player ID for targeted cards.</param>
    /// <param name="additionalData">Optional additional parameters.</param>
    /// <returns>A new WebSocketMessage ready for transmission.</returns>
    /// <remarks>
    /// Per §4.4: Cards can be played from hand according to their MomentJeu restriction.
    /// </remarks>
    public static WebSocketMessage CreatePlayCard(
        string cardId,
        string targetPlayerId = null,
        Godot.Collections.Dictionary additionalData = null
    )
    {
        var data = new Godot.Collections.Dictionary { ["card_id"] = cardId };

        if (!string.IsNullOrEmpty(targetPlayerId))
        {
            data["target_player_id"] = targetPlayerId;
        }

        if (additionalData != null && additionalData.Count > 0)
        {
            data["additional_data"] = additionalData;
        }

        return new WebSocketMessage(PLAY_CARD, data);
    }

    /// <summary>
    /// Creates a COMBAT_RESPONSE message.
    /// </summary>
    /// <param name="response">The combat response type.</param>
    /// <param name="cardId">Optional card ID if playing a card.</param>
    /// <param name="negotiationTerms">Optional negotiation terms for alliance offers.</param>
    /// <returns>A new WebSocketMessage ready for transmission.</returns>
    /// <remarks>
    /// Per §8.2 and §8.6: Used to respond during combat interaction window or flee attempts.
    /// </remarks>
    public static WebSocketMessage CreateCombatResponse(
        CombatResponseType response,
        string cardId = null,
        Godot.Collections.Dictionary negotiationTerms = null
    )
    {
        var data = new Godot.Collections.Dictionary { ["response"] = response.ToString() };

        if (!string.IsNullOrEmpty(cardId))
        {
            data["card_id"] = cardId;
        }

        if (negotiationTerms != null && negotiationTerms.Count > 0)
        {
            data["negotiation_terms"] = negotiationTerms;
        }

        return new WebSocketMessage(COMBAT_RESPONSE, data);
    }

    /// <summary>
    /// Creates a NEGOTIATION message.
    /// </summary>
    /// <param name="action">The negotiation action type.</param>
    /// <param name="negotiationId">The unique negotiation identifier.</param>
    /// <param name="terms">The negotiation terms.</param>
    /// <returns>A new WebSocketMessage ready for transmission.</returns>
    /// <remarks>
    /// Per §8.2: Used to negotiate alliance terms before combat resolution.
    /// </remarks>
    public static WebSocketMessage CreateNegotiation(
        NegotiationActionType action,
        string negotiationId,
        Godot.Collections.Dictionary terms
    )
    {
        var data = new Godot.Collections.Dictionary
        {
            ["action"] = action.ToString(),
            ["negotiation_id"] = negotiationId,
            ["terms"] = terms,
        };

        return new WebSocketMessage(NEGOTIATION, data);
    }

    /// <summary>
    /// Creates a USE_ABILITY message.
    /// </summary>
    /// <param name="ability">The class ability to use.</param>
    /// <param name="targetPlayerId">Optional target for targeted abilities.</param>
    /// <param name="cardIds">Optional card IDs for abilities requiring discards.</param>
    /// <returns>A new WebSocketMessage ready for transmission.</returns>
    /// <remarks>
    /// Per §5.2: Activates class-specific abilities like Thief steal or Warrior discard.
    /// </remarks>
    public static WebSocketMessage CreateUseAbility(
        AbilityType ability,
        string targetPlayerId = null,
        string[] cardIds = null
    )
    {
        var data = new Godot.Collections.Dictionary { ["ability"] = ability.ToString() };

        if (!string.IsNullOrEmpty(targetPlayerId))
        {
            data["target_player_id"] = targetPlayerId;
        }

        if (cardIds != null && cardIds.Length > 0)
        {
            var cardIdsArray = new Godot.Collections.Array();
            foreach (var cardId in cardIds)
            {
                cardIdsArray.Add(cardId);
            }
            data["card_ids"] = cardIdsArray;
        }

        return new WebSocketMessage(USE_ABILITY, data);
    }

    // ============== SERVER MESSAGE PARSERS ==============

    /// <summary>
    /// Parses a JSON message into type and data components.
    /// </summary>
    /// <param name="json">The JSON string to parse.</param>
    /// <param name="messageType">Output parameter for the message type.</param>
    /// <param name="data">Output parameter for the message data.</param>
    /// <returns>True if parsing succeeded; false otherwise.</returns>
    public static bool TryParseMessage(
        string json,
        out string messageType,
        out Godot.Collections.Dictionary data
    )
    {
        messageType = null;
        data = null;

        try
        {
            var jsonParser = new Json();
            Error parseError = jsonParser.Parse(json);

            if (parseError != Error.Ok)
            {
                GameLogger.Error($"Failed to parse JSON: {parseError}", nameof(MessageProtocol));
                return false;
            }

            var parsedData = jsonParser.Data.AsGodotDictionary();

            if (!parsedData.ContainsKey("type"))
            {
                GameLogger.Error("Message missing 'type' field", nameof(MessageProtocol));
                return false;
            }

            messageType = (string)parsedData["type"];
            data = parsedData.ContainsKey("data")
                ? parsedData["data"].AsGodotDictionary()
                : new Godot.Collections.Dictionary();

            return true;
        }
        catch (Exception ex)
        {
            GameLogger.Exception(ex, "Error parsing message", nameof(MessageProtocol));
            return false;
        }
    }

    /// <summary>
    /// Parses a GAME_STATE message from server data.
    /// </summary>
    /// <param name="data">The message data dictionary.</param>
    /// <param name="gameState">Output parameter for parsed game state.</param>
    /// <returns>True if parsing succeeded; false otherwise.</returns>
    /// <remarks>
    /// Per §16: GAME_STATE contains complete snapshot of game including players,
    /// current turn, combat state, and deck information.
    /// </remarks>
    public static bool TryParseGameState(
        Godot.Collections.Dictionary data,
        out GameStateMessage gameState
    )
    {
        gameState = null;

        try
        {
            gameState = new GameStateMessage
            {
                GameId = data.ContainsKey("game_id") ? (string)data["game_id"] : "",
                Players = ParsePlayers(data),
                CurrentTurn = ParseCurrentTurn(data),
                Combat = ParseCombat(data),
                Decks = ParseDecks(data),
                Winner = data.ContainsKey("winner") ? (string)data["winner"] : null,
            };

            return true;
        }
        catch (Exception ex)
        {
            GameLogger.Exception(ex, "Error parsing GAME_STATE", nameof(MessageProtocol));
            return false;
        }
    }

    /// <summary>
    /// Parses a TURN_PHASE_CHANGE message from server data.
    /// </summary>
    /// <param name="data">The message data dictionary.</param>
    /// <param name="phaseChange">Output parameter for parsed phase change.</param>
    /// <returns>True if parsing succeeded; false otherwise.</returns>
    /// <remarks>
    /// Per §7: Sent when turn phase changes to notify clients of phase transitions.
    /// </remarks>
    public static bool TryParseTurnPhaseChange(
        Godot.Collections.Dictionary data,
        out TurnPhaseChangeMessage phaseChange
    )
    {
        phaseChange = null;

        try
        {
            phaseChange = new TurnPhaseChangeMessage
            {
                PlayerId = data.ContainsKey("player_id") ? (string)data["player_id"] : "",
                Phase = data.ContainsKey("phase") ? (string)data["phase"] : "",
                Result = data.ContainsKey("result")
                    ? data["result"].AsGodotDictionary()
                    : new Godot.Collections.Dictionary(),
            };

            return true;
        }
        catch (Exception ex)
        {
            GameLogger.Exception(ex, "Error parsing TURN_PHASE_CHANGE", nameof(MessageProtocol));
            return false;
        }
    }

    /// <summary>
    /// Parses a COMBAT_START message from server data.
    /// </summary>
    /// <param name="data">The message data dictionary.</param>
    /// <param name="combatStart">Output parameter for parsed combat start info.</param>
    /// <returns>True if parsing succeeded; false otherwise.</returns>
    /// <remarks>
    /// Per §8: Sent when combat begins with monster info and player initial force.
    /// </remarks>
    public static bool TryParseCombatStart(
        Godot.Collections.Dictionary data,
        out CombatStartMessage combatStart
    )
    {
        combatStart = null;

        try
        {
            combatStart = new CombatStartMessage
            {
                Monster = data.ContainsKey("monster")
                    ? data["monster"].AsGodotDictionary()
                    : new Godot.Collections.Dictionary(),
                PlayerForce = data.ContainsKey("player_force")
                    ? (int)(long)data["player_force"]
                    : 0,
                InteractionWindowDuration = data.ContainsKey("interaction_window_duration")
                    ? (int)(long)data["interaction_window_duration"]
                    : 30,
            };

            return true;
        }
        catch (Exception ex)
        {
            GameLogger.Exception(ex, "Error parsing COMBAT_START", nameof(MessageProtocol));
            return false;
        }
    }

    /// <summary>
    /// Parses an ERROR message from server data.
    /// </summary>
    /// <param name="data">The message data dictionary.</param>
    /// <param name="error">Output parameter for parsed error.</param>
    /// <returns>True if parsing succeeded; false otherwise.</returns>
    public static bool TryParseError(Godot.Collections.Dictionary data, out ErrorMessage error)
    {
        error = null;

        try
        {
            error = new ErrorMessage
            {
                Code = data.ContainsKey("code") ? (string)data["code"] : "",
                Message = data.ContainsKey("message") ? (string)data["message"] : "",
                Recoverable = data.ContainsKey("recoverable") ? (bool)data["recoverable"] : false,
                SuggestedAction = data.ContainsKey("suggested_action")
                    ? (string)data["suggested_action"]
                    : "",
            };

            return true;
        }
        catch (Exception ex)
        {
            GameLogger.Exception(ex, "Error parsing ERROR", nameof(MessageProtocol));
            return false;
        }
    }

    // ============== HELPER METHODS ==============

    /// <summary>
    /// Parses players array from game state data.
    /// </summary>
    /// <param name="data">The game state dictionary.</param>
    /// <returns>The players array or empty array if not found.</returns>
    private static Godot.Collections.Array ParsePlayers(Godot.Collections.Dictionary data)
    {
        if (!data.ContainsKey("players"))
            return new Godot.Collections.Array();

        return data["players"].AsGodotArray();
    }

    /// <summary>
    /// Parses current turn info from game state data.
    /// </summary>
    /// <param name="data">The game state dictionary.</param>
    /// <returns>The current turn dictionary or empty dictionary if not found.</returns>
    private static Godot.Collections.Dictionary ParseCurrentTurn(Godot.Collections.Dictionary data)
    {
        if (!data.ContainsKey("current_turn"))
            return new Godot.Collections.Dictionary();

        return data["current_turn"].AsGodotDictionary();
    }

    /// <summary>
    /// Parses combat info from game state data.
    /// </summary>
    /// <param name="data">The game state dictionary.</param>
    /// <returns>The combat dictionary or null if not in combat.</returns>
    private static Godot.Collections.Dictionary ParseCombat(Godot.Collections.Dictionary data)
    {
        if (!data.ContainsKey("combat"))
            return null;

        var combat = data["combat"];
        if (combat.VariantType == Variant.Type.Nil)
            return null;

        return combat.AsGodotDictionary();
    }

    /// <summary>
    /// Parses deck info from game state data.
    /// </summary>
    /// <param name="data">The game state dictionary.</param>
    /// <returns>The decks dictionary or empty dictionary if not found.</returns>
    private static Godot.Collections.Dictionary ParseDecks(Godot.Collections.Dictionary data)
    {
        if (!data.ContainsKey("decks"))
            return new Godot.Collections.Dictionary();

        return data["decks"].AsGodotDictionary();
    }

    // ============== MESSAGE DATA CLASSES ==============

    /// <summary>
    /// Contains complete game state information from server.
    /// </summary>
    public class GameStateMessage
    {
        /// <summary>
        /// Gets or sets the game session identifier.
        /// </summary>
        public string GameId { get; set; }

        /// <summary>
        /// Gets or sets the array of player data.
        /// </summary>
        public Godot.Collections.Array Players { get; set; }

        /// <summary>
        /// Gets or sets current turn information.
        /// </summary>
        public Godot.Collections.Dictionary CurrentTurn { get; set; }

        /// <summary>
        /// Gets or sets combat state, or null if not in combat.
        /// </summary>
        public Godot.Collections.Dictionary Combat { get; set; }

        /// <summary>
        /// Gets or sets deck information.
        /// </summary>
        public Godot.Collections.Dictionary Decks { get; set; }

        /// <summary>
        /// Gets or sets winner player ID if game is over.
        /// </summary>
        public string Winner { get; set; }
    }

    /// <summary>
    /// Contains turn phase change information.
    /// </summary>
    public class TurnPhaseChangeMessage
    {
        /// <summary>
        /// Gets or sets the active player ID.
        /// </summary>
        public string PlayerId { get; set; }

        /// <summary>
        /// Gets or sets the new phase string.
        /// </summary>
        public string Phase { get; set; }

        /// <summary>
        /// Gets or sets result data from previous phase.
        /// </summary>
        public Godot.Collections.Dictionary Result { get; set; }
    }

    /// <summary>
    /// Contains combat start information.
    /// </summary>
    public class CombatStartMessage
    {
        /// <summary>
        /// Gets or sets the monster data.
        /// </summary>
        public Godot.Collections.Dictionary Monster { get; set; }

        /// <summary>
        /// Gets or sets the player's initial combat force.
        /// </summary>
        public int PlayerForce { get; set; }

        /// <summary>
        /// Gets or sets the interaction window duration in seconds.
        /// </summary>
        public int InteractionWindowDuration { get; set; }
    }

    /// <summary>
    /// Contains error information.
    /// </summary>
    public class ErrorMessage
    {
        /// <summary>
        /// Gets or sets the error code.
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// Gets or sets the human-readable error message.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets whether the error is recoverable.
        /// </summary>
        public bool Recoverable { get; set; }

        /// <summary>
        /// Gets or sets the suggested action to recover.
        /// </summary>
        public string SuggestedAction { get; set; }
    }

    // ============== UTILITY METHODS ==============

    /// <summary>
    /// Gets a human-readable description of a message type.
    /// </summary>
    /// <param name="messageType">The message type constant.</param>
    /// <returns>A descriptive string.</returns>
    public static string GetMessageDescription(string messageType)
    {
        return messageType switch
        {
            PLAYER_ACTION => "Player takes action during turn",
            PLAY_CARD => "Player plays a card",
            COMBAT_RESPONSE => "Response to combat interaction",
            NEGOTIATION => "Negotiation offer/response",
            USE_ABILITY => "Use class ability",
            GAME_STATE => "Full game state update",
            TURN_PHASE_CHANGE => "Turn phase changed",
            COMBAT_START => "Combat started",
            COMBAT_RESOLUTION => "Combat resolved",
            CARD_PLAY_RESULT => "Card play result",
            PLAYER_UPDATE => "Player state update",
            ERROR => "Error message",
            _ => "Unknown message type",
        };
    }

    /// <summary>
    /// Determines if a message type is a client-to-server message.
    /// </summary>
    /// <param name="messageType">The message type to check.</param>
    /// <returns>True if it's a client message; false otherwise.</returns>
    public static bool IsClientMessage(string messageType)
    {
        return messageType == PLAYER_ACTION
            || messageType == PLAY_CARD
            || messageType == COMBAT_RESPONSE
            || messageType == NEGOTIATION
            || messageType == USE_ABILITY;
    }

    /// <summary>
    /// Determines if a message type is a server-to-client message.
    /// </summary>
    /// <param name="messageType">The message type to check.</param>
    /// <returns>True if it's a server message; false otherwise.</returns>
    public static bool IsServerMessage(string messageType)
    {
        return messageType == GAME_STATE
            || messageType == TURN_PHASE_CHANGE
            || messageType == COMBAT_START
            || messageType == COMBAT_RESOLUTION
            || messageType == CARD_PLAY_RESULT
            || messageType == PLAYER_UPDATE
            || messageType == ERROR;
    }

    // ============== CARD REFERENCE NOTES ==============

    // Cards are referenced by unique card_id string in all network messages.
    // Full card data (stats, effects, etc.) is NOT transmitted over the network.
    // Both client and server maintain their own card database loaded from local files.
    // Example card IDs: "monster_goblin_001", "item_broad_sword_001", "race_elf_001"

    /// <summary>
    /// Looks up card data by ID using local CardFactory.
    /// </summary>
    /// <param name="cardId">The unique card identifier.</param>
    /// <returns>The CardData if found; null otherwise.</returns>
    /// <remarks>
    /// Per AGENTS.md architecture: Cards are referenced by ID in network messages.
    /// Full card definitions should be loaded from Resources/Cards/Definitions/ at startup.
    /// </remarks>
    public static CardData GetCardById(string cardId)
    {
        if (string.IsNullOrEmpty(cardId))
            return null;

        // CardFactory should be initialized and loaded with all card definitions
        // This is the preferred way to get card data - never send full card data over network
        return CardFactory.Instance.GetCardById(cardId);
    }
}
