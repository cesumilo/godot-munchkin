using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Manages the lobby scene for game session selection and joining.
/// Handles HTTP API calls for lobby management and WebSocket connection.
/// </summary>
/// <remarks>
/// Per AGENTS.md architecture: HTTP for lobby management, WebSocket for gameplay.
/// Supports lobby creation, listing, joining, and game start.
/// </remarks>
public partial class Lobby : Node3D
{
    // HTTP client for API calls
    private HttpRequest _httpRequest;
    private Queue<Action> _requestQueue = new Queue<Action>();
    private bool _isRequestInProgress = false;

    // UI elements
    private Control _uiContainer;
    private ItemList _lobbyList;
    private Button _createLobbyButton;
    private Button _refreshLobbyButton;
    private Button _joinLobbyButton;
    private Button _startGameButton;
    private Label _statusLabel;
    private Label _playerCountLabel;

    // Lobby data
    private List<LobbyItem> _lobbies = new List<LobbyItem>();
    private string _selectedLobbyId = "";
    private string _currentLobbyId = "";
    private bool _isHost = false;

    /// <summary>
    /// Defines types of HTTP requests for tracking.
    /// </summary>
    private enum RequestType
    {
        ListLobbies,
        CreateLobby,
        JoinLobby,
        StartGame,
    }

    private RequestType _lastRequestType = RequestType.ListLobbies;

    /// <summary>
    /// Represents a lobby item from the server.
    /// </summary>
    public class LobbyItem
    {
        /// <summary>
        /// Gets or sets the lobby identifier.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the lobby display name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the current player count.
        /// </summary>
        public int PlayerCount { get; set; }

        /// <summary>
        /// Returns formatted lobby string for display.
        /// </summary>
        public override string ToString()
        {
            return $"{Name} ({PlayerCount}/6 players)";
        }
    }

    /// <summary>
    /// Initializes UI and starts fetching lobbies.
    /// </summary>
    public override void _Ready()
    {
        try
        {
            InitializeUI();
            InitializeHttpRequest();

            GD.Print("[Lobby] Initialized, player ID: " + Main.PlayerId);

            // Start fetching lobbies
            RefreshLobbyList();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Lobby] Error during initialization: {ex.Message}");
            ShowStatus($"Initialization error: {ex.Message}", true);
        }
    }

    /// <summary>
    /// Initializes UI element references.
    /// </summary>
    private void InitializeUI()
    {
        _uiContainer = GetNode<Control>("UI");

        // Get UI elements
        _lobbyList = GetNode<ItemList>("UI/LobbyList");
        _createLobbyButton = GetNode<Button>("UI/CreateLobbyButton");
        _refreshLobbyButton = GetNode<Button>("UI/RefreshLobbyButton");
        _joinLobbyButton = GetNode<Button>("UI/JoinLobbyButton");
        _startGameButton = GetNode<Button>("UI/StartGameButton");
        _statusLabel = GetNode<Label>("UI/StatusLabel");
        _playerCountLabel = GetNode<Label>("UI/PlayerCountLabel");

        // Set initial button states
        UpdateButtonStates(true);
        _startGameButton.Visible = false;

        // Update player count display
        UpdatePlayerCountDisplay();

        GD.Print("[Lobby] UI initialized");
    }

    /// <summary>
    /// Creates and configures HTTP request node.
    /// </summary>
    private void InitializeHttpRequest()
    {
        _httpRequest = new HttpRequest();
        AddChild(_httpRequest);
        _httpRequest.RequestCompleted += OnHttpRequestCompleted;

        GD.Print("[Lobby] HTTP request initialized");
    }

    /// <summary>
    /// Updates the player count display label.
    /// </summary>
    private void UpdatePlayerCountDisplay()
    {
        if (_playerCountLabel != null)
        {
            _playerCountLabel.Text = $"Player: {Main.PlayerId}";
        }
    }

    /// <summary>
    /// Queues or executes an HTTP request.
    /// </summary>
    /// <param name="requestAction">The action to execute.</param>
    private void ExecuteOrQueueRequest(Action requestAction)
    {
        if (_isRequestInProgress)
        {
            // Don't queue duplicate refresh requests
            if (_requestQueue.Count > 0)
            {
                GD.Print("[Lobby] Request already queued, skipping duplicate");
                ShowStatus("Already processing...", false);
                return;
            }

            // Queue the request
            _requestQueue.Enqueue(requestAction);
            ShowStatus("Processing...", false);
            GD.Print("[Lobby] Request queued - another request in progress");
        }
        else
        {
            // Execute immediately
            _isRequestInProgress = true;
            UpdateButtonStates(false); // Disable buttons during request
            requestAction();
        }
    }

    /// <summary>
    /// Processes next request in queue.
    /// </summary>
    private void ProcessNextRequest()
    {
        if (_requestQueue.Count > 0 && !_isRequestInProgress)
        {
            _isRequestInProgress = true;
            UpdateButtonStates(false); // Disable buttons during request
            var nextRequest = _requestQueue.Dequeue();

            GD.Print($"[Lobby] Processing queued request (remaining: {_requestQueue.Count})");
            nextRequest();
        }
        else if (_requestQueue.Count == 0 && !_isRequestInProgress)
        {
            // No requests in progress or queue, re-enable buttons
            UpdateButtonStates(true);
        }
    }

    /// <summary>
    /// Updates UI button enabled states.
    /// </summary>
    /// <param name="enabled">Whether buttons should be enabled.</param>
    private void UpdateButtonStates(bool enabled)
    {
        if (_createLobbyButton != null)
            _createLobbyButton.Disabled = !enabled;

        if (_refreshLobbyButton != null)
            _refreshLobbyButton.Disabled = !enabled;

        if (_joinLobbyButton != null)
            _joinLobbyButton.Disabled = !enabled || string.IsNullOrEmpty(_selectedLobbyId);

        if (_startGameButton != null)
            _startGameButton.Disabled = !enabled || !_isHost;
    }

    /// <summary>
    /// Shows status message in UI.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="isError">True if error (red text); false otherwise.</param>
    private void ShowStatus(string message, bool isError = false)
    {
        if (_statusLabel != null)
        {
            _statusLabel.Text = message;
            _statusLabel.AddThemeColorOverride(
                "font_color",
                isError ? new Color(1, 0.2f, 0.2f) : new Color(1, 1, 1)
            );
        }

        GD.Print($"[Lobby] Status: {message}");
    }

    /// <summary>
    /// Fetches list of available lobbies from server.
    /// </summary>
    private void RefreshLobbyList()
    {
        if (string.IsNullOrEmpty(Main.JwtToken))
        {
            ShowStatus("Not authenticated. Please login first.", true);
            return;
        }

        ShowStatus("Fetching lobbies...");
        _lastRequestType = RequestType.ListLobbies;

        string url = $"{NetworkManager.SERVER_BASE_URL}/lobby";
        string[] headers = new string[] { $"Authorization: Bearer {Main.JwtToken}" };

        ExecuteOrQueueRequest(() =>
        {
            Error err = _httpRequest.Request(url, headers, HttpClient.Method.Get);

            if (err != Error.Ok)
            {
                if (err == Error.Busy)
                {
                    ShowStatus("Please wait...", false);
                    GD.Print("[Lobby] Request busy, will retry");
                }
                else
                {
                    ShowStatus($"Failed to fetch lobbies: {err}", true);
                    GD.PrintErr($"[Lobby] Failed to start request: {err}");
                }
                _isRequestInProgress = false;
                ProcessNextRequest();
            }
        });
    }

    /// <summary>
    /// Creates a new lobby.
    /// </summary>
    private void CreateLobby()
    {
        if (string.IsNullOrEmpty(Main.JwtToken))
        {
            ShowStatus("Not authenticated. Please login first.", true);
            return;
        }

        ShowStatus("Creating lobby...");
        _lastRequestType = RequestType.CreateLobby;

        string url = $"{NetworkManager.SERVER_BASE_URL}/lobby";
        string[] headers = new string[] { $"Authorization: Bearer {Main.JwtToken}" };

        ExecuteOrQueueRequest(() =>
        {
            Error err = _httpRequest.Request(url, headers, HttpClient.Method.Post);

            if (err != Error.Ok)
            {
                if (err == Error.Busy)
                {
                    ShowStatus("Please wait...", false);
                    GD.Print("[Lobby] Request busy, will retry");
                }
                else
                {
                    ShowStatus($"Failed to create lobby: {err}", true);
                    GD.PrintErr($"[Lobby] Failed to start request: {err}");
                }
                _isRequestInProgress = false;
                ProcessNextRequest();
            }
        });
    }

    /// <summary>
    /// Joins the selected lobby.
    /// </summary>
    /// <param name="lobbyId">The lobby ID to join.</param>
    private void JoinLobby(string lobbyId)
    {
        if (string.IsNullOrEmpty(Main.JwtToken))
        {
            ShowStatus("Not authenticated. Please login first.", true);
            return;
        }

        if (string.IsNullOrEmpty(lobbyId))
        {
            ShowStatus("Please select a lobby first.", true);
            return;
        }

        ShowStatus($"Joining lobby {lobbyId}...");
        _lastRequestType = RequestType.JoinLobby;
        _selectedLobbyId = lobbyId;

        string url = $"{NetworkManager.SERVER_BASE_URL}/lobby/{lobbyId}/join";
        string[] headers = new string[] { $"Authorization: Bearer {Main.JwtToken}" };

        ExecuteOrQueueRequest(() =>
        {
            Error err = _httpRequest.Request(url, headers, HttpClient.Method.Post);

            if (err != Error.Ok)
            {
                if (err == Error.Busy)
                {
                    ShowStatus("Please wait...", false);
                    GD.Print("[Lobby] Request busy, will retry");
                }
                else
                {
                    ShowStatus($"Failed to join lobby: {err}", true);
                    GD.PrintErr($"[Lobby] Failed to start request: {err}");
                }
                _isRequestInProgress = false;
                ProcessNextRequest();
            }
        });
    }

    /// <summary>
    /// Starts the game (host only).
    /// </summary>
    /// <param name="lobbyId">The lobby ID to start.</param>
    private void StartGame(string lobbyId)
    {
        if (string.IsNullOrEmpty(Main.JwtToken))
        {
            ShowStatus("Not authenticated. Please login first.", true);
            return;
        }

        if (string.IsNullOrEmpty(lobbyId))
        {
            ShowStatus("No active lobby.", true);
            return;
        }

        ShowStatus($"Starting game for lobby {lobbyId}...");
        _lastRequestType = RequestType.StartGame;

        string url = $"{NetworkManager.SERVER_BASE_URL}/lobby/{lobbyId}/start";
        string[] headers = new string[] { $"Authorization: Bearer {Main.JwtToken}" };

        ExecuteOrQueueRequest(() =>
        {
            Error err = _httpRequest.Request(url, headers, HttpClient.Method.Post);

            if (err != Error.Ok)
            {
                if (err == Error.Busy)
                {
                    ShowStatus("Please wait...", false);
                    GD.Print("[Lobby] Request busy, will retry");
                }
                else
                {
                    ShowStatus($"Failed to start game: {err}", true);
                    GD.PrintErr($"[Lobby] Failed to start request: {err}");
                }
                _isRequestInProgress = false;
                ProcessNextRequest();
            }
        });
    }

    /// <summary>
    /// Connects to lobby WebSocket for real-time gameplay.
    /// </summary>
    /// <param name="lobbyId">The lobby ID to connect to.</param>
    private void ConnectToLobbyWebSocket(string lobbyId)
    {
        if (string.IsNullOrEmpty(lobbyId))
        {
            ShowStatus("No lobby ID provided.", true);
            return;
        }

        var networkManager = NetworkManager.Instance;
        if (networkManager == null || networkManager.WebSocketClient == null)
        {
            ShowStatus("Network manager not available.", true);
            return;
        }

        var wsClient = networkManager.WebSocketClient;
        GD.Print(
            $"[Lobby] WebSocketClient instance: {wsClient}, IsConnected: {wsClient?.IsConnected()}, IsConnecting: {wsClient?.IsConnecting()}"
        );

        // Check if already connected or connecting
        if (wsClient.IsConnected())
        {
            ShowStatus("Already connected to WebSocket", false);
            GD.Print($"[Lobby] Already connected to WebSocket for lobby: {lobbyId}");
            return;
        }

        if (wsClient.IsConnecting())
        {
            ShowStatus("Already connecting to WebSocket...", false);
            GD.Print($"[Lobby] Already connecting to WebSocket for lobby: {lobbyId}");
            return;
        }

        ShowStatus($"Connecting to lobby {lobbyId} WebSocket...");

        // Remove existing event handlers to avoid duplicates
        wsClient.ConnectionStateChanged -= OnWebSocketConnectionStateChanged;
        wsClient.MessageReceived -= OnWebSocketMessageReceived;
        wsClient.ErrorOccurred -= OnWebSocketError;

        // Connect event handlers
        wsClient.ConnectionStateChanged += OnWebSocketConnectionStateChanged;
        wsClient.MessageReceived += OnWebSocketMessageReceived;
        wsClient.ErrorOccurred += OnWebSocketError;

        // Connect to WebSocket
        bool connectionStarted = networkManager.ConnectToLobby(lobbyId);

        if (!connectionStarted)
        {
            ShowStatus("Failed to start WebSocket connection (already connecting?)", true);
            GD.PrintErr("[Lobby] Failed to start WebSocket connection");
        }
        else
        {
            GD.Print("[Lobby] WebSocket connection started successfully");
        }
    }

    /// <summary>
    /// Transitions to game scene when game starts.
    /// </summary>
    private void TransitionToGame()
    {
        try
        {
            ShowStatus("Transitioning to game...");

            // Enable start game button for host
            if (_isHost)
            {
                _startGameButton.Visible = true;
                UpdateButtonStates(true);
                ShowStatus("You are the host. Ready to start the game!", false);
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Lobby] Error transitioning to game: {ex.Message}");
            ShowStatus($"Transition error: {ex.Message}", true);
        }
    }

    // UI Event Handlers
    private void OnCreateLobbyButtonPressed()
    {
        CreateLobby();
    }

    private void OnRefreshLobbyButtonPressed()
    {
        RefreshLobbyList();
    }

    private void OnJoinLobbyButtonPressed()
    {
        GD.Print("[Lobby] OnJoinLobbyButtonPressed called!");
        if (!string.IsNullOrEmpty(_selectedLobbyId))
        {
            JoinLobby(_selectedLobbyId);
        }
    }

    private void OnStartGameButtonPressed()
    {
        if (!string.IsNullOrEmpty(_currentLobbyId))
        {
            StartGame(_currentLobbyId);
        }
    }

    private void OnLobbyListItemSelected(long index)
    {
        if (index >= 0 && index < _lobbies.Count)
        {
            _selectedLobbyId = _lobbies[(int)index].Id;
            UpdateButtonStates(true);

            GD.Print($"[Lobby] Selected lobby: {_selectedLobbyId}");
        }
    }

    /// <summary>
    /// Handles HTTP response completion.
    /// </summary>
    private void OnHttpRequestCompleted(
        long result,
        long responseCode,
        string[] headers,
        byte[] body
    )
    {
        if (result != (long)HttpRequest.Result.Success)
        {
            ShowStatus($"Connection Error: {result}", true);
            GD.PrintErr($"[Lobby] Connection Error: {result}");

            _isRequestInProgress = false;
            ProcessNextRequest();
            return;
        }

        string responseBody = Encoding.UTF8.GetString(body);
        GD.Print($"[Lobby] Response {responseCode}: {responseBody}");

        try
        {
            switch (responseCode)
            {
                case 200:
                    Handle200Response(responseBody);
                    break;
                case 201:
                    HandleLobbyCreationResponse(responseBody);
                    break;
                case 400:
                    ShowStatus("Bad request. Please try again.", true);
                    break;
                case 401:
                    ShowStatus("Authentication failed. Please login again.", true);
                    break;
                case 404:
                    ShowStatus("Lobby not found.", true);
                    break;
                default:
                    ShowStatus($"Server error: {responseCode}", true);
                    break;
            }

            _isRequestInProgress = false;
            ProcessNextRequest();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Lobby] Error handling response: {ex.Message}");
            ShowStatus($"Error processing response: {ex.Message}", true);
        }
    }

    /// <summary>
    /// Routes 200 responses to appropriate handler.
    /// </summary>
    /// <param name="responseBody">Response body string.</param>
    private void Handle200Response(string responseBody)
    {
        GD.Print($"[Lobby] Handling 200 response for request type: {_lastRequestType}");

        switch (_lastRequestType)
        {
            case RequestType.ListLobbies:
                HandleLobbyListResponse(responseBody);
                break;
            case RequestType.JoinLobby:
                HandleJoinLobbyResponse(responseBody);
                break;
            case RequestType.StartGame:
                HandleStartGameResponse(responseBody);
                break;
            case RequestType.CreateLobby:
                HandleLobbyCreationResponse(responseBody);
                break;
            default:
                GD.PrintErr($"[Lobby] Unknown request type for 200 response: {_lastRequestType}");
                ShowStatus("Unexpected response from server", true);
                break;
        }
    }

    /// <summary>
    /// Parses lobby list response.
    /// </summary>
    /// <param name="responseBody">Response JSON string.</param>
    private void HandleLobbyListResponse(string responseBody)
    {
        try
        {
            var json = new Json();
            json.Parse(responseBody);
            var data = json.Data.AsGodotDictionary();

            _lobbies.Clear();
            _lobbyList.Clear();

            if (data.ContainsKey("items"))
            {
                var items = data["items"].AsGodotArray();

                foreach (var item in items)
                {
                    var lobbyData = item.AsGodotDictionary();

                    var lobby = new LobbyItem
                    {
                        Id = lobbyData.ContainsKey("id") ? (string)lobbyData["id"] : "",
                        Name = lobbyData.ContainsKey("name")
                            ? (string)lobbyData["name"]
                            : "Unnamed Lobby",
                        PlayerCount = lobbyData.ContainsKey("playerCount")
                            ? (int)(long)lobbyData["playerCount"]
                            : 0,
                    };

                    _lobbies.Add(lobby);
                    _lobbyList.AddItem(lobby.ToString());
                }

                ShowStatus($"Found {_lobbies.Count} lobbies", false);
            }
            else
            {
                ShowStatus("No lobbies available", false);
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Lobby] Error parsing lobby list: {ex.Message}");
            ShowStatus("Error parsing lobby data", true);
        }
    }

    /// <summary>
    /// Parses lobby creation response.
    /// </summary>
    /// <param name="responseBody">Response JSON string.</param>
    private void HandleLobbyCreationResponse(string responseBody)
    {
        try
        {
            var json = new Json();
            json.Parse(responseBody);
            var data = json.Data.AsGodotDictionary();

            if (data.ContainsKey("lobby_id"))
            {
                string lobbyId = (string)data["lobby_id"];
                _currentLobbyId = lobbyId;
                _isHost = true;

                ShowStatus($"Lobby created: {lobbyId}", false);
                GD.Print($"[Lobby] Created lobby with ID: {lobbyId}");

                _selectedLobbyId = lobbyId;
                GD.Print($"[Lobby] Auto-joining created lobby: {lobbyId}");
                JoinLobby(lobbyId);
            }
            else if (data.ContainsKey("error"))
            {
                string error = (string)data["error"];
                ShowStatus($"Lobby creation failed: {error}", true);
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Lobby] Error parsing lobby creation response: {ex.Message}");
            ShowStatus("Error creating lobby", true);
        }
    }

    /// <summary>
    /// Parses join lobby response.
    /// </summary>
    /// <param name="responseBody">Response JSON string.</param>
    private void HandleJoinLobbyResponse(string responseBody)
    {
        try
        {
            GD.Print($"[Lobby] Join lobby response: {responseBody}");

            ShowStatus("Successfully joined lobby!", false);
            _currentLobbyId = _selectedLobbyId;
            _isHost = false;

            GD.Print(
                $"[Lobby] After join - Current lobby ID: {_currentLobbyId}, IsHost: {_isHost}"
            );

            if (!string.IsNullOrEmpty(_currentLobbyId))
            {
                GD.Print($"[Lobby] Calling ConnectToLobbyWebSocket for lobby: {_currentLobbyId}");
                ConnectToLobbyWebSocket(_currentLobbyId);
            }
            else
            {
                GD.PrintErr("[Lobby] No lobby ID available for WebSocket connection");
                ShowStatus("Joined but failed to connect to game", true);
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Lobby] Error parsing join response: {ex.Message}");
            ShowStatus($"Joined but error processing response: {ex.Message}", true);
        }
    }

    /// <summary>
    /// Parses start game response.
    /// </summary>
    /// <param name="responseBody">Response JSON string.</param>
    private void HandleStartGameResponse(string responseBody)
    {
        try
        {
            GD.Print($"[Lobby] Start game response: {responseBody}");
            ShowStatus("Game started!", false);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Lobby] Error parsing start game response: {ex.Message}");
            ShowStatus($"Game started but error processing response: {ex.Message}", true);
        }
    }

    // WebSocket Event Handlers
    private void OnWebSocketConnectionStateChanged(bool connected)
    {
        if (connected)
        {
            ShowStatus("WebSocket connected successfully!", false);
            TransitionToGame();
        }
        else
        {
            ShowStatus("WebSocket disconnected", true);
        }
    }

    private void OnWebSocketMessageReceived(string messageType, Godot.Collections.Dictionary data)
    {
        GD.Print($"[Lobby] WebSocket message received: {messageType}");

        switch (messageType)
        {
            case MessageProtocol.GAME_STATE:
                HandleGameStateMessage(data);
                break;
            case MessageProtocol.ERROR:
                HandleErrorMessage(data);
                break;
            default:
                GD.Print($"[Lobby] Unhandled message type: {messageType}");
                break;
        }
    }

    private void OnWebSocketError(string errorMessage)
    {
        ShowStatus($"WebSocket error: {errorMessage}", true);
        GD.PrintErr($"[Lobby] WebSocket error: {errorMessage}");
    }

    /// <summary>
    /// Parses GAME_STATE message from server.
    /// </summary>
    /// <param name="data">Message data dictionary.</param>
    private void HandleGameStateMessage(Godot.Collections.Dictionary data)
    {
        GD.Print("[Lobby] Received GAME_STATE message");

        if (MessageProtocol.TryParseGameState(data, out var gameState))
        {
            if (!string.IsNullOrEmpty(gameState.Winner))
            {
                ShowStatus($"Game over! Winner: {gameState.Winner}", false);
            }
        }
    }

    /// <summary>
    /// Parses ERROR message from server.
    /// </summary>
    /// <param name="data">Message data dictionary.</param>
    private void HandleErrorMessage(Godot.Collections.Dictionary data)
    {
        if (MessageProtocol.TryParseError(data, out var error))
        {
            ShowStatus($"Server error: {error.Message}", true);
            GD.PrintErr($"[Lobby] Server error: {error.Code} - {error.Message}");
        }
    }

    /// <summary>
    /// Unsubscribes from events on cleanup.
    /// </summary>
    public override void _ExitTree()
    {
        var networkManager = NetworkManager.Instance;
        if (networkManager != null && networkManager.WebSocketClient != null)
        {
            var wsClient = networkManager.WebSocketClient;
            wsClient.ConnectionStateChanged -= OnWebSocketConnectionStateChanged;
            wsClient.MessageReceived -= OnWebSocketMessageReceived;
            wsClient.ErrorOccurred -= OnWebSocketError;
        }

        GD.Print("[Lobby] Cleaned up");
    }
}
