using System;
using System.Collections.Generic;
using System.Text;
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

            GameLogger.Info("Initialized, player ID: " + Main.PlayerId, this);

            // Start fetching lobbies
            RefreshLobbyList();
        }
        catch (Exception ex)
        {
            GameLogger.Exception(ex, "Error during initialization", this);
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

        GameLogger.Debug("UI initialized", this);
    }

    /// <summary>
    /// Creates and configures HTTP request node.
    /// </summary>
    private void InitializeHttpRequest()
    {
        _httpRequest = new HttpRequest();
        AddChild(_httpRequest);
        _httpRequest.RequestCompleted += OnHttpRequestCompleted;

        GameLogger.Debug("HTTP request initialized", this);
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
                GameLogger.Debug("Request already queued, skipping duplicate", this);
                ShowStatus("Already processing...", false);
                return;
            }

            // Queue the request
            _requestQueue.Enqueue(requestAction);
            ShowStatus("Processing...", false);
            GameLogger.Debug("Request queued - another request in progress", this);
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

            GameLogger.Info($"Processing queued request (remaining: {_requestQueue.Count})", this);
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

        GameLogger.Info($"Status: {message}", this);
    }

    /// <summary>
    /// Validates authentication and returns authentication headers.
    /// </summary>
    /// <returns>True if authenticated; false otherwise.</returns>
    private bool ValidateAuthentication()
    {
        if (string.IsNullOrEmpty(Main.JwtToken))
        {
            ShowStatus("Not authenticated. Please login first.", true);
            return false;
        }
        return true;
    }

    /// <summary>
    /// Executes a standardized HTTP request with error handling.
    /// </summary>
    /// <param name="url">The URL to request.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="requestType">The type of request for tracking.</param>
    /// <param name="statusMessage">Status message to display.</param>
    /// <param name="errorMessage">Error message prefix.</param>
    private void ExecuteRequest(
        string url,
        HttpClient.Method method,
        RequestType requestType,
        string statusMessage,
        string errorMessage
    )
    {
        if (!ValidateAuthentication())
            return;

        ShowStatus(statusMessage);
        _lastRequestType = requestType;

        string[] headers = new string[] { ServerUrls.AuthorizationHeader(Main.JwtToken) };

        ExecuteOrQueueRequest(() =>
        {
            Error err = _httpRequest.Request(url, headers, method);

            if (err != Error.Ok)
            {
                if (err == Error.Busy)
                {
                    ShowStatus("Please wait...", false);
                    GameLogger.Debug("Request busy, will retry", this);
                }
                else
                {
                    ShowStatus($"{errorMessage}: {err}", true);
                    GameLogger.Error($"{errorMessage}: {err}", this);
                }
                _isRequestInProgress = false;
                ProcessNextRequest();
            }
        });
    }

    /// <summary>
    /// Fetches list of available lobbies from server.
    /// </summary>
    private void RefreshLobbyList()
    {
        ExecuteRequest(
            ServerUrls.ListLobbies(),
            HttpClient.Method.Get,
            RequestType.ListLobbies,
            "Fetching lobbies...",
            "Failed to fetch lobbies"
        );
    }

    /// <summary>
    /// Creates a new lobby.
    /// </summary>
    private void CreateLobby()
    {
        ExecuteRequest(
            ServerUrls.CreateLobby(),
            HttpClient.Method.Post,
            RequestType.CreateLobby,
            "Creating lobby...",
            "Failed to create lobby"
        );
    }

    /// <summary>
    /// Joins the selected lobby.
    /// </summary>
    /// <param name="lobbyId">The lobby ID to join.</param>
    private void JoinLobby(string lobbyId)
    {
        if (string.IsNullOrEmpty(lobbyId))
        {
            ShowStatus("Please select a lobby first.", true);
            return;
        }

        _selectedLobbyId = lobbyId;
        ExecuteRequest(
            ServerUrls.JoinLobby(lobbyId),
            HttpClient.Method.Post,
            RequestType.JoinLobby,
            $"Joining lobby {lobbyId}...",
            "Failed to join lobby"
        );
    }

    /// <summary>
    /// Starts the game (host only).
    /// </summary>
    /// <param name="lobbyId">The lobby ID to start.</param>
    private void StartGame(string lobbyId)
    {
        if (string.IsNullOrEmpty(lobbyId))
        {
            ShowStatus("No active lobby.", true);
            return;
        }

        ExecuteRequest(
            ServerUrls.StartGame(lobbyId),
            HttpClient.Method.Post,
            RequestType.StartGame,
            $"Starting game for lobby {lobbyId}...",
            "Failed to start game"
        );
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
        GameLogger.Debug(
            $"WebSocketClient instance: {wsClient}, IsConnected: {wsClient?.IsConnected()}, IsConnecting: {wsClient?.IsConnecting()}",
            this
        );

        // Check if already connected or connecting
        if (wsClient.IsConnected())
        {
            ShowStatus("Already connected to WebSocket", false);
            GameLogger.Info($"Already connected to WebSocket for lobby: {lobbyId}", this);
            return;
        }

        if (wsClient.IsConnecting())
        {
            ShowStatus("Already connecting to WebSocket...", false);
            GameLogger.Info($"Already connecting to WebSocket for lobby: {lobbyId}", this);
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
            GameLogger.Error("Failed to start WebSocket connection", this);
        }
        else
        {
            GameLogger.Info("WebSocket connection started successfully", this);
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
            GameLogger.Exception(ex, "Error transitioning to game", this);
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
        GameLogger.Debug("OnJoinLobbyButtonPressed called!", this);
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

            GameLogger.Info($"Selected lobby: {_selectedLobbyId}", this);
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
            GameLogger.Error($"Connection Error: {result}", this);

            _isRequestInProgress = false;
            ProcessNextRequest();
            return;
        }

        string responseBody = Encoding.UTF8.GetString(body);
        GameLogger.Debug($"Response {responseCode}: {responseBody}", this);

        try
        {
            switch (responseCode)
            {
                case 200:
                    Handle200Response(responseBody);
                    break;
                case 201:
                    Handle201Response(responseBody);
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
            GameLogger.Exception(ex, "Error handling response", this);
            ShowStatus($"Error processing response: {ex.Message}", true);
        }
    }

    /// <summary>
    /// Routes 200 responses to appropriate handler.
    /// </summary>
    /// <param name="responseBody">Response body string.</param>
    private void Handle200Response(string responseBody)
    {
        GameLogger.Debug($"Handling 200 response for request type: {_lastRequestType}", this);

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
                GameLogger.Error(
                    $"Unknown request type for 200 response: {_lastRequestType}",
                    this
                );
                ShowStatus("Unexpected response from server", true);
                break;
        }
    }

    /// <summary>
    /// Handles 201 responses (creation success).
    /// </summary>
    /// <param name="responseBody">Response body string.</param>
    private void Handle201Response(string responseBody)
    {
        // 201 is typically used for creation success
        if (_lastRequestType == RequestType.CreateLobby)
        {
            HandleLobbyCreationResponse(responseBody);
        }
        else
        {
            Handle200Response(responseBody);
        }
    }

    /// <summary>
    /// Parses lobby list response.
    /// </summary>
    /// <param name="responseBody">Response JSON string.</param>
    private void HandleLobbyListResponse(string responseBody)
    {
        if (!JsonHelper.TryParseDictionary(responseBody, out var data, nameof(Lobby)))
        {
            ShowStatus("Error parsing lobby data", true);
            return;
        }

        _lobbies.Clear();
        _lobbyList.Clear();

        var items = JsonHelper.GetArray(data, "items");

        if (items.Count > 0)
        {
            foreach (var item in items)
            {
                var lobbyData = item.AsGodotDictionary();

                var lobby = new LobbyItem
                {
                    Id = JsonHelper.GetString(lobbyData, "id"),
                    Name = JsonHelper.GetString(lobbyData, "name", "Unnamed Lobby"),
                    PlayerCount = JsonHelper.GetInt(lobbyData, "playerCount"),
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

    /// <summary>
    /// Parses lobby creation response.
    /// </summary>
    /// <param name="responseBody">Response JSON string.</param>
    private void HandleLobbyCreationResponse(string responseBody)
    {
        if (!JsonHelper.TryParseDictionary(responseBody, out var data, nameof(Lobby)))
        {
            ShowStatus("Error creating lobby", true);
            return;
        }

        string error = JsonHelper.GetString(data, "error");
        if (!string.IsNullOrEmpty(error))
        {
            ShowStatus($"Lobby creation failed: {error}", true);
            return;
        }

        string lobbyId = JsonHelper.GetString(data, "lobby_id");
        if (!string.IsNullOrEmpty(lobbyId))
        {
            _currentLobbyId = lobbyId;
            _isHost = true;

            ShowStatus($"Lobby created: {lobbyId}", false);
            GameLogger.Info($"Created lobby with ID: {lobbyId}", this);

            _selectedLobbyId = lobbyId;
            GameLogger.Info($"Auto-joining created lobby: {lobbyId}", this);
            JoinLobby(lobbyId);
        }
    }

    /// <summary>
    /// Parses join lobby response.
    /// </summary>
    /// <param name="responseBody">Response JSON string.</param>
    private void HandleJoinLobbyResponse(string responseBody)
    {
        GameLogger.Info($"Join lobby response: {responseBody}", this);

        ShowStatus("Successfully joined lobby!", false);
        _currentLobbyId = _selectedLobbyId;
        _isHost = false;

        GameLogger.Debug(
            $"After join - Current lobby ID: {_currentLobbyId}, IsHost: {_isHost}",
            this
        );

        if (!string.IsNullOrEmpty(_currentLobbyId))
        {
            GameLogger.Info($"Calling ConnectToLobbyWebSocket for lobby: {_currentLobbyId}", this);
            ConnectToLobbyWebSocket(_currentLobbyId);
        }
        else
        {
            GameLogger.Error("No lobby ID available for WebSocket connection", this);
            ShowStatus("Joined but failed to connect to game", true);
        }
    }

    /// <summary>
    /// Parses start game response.
    /// </summary>
    /// <param name="responseBody">Response JSON string.</param>
    private void HandleStartGameResponse(string responseBody)
    {
        GameLogger.Info($"Start game response: {responseBody}", this);
        ShowStatus("Game started!", false);
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
        GameLogger.Debug($"WebSocket message received: {messageType}", this);

        switch (messageType)
        {
            case MessageProtocol.GAME_STATE:
                HandleGameStateMessage(data);
                break;
            case MessageProtocol.ERROR:
                HandleErrorMessage(data);
                break;
            default:
                GameLogger.Debug($"Unhandled message type: {messageType}", this);
                break;
        }
    }

    private void OnWebSocketError(string errorMessage)
    {
        ShowStatus($"WebSocket error: {errorMessage}", true);
        GameLogger.Error($"WebSocket error: {errorMessage}", this);
    }

    /// <summary>
    /// Parses GAME_STATE message from server.
    /// </summary>
    /// <param name="data">Message data dictionary.</param>
    private void HandleGameStateMessage(Godot.Collections.Dictionary data)
    {
        GameLogger.Debug("Received GAME_STATE message", this);

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
            GameLogger.Error($"Server error: {error.Code} - {error.Message}", this);
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

        GameLogger.Info("Cleaned up", this);
    }
}
