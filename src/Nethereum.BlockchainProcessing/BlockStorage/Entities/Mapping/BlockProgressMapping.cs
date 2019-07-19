using System.Numerics;

namespace Nethereum.BlockchainProcessing.BlockStorage.Entities.Mapping
{
    public static class BlockProgressMapping
    {
        public static BlockProgress MapToStorageEntityForUpsert(this BigInteger source)
        {
            return source.MapToStorageEntityForUpsert<BlockProgress>();
        }

        public static TEntity MapToStorageEntityForUpsert<TEntity>(this BigInteger source) where TEntity : BlockProgress, new()
        {
            return new TEntity().MapToStorageEntityForUpsert(source);
        }

        public static TEntity MapToStorageEntityForUpsert<TEntity>(this TEntity block, BigInteger source) where TEntity : BlockProgress, new()
        {
            block.LastBlockProcessed = source.ToString();
            block.UpdateRowDates();
            return block;
        }
    }
}
