using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.BlockchainProcessing.ProgressRepositories
{
    public class InMemoryBlockchainProgressRepository : IBlockProgressRepository
    {
        public InMemoryBlockchainProgressRepository(BigInteger? lastBlockProcessed)
        {
            LastBlockProcessed = lastBlockProcessed;
        }

        public BigInteger? LastBlockProcessed { get; private set;}

        public Task<BigInteger?> GetLastBlockNumberProcessedAsync() => Task.FromResult((BigInteger?)LastBlockProcessed);

        public Task UpsertProgressAsync(BigInteger blockNumber)
        {
            LastBlockProcessed = blockNumber;
            return Task.FromResult(0);
        }
    }
}
