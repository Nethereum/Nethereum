using System;
using System.Text;

namespace Nethereum.ZkProofs.RapidSnark
{
    public static class RapidSnarkVerifier
    {
        private const int DefaultErrorBufferSize = 4096;

        public static bool Verify(string proofJson, string publicSignalsJson, string verificationKeyJson)
        {
            if (string.IsNullOrEmpty(proofJson))
                throw new ArgumentException("Proof JSON must not be empty.", nameof(proofJson));
            if (string.IsNullOrEmpty(publicSignalsJson))
                throw new ArgumentException("Public signals JSON must not be empty.", nameof(publicSignalsJson));
            if (string.IsNullOrEmpty(verificationKeyJson))
                throw new ArgumentException("Verification key JSON must not be empty.", nameof(verificationKeyJson));

            var errorMsg = new byte[DefaultErrorBufferSize];

            var result = RapidSnarkBindings.groth16_verify(
                proofJson, publicSignalsJson, verificationKeyJson,
                errorMsg, (ulong)errorMsg.Length);

            if (result == RapidSnarkBindings.RAPIDSNARK_OK)
                return true;

            var errorStr = GetErrorString(errorMsg);
            if (!string.IsNullOrEmpty(errorStr))
                throw new RapidSnarkException($"Verification error (code {result}): {errorStr}");

            return false;
        }

        private static string GetErrorString(byte[] errorBuffer)
        {
            var nullIndex = Array.IndexOf(errorBuffer, (byte)0);
            if (nullIndex < 0) nullIndex = errorBuffer.Length;
            return Encoding.UTF8.GetString(errorBuffer, 0, nullIndex);
        }
    }
}
