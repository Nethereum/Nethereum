namespace Nethereum.BlockchainProcessing.BlockStorage.Entities
{
    public class TransactionBase : TableRow, ITransactionView
    {
        public string BlockHash { get; set; }
        public string BlockNumber { get; set; }
        public string Hash { get; set; }
        public string AddressFrom  { get; set; }
        public string TimeStamp { get; set; }
        public string TransactionIndex { get; set; }
        public string Value { get; set; }
        public string AddressTo { get;set; }
        public string Gas { get; set; }
        public string GasPrice { get;set; }
        public string Input { get; set; }
        public string Nonce { get; set;}
        public bool Failed { get; set; }
        public string ReceiptHash { get; set; }
        public string GasUsed { get;set; }
        public string CumulativeGasUsed { get; set; }
        public bool HasLog { get;set; }
        public string Error { get; set; }
        public bool HasVmStack { get; set; }
        public string NewContractAddress { get; set; }
        public bool FailedCreateContract { get; set; }
        public string MaxFeePerGas { get; internal set; }
        public string MaxPriorityFeePerGas { get; internal set; }
    }
}