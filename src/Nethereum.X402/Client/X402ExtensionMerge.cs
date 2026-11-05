using System.Text.Json;
using System.Text.Json.Nodes;

namespace Nethereum.X402.Client;

internal static class X402ExtensionMerge
{
    public static object? Merge(object? serverExtensions, JsonObject? clientExtensions)
    {
        if (clientExtensions == null) return serverExtensions;

        var server = ToObject(serverExtensions);
        if (server == null) return clientExtensions;

        var merged = (JsonObject)server.DeepClone();
        foreach (var (key, clientValue) in clientExtensions)
        {
            if (merged[key] is JsonObject serverValue && clientValue is JsonObject clientObject)
                MergeFields(serverValue, clientObject);
            else
                merged[key] = clientValue?.DeepClone();
        }
        return merged;
    }

    public static bool Declares(object? serverExtensions, string key) =>
        ToObject(serverExtensions)?[key] != null;

    private static void MergeFields(JsonObject target, JsonObject source)
    {
        foreach (var (key, sourceValue) in source)
        {
            if (target[key] is JsonObject targetObject && sourceValue is JsonObject sourceObject)
                MergeFields(targetObject, sourceObject);
            else if (!target.ContainsKey(key))
                target[key] = sourceValue?.DeepClone();
        }
    }

    private static JsonObject? ToObject(object? extensions) =>
        extensions == null ? null : JsonSerializer.SerializeToNode(extensions) as JsonObject;
}
