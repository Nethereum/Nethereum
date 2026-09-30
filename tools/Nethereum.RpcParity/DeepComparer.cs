using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RpcParity
{
    /// <summary>
    /// Reflection-based structural diff for Nethereum RPC DTOs. Walks the
    /// public instance properties of two same-shaped objects recursively —
    /// nested objects, lists/arrays by position, <see cref="HexBigInteger"/>
    /// and <see cref="BigInteger"/> by numeric value, hex strings by value
    /// (leading-zero padding is not a divergence) — and reports every
    /// mismatch as a <see cref="Diff"/> carrying the full property path, so a
    /// caller can point at exactly where two node responses diverge.
    ///
    /// Two normalisations keep the diff limited to real geth-parity gaps:
    /// a null and an empty collection are the same "nothing here" shape, and
    /// a <see cref="Transaction"/> field that its <c>Type</c> does not define
    /// is skipped rather than compared, so an implementation defaulting an
    /// inapplicable field to zero instead of null is not reported as a diff.
    /// </summary>
    public static class DeepComparer
    {
        private const int MaxDepth = 24;

        public static List<Diff> Compare(object x, object y, ISet<string> ignoredProperties = null)
        {
            var diffs = new List<Diff>();
            var ignore = ignoredProperties ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CompareInto(diffs, x, y, "$", ignore, 0);
            return diffs;
        }

        private static void CompareInto(List<Diff> diffs, object x, object y, string path, ISet<string> ignore, int depth)
        {
            if (depth > MaxDepth)
                return;

            if (x == null && y == null)
                return;
            if (x == null || y == null)
            {
                if (IsEmptyOrNullCollection(x) && IsEmptyOrNullCollection(y))
                    return;
                diffs.Add(new Diff(path, x, y));
                return;
            }

            if (x is HexBigInteger xHex)
            {
                if (!(y is HexBigInteger yHex) || xHex.Value != yHex.Value)
                    diffs.Add(new Diff(path, x, y));
                return;
            }

            if (x is BigInteger xBig)
            {
                if (!(y is BigInteger yBig) || xBig != yBig)
                    diffs.Add(new Diff(path, x, y));
                return;
            }

            if (x is byte[] xBytes)
            {
                if (!(y is byte[] yBytes) || !xBytes.SequenceEqual(yBytes))
                    diffs.Add(new Diff(path, x, y));
                return;
            }

            if (x is string xString)
            {
                if (!HexAwareStringEquals(xString, y as string))
                    diffs.Add(new Diff(path, x, y));
                return;
            }

            var type = x.GetType();
            if (type.IsPrimitive || type.IsEnum || x is decimal)
            {
                if (!Equals(x, y))
                    diffs.Add(new Diff(path, x, y));
                return;
            }

            if (x is IEnumerable xEnumerable && y is IEnumerable yEnumerable)
            {
                CompareEnumerables(diffs, xEnumerable, yEnumerable, path, ignore, depth);
                return;
            }

            CompareObjects(diffs, x, y, path, ignore, depth);
        }

        private static void CompareEnumerables(List<Diff> diffs, IEnumerable xEnumerable, IEnumerable yEnumerable, string path, ISet<string> ignore, int depth)
        {
            var xList = xEnumerable.Cast<object>().ToList();
            var yList = yEnumerable.Cast<object>().ToList();
            if (xList.Count != yList.Count)
            {
                diffs.Add(new Diff(path + ".Length", xList.Count, yList.Count));
                return;
            }
            for (int i = 0; i < xList.Count; i++)
                CompareInto(diffs, xList[i], yList[i], $"{path}[{i}]", ignore, depth + 1);
        }

        private static void CompareObjects(List<Diff> diffs, object x, object y, string path, ISet<string> ignore, int depth)
        {
            var type = x.GetType();
            if (y.GetType() != type)
            {
                diffs.Add(new Diff(path + ".GetType()", type.Name, y.GetType().Name));
                return;
            }

            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0);

            var txType = type == typeof(Transaction) ? ResolveTxType((Transaction)x, (Transaction)y) : (BigInteger?)null;

            foreach (var property in properties)
            {
                if (ignore.Contains(property.Name))
                    continue;
                if (txType.HasValue && !IsTxFieldApplicable(property.Name, txType.Value))
                    continue;

                var xValue = property.GetValue(x);
                var yValue = property.GetValue(y);
                CompareInto(diffs, xValue, yValue, $"{path}.{property.Name}", ignore, depth + 1);
            }
        }

        // A property that is null on one side and an empty collection on the
        // other reports "nothing here" on both sides — not a divergence. A
        // property that is null on one side and non-empty on the other is a
        // real gap and must still fall through to the diff below.
        private static bool IsEmptyOrNullCollection(object value)
        {
            if (value == null)
                return true;
            if (value is string)
                return false;
            return value is IEnumerable enumerable && !enumerable.Cast<object>().Any();
        }

        private static readonly HashSet<string> TxTypeScopedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            nameof(Transaction.MaxFeePerGas),
            nameof(Transaction.MaxPriorityFeePerGas),
            nameof(Transaction.MaxFeePerBlobGas),
            nameof(Transaction.BlobVersionedHashes),
            nameof(Transaction.AuthorisationList)
        };

        private const int DynamicFeeTxType = 2;
        private const int BlobTxType = 3;
        private const int SetCodeTxType = 4;

        private static bool IsTxFieldApplicable(string propertyName, BigInteger txType)
        {
            if (!TxTypeScopedFields.Contains(propertyName))
                return true;

            switch (propertyName)
            {
                case nameof(Transaction.MaxFeePerGas):
                case nameof(Transaction.MaxPriorityFeePerGas):
                    return txType >= DynamicFeeTxType;
                case nameof(Transaction.MaxFeePerBlobGas):
                case nameof(Transaction.BlobVersionedHashes):
                    return txType == BlobTxType;
                case nameof(Transaction.AuthorisationList):
                    return txType == SetCodeTxType;
                default:
                    return true;
            }
        }

        private static BigInteger ResolveTxType(Transaction x, Transaction y)
        {
            var raw = x.Type ?? y.Type;
            return raw?.Value ?? BigInteger.Zero;
        }

        private static bool HexAwareStringEquals(string x, string y)
        {
            if (x == null || y == null)
                return x == y;
            if (LooksLikeHex(x) && LooksLikeHex(y))
                return NormalizeHex(x) == NormalizeHex(y);
            return string.Equals(x, y, StringComparison.Ordinal);
        }

        private static bool LooksLikeHex(string s) =>
            s.Length >= 2 && s[0] == '0' && (s[1] == 'x' || s[1] == 'X');

        // Numeric-value comparison of hex strings: strips the leading-zero
        // padding differences nodes routinely disagree on (e.g. storage
        // reads) without masking a genuine value difference.
        private static string NormalizeHex(string s)
        {
            var trimmed = s.Substring(2).ToLowerInvariant().TrimStart('0');
            return trimmed.Length == 0 ? "0" : trimmed;
        }
    }
}
