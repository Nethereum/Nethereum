namespace Nethereum.BlockchainProcessing.Storage.Entities.Mapping
{
    public static class AddressTransactionMapping
    {
        public static void Map(this AddressTransaction to, Nethereum.RPC.Eth.DTOs.Transaction @from, string address)
        {
            to.BlockNumber = @from.BlockNumber.Value.ToString();
            to.Hash = @from.TransactionHash;
            to.Address = address;
        }

        public static AddressTransaction MapToStorageEntityForUpsert(this Nethereum.RPC.Eth.DTOs.TransactionReceiptVO @from, string address)
        {
            return from.MapToStorageEntityForUpsert<AddressTransaction>(address);
        }

        public static TEntity MapToStorageEntityForUpsert<TEntity>(this Nethereum.RPC.Eth.DTOs.TransactionReceiptVO @from, string address)
            where TEntity : AddressTransaction, new()
        {
            return MapToStorageEntityForUpsert<TEntity>(new TEntity(), from, address);
        }

        public static TEntity MapToStorageEntityForUpsert<TEntity>(this TEntity to, Nethereum.RPC.Eth.DTOs.TransactionReceiptVO @from, string address)
            where TEntity : AddressTransaction
        {
            to.Map(from.Transaction, address);
            to.UpdateRowDates();
            return to;
        }
    }
}
