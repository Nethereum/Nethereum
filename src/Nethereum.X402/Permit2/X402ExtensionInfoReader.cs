using System.Text.Json;

namespace Nethereum.X402.Permit2;

internal static class X402ExtensionInfoReader
{
    public static bool TryGetInfo(object? extensions, string key, out JsonElement info)
    {
        info = default;
        if (extensions == null) return false;

        var root = extensions is JsonElement element ? element : JsonSerializer.SerializeToElement(extensions);
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty(key, out var extension) ||
            extension.ValueKind != JsonValueKind.Object ||
            !extension.TryGetProperty("info", out info) ||
            info.ValueKind != JsonValueKind.Object)
            return false;

        return true;
    }

    public static string? ReadString(JsonElement owner, string name) =>
        owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
