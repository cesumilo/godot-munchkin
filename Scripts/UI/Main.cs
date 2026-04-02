using System;
using System.Diagnostics;
using System.Text;
using Godot;

/// <summary>
/// Main entry point and authentication scene for the Munchkin game.
/// Handles user login via HTTP API and transitions to lobby scene.
/// </summary>
/// <remarks>
/// Per AGENTS.md architecture: Uses HTTP REST API for authentication before WebSocket gameplay.
/// Stores JWT token for subsequent API calls and WebSocket connection.
/// </remarks>
public partial class Main : Node3D
{
    // Input state
    private string usernameText = "";
    private string passwordText = "";

    // Node references
    private HttpRequest httpRequest;
    private Label errorLabel;
    private Button loginButton;
    private bool hasError = false;

    // JWT token storage - accessible throughout the application
    /// <summary>
    /// Gets the JWT authentication token for API calls.
    /// </summary>
    public static string JwtToken { get; private set; } = "";

    /// <summary>
    /// Gets the authenticated player identifier.
    /// </summary>
    public static string PlayerId { get; private set; } = "";

    /// <summary>
    /// Initializes UI elements and sets up error label styling.
    /// </summary>
    public override void _Ready()
    {
        this.httpRequest = GetNode<HttpRequest>("HTTPRequest");
        this.errorLabel = GetNode<Label>("UI/ErrorLabel");
        this.loginButton = GetNode<Button>("UI/LoginButton");

        if (this.httpRequest == null)
        {
            GD.PrintErr("[Main] HTTPRequest node not found — check scene tree.");
            throw new InvalidOperationException("HTTPRequest node is missing.");
        }
        if (errorLabel == null)
        {
            GD.PrintErr("[Main] UI/ErrorLabel node not found — check scene tree.");
            throw new InvalidOperationException("UI/ErrorLabel node is missing.");
        }
        if (loginButton == null)
        {
            GD.PrintErr("[Main] UI/LoginButton node not found — check scene tree.");
            throw new InvalidOperationException("UI/LoginButton node is missing.");
        }

        this.errorLabel.AddThemeFontSizeOverride("font_size", 16);
        this.errorLabel.AddThemeColorOverride("font_color", new Color(1, 0.2f, 0.2f));
        this.errorLabel.HorizontalAlignment = HorizontalAlignment.Center;
        this.errorLabel.VerticalAlignment = VerticalAlignment.Center;
        this.errorLabel.Hide();

        // Initial button state - disabled until both fields are filled
        UpdateLoginButtonState();
    }

    /// <summary>
    /// Main game loop - currently unused for login scene.
    /// </summary>
    /// <param name="delta">Elapsed time since last frame.</param>
    public override void _Process(double delta) { }

    /// <summary>
    /// Handles username text input changes.
    /// </summary>
    /// <param name="text">The current username text.</param>
    public void OnUsernameInputTextChanged(string text)
    {
        this.usernameText = text;

        // Clear error state when user starts typing after an error
        if (hasError)
        {
            ClearError();
        }

        UpdateLoginButtonState();
    }

    /// <summary>
    /// Handles password text input changes.
    /// </summary>
    /// <param name="text">The current password text.</param>
    public void OnPasswordInputTextChanged(string text)
    {
        this.passwordText = text;

        // Clear error state when user starts typing after an error
        if (hasError)
        {
            ClearError();
        }

        UpdateLoginButtonState();
    }

    /// <summary>
    /// Initiates login when button is pressed.
    /// </summary>
    public void OnLoginButtonPressed()
    {
        // Disable button while request is in progress
        loginButton.Disabled = true;

        string[] customHeaders = ["Content-Type: application/json"];
        string jsonBody =
            $"{{\"username\": \"{this.usernameText}\", \"password\": \"{this.passwordText}\"}}";

        GD.Print(jsonBody);
        Error err = this.httpRequest.Request(
            "http://90.28.104.14:1337/auth/login",
            customHeaders,
            HttpClient.Method.Post,
            jsonBody
        );

        if (err != Error.Ok)
        {
            GD.PrintErr("[Main] An error occurred creating the HTTP request.");
            // Re-enable button on request error
            UpdateLoginButtonState();
        }
    }

    /// <summary>
    /// Handles HTTP response from login request.
    /// </summary>
    /// <param name="result">HTTP request result code.</param>
    /// <param name="responseCode">HTTP status code.</param>
    /// <param name="headers">Response headers.</param>
    /// <param name="body">Response body bytes.</param>
    private void OnHttpRequestRequestCompleted(
        long result,
        long responseCode,
        string[] headers,
        byte[] body
    )
    {
        // Check if the connection itself succeeded (DNS, Timeout, etc.)
        if (result != (long)HttpRequest.Result.Success)
        {
            GD.PrintErr($"[Main] Connection Error: {result}");
            return;
        }

        switch (responseCode)
        {
            case 200:
                {
                    string jsonString = Encoding.UTF8.GetString(body);

                    GD.Print("Success! Data received:");
                    GD.Print(jsonString);

                    var json = new Json();
                    json.Parse(jsonString);
                    var data = json.Data.AsGodotDictionary();
                    string token = (string)data["token"];

                    // Store the JWT token for future use
                    JwtToken = token;
                    GD.Print(
                        $"JWT Token stored: {token.Substring(0, Math.Min(20, token.Length))}..."
                    );

                    // Extract player ID from JWT token
                    ExtractPlayerIdFromToken(token);

                    // Transition to lobby scene
                    TransitionToLobby();
                }
                break;
            case 400:
                GD.PrintErr("[Main] Invalid credentials");
                ShowErrorMessage("Invalid username or password");
                // Re-enable button after showing error
                UpdateLoginButtonState();
                break;
            default:
                GD.PrintErr($"[Main] HTTP Error: {responseCode}");
                ShowErrorMessage($"Server error: {responseCode}");
                // Re-enable button after showing error
                UpdateLoginButtonState();
                break;
        }
    }

    /// <summary>
    /// Extracts player ID from JWT token.
    /// </summary>
    /// <param name="token">The JWT token string.</param>
    /// <remarks>
    /// Simplified implementation - uses username as player ID.
    /// In production, would parse JWT payload to extract actual user ID.
    /// </remarks>
    private void ExtractPlayerIdFromToken(string token)
    {
        // In a real implementation, we would parse the JWT token to get the user ID
        // For now, we'll use the username as player ID
        PlayerId = usernameText;
        GD.Print($"Player ID set to: {PlayerId}");
    }

    /// <summary>
    /// Transitions to the lobby scene after successful login.
    /// </summary>
    private void TransitionToLobby()
    {
        try
        {
            // Change scene to Lobby.tscn
            var lobbyScene = GD.Load<PackedScene>("res://Scenes/Lobby/Lobby.tscn");
            if (lobbyScene != null)
            {
                GetTree().ChangeSceneToPacked(lobbyScene);
                GD.Print("Transitioning to lobby scene...");
            }
            else
            {
                GD.PrintErr("[Main] Failed to load Lobby scene!");
                ShowErrorMessage("Failed to load lobby. Please check game files.");
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Main] Error transitioning to lobby: {ex.Message}");
            ShowErrorMessage($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates login button state based on input validation.
    /// </summary>
    private void UpdateLoginButtonState()
    {
        if (loginButton == null)
            return;

        bool isUsernameValid = !string.IsNullOrWhiteSpace(usernameText);
        bool isPasswordValid = !string.IsNullOrWhiteSpace(passwordText);

        // Enable button only if both fields are filled and no error is displayed
        loginButton.Disabled = !(isUsernameValid && isPasswordValid && !hasError);
    }

    /// <summary>
    /// Displays an error message to the user.
    /// </summary>
    /// <param name="message">The error message to display.</param>
    private void ShowErrorMessage(string message)
    {
        hasError = true;
        this.errorLabel.Show();
        this.errorLabel.Text = message;
        UpdateLoginButtonState();
    }

    /// <summary>
    /// Clears the error state and hides the error label.
    /// </summary>
    private void ClearError()
    {
        hasError = false;
        this.errorLabel.Hide();
        this.errorLabel.Text = "";
    }
}
