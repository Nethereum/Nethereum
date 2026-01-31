using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.EVM.Execution
{
    public class BuiltInPrecompileProvider : IPrecompileProvider
    {
        private readonly HashSet<string> _addresses;
        private readonly EvmPreCompiledContractsExecution _execution;

        public static BuiltInPrecompileProvider Cancun() => new(1, 10);
        public static BuiltInPrecompileProvider Prague() => new(1, 17);

        public BuiltInPrecompileProvider(int start, int end)
        {
            _addresses = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            for (int i = start; i <= end; i++)
                _addresses.Add(FormatAddress(i));
            _execution = new EvmPreCompiledContractsExecution();
        }

        public IEnumerable<string> GetHandledAddresses() => _addresses;

        public bool CanHandle(string address) => _addresses.Contains(NormalizeAddress(address));

        public BigInteger GetGasCost(string address, byte[] data) => _execution.GetPrecompileGasCost(address, data);

        public byte[] Execute(string address, byte[] data) => _execution.ExecutePreCompile(address, data);

        private static string FormatAddress(int i) => "0x" + i.ToString("x").PadLeft(40, '0');

        private static string NormalizeAddress(string address)
        {
            var compact = address.ToHexCompact().ToLowerInvariant();
            return "0x" + compact.PadLeft(40, '0');
        }
    }
}
