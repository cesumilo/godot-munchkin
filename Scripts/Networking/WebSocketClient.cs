using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Manages WebSocket connection to the game server for real-time communication.
/// Handles connection lifecycle, message sending/receiving, and reconnection logic.
/// </summary>
/// <remarks>
/// Per AGENTS.md architecture: Uses Godot's WebSocketPeer for custom WebSocket implementation.
/// Not using Godot's built-in MultiplayerAPI. Connection includes JWT token for authentication.
/// </remarks>
public partial class WebSocketClient : Node
{
    // Singleton instance
    private static WebSocketClient _instance;

    /// <summary>
    /// Gets the singleton instance of WebSocketClient.
    /// </summary>
    public static WebSocketClient Instance => _instance;

    // Connection state
    private WebSocketPeer _webSocketPeer;
    private string _serverUrl = "";
    private bool _isConnecting = false;
    private float _reconnectTimer = 0f;
    private const float RECONNECT_INTERVAL = 5f; // seconds

    // Event delegates
    /// <summary>
    /// Delegate for connection state change events.
    /// </summary>
    /// <param name="connected">True if now connected; false if disconnected.</param>
    public delegate void ConnectionStateChangedHandler(bool connected);

    /// <summary>
    /// Delegate for received message events.
    /// </summary>
    /// <param name="messageType">The type of message received.</param>
    /// <param name="data">The message data dictionary.</param>
    public delegate void MessageReceivedHandler(
        string messageType,
        Godot.Collections.Dictionary data
    );

    /// <summary>
    /// Delegate for error events.
    /// </summary>
    /// <param name="errorMessage">The error description.</param>
    public delegate void ErrorHandler(string errorMessage);

    /// <summary>
    /// Emitted when connection state changes.
    /// </summary>
    public event ConnectionStateChangedHandler ConnectionStateChanged;

    /// <summary>
    /// Emitted when a message is received from the server.
    /// </summary>
    public event MessageReceivedHandler MessageReceived;

    /// <summary>
    /// Emitted when an error occurs.
    /// </summary>
    public event ErrorHandler ErrorOccurred;

    // Message queue for outgoing messages
    private Queue<string> _outgoingMessageQueue = new Queue<string>();
    private bool _isProcessingQueue = false;

    // Connection statistics
    /// <summary>
    /// Gets the number of messages sent since connection.
    /// </summary>
    public int MessagesSent { get; private set; } = 0;

    /// <summary>
    /// Gets the number of messages received since connection.
    /// </summary>
    public int MessagesReceived { get; private set; } = 0;

    /// <summary>
    /// Gets the timestamp of last successful connection.
    /// </summary>
    public DateTime LastConnectionTime { get; private set; }

    /// <summary>
    /// Initializes the WebSocketClient singleton and sets process mode.
    /// </summary>
    /// <remarks>
    /// Uses ProcessModeEnum.Always to continue processing even when game is paused.
    /// </remarks>
    public override void _Ready()
    {
        if (_instance != null && _instance != this)
        {
            QueueFree();
            return;
        }

        _instance = this;
        ProcessMode = ProcessModeEnum.Always; // Keep processing even when paused

        GameLogger.Info("Initialized", this);
    }

    /// <summary>
    /// Polls WebSocket connection and handles messages each frame.
    /// </summary>
    /// <param name="delta">Elapsed time since last frame in seconds.</param>
    /// <remarks>
    /// Per Godot 4.6 API: WebSocketPeer requires polling in _Process, not event-driven.
    /// </remarks>
    public override void _Process(double delta)
    {
        if (_webSocketPeer == null)
            return;

        _webSocketPeer.Poll();

        // Handle connection state
        var state = _webSocketPeer.GetReadyState();
        bool wasConnected = IsConnected();
        bool isConnected = state == WebSocketPeer.State.Open;

        // Update connecting state based on actual WebSocket state
        if (state != WebSocketPeer.State.Connecting && _isConnecting)
        {
            _isConnecting = false;
            GameLogger.Debug($"Connection attempt finished (state={state})", this);
        }

        // Fire connection state changed event if needed
        if (wasConnected != isConnected)
        {
            GameLogger.Info($"Connection state changed: {isConnected}", this);
            ConnectionStateChanged?.Invoke(isConnected);

            if (isConnected)
            {
                LastConnectionTime = DateTime.Now;
                GameLogger.Info("Connected successfully", this);

                // Send JOIN_GAME message with JWT token if available
                SendJoinGameMessage();
                ProcessMessageQueue(); // Start processing queued messages
            }
            else
            {
                GameLogger.Info("Disconnected", this);
            }
        }

        // Handle incoming messages
        while (_webSocketPeer.GetAvailablePacketCount() > 0)
        {
            byte[] packet = _webSocketPeer.GetPacket();
            string message = Encoding.UTF8.GetString(packet);
            MessagesReceived++;

            HandleIncomingMessage(message);
        }

        // Handle connection closure
        if (state == WebSocketPeer.State.Closed)
        {
            var code = _webSocketPeer.GetCloseCode();
            var reason = _webSocketPeer.GetCloseReason();

            if (code != -1)
            {
                // Connection was closed by peer or with error code
                string errorMsg = $"Connection closed: Code={code}, Reason={reason}";
                GameLogger.Error(errorMsg, this);
                ErrorOccurred?.Invoke(errorMsg);
            }
            else if (_isConnecting)
            {
                // Connection failed during connect attempt
                string errorMsg = "Connection failed: Could not connect to server";
                GameLogger.Error(errorMsg, this);
                ErrorOccurred?.Invoke(errorMsg);
            }
            else
            {
                // Normal closure without error
                GameLogger.Info("Connection closed normally", this);
            }

            // Attempt reconnect if we were connected before
            if (wasConnected)
            {
                _reconnectTimer += (float)delta;
                if (_reconnectTimer >= RECONNECT_INTERVAL)
                {
                    _reconnectTimer = 0f;
                    GameLogger.Info("Attempting to reconnect...", this);
                    ConnectToServer(_serverUrl);
                }
            }
        }
    }

    /// <summary>
    /// Initiates connection to a WebSocket server.
    /// </summary>
    /// <param name="url">The WebSocket URL (e.g., ws://server:port/lobby/id/ws).</param>
    /// <returns>True if connection attempt started successfully; false otherwise.</returns>
    /// <remarks>
    /// Per AGENTS.md: URL should include JWT token as query parameter for authentication.
    /// </remarks>
    public bool ConnectToServer(string url)
    {
        GameLogger.Debug($"ConnectToServer called with URL: {url}", this);

        if (_isConnecting)
        {
            GameLogger.Debug("Already connecting to server, returning false", this);
            return false;
        }

        _serverUrl = url;
        _isConnecting = true;

        try
        {
            GameLogger.Debug($"Creating new WebSocketPeer and connecting to {url}", this);

            // Create WebSocket peer
            _webSocketPeer = new WebSocketPeer();

            // Connect to server
            Error error = _webSocketPeer.ConnectToUrl(url);

            if (error != Error.Ok)
            {
                GameLogger.Error($"Failed to start connection: {error}", this);
                ErrorOccurred?.Invoke($"Failed to start connection: {error}");
                _isConnecting = false;
                _webSocketPeer = null;
                return false;
            }

            GameLogger.Info("Connection attempt started successfully", this);
            return true;
        }
        catch (Exception ex)
        {
            GameLogger.Exception(ex, "Exception during connection", this);
            ErrorOccurred?.Invoke($"Connection exception: {ex.Message}");
            _isConnecting = false;
            return false;
        }
    }

    /// <summary>
    /// Disconnects from the server and cleans up resources.
    /// </summary>
    public void Disconnect()
    {
        if (_webSocketPeer != null)
        {
            GameLogger.Info("Disconnecting from server", this);
            _webSocketPeer.Close();
            _webSocketPeer = null;
        }

        _isConnecting = false;
        _outgoingMessageQueue.Clear();
        _isProcessingQueue = false;
    }

    /// <summary>
    /// Resets connection state flags without disconnecting.
    /// </summary>
    /// <remarks>
    /// Useful for clearing stale connection state before reconnection attempts.
    /// </remarks>
    public void ResetConnectionState()
    {
        _isConnecting = false;
    }

    /// <summary>
    /// Sends a message to the server.
    /// </summary>
    /// <param name="message">The JSON message string to send.</param>
    /// <param name="queueIfDisconnected">If true, queues message for later if not connected.</param>
    /// <returns>True if message sent or queued successfully; false otherwise.</returns>
    public bool SendMessage(string message, bool queueIfDisconnected = true)
    {
        if (IsConnected() && _webSocketPeer != null)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(message);
                Error error = _webSocketPeer.Send(data);

                if (error == Error.Ok)
                {
                    MessagesSent++;
                    string preview =
                        message.Length > 50 ? message.Substring(0, 50) + "..." : message;
                    GameLogger.Debug($"Message sent: {preview}", this);
                    return true;
                }
                else
                {
                    GameLogger.Error($"Failed to send message: {error}", this);
                    ErrorOccurred?.Invoke($"Failed to send message: {error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                GameLogger.Exception(ex, "Exception sending message", this);
                ErrorOccurred?.Invoke($"Message send exception: {ex.Message}");
                return false;
            }
        }
        else if (queueIfDisconnected)
        {
            // Queue message for when we reconnect
            _outgoingMessageQueue.Enqueue(message);
            string preview = message.Length > 50 ? message.Substring(0, 50) + "..." : message;
            GameLogger.Debug($"Message queued (not connected): {preview}", this);
            return true;
        }

        GameLogger.Error("Cannot send message: Not connected", this);
        return false;
    }

    /// <summary>
    /// Sends JOIN_GAME message with player ID after connection.
    /// </summary>
    /// <remarks>
    /// Automatically called after successful connection to identify the player.
    /// </remarks>
    private void SendJoinGameMessage()
    {
        if (string.IsNullOrEmpty(Main.PlayerId))
        {
            GameLogger.Error("Cannot send JOIN_GAME: PlayerId not available", this);
            return;
        }

        var joinData = new Godot.Collections.Dictionary { ["player_id"] = Main.PlayerId };
        SendStructuredMessage("JOIN_GAME", joinData);
    }

    /// <summary>
    /// Sends a structured message with type and data.
    /// </summary>
    /// <param name="messageType">The message type identifier.</param>
    /// <param name="data">The message data dictionary.</param>
    /// <returns>True if message sent successfully; false otherwise.</returns>
    public bool SendStructuredMessage(string messageType, Godot.Collections.Dictionary data)
    {
        try
        {
            var message = new Godot.Collections.Dictionary
            {
                ["type"] = messageType,
                ["data"] = data,
            };

            string jsonString = Json.Stringify(message);
            return SendMessage(jsonString);
        }
        catch (Exception ex)
        {
            GameLogger.Exception(ex, "Failed to create structured message", this);
            ErrorOccurred?.Invoke($"Message creation failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Processes queued outgoing messages.
    /// </summary>
    /// <remarks>
    /// Sends queued messages with a small delay between each to avoid flooding.
    /// </remarks>
    private async void ProcessMessageQueue()
    {
        if (_isProcessingQueue || !IsConnected())
            return;

        _isProcessingQueue = true;

        while (_outgoingMessageQueue.Count > 0 && IsConnected())
        {
            string message = _outgoingMessageQueue.Dequeue();

            // Small delay between messages to avoid flooding
            await Task.Delay(50);

            if (!SendMessage(message, false))
            {
                // If send fails, re-queue the message
                _outgoingMessageQueue.Enqueue(message);
                break;
            }
        }

        _isProcessingQueue = false;
    }

    /// <summary>
    /// Parses and dispatches an incoming message.
    /// </summary>
    /// <param name="message">The raw JSON message string.</param>
    /// <remarks>
    /// Parses message type and emits MessageReceived event with structured data.
    /// </remarks>
    private void HandleIncomingMessage(string message)
    {
        try
        {
            string preview = message.Length > 100 ? message.Substring(0, 100) + "..." : message;
            GameLogger.Debug($"Received: {preview}", this);

            var json = new Json();
            Error parseError = json.Parse(message);

            if (parseError != Error.Ok)
            {
                GameLogger.Error($"Failed to parse JSON: {parseError}", this);
                ErrorOccurred?.Invoke($"Failed to parse server message: {parseError}");
                return;
            }

            var data = json.Data.AsGodotDictionary();

            if (!data.ContainsKey("type"))
            {
                GameLogger.Error("Message missing 'type' field", this);
                ErrorOccurred?.Invoke("Server message missing 'type' field");
                return;
            }

            string messageType = (string)data["type"];
            Godot.Collections.Dictionary messageData = data.ContainsKey("data")
                ? data["data"].AsGodotDictionary()
                : new Godot.Collections.Dictionary();

            GameLogger.Debug($"Parsed message type: {messageType}", this);

            // Fire message received event
            MessageReceived?.Invoke(messageType, messageData);
        }
        catch (Exception ex)
        {
            GameLogger.Exception(ex, "Error handling message", this);
            ErrorOccurred?.Invoke($"Message handling error: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the current connection status.
    /// </summary>
    /// <returns>True if connected and WebSocket is open; false otherwise.</returns>
    public bool IsConnected()
    {
        return _webSocketPeer != null && _webSocketPeer.GetReadyState() == WebSocketPeer.State.Open;
    }

    /// <summary>
    /// Gets whether a connection is currently being attempted.
    /// </summary>
    /// <returns>True if connection is in progress; false otherwise.</returns>
    public bool IsConnecting()
    {
        return _isConnecting;
    }

    /// <summary>
    /// Gets connection statistics.
    /// </summary>
    /// <returns>Dictionary containing connection statistics.</returns>
    public Godot.Collections.Dictionary GetStatistics()
    {
        return new Godot.Collections.Dictionary
        {
            ["messages_sent"] = MessagesSent,
            ["messages_received"] = MessagesReceived,
            ["is_connected"] = IsConnected(),
            ["is_connecting"] = _isConnecting,
            ["queued_messages"] = _outgoingMessageQueue.Count,
            ["last_connection_time"] = LastConnectionTime.ToString("yyyy-MM-dd HH:mm:ss"),
            ["server_url"] = _serverUrl,
        };
    }

    /// <summary>
    /// Cleans up resources when exiting scene tree.
    /// </summary>
    public override void _ExitTree()
    {
        Disconnect();

        if (_instance == this)
        {
            _instance = null;
        }

        GameLogger.Info("Cleaned up", this);
    }
}
