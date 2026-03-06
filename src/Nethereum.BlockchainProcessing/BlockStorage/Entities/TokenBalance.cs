namespace Nethereum.BlockchainProcessing.BlockStorage.Entities
{
    public class TokenBalance : TableRow, ITokenBalanceView
    {
        public string Address { get; set; }
        public string ContractAddress { get; set; }
        public string Balance { get; set; }
        public string TokenType { get; set; }
        public long LastUpdatedBlockNumber { get; set; }
    }
}
