using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Godot;

public partial class WebSocketClient : Node
{
    // Singleton instance
    private static WebSocketClient _instance;
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
    public delegate void ConnectionStateChangedHandler(bool connected);
    public delegate void MessageReceivedHandler(
        string messageType,
        Godot.Collections.Dictionary data
    );
    public delegate void ErrorHandler(string errorMessage);

    public event ConnectionStateChangedHandler ConnectionStateChanged;
    public event MessageReceivedHandler MessageReceived;
    public event ErrorHandler ErrorOccurred;

    // Message queue for outgoing messages
    private Queue<string> _outgoingMessageQueue = new Queue<string>();
    private bool _isProcessingQueue = false;

    // Connection statistics
    public int MessagesSent { get; private set; } = 0;
    public int MessagesReceived { get; private set; } = 0;
    public DateTime LastConnectionTime { get; private set; }

    /// <summary>
    /// Initialize the WebSocket client
    /// </summary>
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
    /// Process WebSocket connection each frame
    /// </summary>
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
    /// Connect to a WebSocket server
    /// </summary>
    /// <param name="url">WebSocket URL (e.g., ws://server:port/lobby/id/ws)</param>
    /// <returns>True if connection attempt started successfully</returns>
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
    /// Disconnect from the server
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
    /// Reset connection state (useful for reconnection attempts)
    /// </summary>
    public void ResetConnectionState()
    {
        _isConnecting = false;
        _isConnected = false;
    }

    /// <summary>
    /// Send a message to the server
    /// </summary>
    /// <param name="message">JSON message string</param>
    /// <param name="queueIfDisconnected">Queue message if not connected</param>
    /// <returns>True if message sent or queued successfully</returns>
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
    /// Send JOIN_GAME message with player ID after connection
    /// </summary>
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
    /// Send a structured message (converts to JSON)
    /// </summary>
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
    /// Process queued messages
    /// </summary>
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
    /// Handle incoming message from server
    /// </summary>
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
    /// Check if connected to server
    /// </summary>
    public bool IsConnected()
    {
        return _isConnected
            && _webSocketPeer != null
            && _webSocketPeer.GetReadyState() == WebSocketPeer.State.Open;
    }

    /// <summary>
    /// Check if connecting to server
    /// </summary>
    public bool IsConnecting()
    {
        return _isConnecting;
    }

    /// <summary>
    /// Get connection statistics
    /// </summary>
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
    /// Clean up resources
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
