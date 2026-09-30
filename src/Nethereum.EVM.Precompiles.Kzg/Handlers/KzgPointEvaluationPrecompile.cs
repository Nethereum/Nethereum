using System;
using Nethereum.EVM.Execution.Precompiles;
using Nethereum.Util;

namespace Nethereum.EVM.Precompiles.Kzg.Handlers
{
    public sealed class KzgPointEvaluationPrecompile : PrecompileHandlerBase
    {
        public const int InputSize = 192;
        public const int VersionedHashSize = 32;
        public const int FieldElementSize = 32;
        public const int CommitmentSize = 48;
        public const int ProofSize = 48;
        public const byte VersionedHashVersionKzg = 0x01;

        private static readonly byte[] FieldElementsPerBlob = new byte[]
        {
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x10, 0x00
        };

        private static readonly byte[] BlsModulus = new byte[]
        {
            0x73, 0xed, 0xa7, 0x53, 0x29, 0x9d, 0x7d, 0x48,
            0x33, 0x39, 0xd8, 0x08, 0x09, 0xa1, 0xd8, 0x05,
            0x53, 0xbd, 0xa4, 0x02, 0xff, 0xfe, 0x5b, 0xfe,
            0xff, 0xff, 0xff, 0xff, 0x00, 0x00, 0x00, 0x01
        };

        private readonly IKzgOperations _ops;

        public KzgPointEvaluationPrecompile(IKzgOperations ops)
        {
            _ops = ops ?? throw new ArgumentNullException(nameof(ops));
        }

        public override int AddressNumeric => 0x0a;

        public override byte[] Execute(byte[] input)
        {
            RequireInputLength(input, InputSize, "KZG point evaluation");

            var versionedHash = input.Slice(0, VersionedHashSize);
            if (versionedHash[0] != VersionedHashVersionKzg)
                throw new ArgumentException(
                    $"Invalid KZG versioned hash version: expected 0x{VersionedHashVersionKzg:x2}, got 0x{versionedHash[0]:x2}");

            var z = input.Slice(32, 64);
            var y = input.Slice(64, 96);
            var commitment = input.Slice(96, 144);
            var proof = input.Slice(144, 192);

            var computedVersionedHash = _ops.ComputeVersionedHash(commitment);
            if (!ByteUtil.AreEqual(versionedHash, computedVersionedHash))
                throw new ArgumentException("KZG versioned hash mismatch");

            if (!_ops.VerifyKzgProof(commitment, z, y, proof))
                throw new ArgumentException("KZG proof verification failed");

            var result = new byte[64];
            Array.Copy(FieldElementsPerBlob, 0, result, 0, 32);
            Array.Copy(BlsModulus, 0, result, 32, 32);
            return result;
        }
    }
}
