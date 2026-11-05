using System;
using System.Threading;
using Nethereum.ZkProofsVerifier.Abstractions;
using Nethereum.ZkProofsVerifier.Circom;
using Nethereum.ZkProofsVerifier.Groth16;

namespace Nethereum.PrivacyPools
{
    public class PrivacyPoolProofVerifier
    {
        private readonly ParsedVerificationKey _withdrawalVk;
        private readonly ParsedVerificationKey? _ragequitVk;

        public PrivacyPoolProofVerifier(string withdrawalVkJson, string? ragequitVkJson = null)
        {
            if (withdrawalVkJson == null) throw new ArgumentNullException(nameof(withdrawalVkJson));
            _withdrawalVk = new ParsedVerificationKey(withdrawalVkJson);
            _ragequitVk = ragequitVkJson == null ? null : new ParsedVerificationKey(ragequitVkJson);
        }

        public ZkVerificationResult VerifyWithdrawalProof(string proofJson, string publicInputsJson)
        {
            return _withdrawalVk.Verify(proofJson, publicInputsJson);
        }

        public ZkVerificationResult VerifyWithdrawalProof(string proofJson, WithdrawProofSignals signals)
        {
            var publicInputsJson = SignalsToJson(signals.ToArray());
            return _withdrawalVk.Verify(proofJson, publicInputsJson);
        }

        public ZkVerificationResult VerifyRagequitProof(string proofJson, string publicInputsJson)
        {
            if (_ragequitVk == null)
                throw new InvalidOperationException("Ragequit verification key not provided");

            return _ragequitVk.Verify(proofJson, publicInputsJson);
        }

        public ZkVerificationResult VerifyRagequitProof(string proofJson, RagequitProofSignals signals)
        {
            if (_ragequitVk == null)
                throw new InvalidOperationException("Ragequit verification key not provided");

            var publicInputsJson = SignalsToJson(signals.ToArray());
            return _ragequitVk.Verify(proofJson, publicInputsJson);
        }

        private sealed class ParsedVerificationKey
        {
            private readonly string _json;
            private readonly Lazy<Groth16VerificationKey?> _key;

            public ParsedVerificationKey(string json)
            {
                _json = json;
                _key = new Lazy<Groth16VerificationKey?>(Parse, LazyThreadSafetyMode.ExecutionAndPublication);
            }

            public ZkVerificationResult Verify(string proofJson, string publicInputsJson)
            {
                var key = _key.Value;
                if (key == null)
                    return CircomGroth16Adapter.Verify(proofJson, _json, publicInputsJson);
                return CircomGroth16Adapter.Verify(proofJson, key, publicInputsJson);
            }

            private Groth16VerificationKey? Parse()
            {
                try
                {
                    return SnarkjsVerificationKeyParser.Parse(_json);
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        private static string SignalsToJson(System.Numerics.BigInteger[] signals)
        {
            var parts = new string[signals.Length];
            for (int i = 0; i < signals.Length; i++)
            {
                parts[i] = "\"" + signals[i].ToString() + "\"";
            }
            return "[" + string.Join(",", parts) + "]";
        }
    }
}
