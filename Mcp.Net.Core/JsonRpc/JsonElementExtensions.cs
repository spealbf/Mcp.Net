using System;
using System.Text.Json;

namespace Mcp.Net.Core.JsonRpc;

/// <summary>
/// Extension methods for JsonElement
/// </summary>
public static class JsonElementExtensions
{
    /// <summary>
    /// Converts a JsonElement to an object
    /// </summary>
    /// <param name="element">The JsonElement to convert</param>
    /// <returns>An object representation of the JsonElement</returns>
    public static object? ToObject(this JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                return element; // Return the JsonElement itself as an object
            case JsonValueKind.Array:
                return element; // Return the JsonElement itself as an array
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Number:
                if (element.TryGetInt32(out int intValue))
                    return intValue;
                if (element.TryGetInt64(out long longValue))
                    return longValue;
                return element.GetDouble();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Null:
                return null;
            default:
                return null;
        }
    }
}
