using System.Collections.Generic;

namespace Nethereum.EVM
{
    public sealed class HardforkRegistry
    {
        private readonly Dictionary<HardforkName, HardforkConfig> _entries
            = new Dictionary<HardforkName, HardforkConfig>();

        public void Register(HardforkName name, HardforkConfig config)
        {
            if (name == HardforkName.Unspecified)
                throw new System.ArgumentException(
                    "Cannot register HardforkName.Unspecified.", nameof(name));
            if (config is null)
                throw new System.ArgumentNullException(nameof(config));
            _entries[name] = config;
        }

        public bool Remove(HardforkName name) => _entries.Remove(name);

        public bool Contains(HardforkName name) => _entries.ContainsKey(name);

        public bool TryGet(HardforkName name, out HardforkConfig config) => _entries.TryGetValue(name, out config);

        public HardforkConfig Get(HardforkName name)
        {
            if (name == HardforkName.Unspecified)
                throw new System.InvalidOperationException(
                    "HardforkName.Unspecified passed to HardforkRegistry.Get — the witness producer " +
                    "did not stamp a fork on BlockWitnessData.Features.Fork. This is a producer bug.");
            if (!_entries.TryGetValue(name, out var config))
                throw new System.InvalidOperationException(
                    $"HardforkName.{name} is not registered in this HardforkRegistry. " +
                    "Register it or use a registry that includes this fork.");
            return config;
        }

        public IEnumerable<HardforkName> RegisteredNames => _entries.Keys;
    }
}
