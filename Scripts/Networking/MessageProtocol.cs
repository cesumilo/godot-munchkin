using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Message protocol for Munchkin WebSocket communication
/// Based on PROTOCOL.md specification
/// </summary>
public static class MessageProtocol
{
    // ============== CLIENT MESSAGE TYPES ==============

    public const string JOIN_GAME = "JOIN_GAME";
    public const string PLAYER_ACTION = "PLAYER_ACTION";
    public const string PLAY_CARD = "PLAY_CARD";
    public const string COMBAT_RESPONSE = "COMBAT_RESPONSE";
    public const string NEGOTIATION = "NEGOTIATION";
    public const string USE_ABILITY = "USE_ABILITY";

    // ============== SERVER MESSAGE TYPES ==============

    public const string GAME_STATE = "GAME_STATE";
    public const string TURN_PHASE_CHANGE = "TURN_PHASE_CHANGE";
    public const string COMBAT_START = "COMBAT_START";
    public const string COMBAT_RESOLUTION = "COMBAT_RESOLUTION";
    public const string CARD_PLAY_RESULT = "CARD_PLAY_RESULT";
    public const string PLAYER_UPDATE = "PLAYER_UPDATE";
    public const string ERROR = "ERROR";

    // ============== ENUMERATIONS ==============

    /// <summary>
    /// Player actions during their turn
    /// </summary>
    public enum PlayerActionType
    {
        OPEN_DOOR,
        LOOK_FOR_TROUBLE,
        LOOT_ROOM,
        END_TURN,
    }

    /// <summary>
    /// Combat response options
    /// </summary>
    public enum CombatResponseType
    {
        ACCEPT_ALLIANCE,
        DECLINE_ALLIANCE,
        FLEE,
        PLAY_CARD,
    }

    /// <summary>
    /// Negotiation actions
    /// </summary>
    public enum NegotiationActionType
    {
        OFFER,
        ACCEPT,
        REJECT,
        COUNTER_OFFER,
    }

    /// <summary>
    /// Class abilities
    /// </summary>
    public enum AbilityType
    {
        THIEF_STEAL,
        WARRIOR_DISCARD,
        MAGE_CHARM,
        CLERIC_RESURRECT,
    }

    /// <summary>
    /// Turn phases from game rules §7
    /// </summary>
    public enum TurnPhase
    {
        OPEN_DOOR,
        LOOK_FOR_TROUBLE,
        LOOT_ROOM,
        CHARITY,
        TURN_END,
    }

    /// <summary>
    /// Combat results
    /// </summary>
    public enum CombatResult
    {
        VICTORY,
        DEFEAT,
    }

    /// <summary>
    /// Error codes
    /// </summary>
    public enum ErrorCode
    {
        INVALID_ACTION,
        NOT_YOUR_TURN,
        INVALID_PHASE,
        NETWORK_ERROR,
    }

    // ============== DATA STRUCTURES ==============

    /// <summary>
    /// Base message structure
    /// </summary>
    public class WebSocketMessage
    {
        public string Type { get; set; }
        public Godot.Collections.Dictionary Data { get; set; }

        public WebSocketMessage(string type, Godot.Collections.Dictionary data)
        {
            Type = type;
            Data = data;
        }

        public string ToJson()
        {
            var message = new Godot.Collections.Dictionary { ["type"] = Type, ["data"] = Data };

            return Json.Stringify(message);
        }
    }

    // ============== CLIENT MESSAGE BUILDERS ==============

    /// <summary>
    /// Create JOIN_GAME message
    /// </summary>
    public static WebSocketMessage CreateJoinGame(string playerId, string token = null)
    {
        var data = new Godot.Collections.Dictionary { ["player_id"] = playerId };

        if (!string.IsNullOrEmpty(token))
        {
            data["token"] = token;
        }

        return new WebSocketMessage(JOIN_GAME, data);
    }

    /// <summary>
    /// Create PLAYER_ACTION message
    /// </summary>
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
    /// Create PLAY_CARD message
    /// </summary>
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
    /// Create COMBAT_RESPONSE message
    /// </summary>
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
    /// Create NEGOTIATION message
    /// </summary>
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
    /// Create USE_ABILITY message
    /// </summary>
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
    /// Parse incoming message
    /// </summary>
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
                GD.PrintErr($"[MessageProtocol] Failed to parse JSON: {parseError}");
                return false;
            }

            var parsedData = jsonParser.Data.AsGodotDictionary();

            if (!parsedData.ContainsKey("type"))
            {
                GD.PrintErr("[MessageProtocol] Message missing 'type' field");
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
            GD.PrintErr($"[MessageProtocol] Error parsing message: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Parse GAME_STATE message
    /// </summary>
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
            GD.PrintErr($"[MessageProtocol] Error parsing GAME_STATE: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Parse TURN_PHASE_CHANGE message
    /// </summary>
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
            GD.PrintErr($"[MessageProtocol] Error parsing TURN_PHASE_CHANGE: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Parse COMBAT_START message
    /// </summary>
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
            GD.PrintErr($"[MessageProtocol] Error parsing COMBAT_START: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Parse ERROR message
    /// </summary>
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
            GD.PrintErr($"[MessageProtocol] Error parsing ERROR: {ex.Message}");
            return false;
        }
    }

    // ============== HELPER METHODS ==============

    private static Godot.Collections.Array ParsePlayers(Godot.Collections.Dictionary data)
    {
        if (!data.ContainsKey("players"))
            return new Godot.Collections.Array();

        return data["players"].AsGodotArray();
    }

    private static Godot.Collections.Dictionary ParseCurrentTurn(Godot.Collections.Dictionary data)
    {
        if (!data.ContainsKey("current_turn"))
            return new Godot.Collections.Dictionary();

        return data["current_turn"].AsGodotDictionary();
    }

    private static Godot.Collections.Dictionary ParseCombat(Godot.Collections.Dictionary data)
    {
        if (!data.ContainsKey("combat"))
            return null;

        var combat = data["combat"];
        if (combat.VariantType == Variant.Type.Nil)
            return null;

        return combat.AsGodotDictionary();
    }

    private static Godot.Collections.Dictionary ParseDecks(Godot.Collections.Dictionary data)
    {
        if (!data.ContainsKey("decks"))
            return new Godot.Collections.Dictionary();

        return data["decks"].AsGodotDictionary();
    }

    // ============== MESSAGE DATA CLASSES ==============

    public class GameStateMessage
    {
        public string GameId { get; set; }
        public Godot.Collections.Array Players { get; set; }
        public Godot.Collections.Dictionary CurrentTurn { get; set; }
        public Godot.Collections.Dictionary Combat { get; set; }
        public Godot.Collections.Dictionary Decks { get; set; }
        public string Winner { get; set; }
    }

    public class TurnPhaseChangeMessage
    {
        public string PlayerId { get; set; }
        public string Phase { get; set; }
        public Godot.Collections.Dictionary Result { get; set; }
    }

    public class CombatStartMessage
    {
        public Godot.Collections.Dictionary Monster { get; set; }
        public int PlayerForce { get; set; }
        public int InteractionWindowDuration { get; set; }
    }

    public class ErrorMessage
    {
        public string Code { get; set; }
        public string Message { get; set; }
        public bool Recoverable { get; set; }
        public string SuggestedAction { get; set; }
    }

    // ============== UTILITY METHODS ==============

    /// <summary>
    /// Get human-readable description of message type
    /// </summary>
    public static string GetMessageDescription(string messageType)
    {
        return messageType switch
        {
            JOIN_GAME => "Client joins the game",
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
    /// Validate if a message type is a client message
    /// </summary>
    public static bool IsClientMessage(string messageType)
    {
        return messageType == JOIN_GAME
            || messageType == PLAYER_ACTION
            || messageType == PLAY_CARD
            || messageType == COMBAT_RESPONSE
            || messageType == NEGOTIATION
            || messageType == USE_ABILITY;
    }

    /// <summary>
    /// Validate if a message type is a server message
    /// </summary>
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
}
