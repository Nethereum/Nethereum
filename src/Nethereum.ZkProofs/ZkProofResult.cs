using System;
using System.Numerics;
using System.Text;
using Nethereum.ZkProofsVerifier.Circom;
#if NET6_0_OR_GREATER
using System.Text.Json;
#else
using Newtonsoft.Json.Linq;
#endif

namespace Nethereum.ZkProofs
{
    public class ZkProofResult
    {
        public ZkProofScheme Scheme { get; set; } = ZkProofScheme.Unknown;

        public string ProofJson { get; set; } = "";
        public string PublicSignalsJson { get; set; } = "";

        public byte[] ProofBytes { get; set; } = new byte[0];
        public byte[] PublicInputsBytes { get; set; } = new byte[0];

        public BigInteger[] PublicSignals { get; set; } = new BigInteger[0];

        public static ZkProofResult BuildFromJson(ZkProofScheme scheme, string proofJson, string publicSignalsJson)
        {
            return new ZkProofResult
            {
                Scheme = scheme,
                ProofJson = proofJson,
                PublicSignalsJson = publicSignalsJson,
                PublicSignals = ParsePublicSignals(publicSignalsJson)
            };
        }

        public const int MaxPublicSignals = 1024;

        public static BigInteger[] ParsePublicSignals(string publicSignalsJson)
        {
            if (string.IsNullOrEmpty(publicSignalsJson))
                return new BigInteger[0];

#if NET6_0_OR_GREATER
            return ParsePublicSignalsStj(publicSignalsJson);
#else
            return ParsePublicSignalsNewtonsoft(publicSignalsJson);
#endif
        }

#if NET6_0_OR_GREATER
        private static BigInteger[] ParsePublicSignalsStj(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("Failed to parse public signals JSON");

            var count = root.GetArrayLength();
            if (count > MaxPublicSignals)
                throw new FormatException("Public signals array is longer than " + MaxPublicSignals + " elements");

            var signals = new BigInteger[count];
            for (int i = 0; i < count; i++)
            {
                signals[i] = ParseSignal(root[i].GetString());
            }
            return signals;
        }
#else
        private static BigInteger[] ParsePublicSignalsNewtonsoft(string json)
        {
            var array = JArray.Parse(json);
            if (array.Count > MaxPublicSignals)
                throw new FormatException("Public signals array is longer than " + MaxPublicSignals + " elements");

            var signals = new BigInteger[array.Count];
            for (int i = 0; i < array.Count; i++)
            {
                signals[i] = ParseSignal((string)array[i]);
            }
            return signals;
        }
#endif

        private static BigInteger ParseSignal(string text)
        {
            return BigInteger.Parse(FieldElementText.RequireBoundedDecimal(text), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture);
        }

        public byte[] GetProofBytesOrFromJson()
        {
            if (ProofBytes.Length > 0) return ProofBytes;
            if (!string.IsNullOrEmpty(ProofJson)) return Encoding.UTF8.GetBytes(ProofJson);
            return new byte[0];
        }

        public byte[] GetPublicInputsBytesOrFromJson()
        {
            if (PublicInputsBytes.Length > 0) return PublicInputsBytes;
            if (!string.IsNullOrEmpty(PublicSignalsJson)) return Encoding.UTF8.GetBytes(PublicSignalsJson);
            return new byte[0];
        }
    }
}
