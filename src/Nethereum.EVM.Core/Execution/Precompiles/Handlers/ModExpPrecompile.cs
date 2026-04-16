using System;
using Nethereum.EVM.Execution.Precompiles.CryptoBackends;
using Nethereum.Util;

namespace Nethereum.EVM.Execution.Precompiles.Handlers
{
    /// <summary>
    /// Precompile 0x05 — MODEXP (EIP-198). Computes <c>base^exp mod mod</c>.
    /// Available from Byzantium onwards. Gas cost is defined by the fork's
    /// <see cref="PrecompileGasCalculators"/>:
    /// Byzantium (EIP-198), Berlin (EIP-2565), Osaka (EIP-7883).
    ///
    /// Input layout (big-endian):
    ///   [0..32)    baseLen (uint256)
    ///   [32..64)   expLen  (uint256)
    ///   [64..96)   modLen  (uint256)
    ///   [96..96+baseLen)                         base
    ///   [96+baseLen..96+baseLen+expLen)          exp
    ///   [96+baseLen+expLen..96+baseLen+expLen+modLen)  mod
    ///
    /// EIP-7823 (Osaka) caps each length at 1024 bytes; when
    /// <see cref="EnforceEip7823Bounds"/> is set, oversized operands throw.
    /// The actual <c>base^exp mod modulus</c> primitive is provided by an
    /// <see cref="IModExpBackend"/>; production uses
    /// <c>System.Numerics.BigInteger.ModPow</c> (in
    /// <c>Nethereum.EVM</c>, kept off the Core hot path) and Zisk uses a
    /// witness-backed variant. Length parsing and bounds checks run on
    /// <see cref="EvmUInt256"/> so no <c>BigInteger</c> is referenced
    /// from Core.
    /// </summary>
    public sealed class ModExpPrecompile : PrecompileHandlerBase
    {
        private readonly IModExpBackend _backend;

        public override int AddressNumeric => 5;

        /// <summary>
        /// When true, reject operands longer than 1024 bytes (EIP-7823).
        /// Set by the Osaka spec at construction.
        /// </summary>
        public bool EnforceEip7823Bounds { get; }

        // EIP-7823 bound as an EvmUInt256 so comparisons stay off BigInteger.
        private static readonly EvmUInt256 MaxEip7823Length = new EvmUInt256(1024UL);

        public ModExpPrecompile(IModExpBackend backend, bool enforceEip7823Bounds = false)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            EnforceEip7823Bounds = enforceEip7823Bounds;
        }

        public override byte[] Execute(byte[] input)
        {
            var data = OrEmpty(input);

            var baseLenU256 = ReadHeaderField(data, 0);
            var expLenU256 = ReadHeaderField(data, 32);
            var modLenU256 = ReadHeaderField(data, 64);

            if (EnforceEip7823Bounds &&
                (baseLenU256 > MaxEip7823Length ||
                 expLenU256 > MaxEip7823Length ||
                 modLenU256 > MaxEip7823Length))
            {
                throw new ArgumentException("MODEXP length exceeded 1024 bytes");
            }

            // EIP-198: modLen == 0 returns empty.
            if (modLenU256.IsZero) return new byte[0];

            if (!baseLenU256.FitsInInt || !expLenU256.FitsInInt || !modLenU256.FitsInInt)
            {
                throw new ArgumentException(
                    $"MODEXP length too large: baseLen={baseLenU256}, expLen={expLenU256}, modLen={modLenU256}");
            }

            var baseLen = baseLenU256.ToInt();
            var expLen = expLenU256.ToInt();
            var modLen = modLenU256.ToInt();

            int offset = 96;
            var baseBytes = ReadBigEndianOperand(data, offset, baseLen); offset += baseLen;
            var expBytes = ReadBigEndianOperand(data, offset, expLen); offset += expLen;
            var modBytes = ReadBigEndianOperand(data, offset, modLen);

            // Short-circuit when modulus is zero (matches legacy behaviour).
            bool modIsZero = true;
            for (int i = 0; i < modBytes.Length; i++)
            {
                if (modBytes[i] != 0) { modIsZero = false; break; }
            }
            if (modIsZero) return new byte[modLen];

            return _backend.ModExp(baseBytes, expBytes, modBytes);
        }

        private static EvmUInt256 ReadHeaderField(byte[] data, int offset)
        {
            // A header slot is a 32-byte big-endian uint256. The input may
            // be shorter than 96 bytes, in which case any missing bytes
            // are treated as zero-padded on the right (matching Geth's
            // getData semantics).
            var buf = new byte[32];
            if (offset < data.Length)
            {
                var available = Math.Min(32, data.Length - offset);
                Array.Copy(data, offset, buf, 0, available);
            }
            return EvmUInt256.FromBigEndian(buf);
        }

        private static byte[] ReadBigEndianOperand(byte[] data, int offset, int length)
        {
            var result = new byte[length];
            if (length == 0 || offset >= data.Length) return result;
            var available = Math.Min(length, data.Length - offset);
            Array.Copy(data, offset, result, 0, available);
            return result;
        }
    }
}
