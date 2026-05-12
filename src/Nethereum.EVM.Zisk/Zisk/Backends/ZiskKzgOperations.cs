using System;
using Nethereum.EVM.Precompiles.Kzg;
using Nethereum.Zisk.Core;

namespace Nethereum.EVM.Zisk.Backends
{
    public sealed class ZiskKzgOperations : IKzgOperations
    {
        public static readonly ZiskKzgOperations Instance = new ZiskKzgOperations();

        public const byte VersionedHashVersionKzg = 0x01;
        private const int CommitmentSize = 48;
        private const int FieldElementSize = 32;
        private const int ProofSize = 48;

        public bool IsInitialized => true;

        public bool VerifyKzgProof(byte[] commitment, byte[] z, byte[] y, byte[] proof)
        {
            if (commitment == null || commitment.Length != CommitmentSize)
                throw new ArgumentException($"KZG: commitment must be {CommitmentSize} bytes");
            if (z == null || z.Length != FieldElementSize)
                throw new ArgumentException($"KZG: z must be {FieldElementSize} bytes");
            if (y == null || y.Length != FieldElementSize)
                throw new ArgumentException($"KZG: y must be {FieldElementSize} bytes");
            if (proof == null || proof.Length != ProofSize)
                throw new ArgumentException($"KZG: proof must be {ProofSize} bytes");

            bool verified = false;
            uint status;
            unsafe { status = ZiskCrypto.zkvm_kzg_point_eval(commitment, z, y, proof, &verified); }
            return status == 0 && verified;
        }

        public byte[] BlobToKzgCommitment(byte[] blob)
        {
            throw new NotSupportedException("Blob KZG operations not available in zkVM guest");
        }

        public byte[] ComputeBlobKzgProof(byte[] blob, byte[] commitment)
        {
            throw new NotSupportedException("Blob KZG operations not available in zkVM guest");
        }

        public bool VerifyBlobKzgProof(byte[] blob, byte[] commitment, byte[] proof)
        {
            throw new NotSupportedException("Blob KZG operations not available in zkVM guest");
        }

        public byte[] ComputeVersionedHash(byte[] commitment)
        {
            if (commitment == null || commitment.Length != CommitmentSize)
                throw new ArgumentException($"KZG: commitment must be {CommitmentSize} bytes");

            var hash = new byte[32];
            uint shaStatus = ZiskCrypto.zkvm_sha256(commitment, (nuint)commitment.Length, hash);
            if (shaStatus != 0) throw new ArgumentException($"SHA256 for versioned hash failed (status {shaStatus})");
            hash[0] = VersionedHashVersionKzg;
            return hash;
        }
    }
}
