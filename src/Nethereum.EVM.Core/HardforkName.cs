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
        Osaka
    }

    public static class HardforkNames
    {
        public static HardforkName Parse(string name)
        {
            if (name == null) throw new System.ArgumentNullException(nameof(name));
            if (!System.Enum.TryParse<HardforkName>(name, ignoreCase: true, out var fork) || fork == HardforkName.Unspecified)
                throw new System.ArgumentException($"Unknown hardfork name: '{name}'", nameof(name));
            return fork;
        }
    }
}
