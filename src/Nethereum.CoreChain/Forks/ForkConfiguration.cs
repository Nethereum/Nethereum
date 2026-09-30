using System.Collections.Generic;
using System.Linq;
using Nethereum.EVM;
using Nethereum.EVM.Precompiles;

namespace Nethereum.CoreChain.Forks
{
    /// <summary>
    /// A fork's observable configuration by EIP-7910 canonical name: the precompiles it
    /// dispatches and the system contracts a chain running it carries. Built from the same
    /// HardforkConfig the engine executes against and the same predeploys genesis provisions,
    /// so it cannot drift from what the chain does. Serve it from eth_config or hand it to a
    /// simulator as a ready preset - it is one projection, not two.
    /// </summary>
    public sealed class ForkConfiguration
    {
        private ForkConfiguration(
            IReadOnlyDictionary<string, string> precompiles,
            IReadOnlyDictionary<string, string> systemContracts)
        {
            Precompiles = precompiles;
            SystemContracts = systemContracts;
        }

        public IReadOnlyDictionary<string, string> Precompiles { get; }

        public IReadOnlyDictionary<string, string> SystemContracts { get; }

        public static ForkConfiguration For(HardforkName fork, HardforkConfig config, string depositContractAddress = null)
        {
            var precompiles = new Dictionary<string, string>();
            foreach (var address in config.Precompiles.GetWiredAddresses())
                if (PrecompileNames.TryGetName(address, out var name))
                    precompiles[name] = PrecompileNames.AddressHex(address);

            var systemContracts = new Dictionary<string, string>();
            foreach (var predeploy in SystemContractPredeploys.For(fork))
                if (!string.IsNullOrEmpty(predeploy.Name))
                    systemContracts[predeploy.Name] = predeploy.Address;

            if (fork >= HardforkName.Prague && depositContractAddress != null)
                systemContracts["DEPOSIT_CONTRACT_ADDRESS"] = depositContractAddress;

            var orderedSystemContracts = systemContracts
                .OrderBy(entry => entry.Key, System.StringComparer.Ordinal)
                .ToDictionary(entry => entry.Key, entry => entry.Value);

            return new ForkConfiguration(precompiles, orderedSystemContracts);
        }
    }
}
