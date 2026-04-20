using System;
using Nethereum.EVM.Precompiles.Kzg;
using Nethereum.Zisk.Core;

namespace Nethereum.EVM.Zisk.Backends
{
    /// <summary>
    /// Zisk / zkVM KZG point-evaluation backend for EIP-4844 precompile 0x0a
    /// (Cancun+). <c>VerifyKzgProof</c> wraps the native
    /// <c>ZiskCrypto.verify_kzg_proof_c</c> which issues the Zisk BLS12-381
    /// CSR sequence over libziskos.a. <c>ComputeVersionedHash</c> issues
    /// <c>ZiskCrypto.sha256_c</c> (CSR 0x805) over the 48-byte commitment
    /// and stamps byte 0 with the EIP-4844 KZG version tag (0x01).
    ///
    /// No trusted-setup loading happens guest-side — the Zisk prover binds
    /// the setup into its precompile circuit. <c>IsInitialized</c> always
    /// returns true.
    /// </summary>
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

            byte status = ZiskCrypto.verify_kzg_proof_c(z, y, commitment, proof);
            return status == 0;
        }

        public byte[] ComputeVersionedHash(byte[] commitment)
        {
            if (commitment == null || commitment.Length != CommitmentSize)
                throw new ArgumentException($"KZG: commitment must be {CommitmentSize} bytes");

            var hash = new byte[32];
            ZiskCrypto.sha256_c(commitment, (nuint)commitment.Length, hash);
            hash[0] = VersionedHashVersionKzg;
            return hash;
        }
    }
}
