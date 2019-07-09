namespace Nethereum.BlockchainProcessing.Storage.Entities
{
    public class TransactionBase : TableRow, ITransactionView
    {
        public string BlockHash { get; set; }
        public string BlockNumber { get; set; }
        public string Hash { get; set; }
        public string AddressFrom  { get; set; }
        public long TimeStamp { get; set; }
        public long TransactionIndex { get; set; }
        public string Value { get; set; }
        public string AddressTo { get;set; }
        public long Gas { get; set; }
        public long GasPrice { get;set; }
        public string Input { get; set; }
        public long Nonce { get; set;}
        public bool Failed { get; set; }
        public string ReceiptHash { get; set; }
        public long GasUsed { get;set; }
        public long CumulativeGasUsed { get; set; }
        public bool HasLog { get;set; }
        public string Error { get; set; }
        public bool HasVmStack { get; set; }
        public string NewContractAddress { get; set; }
        public bool FailedCreateContract { get; set; }
    }
}