using System;
using Nethereum.EVM.Execution.Precompiles.CryptoBackends;
using Nethereum.Util;

namespace Nethereum.EVM.Execution.Precompiles.Handlers
{
    /// <summary>
    /// Precompile 0x01 — ECRECOVER. Recovers the Ethereum address that
    /// signed a message hash using secp256k1. Available from Frontier.
    ///
    /// Input layout (big-endian, padded to 128 bytes):
    ///   [0..32)     hash
    ///   [32..64)    v  (only low byte used; bytes 32..63 must be zero)
    ///   [64..96)    r
    ///   [96..128)   s
    ///
    /// Output: 32-byte right-padded 20-byte address, or empty on any
    /// validation / recovery failure (matching the consensus behaviour
    /// of returning empty data for an invalid signature).
    ///
    /// The secp256k1 recovery primitive itself is provided by an
    /// <see cref="IEcRecoverBackend"/>; production uses
    /// <c>Nethereum.Signer.EthECKey</c> and Zisk uses a witness-backed
    /// variant. All bounds checking is performed with
    /// <see cref="EvmUInt256"/> to keep <c>System.Numerics.BigInteger</c>
    /// off the hot path.
    /// </summary>
    public sealed class EcRecoverPrecompile : PrecompileHandlerBase
    {
        private readonly IEcRecoverBackend _backend;

        public EcRecoverPrecompile(IEcRecoverBackend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        public override int AddressNumeric => 1;

        // secp256k1 curve order N:
        //   0xfffffffffffffffffffffffffffffffebaaedce6af48a03bbfd25e8cd0364141
        // Constructed from 64-bit limbs (u3 = most significant, u0 = least).
        private static readonly EvmUInt256 Secp256K1N = new EvmUInt256(
            0xFFFFFFFFFFFFFFFFUL,
            0xFFFFFFFFFFFFFFFEUL,
            0xBAAEDCE6AF48A03BUL,
            0xBFD25E8CD0364141UL);

        public override byte[] Execute(byte[] input)
        {
            // Pad short input up to 128 bytes with zeros — matches consensus.
            byte[] data;
            if (input == null || input.Length == 0)
            {
                data = new byte[128];
            }
            else if (input.Length < 128)
            {
                data = new byte[128];
                Array.Copy(input, 0, data, 0, input.Length);
            }
            else
            {
                data = input;
            }

            var hash = data.Slice(0, 32);

            // v must fit in a single byte; the upper 31 bytes of slot [32..64) must be zero.
            for (int i = 32; i < 63; i++)
            {
                if (data[i] != 0) return new byte[0];
            }

            var v = data[63];
            var r = data.Slice(64, 96);
            var s = data.Slice(96, 128);

            // 0 < r < N and 0 < s < N (not the canonical EIP-2 check — the
            // ECRECOVER precompile predates EIP-2 and accepts s in the upper
            // half; only rejects out-of-bounds values).
            var rU256 = EvmUInt256.FromBigEndian(r);
            var sU256 = EvmUInt256.FromBigEndian(s);
            if (rU256.IsZero || rU256 >= Secp256K1N) return new byte[0];
            if (sU256.IsZero || sU256 >= Secp256K1N) return new byte[0];

            byte[] recoveredAddress;
            try
            {
                recoveredAddress = _backend.Recover(hash, v, r, s);
            }
            catch
            {
                return new byte[0];
            }

            if (recoveredAddress == null || recoveredAddress.Length == 0)
                return new byte[0];

            return recoveredAddress.PadTo32Bytes();
        }
    }
}
