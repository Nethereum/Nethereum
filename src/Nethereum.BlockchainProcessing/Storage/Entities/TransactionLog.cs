namespace Nethereum.BlockchainProcessing.Storage.Entities
{
    public class TransactionLog : TableRow, ITransactionLogView
    {
        public string TransactionHash { get; set; }
        public string LogIndex { get; set; }
        public string Address { get; set; }
        public string EventHash { get; set; }
        public string IndexVal1 { get; set; }
        public string IndexVal2 { get; set; }
        public string IndexVal3 { get; set; }
        public string Data { get; set; }

        //GB61HBUK40350571199315
        //event hash, index1, index2, index3
    }
}