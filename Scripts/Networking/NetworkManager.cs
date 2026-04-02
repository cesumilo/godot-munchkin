using System;
using Godot;

/// <summary>
/// Manages network connectivity as a singleton autoload.
/// Provides centralized access to WebSocket client and HTTP operations.
/// </summary>
/// <remarks>
/// Per AGENTS.md architecture: Configured as autoload singleton for global access.
/// Initializes WebSocketClient and provides helper methods for common network operations.
/// </remarks>
public partial class NetworkManager : Node
{
    // Singleton instance
    private static NetworkManager _instance;

    /// <summary>
    /// Gets the singleton instance of NetworkManager.
    /// </summary>
    public static NetworkManager Instance => _instance;

    /// <summary>
    /// Gets the WebSocket client for real-time communication.
    /// </summary>
    public WebSocketClient WebSocketClient { get; private set; }

    /// <summary>
    /// The base URL for the game server.
    /// </summary>
    public const string SERVER_BASE_URL = "http://90.28.104.14:1337";

    /// <summary>
    /// The WebSocket base URL for the game server.
    /// </summary>
    public const string SERVER_WS_BASE_URL = "ws://90.28.104.14:1337";

    /// <summary>
    /// Initializes the NetworkManager singleton and WebSocket client.
    /// </summary>
    /// <remarks>
    /// Uses ProcessModeEnum.Always to continue processing during pauses.
    /// </remarks>
    public override void _Ready()
    {
        if (_instance != null && _instance != this)
        {
            QueueFree();
            return;
        }

        _instance = this;
        ProcessMode = ProcessModeEnum.Always;

        // Initialize WebSocket client
        WebSocketClient = new WebSocketClient();
        AddChild(WebSocketClient);

        GD.Print("[NetworkManager] Initialized with WebSocket client");
    }

    /// <summary>
    /// Connects to a lobby WebSocket endpoint.
    /// </summary>
    /// <param name="lobbyId">The unique lobby identifier.</param>
    /// <returns>True if connection attempt started; false if failed.</returns>
    /// <remarks>
    /// Per AGENTS.md: Includes JWT token in query parameters for authentication.
    /// </remarks>
    public bool ConnectToLobby(string lobbyId)
    {
        if (string.IsNullOrEmpty(lobbyId))
        {
            GD.PrintErr("[NetworkManager] Cannot connect: lobbyId is empty");
            return false;
        }

        if (string.IsNullOrEmpty(Main.JwtToken))
        {
            GD.PrintErr("[NetworkManager] Cannot connect: JWT token not available");
            return false;
        }

        // Add token as query parameter per server implementation
        string wsUrl =
            $"{SERVER_WS_BASE_URL}/lobby/{lobbyId}/ws?token={Uri.EscapeDataString(Main.JwtToken)}";
        GD.Print($"[NetworkManager] Connecting to WebSocket: {wsUrl}");

        return WebSocketClient.ConnectToServer(wsUrl);
    }

    /// <summary>
    /// Sends a player action message to the server.
    /// </summary>
    /// <param name="action">The action type to perform.</param>
    /// <returns>True if message sent; false otherwise.</returns>
    /// <remarks>
    /// Per §7: Actions include OPEN_DOOR, LOOK_FOR_TROUBLE, LOOT_ROOM, END_TURN.
    /// </remarks>
    public bool SendPlayerAction(MessageProtocol.PlayerActionType action)
    {
        var message = MessageProtocol.CreatePlayerAction(action);
        return WebSocketClient.SendMessage(message.ToJson());
    }

    /// <summary>
    /// Sends a play card message to the server.
    /// </summary>
    /// <param name="cardId">The unique card identifier to play.</param>
    /// <param name="targetPlayerId">Optional target player for targeted cards.</param>
    /// <returns>True if message sent; false otherwise.</returns>
    /// <remarks>
    /// Per §4.4: Cards can be played according to their MomentJeu restriction.
    /// </remarks>
    public bool SendPlayCard(string cardId, string targetPlayerId = null)
    {
        var message = MessageProtocol.CreatePlayCard(cardId, targetPlayerId);
        return WebSocketClient.SendMessage(message.ToJson());
    }

    /// <summary>
    /// Sends a combat response message to the server.
    /// </summary>
    /// <param name="response">The combat response type.</param>
    /// <param name="cardId">Optional card ID if playing a card.</param>
    /// <returns>True if message sent; false otherwise.</returns>
    /// <remarks>
    /// Per §8.2 and §8.6: Used during combat interaction window or flee attempts.
    /// </remarks>
    public bool SendCombatResponse(
        MessageProtocol.CombatResponseType response,
        string cardId = null
    )
    {
        var message = MessageProtocol.CreateCombatResponse(response, cardId);
        return WebSocketClient.SendMessage(message.ToJson());
    }

    /// <summary>
    /// Sends a use ability message to the server.
    /// </summary>
    /// <param name="ability">The class ability to use.</param>
    /// <param name="targetPlayerId">Optional target for targeted abilities.</param>
    /// <param name="cardIds">Optional card IDs for abilities requiring discards.</param>
    /// <returns>True if message sent; false otherwise.</returns>
    /// <remarks>
    /// Per §5.2: Class abilities like Thief steal, Warrior discard bonus, etc.
    /// </remarks>
    public bool SendUseAbility(
        MessageProtocol.AbilityType ability,
        string targetPlayerId = null,
        string[] cardIds = null
    )
    {
        var message = MessageProtocol.CreateUseAbility(ability, targetPlayerId, cardIds);
        return WebSocketClient.SendMessage(message.ToJson());
    }

    /// <summary>
    /// Gets current connection statistics.
    /// </summary>
    /// <returns>Dictionary with connection stats, or empty dictionary if not initialized.</returns>
    public Godot.Collections.Dictionary GetConnectionStats()
    {
        return WebSocketClient?.GetStatistics() ?? new Godot.Collections.Dictionary();
    }

    /// <summary>
    /// Checks if connected to a lobby WebSocket.
    /// </summary>
    /// <returns>True if connected; false otherwise.</returns>
    public bool IsConnected()
    {
        return WebSocketClient?.IsConnected() ?? false;
    }

    /// <summary>
    /// Disconnects from the server.
    /// </summary>
    public void Disconnect()
    {
        WebSocketClient?.Disconnect();
        GD.Print("[NetworkManager] Disconnected from server");
    }

    /// <summary>
    /// Cleans up resources when exiting scene tree.
    /// </summary>
    public override void _ExitTree()
    {
        if (_instance == this)
        {
            _instance = null;
        }

        GD.Print("[NetworkManager] Cleaned up");
    }
}
