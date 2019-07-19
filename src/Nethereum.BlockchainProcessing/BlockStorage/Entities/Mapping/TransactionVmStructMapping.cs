using Newtonsoft.Json.Linq;

namespace Nethereum.BlockchainProcessing.BlockStorage.Entities.Mapping
{
    public static class TransactionVmStructMapping
    {
        public static TEntity MapToStorageEntityForUpsert<TEntity>(this JObject stackTrace, string transactionHash, string address) where TEntity: TransactionVmStack, new()
        {
            return new TEntity().MapToStorageEntityForUpsert(stackTrace, transactionHash, address);
        }

        public static TEntity MapToStorageEntityForUpsert<TEntity>(this TEntity entity, JObject stackTrace, string transactionHash, string address) where TEntity : TransactionVmStack, new()
        {
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
