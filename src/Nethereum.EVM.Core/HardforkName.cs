
using Nethereum.Documentation;

namespace Nethereum.EVM
{
    [NethereumDocExample(DocSection.EvmSimulator, "hardfork-config", "Every Ethereum mainnet hardfork the engine knows, in order")]
    public enum HardforkName
    {
        Unspecified = 0,
        Frontier,
        FrontierThawing,
        Homestead,
        DaoFork,
        TangerineWhistle,
        SpuriousDragon,
        Byzantium,
        Constantinople,
        Petersburg,
        Istanbul,
        MuirGlacier,
        Berlin,
        London,
        ArrowGlacier,
        GrayGlacier,
        Paris,
        Shanghai,
        Cancun,
        Prague,
        Osaka,
        OsakaBpo1,
        OsakaBpo2,
        Amsterdam
    }

    public static class HardforkNames
    {
        private static readonly System.Collections.Generic.Dictionary<string, HardforkName> Aliases =
            new(System.StringComparer.OrdinalIgnoreCase)
            {
                ["EIP150"] = HardforkName.TangerineWhistle,
                ["EIP158"] = HardforkName.SpuriousDragon,
                ["EIP155"] = HardforkName.SpuriousDragon,
                ["ConstantinopleFix"] = HardforkName.Petersburg,
                ["Merge"] = HardforkName.Paris,
                ["MergeNetSplitFork"] = HardforkName.Paris,
                ["BPO1"] = HardforkName.OsakaBpo1,
                ["BPO2"] = HardforkName.OsakaBpo2,
            };

        public static HardforkName Parse(string name)
        {
            if (name == null) throw new System.ArgumentNullException(nameof(name));
            if (Aliases.TryGetValue(name, out var aliased)) return aliased;
            if (!System.Enum.TryParse<HardforkName>(name, ignoreCase: true, out var fork) || fork == HardforkName.Unspecified)
                throw new System.ArgumentException($"Unknown hardfork name: '{name}'", nameof(name));
            return fork;
        }
    }
}
