using System;

/// <summary>
/// Centralizes server URL construction for HTTP and WebSocket endpoints.
/// </summary>
/// <remarks>
/// Eliminates hardcoded URL patterns scattered throughout the codebase.
/// All URLs are constructed here to ensure consistency.
/// </remarks>
public static class ServerUrls
{
    /// <summary>
    /// Gets or sets the base server URL.
    /// </summary>
    /// <remarks>
    /// Can be overridden for testing or different environments.
    /// </remarks>
    public static string BaseUrl { get; set; } = "http://90.28.104.14:1337";

    /// <summary>
    /// Gets or sets the WebSocket base URL.
    /// </summary>
    public static string WebSocketBaseUrl { get; set; } = "ws://90.28.104.14:1337";

    /// <summary>
    /// Constructs the login endpoint URL.
    /// </summary>
    /// <returns>The full URL for login requests.</returns>
    public static string Login()
    {
        return $"{BaseUrl}/auth/login";
    }

    /// <summary>
    /// Constructs the lobby list endpoint URL.
    /// </summary>
    /// <returns>The full URL for fetching available lobbies.</returns>
    public static string ListLobbies()
    {
        return $"{BaseUrl}/lobby";
    }

    /// <summary>
    /// Constructs the lobby creation endpoint URL.
    /// </summary>
    /// <returns>The full URL for creating a new lobby.</returns>
    public static string CreateLobby()
    {
        return $"{BaseUrl}/lobby";
    }

    /// <summary>
    /// Constructs the join lobby endpoint URL.
    /// </summary>
    /// <param name="lobbyId">The lobby ID to join.</param>
    /// <returns>The full URL for joining a lobby.</returns>
    /// <exception cref="ArgumentException">Thrown when lobbyId is null or empty.</exception>
    public static string JoinLobby(string lobbyId)
    {
        if (string.IsNullOrEmpty(lobbyId))
            throw new ArgumentException("Lobby ID cannot be null or empty", nameof(lobbyId));

        return $"{BaseUrl}/lobby/{lobbyId}/join";
    }

    /// <summary>
    /// Constructs the start game endpoint URL.
    /// </summary>
    /// <param name="lobbyId">The lobby ID to start the game in.</param>
    /// <returns>The full URL for starting a game.</returns>
    /// <exception cref="ArgumentException">Thrown when lobbyId is null or empty.</exception>
    public static string StartGame(string lobbyId)
    {
        if (string.IsNullOrEmpty(lobbyId))
            throw new ArgumentException("Lobby ID cannot be null or empty", nameof(lobbyId));

        return $"{BaseUrl}/lobby/{lobbyId}/start";
    }

    /// <summary>
    /// Constructs the lobby WebSocket URL with authentication token.
    /// </summary>
    /// <param name="lobbyId">The lobby ID to connect to.</param>
    /// <param name="jwtToken">The JWT authentication token.</param>
    /// <returns>The full WebSocket URL with token parameter.</returns>
    /// <exception cref="ArgumentException">Thrown when lobbyId or jwtToken is null or empty.</exception>
    public static string LobbyWebSocket(string lobbyId, string jwtToken)
    {
        if (string.IsNullOrEmpty(lobbyId))
            throw new ArgumentException("Lobby ID cannot be null or empty", nameof(lobbyId));

        if (string.IsNullOrEmpty(jwtToken))
            throw new ArgumentException("JWT token cannot be null or empty", nameof(jwtToken));

        string encodedToken = Uri.EscapeDataString(jwtToken);
        return $"{WebSocketBaseUrl}/lobby/{lobbyId}/ws?token={encodedToken}";
    }

    /// <summary>
    /// Constructs authorization header with Bearer token.
    /// </summary>
    /// <param name="jwtToken">The JWT token.</param>
    /// <returns>The formatted authorization header.</returns>
    public static string AuthorizationHeader(string jwtToken)
    {
        return $"Authorization: Bearer {jwtToken}";
    }
}
