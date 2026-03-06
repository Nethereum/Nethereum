namespace Nethereum.BlockchainProcessing.BlockStorage.Entities
{
    public class InternalTransaction : TableRow, IInternalTransactionView
    {
        public string TransactionHash { get; set; }
        public long BlockNumber { get; set; }
        public string BlockHash { get; set; }
        public int TraceIndex { get; set; }
        public int Depth { get; set; }
        public string Type { get; set; }
        public string AddressFrom { get; set; }
        public string AddressTo { get; set; }
        public string Value { get; set; }
        public string Gas { get; set; }
        public string GasUsed { get; set; }
        public string Input { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }
        public string RevertReason { get; set; }
        public bool IsCanonical { get; set; } = true;
    }
}
