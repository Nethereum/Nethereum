using System;
using Nethereum.EVM.Execution.Precompiles.CryptoBackends;
using Nethereum.Util;

namespace Nethereum.EVM.Execution.Precompiles.Handlers
{
    public sealed class EcRecoverPrecompile : PrecompileHandlerBase
    {
        private readonly IEcRecoverBackend _backend;

        public EcRecoverPrecompile(IEcRecoverBackend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        public override int AddressNumeric => 1;

        private static readonly EvmUInt256 Secp256K1N = new EvmUInt256(
            0xFFFFFFFFFFFFFFFFUL,
            0xFFFFFFFFFFFFFFFEUL,
            0xBAAEDCE6AF48A03BUL,
            0xBFD25E8CD0364141UL);

        public override byte[] Execute(byte[] input)
        {
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

            for (int i = 32; i < 63; i++)
            {
                if (data[i] != 0) return new byte[0];
            }

            var v = data[63];
            if (v != 27 && v != 28) return new byte[0];

            var r = data.Slice(64, 96);
            var s = data.Slice(96, 128);

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
