using System;
using Nethereum.ZkProofsVerifier.Abstractions;
using Nethereum.ZkProofsVerifier.Circom;

namespace Nethereum.PrivacyPools
{
    public class PrivacyPoolProofVerifier
    {
        private readonly string _withdrawalVkJson;
        private readonly string? _ragequitVkJson;

        public PrivacyPoolProofVerifier(string withdrawalVkJson, string? ragequitVkJson = null)
        {
            _withdrawalVkJson = withdrawalVkJson ?? throw new ArgumentNullException(nameof(withdrawalVkJson));
            _ragequitVkJson = ragequitVkJson;
        }

        public ZkVerificationResult VerifyWithdrawalProof(string proofJson, string publicInputsJson)
        {
            return CircomGroth16Adapter.Verify(proofJson, _withdrawalVkJson, publicInputsJson);
        }

        public ZkVerificationResult VerifyWithdrawalProof(string proofJson, WithdrawProofSignals signals)
        {
            var publicInputsJson = SignalsToJson(signals.ToArray());
            return CircomGroth16Adapter.Verify(proofJson, _withdrawalVkJson, publicInputsJson);
        }

        public ZkVerificationResult VerifyRagequitProof(string proofJson, string publicInputsJson)
        {
            if (_ragequitVkJson == null)
                throw new InvalidOperationException("Ragequit verification key not provided");

            return CircomGroth16Adapter.Verify(proofJson, _ragequitVkJson, publicInputsJson);
        }

        public ZkVerificationResult VerifyRagequitProof(string proofJson, RagequitProofSignals signals)
        {
            if (_ragequitVkJson == null)
                throw new InvalidOperationException("Ragequit verification key not provided");

            var publicInputsJson = SignalsToJson(signals.ToArray());
            return CircomGroth16Adapter.Verify(proofJson, _ragequitVkJson, publicInputsJson);
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
