using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
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
    private bool _isConnected = false;
    private WebSocketPeer.State _lastState = WebSocketPeer.State.Closed;
    private bool _lastIsConnecting = false;
    private bool _lastIsConnected = false;
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

        GD.Print("[WebSocketClient] Initialized");
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
        bool wasConnected = _isConnected;
        _isConnected = state == WebSocketPeer.State.Open;

        // Only print state changes for debugging
        if (
            state != _lastState
            || _isConnecting != _lastIsConnecting
            || _isConnected != _lastIsConnected
        )
        {
            GD.Print(
                $"[WebSocketClient] State: {state}, IsConnecting: {_isConnecting}, IsConnected: {_isConnected}"
            );
            _lastState = state;
            _lastIsConnecting = _isConnecting;
            _lastIsConnected = _isConnected;
        }

        // Update connecting state based on actual WebSocket state
        if (state != WebSocketPeer.State.Connecting && _isConnecting)
        {
            _isConnecting = false;
            GD.Print(
                $"[WebSocketClient] Connection attempt finished (state={state}), setting _isConnecting=false"
            );
        }

        // Fire connection state changed event if needed
        if (wasConnected != _isConnected)
        {
            GD.Print($"[WebSocketClient] Connection state changed: {_isConnected}");
            ConnectionStateChanged?.Invoke(_isConnected);

            if (_isConnected)
            {
                LastConnectionTime = DateTime.Now;
                GD.Print("[WebSocketClient] Connected successfully");

                // Send JOIN_GAME message with JWT token if available
                SendJoinGameMessage();

                ProcessMessageQueue(); // Start processing queued messages
            }
            else
            {
                GD.Print("[WebSocketClient] Disconnected");
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

            string errorMsg;
            if (code != -1)
            {
                // Connection was closed by peer or with error code
                errorMsg = $"[WebSocketClient] Connection closed: Code={code}, Reason={reason}";
                GD.PrintErr(errorMsg);
                ErrorOccurred?.Invoke(errorMsg);
            }
            else if (_isConnecting)
            {
                // Connection failed during connect attempt
                errorMsg = $"[WebSocketClient] Connection failed: Could not connect to server";
                GD.PrintErr(errorMsg);
                ErrorOccurred?.Invoke(errorMsg);
            }
            else
            {
                // Normal closure without error
                GD.Print("[WebSocketClient] Connection closed normally");
            }

            // Attempt reconnect if we were connected before
            if (wasConnected)
            {
                _reconnectTimer += (float)delta;
                if (_reconnectTimer >= RECONNECT_INTERVAL)
                {
                    _reconnectTimer = 0f;
                    GD.Print("[WebSocketClient] Attempting to reconnect...");
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
        GD.Print($"[WebSocketClient] ConnectToServer called with URL: {url}");
        GD.Print(
            $"[WebSocketClient] Current state: _isConnecting={_isConnecting}, _isConnected={_isConnected}, _webSocketPeer={_webSocketPeer}"
        );

        if (_isConnecting)
        {
            GD.Print("[WebSocketClient] Already connecting to server, returning false");
            return false;
        }

        _serverUrl = url;
        _isConnecting = true;
        _lastIsConnecting = false; // Reset to force state change print
        GD.Print($"[WebSocketClient] Set _isConnecting=true");

        try
        {
            GD.Print($"[WebSocketClient] Creating new WebSocketPeer and connecting to {url}");

            // Create WebSocket peer
            _webSocketPeer = new WebSocketPeer();

            // JWT token is included in the URL query parameter by NetworkManager

            // Connect to server
            Error error = _webSocketPeer.ConnectToUrl(url);
            GD.Print($"[WebSocketClient] ConnectToUrl returned: {error}");

            if (error != Error.Ok)
            {
                GD.PrintErr($"[WebSocketClient] Failed to start connection: {error}");
                ErrorOccurred?.Invoke($"Failed to start connection: {error}");
                _isConnecting = false;
                _webSocketPeer = null;
                GD.Print($"[WebSocketClient] Connection failed, set _isConnecting=false");
                return false;
            }

            GD.Print("[WebSocketClient] Connection attempt started successfully");
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[WebSocketClient] Exception during connection: {ex.Message}");
            ErrorOccurred?.Invoke($"Connection exception: {ex.Message}");
            _isConnecting = false;
            GD.Print($"[WebSocketClient] Exception, set _isConnecting=false");
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
            GD.Print("[WebSocketClient] Disconnecting from server");
            _webSocketPeer.Close();
            _webSocketPeer = null;
        }

        _isConnecting = false;
        _isConnected = false;
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
        _isConnected = false;
    }

    /// <summary>
    /// Sends a message to the server.
    /// </summary>
    /// <param name="message">The JSON message string to send.</param>
    /// <param name="queueIfDisconnected">If true, queues message for later if not connected.</param>
    /// <returns>True if message sent or queued successfully; false otherwise.</returns>
    public bool SendMessage(string message, bool queueIfDisconnected = true)
    {
        if (_isConnected && _webSocketPeer != null)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(message);
                Error error = _webSocketPeer.Send(data);

                if (error == Error.Ok)
                {
                    MessagesSent++;
                    GD.Print(
                        $"[WebSocketClient] Message sent: {message.Substring(0, Math.Min(50, message.Length))}..."
                    );
                    return true;
                }
                else
                {
                    GD.PrintErr($"[WebSocketClient] Failed to send message: {error}");
                    ErrorOccurred?.Invoke($"Failed to send message: {error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[WebSocketClient] Exception sending message: {ex.Message}");
                ErrorOccurred?.Invoke($"Message send exception: {ex.Message}");
                return false;
            }
        }
        else if (queueIfDisconnected)
        {
            // Queue message for when we reconnect
            _outgoingMessageQueue.Enqueue(message);
            GD.Print(
                $"[WebSocketClient] Message queued (not connected): {message.Substring(0, Math.Min(50, message.Length))}..."
            );
            return true;
        }

        GD.PrintErr("[WebSocketClient] Cannot send message: Not connected");
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
            GD.PrintErr("[WebSocketClient] Cannot send JOIN_GAME: PlayerId not available");
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
            GD.PrintErr($"[WebSocketClient] Failed to create structured message: {ex.Message}");
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
        if (_isProcessingQueue || !_isConnected)
            return;

        _isProcessingQueue = true;

        while (_outgoingMessageQueue.Count > 0 && _isConnected)
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
            GD.Print(
                $"[WebSocketClient] Received: {message.Substring(0, Math.Min(100, message.Length))}..."
            );

            var json = new Json();
            Error parseError = json.Parse(message);

            if (parseError != Error.Ok)
            {
                GD.PrintErr($"[WebSocketClient] Failed to parse JSON: {parseError}");
                ErrorOccurred?.Invoke($"Failed to parse server message: {parseError}");
                return;
            }

            var data = json.Data.AsGodotDictionary();

            if (!data.ContainsKey("type"))
            {
                GD.PrintErr("[WebSocketClient] Message missing 'type' field");
                ErrorOccurred?.Invoke("Server message missing 'type' field");
                return;
            }

            string messageType = (string)data["type"];
            Godot.Collections.Dictionary messageData = data.ContainsKey("data")
                ? data["data"].AsGodotDictionary()
                : new Godot.Collections.Dictionary();

            GD.Print($"[WebSocketClient] Parsed message type: {messageType}");

            // Fire message received event
            MessageReceived?.Invoke(messageType, messageData);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[WebSocketClient] Error handling message: {ex.Message}");
            ErrorOccurred?.Invoke($"Message handling error: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the current connection status.
    /// </summary>
    /// <returns>True if connected and WebSocket is open; false otherwise.</returns>
    public bool IsConnected()
    {
        return _isConnected
            && _webSocketPeer != null
            && _webSocketPeer.GetReadyState() == WebSocketPeer.State.Open;
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
            ["is_connected"] = _isConnected,
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

        GD.Print("[WebSocketClient] Cleaned up");
    }
}
