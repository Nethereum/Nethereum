namespace Nethereum.BlockchainStorage.Token.Postgres
{
    public sealed class TokenBalanceAggregationOptions
    {
        public string RpcUrl { get; set; }
        public int BatchSize { get; set; } = 500;
        public int ProcessingIntervalSeconds { get; set; } = 10;
    }
}
