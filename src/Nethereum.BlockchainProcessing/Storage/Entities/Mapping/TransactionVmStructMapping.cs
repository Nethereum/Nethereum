using Newtonsoft.Json.Linq;

namespace Nethereum.BlockchainProcessing.Storage.Entities.Mapping
{
    public static class TransactionVmStructMapping
    {
        public static void Map(this TransactionVmStack transactionVmStack, string transactionHash, string address, JObject stackTrace)
        {
            transactionVmStack.TransactionHash = transactionHash;
            transactionVmStack.Address = address;
            transactionVmStack.StructLogs = ((JArray) stackTrace["structLogs"]).ToString();
        }
    }
}
