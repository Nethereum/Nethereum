namespace Nethereum.BlockchainProcessing.Storage.Entities
{
    public class Block : TableRow, IBlockView
    {
        public string BlockNumber { get; set; }
        public string Hash { get; set; }
        public string ParentHash { get; set; }
        public long Nonce { get; set; }
        public string ExtraData { get; set; }
        public long Difficulty { get; set; }
        public long TotalDifficulty {get; set; }
        public long Size {get; set; }
        public string Miner { get; set; }
        public long GasLimit { get;set; }
        public long GasUsed { get; set; }
        public long Timestamp { get; set; }

        public long TimeStamp => Timestamp;

        public long TransactionCount { get;set; }
    }
}