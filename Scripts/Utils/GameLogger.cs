using System;
using Godot;

/// <summary>
/// Provides centralized logging with configurable log levels for the Munchkin game.
/// </summary>
/// <remarks>
/// Replaces scattered GD.Print statements with a unified logging interface.
/// Log level can be configured at runtime or compile time.
/// </remarks>
public static class GameLogger
{
    /// <summary>
    /// Defines the severity levels for log messages.
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// Detailed debugging information (development only).
        /// </summary>
        Debug,

        /// <summary>
        /// General information messages.
        /// </summary>
        Info,

        /// <summary>
        /// Warning messages for potential issues.
        /// </summary>
        Warning,

        /// <summary>
        /// Error messages for failures.
        /// </summary>
        Error,

        /// <summary>
        /// Critical errors that may crash the game.
        /// </summary>
        Fatal,
    }

    /// <summary>
    /// Gets or sets the minimum log level to output.
    /// </summary>
    /// <remarks>
    /// Messages below this level are silently discarded.
    /// Default is Info for production, Debug for development.
    /// </remarks>
    public static LogLevel MinimumLevel { get; set; } = LogLevel.Info;

    /// <summary>
    /// Gets or sets whether to include timestamps in log messages.
    /// </summary>
    public static bool IncludeTimestamp { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include the calling class name in log messages.
    /// </summary>
    public static bool IncludeClassName { get; set; } = true;

    /// <summary>
    /// Logs a debug message (detailed information for development).
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="context">Optional context object for identifying the source.</param>
    public static void Debug(string message, object context = null)
    {
        Log(LogLevel.Debug, message, context);
    }

    /// <summary>
    /// Logs an informational message.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="context">Optional context object for identifying the source.</param>
    public static void Info(string message, object context = null)
    {
        Log(LogLevel.Info, message, context);
    }

    /// <summary>
    /// Logs a warning message (potential issue but not fatal).
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="context">Optional context object for identifying the source.</param>
    public static void Warning(string message, object context = null)
    {
        Log(LogLevel.Warning, message, context);
    }

    /// <summary>
    /// Logs an error message (operation failed but game continues).
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="context">Optional context object for identifying the source.</param>
    public static void Error(string message, object context = null)
    {
        Log(LogLevel.Error, message, context);
    }

    /// <summary>
    /// Logs an exception with optional additional message.
    /// </summary>
    /// <param name="ex">The exception to log.</param>
    /// <param name="message">Optional additional context.</param>
    /// <param name="context">Optional context object for identifying the source.</param>
    public static void Exception(Exception ex, string message = null, object context = null)
    {
        string fullMessage = message != null ? $"{message}: {ex.Message}" : ex.Message;

        Log(LogLevel.Error, fullMessage, context);
    }

    /// <summary>
    /// Logs a fatal error message (game cannot continue).
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="context">Optional context object for identifying the source.</param>
    public static void Fatal(string message, object context = null)
    {
        Log(LogLevel.Fatal, message, context);
    }

    /// <summary>
    /// Internal logging method that filters by level and formats output.
    /// </summary>
    /// <param name="level">The severity level of the message.</param>
    /// <param name="message">The message content.</param>
    /// <param name="context">Optional context for identifying the source.</param>
    private static void Log(LogLevel level, string message, object context)
    {
        // Filter by minimum level
        if (level < MinimumLevel)
            return;

        // Build prefix
        string prefix = "";

        if (IncludeTimestamp)
        {
            prefix += $"[{DateTime.Now:HH:mm:ss}] ";
        }

        if (IncludeClassName && context != null)
        {
            string className = context is string str ? str : context.GetType().Name;
            prefix += $"[{className}] ";
        }

        string levelStr = level.ToString().ToUpper();
        string formattedMessage = $"{prefix}[{levelStr}] {message}";

        // Output based on level
        switch (level)
        {
            case LogLevel.Debug:
            case LogLevel.Info:
                GD.Print(formattedMessage);
                break;
            case LogLevel.Warning:
                GD.PushWarning(formattedMessage);
                break;
            case LogLevel.Error:
            case LogLevel.Fatal:
                GD.PushError(formattedMessage);
                break;
        }
    }
}
