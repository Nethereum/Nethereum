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
            var rejection = Rejection(header);
            if (rejection != null)
                throw new ArgumentException(rejection);
            return Compute(data, header);
        }

        public override bool TryExecute(byte[] input, out byte[] output)
        {
            var data = OrEmpty(input);
            var header = GasCalculators.ModExpHeaderParser.Parse(data);
            if (Rejection(header) != null)
            {
                output = null;
                return false;
            }
            output = Compute(data, header);
            return true;
        }

        private string Rejection(GasCalculators.ModExpHeader header)
        {
            if (EnforceEip7823Bounds &&
                (header.BaseLen > MaxEip7823Length ||
                 header.ExpLen > MaxEip7823Length ||
                 header.ModLen > MaxEip7823Length))
            {
                return "MODEXP length exceeded 1024 bytes";
            }

            if (header.ModLen.IsZero) return null;

            if (!header.BaseLen.FitsInInt || !header.ExpLen.FitsInInt || !header.ModLen.FitsInInt)
            {
                return $"MODEXP length too large: baseLen={header.BaseLen}, expLen={header.ExpLen}, modLen={header.ModLen}";
            }

            return null;
        }

        private byte[] Compute(byte[] data, GasCalculators.ModExpHeader header)
        {
            if (header.ModLen.IsZero) return new byte[0];

            var baseLen = header.BaseLen.ToInt();
            var expLen = header.ExpLen.ToInt();
            var modLen = header.ModLen.ToInt();

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
