using Godot;
using System;

/// <summary>
/// Network manager that initializes WebSocket client as an autoload
/// </summary>
public partial class NetworkManager : Node
{
    private static NetworkManager _instance;
    public static NetworkManager Instance => _instance;

    public WebSocketClient WebSocketClient { get; private set; }

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
    /// Connect to lobby WebSocket
    /// </summary>
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
        string wsUrl = $"ws://90.28.104.14:1337/lobby/{lobbyId}/ws?token={Uri.EscapeDataString(Main.JwtToken)}";
        GD.Print($"[NetworkManager] Connecting to WebSocket: {wsUrl}");

        return WebSocketClient.ConnectToServer(wsUrl);
    }

    /// <summary>
    /// Send a player action
    /// </summary>
    public bool SendPlayerAction(MessageProtocol.PlayerActionType action)
    {
        var message = MessageProtocol.CreatePlayerAction(action);
        return WebSocketClient.SendMessage(message.ToJson());
    }

    /// <summary>
    /// Send a play card action
    /// </summary>
    public bool SendPlayCard(string cardId, string targetPlayerId = null)
    {
        var message = MessageProtocol.CreatePlayCard(cardId, targetPlayerId);
        return WebSocketClient.SendMessage(message.ToJson());
    }

    /// <summary>
    /// Get connection statistics
    /// </summary>
    public Godot.Collections.Dictionary GetConnectionStats()
    {
        return WebSocketClient?.GetStatistics() ?? new Godot.Collections.Dictionary();
    }

    /// <summary>
    /// Clean up
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
