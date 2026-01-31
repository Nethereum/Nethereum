using System.Linq;
using Nethereum.EVM.Execution;

namespace Nethereum.EVM
{
    public static class HardforkConfigExtensions
    {
        public static HardforkConfig WithPrecompileProviders(
            this HardforkConfig config,
            params IPrecompileProvider[] additionalProviders)
        {
            if (additionalProviders == null || additionalProviders.Length == 0)
                return config;

            var allProviders = additionalProviders
                .Concat(new[] { config.PrecompileProvider })
                .ToArray();

            return new HardforkConfig
            {
                EnableEIP4844 = config.EnableEIP4844,
                EnableEIP7623 = config.EnableEIP7623,
                MaxBlobsPerBlock = config.MaxBlobsPerBlock,
                PrecompileProvider = new CompositePrecompileProvider(allProviders)
            };
        }
    }
}
