namespace Nethereum.BlockchainProcessing.BlockStorage.Entities
{
    public class TokenTransferLog : TableRow, ITokenTransferLogView
    {
        public string TransactionHash { get; set; }
        public long LogIndex { get; set; }
        public long BlockNumber { get; set; }
        public string BlockHash { get; set; }
        public string ContractAddress { get; set; }
        public string EventHash { get; set; }
        public string FromAddress { get; set; }
        public string ToAddress { get; set; }
        public string Amount { get; set; }
        public string TokenId { get; set; }
        public string OperatorAddress { get; set; }
        public string TokenType { get; set; }
        public bool IsCanonical { get; set; } = true;
    }
}
