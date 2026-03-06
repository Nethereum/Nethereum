using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.BlockStorage.Entities.Mapping;
using Nethereum.Hex.HexTypes;

namespace Nethereum.BlockchainProcessing.BlockStorage.Repositories
{
    public class InMemoryBlockRepository : IBlockRepository, INonCanonicalBlockRepository
    {
        private readonly object _lock = new object();

        public List<IBlockView> Records { get; set;}

        public InMemoryBlockRepository(List<IBlockView> records)
        {
            Records = records;
        }

        public Task<IBlockView> FindByBlockNumberAsync(HexBigInteger blockNumber)
        {
            lock (_lock)
            {
                var blockNum = (long)blockNumber.Value;
                var block = Records.FirstOrDefault(r => r.BlockNumber == blockNum);
                return Task.FromResult(block);
            }
        }

        public async Task UpsertBlockAsync(RPC.Eth.DTOs.Block source)
        {
            var record = await FindByBlockNumberAsync(source.Number).ConfigureAwait(false);
            lock (_lock)
            {
                if (record != null) Records.Remove(record);
                var entity = source.MapToStorageEntityForUpsert();
                entity.IsCanonical = true;
                Records.Add(entity);
            }
        }

        public async Task MarkNonCanonicalAsync(System.Numerics.BigInteger blockNumber)
        {
            var record = await FindByBlockNumberAsync(blockNumber.ToHexBigInteger()).ConfigureAwait(false);
            lock (_lock)
            {
                if (record is Block block)
                {
                    block.IsCanonical = false;
                }
            }
        }
    }
}
