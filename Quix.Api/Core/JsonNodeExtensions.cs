using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Quix.Api.Core;

public static class JsonNodeExtensions
{
    private static readonly Regex ArrayRegex = new(@"\[(\d+)\]");

    public static T? GetPath<T>(this JsonObject obj, string path)
    {
        JsonNode? current = obj;

        foreach (var segment in path.Split('.'))
        {
            if (current is null) return default;

            string property = segment;
            int? index = null;

            // Handle segments like projects[1]
            var match = ArrayRegex.Match(segment);
            if (match.Success)
            {
                property = segment[..segment.IndexOf('[')];
                index = int.Parse(match.Groups[1].Value);
            }

            // Navigate object property
            if (current is JsonObject o)
            {
                if (!o.TryGetPropertyValue(property, out current) || current is null)
                    return default;
            }
            else
            {
                return default;
            }

            // Navigate array index if present
            if (index.HasValue)
            {
                if (current is JsonArray arr)
                {
                    if (index.Value >= arr.Count) return default;
                    current = arr[index.Value];
                }
                else
                {
                    return default;
                }
            }
        }

        if (current is null)
            return default;

        // Decide primitive vs complex
        if (IsSimpleType(typeof(T)))
        {
            try
            {
                return current.GetValue<T>();
            }
            catch
            {
                return default;
            }
        }
        else
        {
            try
            {
                return JsonSerializer.Deserialize<T>(current.ToJsonString());
            }
            catch
            {
                return default;
            }
        }
    }
    public static JsonObject SetPath<T>(this JsonObject obj, string path, T value)
    {
        JsonNode current = obj;

        string[] parts = path.Split('.');
        for (int i = 0; i < parts.Length; i++)
        {
            string segment = parts[i];
            bool isLast = i == parts.Length - 1;

            string property = segment;
            int? index = null;

            var match = ArrayRegex.Match(segment);
            if (match.Success)
            {
                property = segment[..segment.IndexOf('[')];
                index = int.Parse(match.Groups[1].Value);
            }

            // Navigate or create the property
            JsonNode? child;
            if (current is JsonObject o)
            {
                if (!o.TryGetPropertyValue(property, out child) || child is null)
                {
                    // If next is an array access, create array; otherwise create object
                    child = index.HasValue ? new JsonArray() : new JsonObject();
                    o[property] = child;
                }
            }
            else
            {
                throw new InvalidOperationException($"Cannot set path on non-object node at '{property}'");
            }

            current = child;

            // Navigate or create the array item
            if (index.HasValue)
            {
                if (current is JsonArray arr)
                {
                    while (arr.Count <= index.Value)
                        arr.Add(new JsonObject());

                    if (isLast)
                    {
                        arr[index.Value] = SerializeValue(value);
                        return obj;
                    }
                    else
                    {
                        current = arr[index.Value]!;
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Expected array at '{property}'");
                }
            }

            // If this is the final segment and not array
            if (isLast && !index.HasValue)
            {
                if (current is JsonObject parentObj)
                {
                    parentObj[property] = SerializeValue(value);
                }
                return obj;
            }
        }
        return obj;
    }

    private static JsonNode SerializeValue<T>(T value)
    {
        if (value == null)
            return null;

        if (value is JsonNode node)
            return node;

        // If it's a primitive, create a JsonValue directly
        Type t = typeof(T);
        if (t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal))
            return JsonValue.Create(value);

        // Otherwise serialize to JsonNode
        return JsonNode.Parse(JsonSerializer.Serialize(value))!;
    }

    private static bool IsSimpleType(Type type)
    {
        if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal))
            return true;

        var underlying = Nullable.GetUnderlyingType(type);
        return underlying != null && (underlying.IsPrimitive || underlying.IsEnum || underlying == typeof(decimal));
    }

}
