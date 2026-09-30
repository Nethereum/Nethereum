using System.Collections.Generic;
using Newtonsoft.Json;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.BlockStorage.Entities.Mapping
{
    public static class BlockAccessListAccountMapping
    {
        public static BlockAccessListAccount MapToStorageEntityForUpsert(this AccountAccess source, long blockNumber, string blockHash)
        {
            return new BlockAccessListAccount().MapToStorageEntityForUpsert(source, blockNumber, blockHash);
        }

        public static TEntity MapToStorageEntityForUpsert<TEntity>(this AccountAccess source, long blockNumber, string blockHash) where TEntity : BlockAccessListAccount, new()
        {
            return new TEntity().MapToStorageEntityForUpsert(source, blockNumber, blockHash);
        }

        public static TEntity MapToStorageEntityForUpsert<TEntity>(this TEntity entity, AccountAccess source, long blockNumber, string blockHash) where TEntity : BlockAccessListAccount
        {
            entity.Map(source, blockNumber, blockHash);
            entity.UpdateRowDates();
            return entity;
        }

        public static void Map(this BlockAccessListAccount entity, AccountAccess source, long blockNumber, string blockHash)
        {
            entity.BlockNumber = blockNumber;
            entity.BlockHash = blockHash;
            entity.Address = source.Address;
            entity.IsCanonical = true;
            entity.StorageReads = JsonConvert.SerializeObject(source.StorageReads);
            entity.StorageChanges = JsonConvert.SerializeObject(source.StorageChanges);
            entity.BalanceChanges = JsonConvert.SerializeObject(source.BalanceChanges);
            entity.NonceChanges = JsonConvert.SerializeObject(source.NonceChanges);
            entity.CodeChanges = JsonConvert.SerializeObject(source.CodeChanges);
        }

        public static AccountAccess ToAccountAccess(this BlockAccessListAccount entity)
        {
            return new AccountAccess
            {
                Address = entity.Address,
                StorageReads = JsonConvert.DeserializeObject<List<string>>(entity.StorageReads) ?? new List<string>(),
                StorageChanges = JsonConvert.DeserializeObject<List<SlotChanges>>(entity.StorageChanges) ?? new List<SlotChanges>(),
                BalanceChanges = JsonConvert.DeserializeObject<List<BalanceChange>>(entity.BalanceChanges) ?? new List<BalanceChange>(),
                NonceChanges = JsonConvert.DeserializeObject<List<NonceChange>>(entity.NonceChanges) ?? new List<NonceChange>(),
                CodeChanges = JsonConvert.DeserializeObject<List<CodeChange>>(entity.CodeChanges) ?? new List<CodeChange>()
            };
        }
    }
}
