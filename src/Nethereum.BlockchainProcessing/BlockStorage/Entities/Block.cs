namespace Nethereum.BlockchainProcessing.BlockStorage.Entities
{
    public class Block : TableRow, IBlockView
    {
        public long BlockNumber { get; set; }
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
        public long Timestamp { get; set; }
        public bool IsCanonical { get; set; } = true;
        public bool IsFinalized { get; set; }
        public int? ChainId { get; set; }

        public long TimeStamp => Timestamp;

        public long TransactionCount { get;set; }
        public string BaseFeePerGas { get; set; }
        public string StateRoot { get; set; }
        public string ReceiptsRoot { get; set; }
        public string LogsBloom { get; set; }
        public string WithdrawalsRoot { get; set; }
        public string BlobGasUsed { get; set; }
        public string ExcessBlobGas { get; set; }
        public string ParentBeaconBlockRoot { get; set; }
        public string RequestsHash { get; set; }
        public string TransactionsRoot { get; set; }
        public string MixHash { get; set; }
        public string Sha3Uncles { get; set; }
    }
}
