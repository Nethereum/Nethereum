namespace Nethereum.EVM
{
    /// <summary>
    /// Ethereum mainnet hardforks in chronological activation order. Values that
    /// have no EVM opcode/gas change (FrontierThawing, DaoFork, MuirGlacier,
    /// ArrowGlacier, GrayGlacier) are kept so consumers can name the exact fork;
    /// the registry aliases them to the closest EVM-behaviour-equivalent config.
    /// </summary>
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
        OsakaBpo1
    }

    public static class HardforkNames
    {
        /// <summary>
        /// Legacy / spec aliases used by older ethereum/tests fixtures
        /// (<c>legacytests</c>) and by EIP documents. The fixtures pre-date
        /// the human-readable hardfork names and refer to forks by the
        /// driving EIP number — these map to the same on-chain rules.
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<string, HardforkName> Aliases =
            new(System.StringComparer.OrdinalIgnoreCase)
            {
                ["EIP150"] = HardforkName.TangerineWhistle,           // EIP-150 gas reprice
                ["EIP158"] = HardforkName.SpuriousDragon,             // EIP-158 state clearing
                ["EIP155"] = HardforkName.SpuriousDragon,             // EIP-155 chain id was in same fork
                ["ConstantinopleFix"] = HardforkName.Petersburg,      // Petersburg = Constantinople with EIP-1283 dropped
                ["Merge"] = HardforkName.Paris,                       // ethereum/tests post-Merge name
                ["MergeNetSplitFork"] = HardforkName.Paris,           // EEST shadow-fork variant
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
