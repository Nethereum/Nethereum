using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Nethereum.EVM.Execution
{
    public class CompositePrecompileProvider : IPrecompileProvider
    {
        private readonly IPrecompileProvider[] _providers;

        public CompositePrecompileProvider(params IPrecompileProvider[] providers)
        {
            _providers = providers ?? new IPrecompileProvider[0];
        }

        public IEnumerable<string> GetHandledAddresses()
        {
            return _providers.SelectMany(p => p.GetHandledAddresses()).Distinct();
        }

        public bool CanHandle(string address)
        {
            return _providers.Any(p => p.CanHandle(address));
        }

        public BigInteger GetGasCost(string address, byte[] data)
        {
            var provider = _providers.FirstOrDefault(p => p.CanHandle(address));
            return provider?.GetGasCost(address, data) ?? 0;
        }

        public byte[] Execute(string address, byte[] data)
        {
            var provider = _providers.FirstOrDefault(p => p.CanHandle(address));
            return provider?.Execute(address, data);
        }
    }
}
