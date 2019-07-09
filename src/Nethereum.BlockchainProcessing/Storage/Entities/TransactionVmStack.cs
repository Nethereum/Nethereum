using Newtonsoft.Json.Linq;

namespace Nethereum.BlockchainProcessing.Storage.Entities
{
    public class TransactionVmStack : TableRow, ITransactionVmStackView
    {
        public string Address { get;set; }
        public string TransactionHash { get; set; }
        public string StructLogs { get; set; }

        public JArray GetStructLogs()
        {
            return string.IsNullOrEmpty(StructLogs) ? null : JArray.Parse(StructLogs);
        }
    }
}