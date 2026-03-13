using System;
using System.Diagnostics;
using System.Text;
using Godot;

public partial class Main : Node3D
{
    private string usernameText = "";
    private string passwordText = "";
    private HttpRequest httpRequest;
    private Label errorLabel;
    private Button loginButton;
    private bool hasError = false;

    // JWT token storage - accessible throughout the application
    public static string JwtToken { get; private set; } = "";
    public static string PlayerId { get; private set; } = "";

    // Called when the node enters the scene tree for the first time.
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

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta) { }

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
    /// Extract player ID from JWT token (simplified - in reality would parse JWT)
    /// For now, we'll store the username as player ID
    /// </summary>
    private void ExtractPlayerIdFromToken(string token)
    {
        // In a real implementation, we would parse the JWT token to get the user ID
        // For now, we'll use the username as player ID
        PlayerId = usernameText;
        GD.Print($"Player ID set to: {PlayerId}");
    }

    /// <summary>
    /// Transition to the lobby scene after successful login
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
    /// Update the login button state based on input validation
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
    /// Show error message to the user and disable login button
    /// </summary>
    private void ShowErrorMessage(string message)
    {
        hasError = true;
        this.errorLabel.Show();
        this.errorLabel.Text = message;
        UpdateLoginButtonState();
    }

    /// <summary>
    /// Clear error state and hide error label
    /// </summary>
    private void ClearError()
    {
        hasError = false;
        this.errorLabel.Hide();
        this.errorLabel.Text = "";
    }
}
