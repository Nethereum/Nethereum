using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Nethereum.X402.Server;

internal static class X402DuplicateJsonProperties
{
    public static bool Contains(string json)
    {
        using var document = JsonDocument.Parse(json);
        return Contains(document.RootElement);
    }

    public static bool Contains(ReadOnlyMemory<byte> utf8Json)
    {
        using var document = JsonDocument.Parse(utf8Json);
        return Contains(document.RootElement);
    }

    public static bool Contains(JsonElement element)
    {
        try
        {
            return Walk(element);
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private enum Scope
    {
        Typed,
        Requirements,
        Resource,
        Opaque
    }

    private static readonly HashSet<string> RequirementMembers = new(StringComparer.OrdinalIgnoreCase)
    {
        "scheme", "network", "amount", "asset", "payTo", "maxTimeoutSeconds", "extra"
    };

    private static readonly HashSet<string> ResourceMembers = new(StringComparer.OrdinalIgnoreCase)
    {
        "url", "description", "mimeType", "serviceName", "tags", "iconUrl"
    };

    private static Scope ChildScope(Scope scope, string name)
    {
        if (scope == Scope.Opaque
            || name.Equals("extensions", StringComparison.OrdinalIgnoreCase)
            || name.Equals("extra", StringComparison.OrdinalIgnoreCase))
            return Scope.Opaque;

        if (scope == Scope.Requirements && !RequirementMembers.Contains(name))
            return Scope.Opaque;

        if (scope == Scope.Resource && !ResourceMembers.Contains(name))
            return Scope.Opaque;

        if (name.Equals("accepted", StringComparison.OrdinalIgnoreCase)
            || name.Equals("paymentRequirements", StringComparison.OrdinalIgnoreCase))
            return Scope.Requirements;

        return name.Equals("resource", StringComparison.OrdinalIgnoreCase) ? Scope.Resource : Scope.Typed;
    }

    private static bool Walk(JsonElement element, Scope scope = Scope.Typed)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var names = new HashSet<string>(scope == Scope.Opaque ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
                foreach (var property in element.EnumerateObject())
                {
                    var name = property.Name;
                    if (!names.Add(name) || Walk(property.Value, ChildScope(scope, name)))
                        return true;
                }

                return false;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (Walk(item, scope))
                        return true;
                }

                return false;
            default:
                return false;
        }
    }
}
