using System;
using Org.BouncyCastle.Math;
#if NET6_0_OR_GREATER
using System.Text.Json;
#else
using Newtonsoft.Json.Linq;
#endif

namespace Nethereum.ZkProofsVerifier.Circom
{
    internal static class SnarkjsPublicInputParser
    {
        public static BigInteger[] Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
                throw new ArgumentException("Public inputs JSON is null or empty");

#if NET6_0_OR_GREATER
            return ParseWithStj(json);
#else
            return ParseWithNewtonsoft(json);
#endif
        }

#if NET6_0_OR_GREATER
        private static BigInteger[] ParseWithStj(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var inputs = new BigInteger[root.GetArrayLength()];
            for (int i = 0; i < inputs.Length; i++)
            {
                inputs[i] = new BigInteger(root[i].GetString());
            }
            return inputs;
        }
#else
        private static BigInteger[] ParseWithNewtonsoft(string json)
        {
            var arr = JArray.Parse(json);
            var inputs = new BigInteger[arr.Count];
            for (int i = 0; i < inputs.Length; i++)
            {
                inputs[i] = new BigInteger((string)arr[i]);
            }
            return inputs;
        }
#endif
    }
}
