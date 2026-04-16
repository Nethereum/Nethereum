using System;
using Nethereum.EVM.Execution.Precompiles.CryptoBackends;

namespace Nethereum.EVM.Execution.Precompiles.Handlers
{
    /// <summary>
    /// Precompile 0x100 — P256VERIFY (EIP-7951). Verifies a secp256r1
    /// (P-256 / NIST P-256) ECDSA signature. Added in Osaka.
    ///
    /// Input layout (exactly 160 bytes):
    ///   [0..32)     message hash
    ///   [32..64)    r
    ///   [64..96)    s
    ///   [96..128)   public key X
    ///   [128..160)  public key Y
    ///
    /// Output: 32-byte value <c>0x00..01</c> when the signature verifies;
    /// empty bytes on any failure (invalid length, point not on curve,
    /// signature mismatch). Gas cost is a fixed 6900 defined by the fork's
    /// <see cref="PrecompileGasCalculators"/>.
    ///
    /// The verification primitive is provided by an
    /// <see cref="IP256VerifyBackend"/>.
    /// </summary>
    public sealed class P256VerifyPrecompile : PrecompileHandlerBase
    {
        private readonly IP256VerifyBackend _backend;

        public P256VerifyPrecompile(IP256VerifyBackend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        public override int AddressNumeric => 0x100;

        private const int InputLength = 160;

        public override byte[] Execute(byte[] input)
        {
            if (input == null || input.Length != InputLength)
                return new byte[0];

            try
            {
                var hash = new byte[32];
                var r = new byte[32];
                var s = new byte[32];
                var x = new byte[32];
                var y = new byte[32];
                Array.Copy(input, 0, hash, 0, 32);
                Array.Copy(input, 32, r, 0, 32);
                Array.Copy(input, 64, s, 0, 32);
                Array.Copy(input, 96, x, 0, 32);
                Array.Copy(input, 128, y, 0, 32);

                if (_backend.Verify(hash, r, s, x, y))
                {
                    var result = new byte[32];
                    result[31] = 1;
                    return result;
                }

                return new byte[0];
            }
            catch
            {
                return new byte[0];
            }
        }
    }
}
