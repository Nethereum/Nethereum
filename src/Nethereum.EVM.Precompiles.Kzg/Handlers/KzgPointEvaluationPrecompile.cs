using System;
using System.Linq;
using Nethereum.EVM.Execution.Precompiles;
using Nethereum.Util;

namespace Nethereum.EVM.Precompiles.Kzg.Handlers
{
    /// <summary>
    /// Precompile 0x0a — KZG Point Evaluation (EIP-4844). Added in Cancun.
    /// Verifies a KZG proof that <c>p(z) = y</c> for the polynomial
    /// committed to by <c>commitment</c>, and that
    /// <c>versioned_hash == sha256(commitment)</c> with the KZG version tag.
    ///
    /// Input layout (exactly 192 bytes):
    ///   [0..32)     versioned_hash (byte 0 must be 0x01)
    ///   [32..64)    z  (field element)
    ///   [64..96)    y  (field element)
    ///   [96..144)   commitment (48 bytes)
    ///   [144..192)  proof      (48 bytes)
    ///
    /// Output (64 bytes): FIELD_ELEMENTS_PER_BLOB (32) || BLS_MODULUS (32).
    ///
    /// Backend injected at ctor time via <see cref="IKzgOperations"/>;
    /// production wires in <c>CkzgOperations</c> (ckzg-4844). Gas cost
    /// (50000 per EIP-4844) lives on the fork's
    /// <see cref="PrecompileGasCalculators"/>.
    /// </summary>
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
            if (!versionedHash.SequenceEqual(computedVersionedHash))
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
