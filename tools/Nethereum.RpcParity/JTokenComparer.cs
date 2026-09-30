using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Nethereum.RpcParity
{
    /// <summary>
    /// Structural diff over two Newtonsoft <see cref="JToken"/> trees, used
    /// for raw debug_trace* results that have no typed Nethereum DTO. Object
    /// keys are compared as a set and walked in sorted order so key ordering
    /// alone never produces a false diff.
    /// </summary>
    public static class JTokenComparer
    {
        public static List<Diff> Compare(JToken x, JToken y)
        {
            var diffs = new List<Diff>();
            CompareInto(diffs, x, y, "$");
            return diffs;
        }

        private static void CompareInto(List<Diff> diffs, JToken x, JToken y, string path)
        {
            var xType = x?.Type ?? JTokenType.Null;
            var yType = y?.Type ?? JTokenType.Null;

            if (xType == JTokenType.Null && yType == JTokenType.Null)
                return;

            if (xType == JTokenType.Object && yType == JTokenType.Object)
            {
                CompareObjects(diffs, (JObject)x, (JObject)y, path);
                return;
            }

            if (xType == JTokenType.Array && yType == JTokenType.Array)
            {
                CompareArrays(diffs, (JArray)x, (JArray)y, path);
                return;
            }

            if (xType != yType || !JToken.DeepEquals(x, y))
                diffs.Add(new Diff(path, Describe(x), Describe(y)));
        }

        private static void CompareObjects(List<Diff> diffs, JObject x, JObject y, string path)
        {
            var keys = x.Properties().Select(p => p.Name)
                .Union(y.Properties().Select(p => p.Name), StringComparer.Ordinal)
                .OrderBy(k => k, StringComparer.Ordinal);
            foreach (var key in keys)
                CompareInto(diffs, x[key], y[key], $"{path}.{key}");
        }

        private static void CompareArrays(List<Diff> diffs, JArray x, JArray y, string path)
        {
            if (x.Count != y.Count)
            {
                diffs.Add(new Diff(path + ".Length", x.Count, y.Count));
                return;
            }
            for (int i = 0; i < x.Count; i++)
                CompareInto(diffs, x[i], y[i], $"{path}[{i}]");
        }

        private static string Describe(JToken t) =>
            t == null ? "null" : t.ToString(Newtonsoft.Json.Formatting.None);
    }
}
