namespace Nethereum.EVM
{
    /// <summary>
    /// Canonical Ethereum mainnet (chainId 1) fork activation lists in the
    /// chronological order required by EIP-2124 ForkID computation. The values
    /// come from <see cref="MainnetChainActivations"/>; this class exists so
    /// consumers (e.g. DevP2P Status handshakes) don't have to enumerate the
    /// constants by hand.
    /// </summary>
    public static class MainnetForkSchedule
    {
        /// <summary>
        /// Block-activated fork heights in chronological order. Forks at block 0
        /// (none on mainnet) and bomb-defusal forks that share a block with the
        /// previous fork are filtered by the ForkID algorithm itself per EIP-2124.
        /// </summary>
        public static readonly ulong[] BlockHeights = new ulong[]
        {
            (ulong)MainnetChainActivations.HomesteadBlock,
            (ulong)MainnetChainActivations.DaoForkBlock,
            (ulong)MainnetChainActivations.TangerineWhistleBlock,
            (ulong)MainnetChainActivations.SpuriousDragonBlock,
            (ulong)MainnetChainActivations.ByzantiumBlock,
            (ulong)MainnetChainActivations.ConstantinopleBlock,
            (ulong)MainnetChainActivations.PetersburgBlock,
            (ulong)MainnetChainActivations.IstanbulBlock,
            (ulong)MainnetChainActivations.MuirGlacierBlock,
            (ulong)MainnetChainActivations.BerlinBlock,
            (ulong)MainnetChainActivations.LondonBlock,
            (ulong)MainnetChainActivations.ArrowGlacierBlock,
            (ulong)MainnetChainActivations.GrayGlacierBlock,
            (ulong)MainnetChainActivations.ParisBlock
        };

        /// <summary>Time-activated fork timestamps in chronological order (Shanghai onwards).</summary>
        public static readonly ulong[] Timestamps = new ulong[]
        {
            MainnetChainActivations.ShanghaiTimestamp,
            MainnetChainActivations.CancunTimestamp,
            MainnetChainActivations.PragueTimestamp
        };
    }
}
