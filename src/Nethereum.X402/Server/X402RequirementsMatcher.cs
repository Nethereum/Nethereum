using Nethereum.X402.Models;
using System.Globalization;
using System.Text.Json;

namespace Nethereum.X402.Server;

internal static class X402RequirementsMatcher
{
    public static PaymentRequirements? Find(IEnumerable<PaymentRequirements> offers, PaymentRequirements? accepted)
    {
        if (accepted == null)
            return null;

        var acceptedJson = JsonSerializer.SerializeToElement(accepted);
        foreach (var offer in offers)
        {
            if (JsonEqual(JsonSerializer.SerializeToElement(offer), acceptedJson))
                return offer;
        }

        return null;
    }

    private static bool JsonEqual(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
            return false;

        switch (left.ValueKind)
        {
            case JsonValueKind.Object:
                var rightProperties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                var rightCount = 0;
                foreach (var property in right.EnumerateObject())
                {
                    rightCount++;
                    rightProperties[property.Name] = property.Value;
                }

                var leftCount = 0;
                foreach (var property in left.EnumerateObject())
                {
                    leftCount++;
                    if (!rightProperties.TryGetValue(property.Name, out var other) || !JsonEqual(property.Value, other))
                        return false;
                }

                return leftCount == rightCount && rightCount == rightProperties.Count;
            case JsonValueKind.Array:
                var leftItems = left.EnumerateArray().ToList();
                var rightItems = right.EnumerateArray().ToList();
                if (leftItems.Count != rightItems.Count)
                    return false;

                for (var i = 0; i < leftItems.Count; i++)
                {
                    if (!JsonEqual(leftItems[i], rightItems[i]))
                        return false;
                }

                return true;
            case JsonValueKind.String:
                return string.Equals(left.GetString(), right.GetString(), StringComparison.Ordinal);
            case JsonValueKind.Number:
                return decimal.TryParse(left.GetRawText(), NumberStyles.Float, CultureInfo.InvariantCulture, out var leftNumber)
                       && decimal.TryParse(right.GetRawText(), NumberStyles.Float, CultureInfo.InvariantCulture, out var rightNumber)
                    ? leftNumber == rightNumber
                    : left.GetRawText() == right.GetRawText();
            default:
                return true;
        }
    }
}
