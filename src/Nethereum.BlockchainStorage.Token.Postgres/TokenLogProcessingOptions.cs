using System.Numerics;

namespace Nethereum.BlockchainStorage.Token.Postgres
{
    public sealed class TokenLogProcessingOptions
    {
        public string RpcUrl { get; set; } = string.Empty;
        public string[] ContractAddresses { get; set; }
        public BigInteger StartAtBlockNumberIfNotProcessed { get; set; } = 0;
        public int NumberOfBlocksToProcessPerRequest { get; set; } = 1000;
        public int RetryWeight { get; set; } = 50;
        public uint MinimumNumberOfConfirmations { get; set; } = 0;
        public int ReorgBuffer { get; set; } = 0;
    }
}
