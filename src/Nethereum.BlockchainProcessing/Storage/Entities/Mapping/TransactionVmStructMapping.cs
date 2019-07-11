using Newtonsoft.Json.Linq;

namespace Nethereum.BlockchainProcessing.Storage.Entities.Mapping
{
    public static class TransactionVmStructMapping
    {
        public static TEntity MapToStorageEntityForUpsert<TEntity>(this JObject stackTrace, string transactionHash, string address) where TEntity: TransactionVmStack, new()
        {
            var entity = new TEntity();
            entity.Map(transactionHash, address, stackTrace);
            entity.UpdateRowDates();
            return entity;
        }

        public static void Map(this TransactionVmStack transactionVmStack, string transactionHash, string address, JObject stackTrace)
        {
            transactionVmStack.TransactionHash = transactionHash;
            transactionVmStack.Address = address;
            transactionVmStack.StructLogs = ((JArray) stackTrace["structLogs"]).ToString();
        }
    }
}
