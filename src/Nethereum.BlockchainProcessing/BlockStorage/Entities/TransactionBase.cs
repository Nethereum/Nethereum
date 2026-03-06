namespace Nethereum.BlockchainProcessing.BlockStorage.Entities
{
    public class TransactionBase : TableRow, ITransactionView
    {
        public string BlockHash { get; set; }
        public long BlockNumber { get; set; }
        public string Hash { get; set; }
        public string AddressFrom  { get; set; }
        public long TimeStamp { get; set; }
        public long TransactionIndex { get; set; }
        public string Value { get; set; }
        public string AddressTo { get;set; }
        public string Gas { get; set; }
        public string GasPrice { get;set; }
        public string Input { get; set; }
        public long Nonce { get; set;}
        public bool Failed { get; set; }
        public string ReceiptHash { get; set; }
        public string GasUsed { get;set; }
        public string CumulativeGasUsed { get; set; }
        public string EffectiveGasPrice { get; set; }
        public bool HasLog { get;set; }
        public string Error { get; set; }
        public bool HasVmStack { get; set; }
        public string NewContractAddress { get; set; }
        public bool FailedCreateContract { get; set; }
        public string MaxFeePerGas { get; set; }
        public string MaxPriorityFeePerGas { get; set; }
        public long TransactionType { get; set; }
        public string RevertReason { get; set; }
        public bool IsCanonical { get; set; } = true;
        public string MaxFeePerBlobGas { get; set; }
        public string BlobGasUsed { get; set; }
        public string BlobGasPrice { get; set; }
    }
}
