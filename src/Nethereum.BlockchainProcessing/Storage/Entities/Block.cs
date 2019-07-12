namespace Nethereum.BlockchainProcessing.Storage.Entities
{
    public class Block : TableRow, IBlockView
    {
        public string BlockNumber { get; set; }
        public string Hash { get; set; }
        public string ParentHash { get; set; }
        public string Nonce { get; set; }
        public string ExtraData { get; set; }
        public string Difficulty { get; set; }
        public string TotalDifficulty {get; set; }
        public string Size {get; set; }
        public string Miner { get; set; }
        public string GasLimit { get;set; }
        public string GasUsed { get; set; }
        public string Timestamp { get; set; }

        public string TimeStamp => Timestamp;

        public long TransactionCount { get;set; }
    }
}