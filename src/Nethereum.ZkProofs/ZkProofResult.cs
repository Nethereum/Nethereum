using System;
using System.Numerics;
using System.Text;
#if NET6_0_OR_GREATER
using System.Text.Json;
#else
using Newtonsoft.Json;
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
            var strings = JsonSerializer.Deserialize<string[]>(json)
                ?? throw new InvalidOperationException("Failed to parse public signals JSON");

            var signals = new BigInteger[strings.Length];
            for (int i = 0; i < strings.Length; i++)
            {
                signals[i] = BigInteger.Parse(strings[i]);
            }
            return signals;
        }
#else
        private static BigInteger[] ParsePublicSignalsNewtonsoft(string json)
        {
            var strings = JsonConvert.DeserializeObject<string[]>(json);
            if (strings == null)
                throw new InvalidOperationException("Failed to parse public signals JSON");

            var signals = new BigInteger[strings.Length];
            for (int i = 0; i < strings.Length; i++)
            {
                signals[i] = BigInteger.Parse(strings[i]);
            }
            return signals;
        }
#endif

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
