using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.BlockStorage.Entities.Mapping
{
    public static class BlockMapping
    {
        public static Block MapToStorageEntityForUpsert(this Nethereum.RPC.Eth.DTOs.Block source)
        {
            return new Block().MapToStorageEntityForUpsert(source);
        }

        public static TEntity MapToStorageEntityForUpsert<TEntity>(this Nethereum.RPC.Eth.DTOs.Block source) where TEntity : Block, new()
        {
            return new TEntity().MapToStorageEntityForUpsert(source);
        }

        public static TEntity MapToStorageEntityForUpsert<TEntity>(this TEntity block, Nethereum.RPC.Eth.DTOs.Block source) where TEntity : Block
        {
            block.Map(source);
            block.UpdateRowDates();
            return block;
        }

        public static void Map(this Block block, Nethereum.RPC.Eth.DTOs.Block source)
        {
            block.BlockNumber = source.Number.Value.ToString();
            block.Difficulty = source.Difficulty?.Value.ToString();
            block.GasLimit = source.GasLimit?.Value.ToString();
            block.GasUsed = source.GasUsed?.Value.ToString();
            block.Size = source.Size?.Value.ToString();
            block.Timestamp = source.Timestamp?.Value.ToString();
            block.TotalDifficulty = source.TotalDifficulty?.Value.ToString();
            block.ExtraData = source.ExtraData ?? string.Empty;
            block.Hash = source.BlockHash ?? string.Empty;
            block.ParentHash = source.ParentHash ?? string.Empty;
            block.Miner = source.Miner ?? string.Empty;
            block.Nonce = source.Nonce;
            block.BaseFeePerGas = source.BaseFeePerGas?.Value.ToString();
            block.TransactionCount = TransactionCount(source);
        }

        private static int TransactionCount(Nethereum.RPC.Eth.DTOs.Block block)
        {
            if (block is BlockWithTransactions b)
                return b.Transactions?.Length ?? 0;

            if (block is BlockWithTransactionHashes bh)
                return bh.TransactionHashes?.Length ?? 0;

            return 0;
        }
    }
}
