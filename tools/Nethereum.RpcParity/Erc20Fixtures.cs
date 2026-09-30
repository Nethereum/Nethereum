namespace Nethereum.RpcParity
{
    /// <summary>
    /// Well-known mainnet USDT constants used to exercise state/logs/sim/trace
    /// reads against real, always-populated contract storage.
    /// </summary>
    internal static class Erc20Fixtures
    {
        public const string UsdtAddress = "0xdac17f958d2ee523a2206206994597c13d831ec7";
        public const string TransferTopic = Nethereum.Model.Erc20TransferEventTopic.Prefixed;
        public const string TotalSupplySelector = "0x18160ddd";
        public const string TransferSelector = "0xa9059cbb";
        public const string BurnAddress = "0x000000000000000000000000000000000000dead";
    }
}
