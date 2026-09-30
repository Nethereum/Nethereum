using System;
using Nethereum.EVM.Execution.Precompiles.CryptoBackends;
using Nethereum.Util;

namespace Nethereum.EVM.Execution.Precompiles.Handlers
{
    public sealed class ModExpPrecompile : PrecompileHandlerBase
    {
        private readonly IModExpBackend _backend;

        public override int AddressNumeric => 5;

        public bool EnforceEip7823Bounds { get; }

        private static readonly EvmUInt256 MaxEip7823Length = new EvmUInt256(1024UL);

        public ModExpPrecompile(IModExpBackend backend, bool enforceEip7823Bounds = false)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            EnforceEip7823Bounds = enforceEip7823Bounds;
        }

        public override byte[] Execute(byte[] input)
        {
            var data = OrEmpty(input);

            var header = GasCalculators.ModExpHeaderParser.Parse(data);
            var baseLenU256 = header.BaseLen;
            var expLenU256 = header.ExpLen;
            var modLenU256 = header.ModLen;

            if (EnforceEip7823Bounds &&
                (baseLenU256 > MaxEip7823Length ||
                 expLenU256 > MaxEip7823Length ||
                 modLenU256 > MaxEip7823Length))
            {
                throw new ArgumentException("MODEXP length exceeded 1024 bytes");
            }

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

            bool modIsZero = true;
            for (int i = 0; i < modBytes.Length; i++)
            {
                if (modBytes[i] != 0) { modIsZero = false; break; }
            }
            if (modIsZero) return new byte[modLen];

            return _backend.ModExp(baseBytes, expBytes, modBytes);
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
