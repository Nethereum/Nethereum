namespace Nethereum.BlockchainProcessing.BlockStorage.Entities
{
    public class TokenMetadata : TableRow, ITokenMetadataView
    {
        public string ContractAddress { get; set; }
        public string Name { get; set; }
        public string Symbol { get; set; }
        public int Decimals { get; set; }
        public string TokenType { get; set; }
    }
}
