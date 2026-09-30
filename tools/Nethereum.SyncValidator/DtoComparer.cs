using System.Collections;
using System.Numerics;
using System.Reflection;
using Nethereum.Hex.HexTypes;

namespace Nethereum.SyncValidator;

internal static class DtoComparer
{
    // Fields where a null on one side vs a value on the other is expected drift, not a sync bug.
    // - TotalDifficulty: Erigon-class references drop it from eth_getBlockByNumber (EIP-3675 post-merge cleanup).
    // - BaseFeePerGas, WithdrawalsRoot, BlobGasUsed, ExcessBlobGas, ParentBeaconBlockRoot:
    //     pre-fork blocks won't have them; one side may legitimately encode null while the other returns 0.
    private static readonly HashSet<string> NullableOnEitherSide = new(StringComparer.Ordinal)
    {
        "TotalDifficulty",
    };

    public static List<string> Compare(object? a, object? b, string path = "$")
    {
        var diffs = new List<string>();
        CompareInternal(a, b, path, diffs);
        return diffs;
    }

    private static void CompareInternal(object? a, object? b, string path, List<string> diffs)
    {
        if (ReferenceEquals(a, b)) return;
        if (a is null || b is null)
        {
            if (FieldNameAllowsNullDrift(path)) return;
            diffs.Add($"{path}: reference={Render(a)} our={Render(b)}");
            return;
        }

        switch (a)
        {
            case HexBigInteger hbiA when b is HexBigInteger hbiB:
                if (hbiA.Value != hbiB.Value) diffs.Add($"{path}: reference={hbiA.Value} our={hbiB.Value}");
                return;

            case BigInteger biA when b is BigInteger biB:
                if (biA != biB) diffs.Add($"{path}: reference={biA} our={biB}");
                return;

            case string sA when b is string sB:
                if (!NormaliseHex(sA).Equals(NormaliseHex(sB), StringComparison.Ordinal))
                    diffs.Add($"{path}: reference={sA} our={sB}");
                return;

            case byte[] baA when b is byte[] baB:
                if (!baA.AsSpan().SequenceEqual(baB))
                    diffs.Add($"{path}: reference=0x{Convert.ToHexString(baA).ToLowerInvariant()} our=0x{Convert.ToHexString(baB).ToLowerInvariant()}");
                return;

            case IDictionary _:
                diffs.Add($"{path}: dictionary comparison not implemented");
                return;

            case IEnumerable enA when b is IEnumerable enB:
                CompareEnumerables(enA, enB, path, diffs);
                return;
        }

        var type = a.GetType();
        if (type.IsPrimitive || type == typeof(decimal))
        {
            if (!a.Equals(b)) diffs.Add($"{path}: reference={a} our={b}");
            return;
        }

        if (type != b.GetType())
        {
            diffs.Add($"{path}: type mismatch reference={type.Name} our={b.GetType().Name}");
            return;
        }

        // Recurse over public read properties
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetIndexParameters().Length > 0) continue;
            if (!prop.CanRead) continue;
            object? av, bv;
            try { av = prop.GetValue(a); bv = prop.GetValue(b); }
            catch { continue; }
            CompareInternal(av, bv, $"{path}.{prop.Name}", diffs);
        }
    }

    private static void CompareEnumerables(IEnumerable a, IEnumerable b, string path, List<string> diffs)
    {
        var la = a.Cast<object?>().ToList();
        var lb = b.Cast<object?>().ToList();
        if (la.Count != lb.Count)
        {
            diffs.Add($"{path}.Count: reference={la.Count} our={lb.Count}");
            return;
        }
        for (int i = 0; i < la.Count; i++)
            CompareInternal(la[i], lb[i], $"{path}[{i}]", diffs);
    }

    private static string NormaliseHex(string s)
    {
        // Strip 0x prefix on either side and lowercase — our RPC handlers vary on
        // whether they emit the prefix (e.g. Miner does not, hash fields do).
        // Underlying bytes are what matters; presentation is a separate issue.
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return s.Substring(2).ToLowerInvariant();
        return s.ToLowerInvariant();
    }

    private static bool FieldNameAllowsNullDrift(string path)
    {
        // Last segment after '.' is the property name.
        var dot = path.LastIndexOf('.');
        if (dot < 0) return false;
        return NullableOnEitherSide.Contains(path.Substring(dot + 1));
    }

    private static string Render(object? o) => o switch
    {
        null => "<null>",
        HexBigInteger h => h.Value.ToString(),
        byte[] ba => "0x" + Convert.ToHexString(ba).ToLowerInvariant(),
        _ => o.ToString() ?? "<null>"
    };
}
