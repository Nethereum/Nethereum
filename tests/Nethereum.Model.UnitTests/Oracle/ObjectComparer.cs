using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Model.UnitTests.Oracle
{
    public static class ObjectComparer
    {
        public static string FirstDifference(object expected, object actual)
        {
            return Compare(expected, actual, "$", 0);
        }

        private static string Compare(object expected, object actual, string path, int depth)
        {
            if (depth > 12) return null;

            if (expected is byte[] || actual is byte[])
            {
                var e = expected as byte[] ?? new byte[0];
                var a = actual as byte[] ?? new byte[0];
                if (expected != null && !(expected is byte[]) || actual != null && !(actual is byte[]))
                    return path + ": byte[] against " + expected?.GetType().Name + "/" + actual?.GetType().Name;
                return e.SequenceEqual(a) ? null : path + ": 0x" + e.ToHex() + " != 0x" + a.ToHex();
            }

            if (expected == null || actual == null)
            {
                if (expected == null && actual == null) return null;
                return path + ": " + (expected ?? "null") + " != " + (actual ?? "null");
            }

            var type = expected.GetType();
            if (type != actual.GetType())
                return path + ": type " + type.Name + " != " + actual.GetType().Name;

            if (type.IsPrimitive || type.IsEnum || expected is string || type.IsValueType)
                return expected.Equals(actual) ? null : path + ": " + expected + " != " + actual;

            if (expected is IEnumerable enumerable)
            {
                var left = enumerable.Cast<object>().ToList();
                var right = ((IEnumerable)actual).Cast<object>().ToList();
                if (left.Count != right.Count) return path + ": count " + left.Count + " != " + right.Count;
                for (var i = 0; i < left.Count; i++)
                {
                    var difference = Compare(left[i], right[i], path + "[" + i + "]", depth + 1);
                    if (difference != null) return difference;
                }
                return null;
            }

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length != 0) continue;
                if (property.Name == "Hash" || property.Name == "RawHash") continue;
                object left, right;
                try { left = property.GetValue(expected); } catch (TargetInvocationException) { continue; }
                try { right = property.GetValue(actual); } catch (TargetInvocationException) { return path + "." + property.Name + ": getter throws on actual only"; }
                var difference = Compare(left, right, path + "." + property.Name, depth + 1);
                if (difference != null) return difference;
            }
            return null;
        }
    }
}
