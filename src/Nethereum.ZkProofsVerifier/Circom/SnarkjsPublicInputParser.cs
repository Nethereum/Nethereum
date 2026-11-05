using System;
using Org.BouncyCastle.Math;
#if NET6_0_OR_GREATER
using System.Text.Json;
#else
using Newtonsoft.Json.Linq;
#endif

namespace Nethereum.ZkProofsVerifier.Circom
{
    public static class SnarkjsPublicInputParser
    {
        public const int MaxPublicInputs = 1024;

        public static BigInteger[] Parse(string json)
        {
            return Parse(json, -1);
        }

        internal static BigInteger[] Parse(string json, int expectedCount)
        {
            if (string.IsNullOrEmpty(json))
                throw new ArgumentException("Public inputs JSON is null or empty");

#if NET6_0_OR_GREATER
            return ParseWithStj(json, expectedCount);
#else
            return ParseWithNewtonsoft(json, expectedCount);
#endif
        }

        private static void CheckCount(int count, int expectedCount)
        {
            if (expectedCount >= 0)
            {
                if (count != expectedCount)
                    throw new ArgumentException(string.Format("Expected {0} public inputs but got {1}", expectedCount, count));
            }
            else if (count > MaxPublicInputs)
            {
                throw new ArgumentException("Public inputs array is longer than " + MaxPublicInputs + " elements");
            }
        }

#if NET6_0_OR_GREATER
        private static BigInteger[] ParseWithStj(string json, int expectedCount)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var count = root.GetArrayLength();
            CheckCount(count, expectedCount);
            var inputs = new BigInteger[count];
            for (int i = 0; i < inputs.Length; i++)
            {
                inputs[i] = new BigInteger(FieldElementText.RequireBoundedDecimal(root[i].GetString()));
            }
            return inputs;
        }
#else
        private static BigInteger[] ParseWithNewtonsoft(string json, int expectedCount)
        {
            var arr = JArray.Parse(json);
            CheckCount(arr.Count, expectedCount);
            var inputs = new BigInteger[arr.Count];
            for (int i = 0; i < inputs.Length; i++)
            {
                inputs[i] = new BigInteger(FieldElementText.RequireBoundedDecimal((string)arr[i]));
            }
            return inputs;
        }
#endif
    }
}
