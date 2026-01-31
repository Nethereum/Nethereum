using System;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.EVM.Execution;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.EVM.Precompiles.Kzg
{
    public class KzgPrecompileProvider : IPrecompileProvider
    {
        private readonly IKzgOperations _kzgOperations;
        private static readonly string[] _addresses = new[] { "0x000000000000000000000000000000000000000a" };

        public const string KZG_POINT_EVALUATION_ADDRESS = "a";

        public IEnumerable<string> GetHandledAddresses() => _addresses;

        public const int KZG_GAS_COST = 50000;

        public const int INPUT_SIZE = 192;
        public const int VERSIONED_HASH_SIZE = 32;
        public const int FIELD_ELEMENT_SIZE = 32;
        public const int COMMITMENT_SIZE = 48;
        public const int PROOF_SIZE = 48;

        public const byte VERSIONED_HASH_VERSION_KZG = 0x01;

        public static readonly byte[] FIELD_ELEMENTS_PER_BLOB = new byte[32]
        {
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x10, 0x00
        };

        public static readonly byte[] BLS_MODULUS = new byte[32]
        {
            0x73, 0xed, 0xa7, 0x53, 0x29, 0x9d, 0x7d, 0x48,
            0x33, 0x39, 0xd8, 0x08, 0x09, 0xa1, 0xd8, 0x05,
            0x53, 0xbd, 0xa4, 0x02, 0xff, 0xfe, 0x5b, 0xfe,
            0xff, 0xff, 0xff, 0xff, 0x00, 0x00, 0x00, 0x01
        };

        public KzgPrecompileProvider(IKzgOperations kzgOperations)
        {
            _kzgOperations = kzgOperations ?? throw new ArgumentNullException(nameof(kzgOperations));
        }

        public bool CanHandle(string address)
        {
            var compact = address.ToHexCompact().ToLowerInvariant();
            return compact == KZG_POINT_EVALUATION_ADDRESS;
        }

        public BigInteger GetGasCost(string address, byte[] data)
        {
            var compact = address.ToHexCompact().ToLowerInvariant();
            if (compact == KZG_POINT_EVALUATION_ADDRESS)
                return KZG_GAS_COST;
            return 0;
        }

        public byte[] Execute(string address, byte[] data)
        {
            var compact = address.ToHexCompact().ToLowerInvariant();
            if (compact != KZG_POINT_EVALUATION_ADDRESS)
                throw new ArgumentException($"Unknown KZG precompile address: {address}");

            return KzgPointEvaluation(data);
        }

        private byte[] KzgPointEvaluation(byte[] data)
        {
            if (data == null || data.Length != INPUT_SIZE)
                throw new ArgumentException($"Invalid KZG point evaluation input: expected {INPUT_SIZE} bytes, got {data?.Length ?? 0}");

            var versionedHash = new byte[VERSIONED_HASH_SIZE];
            Array.Copy(data, 0, versionedHash, 0, VERSIONED_HASH_SIZE);

            if (versionedHash[0] != VERSIONED_HASH_VERSION_KZG)
                throw new ArgumentException($"Invalid KZG versioned hash version: expected {VERSIONED_HASH_VERSION_KZG}, got {versionedHash[0]}");

            var z = new byte[FIELD_ELEMENT_SIZE];
            Array.Copy(data, 32, z, 0, FIELD_ELEMENT_SIZE);

            var y = new byte[FIELD_ELEMENT_SIZE];
            Array.Copy(data, 64, y, 0, FIELD_ELEMENT_SIZE);

            var commitment = new byte[COMMITMENT_SIZE];
            Array.Copy(data, 96, commitment, 0, COMMITMENT_SIZE);

            var proof = new byte[PROOF_SIZE];
            Array.Copy(data, 144, proof, 0, PROOF_SIZE);

            var computedVersionedHash = _kzgOperations.ComputeVersionedHash(commitment);
            if (!ByteArraysEqual(versionedHash, computedVersionedHash))
                throw new ArgumentException("KZG versioned hash mismatch");

            bool valid = _kzgOperations.VerifyKzgProof(commitment, z, y, proof);
            if (!valid)
                throw new ArgumentException("KZG proof verification failed");

            var result = new byte[64];
            Array.Copy(FIELD_ELEMENTS_PER_BLOB, 0, result, 0, 32);
            Array.Copy(BLS_MODULUS, 0, result, 32, 32);
            return result;
        }

        private static bool ByteArraysEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
                return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                    return false;
            }
            return true;
        }
    }
}
