using System;
using Godot;

/// <summary>
/// Provides helper methods for JSON parsing with consistent error handling.
/// </summary>
/// <remarks>
/// Eliminates repetitive try-catch blocks around Json.Parse() calls throughout the codebase.
/// All parsing methods return false on failure and log errors consistently.
/// </remarks>
public static class JsonHelper
{
    /// <summary>
    /// Attempts to parse a JSON string into a Godot dictionary.
    /// </summary>
    /// <param name="jsonString">The JSON string to parse.</param>
    /// <param name="result">The parsed dictionary, or null on failure.</param>
    /// <param name="context">Context identifier for error logging (e.g., class name).</param>
    /// <returns>True if parsing succeeded; false otherwise.</returns>
    public static bool TryParseDictionary(
        string jsonString,
        out Godot.Collections.Dictionary result,
        string context = null
    )
    {
        result = null;

        if (string.IsNullOrEmpty(jsonString))
        {
            GameLogger.Error("JSON string is null or empty", context);
            return false;
        }

        try
        {
            var json = new Json();
            Error parseError = json.Parse(jsonString);

            if (parseError != Error.Ok)
            {
                GameLogger.Error($"JSON parse error: {parseError}", context);
                return false;
            }

            result = json.Data.AsGodotDictionary();
            return true;
        }
        catch (Exception ex)
        {
            GameLogger.Exception(ex, "Failed to parse JSON", context);
            return false;
        }
    }

    /// <summary>
    /// Attempts to parse a JSON string into a Godot array.
    /// </summary>
    /// <param name="jsonString">The JSON string to parse.</param>
    /// <param name="result">The parsed array, or null on failure.</param>
    /// <param name="context">Context identifier for error logging.</param>
    /// <returns>True if parsing succeeded; false otherwise.</returns>
    public static bool TryParseArray(
        string jsonString,
        out Godot.Collections.Array result,
        string context = null
    )
    {
        result = null;

        if (string.IsNullOrEmpty(jsonString))
        {
            GameLogger.Error("JSON string is null or empty", context);
            return false;
        }

        try
        {
            var json = new Json();
            Error parseError = json.Parse(jsonString);

            if (parseError != Error.Ok)
            {
                GameLogger.Error($"JSON parse error: {parseError}", context);
                return false;
            }

            result = json.Data.AsGodotArray();
            return true;
        }
        catch (Exception ex)
        {
            GameLogger.Exception(ex, "Failed to parse JSON array", context);
            return false;
        }
    }

    /// <summary>
    /// Safely extracts a string value from a dictionary.
    /// </summary>
    /// <param name="dict">The dictionary to search.</param>
    /// <param name="key">The key to look up.</param>
    /// <param name="defaultValue">Value to return if key not found or not a string.</param>
    /// <returns>The string value, or defaultValue if not found.</returns>
    public static string GetString(
        Godot.Collections.Dictionary dict,
        string key,
        string defaultValue = ""
    )
    {
        if (dict == null || !dict.ContainsKey(key))
            return defaultValue;

        var value = dict[key];
        return value.VariantType == Variant.Type.String ? (string)value : defaultValue;
    }

    /// <summary>
    /// Safely extracts an integer value from a dictionary.
    /// </summary>
    /// <param name="dict">The dictionary to search.</param>
    /// <param name="key">The key to look up.</param>
    /// <param name="defaultValue">Value to return if key not found or not a number.</param>
    /// <returns>The integer value, or defaultValue if not found.</returns>
    public static int GetInt(Godot.Collections.Dictionary dict, string key, int defaultValue = 0)
    {
        if (dict == null || !dict.ContainsKey(key))
            return defaultValue;

        var value = dict[key];
        return value.VariantType == Variant.Type.Int ? (int)(long)value : defaultValue;
    }

    /// <summary>
    /// Safely extracts a boolean value from a dictionary.
    /// </summary>
    /// <param name="dict">The dictionary to search.</param>
    /// <param name="key">The key to look up.</param>
    /// <param name="defaultValue">Value to return if key not found or not a bool.</param>
    /// <returns>The boolean value, or defaultValue if not found.</returns>
    public static bool GetBool(
        Godot.Collections.Dictionary dict,
        string key,
        bool defaultValue = false
    )
    {
        if (dict == null || !dict.ContainsKey(key))
            return defaultValue;

        var value = dict[key];
        return value.VariantType == Variant.Type.Bool ? (bool)value : defaultValue;
    }

    /// <summary>
    /// Safely extracts a nested dictionary from a dictionary.
    /// </summary>
    /// <param name="dict">The dictionary to search.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>The nested dictionary, or empty dictionary if not found.</returns>
    public static Godot.Collections.Dictionary GetDictionary(
        Godot.Collections.Dictionary dict,
        string key
    )
    {
        if (dict == null || !dict.ContainsKey(key))
            return new Godot.Collections.Dictionary();

        var value = dict[key];
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new Godot.Collections.Dictionary();
    }

    /// <summary>
    /// Safely extracts a nested array from a dictionary.
    /// </summary>
    /// <param name="dict">The dictionary to search.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>The nested array, or empty array if not found.</returns>
    public static Godot.Collections.Array GetArray(Godot.Collections.Dictionary dict, string key)
    {
        if (dict == null || !dict.ContainsKey(key))
            return new Godot.Collections.Array();

        var value = dict[key];
        return value.VariantType == Variant.Type.Array
            ? value.AsGodotArray()
            : new Godot.Collections.Array();
    }

    /// <summary>
    /// Converts a Godot Variant to a JSON string.
    /// </summary>
    /// <param name="data">The Variant to serialize.</param>
    /// <param name="context">Context identifier for error logging.</param>
    /// <returns>The JSON string, or null on failure.</returns>
    public static string ToJsonString(Variant data, string context = null)
    {
        try
        {
            return Json.Stringify(data);
        }
        catch (Exception ex)
        {
            GameLogger.Exception(ex, "Failed to serialize to JSON", context);
            return null;
        }
    }
}
