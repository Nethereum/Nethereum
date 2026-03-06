namespace Nethereum.BlockchainStorage.Token.Postgres
{
    public sealed class TokenDenormalizerOptions
    {
        public int ProcessingIntervalSeconds { get; set; } = 5;
        public int BatchSize { get; set; } = 1000;
    }
}
