using System;
using Nethereum.EVM.Execution.Precompiles;

namespace Nethereum.EVM
{
    public static class HardforkConfigExtensions
    {
        public static HardforkConfig WithPrecompiles(
            this HardforkConfig config,
            PrecompileRegistry registry)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            var clone = config.Clone();
            clone.Precompiles = registry;
            return clone;
        }
    }
}
