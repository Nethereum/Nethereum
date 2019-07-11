using System.Numerics;

namespace Nethereum.BlockchainProcessing.Storage.Entities.Mapping
{
    public static class BlockProgressMapping
    {
        public static BlockProgress MapToStorageEntityForUpsert(this BigInteger source)
        {
            return source.MapToStorageEntityForUpsert<BlockProgress>();
        }

        public static TEntity MapToStorageEntityForUpsert<TEntity>(this BigInteger source) where TEntity : BlockProgress, new()
        {
            var block = new TEntity { LastBlockProcessed = source.ToString() };
            block.UpdateRowDates();
            return block;
        }
    }
}
