using System.Dynamic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static class ExpandoExtensions
{
    // 1. To Dynamic
    public static dynamic AsDynamic(this ExpandoObject obj) => obj;

    // 2. To Strongly Typed
    public static T ToObject<T>(this ExpandoObject obj)
    {
        var json = JsonConvert.SerializeObject(obj);
        return JsonConvert.DeserializeObject<T>(json)!;
    }

    // 3. To JObject
    public static JObject ToJObject(this ExpandoObject obj)
    {
        var json = JsonConvert.SerializeObject(obj);
        return JObject.Parse(json);
    }

    // 4. To JSON string
    public static string ToJson(this ExpandoObject obj, bool indented = false)
    {
        return JsonConvert.SerializeObject(obj, indented ? Formatting.Indented : Formatting.None);
    }

    // 5. From JSON string → ExpandoObject
    public static ExpandoObject ToExpando(this string json)
    {
        return JsonConvert.DeserializeObject<ExpandoObject>(json)!;
    }

    // 6. From JObject → ExpandoObject
    public static ExpandoObject ToExpando(this JObject jObj)
    {
        return JsonConvert.DeserializeObject<ExpandoObject>(jObj.ToString())!;
    }

    // 7. From Strongly Typed → ExpandoObject
    public static ExpandoObject ToExpando<T>(this T obj)
    {
        var json = JsonConvert.SerializeObject(obj);
        return JsonConvert.DeserializeObject<ExpandoObject>(json)!;
    }

    // 8. To Dictionary
    public static IDictionary<string, object?> ToDictionary(this ExpandoObject obj)
        => (IDictionary<string, object?>)obj;

    // 9. From Dictionary → ExpandoObject
    public static ExpandoObject ToExpando(this IDictionary<string, object?> dict)
    {
        var expando = new ExpandoObject();
        var expandoDict = (IDictionary<string, object?>)expando;

        foreach (var kvp in dict)
        {
            expandoDict[kvp.Key] = kvp.Value;
        }

        return expando;
    }
}
