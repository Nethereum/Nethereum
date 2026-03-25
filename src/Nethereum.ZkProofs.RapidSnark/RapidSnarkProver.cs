using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Nethereum.ZkProofs.RapidSnark
{
    public class RapidSnarkProver : IDisposable
    {
        private const int DefaultErrorBufferSize = 4096;
        private IntPtr _proverHandle;
        private bool _disposed;

        public RapidSnarkProver()
        {
            _proverHandle = IntPtr.Zero;
        }

        public void LoadZkey(byte[] zkeyBytes)
        {
            if (zkeyBytes == null || zkeyBytes.Length == 0)
                throw new ArgumentException("Zkey bytes must not be empty.", nameof(zkeyBytes));

            Dispose();
            _disposed = false;

            var errorMsg = new byte[DefaultErrorBufferSize];
            var result = RapidSnarkBindings.groth16_prover_create(
                out _proverHandle,
                zkeyBytes, (ulong)zkeyBytes.Length,
                errorMsg, (ulong)errorMsg.Length);

            if (result != RapidSnarkBindings.RAPIDSNARK_OK)
            {
                _proverHandle = IntPtr.Zero;
                throw new RapidSnarkException(
                    $"Failed to create prover (code {result}): {GetErrorString(errorMsg)}");
            }
        }

        public (string proofJson, string publicSignalsJson) Prove(byte[] zkeyBytes, byte[] witnessBytes)
        {
            if (zkeyBytes == null || zkeyBytes.Length == 0)
                throw new ArgumentException("Zkey bytes must not be empty.", nameof(zkeyBytes));
            if (witnessBytes == null || witnessBytes.Length == 0)
                throw new ArgumentException("Witness bytes must not be empty.", nameof(witnessBytes));

            ulong proofSize = 0;
            RapidSnarkBindings.groth16_proof_size(ref proofSize);
            if (proofSize == 0) proofSize = 4096;

            ulong publicSize = 0;
            var sizeErrorMsg = new byte[DefaultErrorBufferSize];
            RapidSnarkBindings.groth16_public_size_for_zkey_buf(
                zkeyBytes, (ulong)zkeyBytes.Length,
                ref publicSize,
                sizeErrorMsg, (ulong)sizeErrorMsg.Length);
            if (publicSize == 0) publicSize = 16384;

            var proofBuffer = new byte[proofSize];
            var publicBuffer = new byte[publicSize];
            var errorMsg = new byte[DefaultErrorBufferSize];

            var result = RapidSnarkBindings.groth16_prover(
                zkeyBytes, (ulong)zkeyBytes.Length,
                witnessBytes, (ulong)witnessBytes.Length,
                proofBuffer, ref proofSize,
                publicBuffer, ref publicSize,
                errorMsg, (ulong)errorMsg.Length);

            if (result == RapidSnarkBindings.RAPIDSNARK_ERROR_SHORT_BUFFER)
            {
                proofBuffer = new byte[proofSize];
                publicBuffer = new byte[publicSize];

                result = RapidSnarkBindings.groth16_prover(
                    zkeyBytes, (ulong)zkeyBytes.Length,
                    witnessBytes, (ulong)witnessBytes.Length,
                    proofBuffer, ref proofSize,
                    publicBuffer, ref publicSize,
                    errorMsg, (ulong)errorMsg.Length);
            }

            if (result != RapidSnarkBindings.RAPIDSNARK_OK)
            {
                throw new RapidSnarkException(
                    $"Proof generation failed (code {result}): {GetErrorString(errorMsg)}");
            }

            var proofJson = Encoding.UTF8.GetString(proofBuffer, 0, (int)proofSize).TrimEnd('\0');
            var publicJson = Encoding.UTF8.GetString(publicBuffer, 0, (int)publicSize).TrimEnd('\0');
            return (proofJson, publicJson);
        }

        public (string proofJson, string publicSignalsJson) ProveWithLoadedZkey(byte[] witnessBytes)
        {
            if (_proverHandle == IntPtr.Zero)
                throw new InvalidOperationException("No zkey loaded. Call LoadZkey first.");
            if (witnessBytes == null || witnessBytes.Length == 0)
                throw new ArgumentException("Witness bytes must not be empty.", nameof(witnessBytes));

            ulong proofSize = 4096;
            ulong publicSize = 16384;

            var proofBuffer = new byte[proofSize];
            var publicBuffer = new byte[publicSize];
            var errorMsg = new byte[DefaultErrorBufferSize];

            var result = RapidSnarkBindings.groth16_prover_prove(
                _proverHandle,
                witnessBytes, (ulong)witnessBytes.Length,
                proofBuffer, ref proofSize,
                publicBuffer, ref publicSize,
                errorMsg, (ulong)errorMsg.Length);

            if (result == RapidSnarkBindings.RAPIDSNARK_ERROR_SHORT_BUFFER)
            {
                proofBuffer = new byte[proofSize];
                publicBuffer = new byte[publicSize];

                result = RapidSnarkBindings.groth16_prover_prove(
                    _proverHandle,
                    witnessBytes, (ulong)witnessBytes.Length,
                    proofBuffer, ref proofSize,
                    publicBuffer, ref publicSize,
                    errorMsg, (ulong)errorMsg.Length);
            }

            if (result != RapidSnarkBindings.RAPIDSNARK_OK)
            {
                throw new RapidSnarkException(
                    $"Proof generation failed (code {result}): {GetErrorString(errorMsg)}");
            }

            var proofJson = Encoding.UTF8.GetString(proofBuffer, 0, (int)proofSize).TrimEnd('\0');
            var publicJson = Encoding.UTF8.GetString(publicBuffer, 0, (int)publicSize).TrimEnd('\0');
            return (proofJson, publicJson);
        }

        private static string GetErrorString(byte[] errorBuffer)
        {
            var nullIndex = Array.IndexOf(errorBuffer, (byte)0);
            if (nullIndex < 0) nullIndex = errorBuffer.Length;
            return Encoding.UTF8.GetString(errorBuffer, 0, nullIndex);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_proverHandle != IntPtr.Zero)
            {
                RapidSnarkBindings.groth16_prover_destroy(_proverHandle);
                _proverHandle = IntPtr.Zero;
            }
        }
    }
}
