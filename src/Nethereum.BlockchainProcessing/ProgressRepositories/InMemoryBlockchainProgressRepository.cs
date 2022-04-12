using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.BlockchainProcessing.ProgressRepositories
{
    public class InMemoryBlockchainProgressRepository : IBlockProgressRepository
    {
        public InMemoryBlockchainProgressRepository()
        {

        }

        public InMemoryBlockchainProgressRepository(BigInteger lastBlockProcessed)
        {
            LastBlockProcessed = lastBlockProcessed;
        }

        public BigInteger? LastBlockProcessed { get; private set;}

        public Task<BigInteger?> GetLastBlockNumberProcessedAsync() => Task.FromResult(LastBlockProcessed);

        public virtual Task UpsertProgressAsync(BigInteger blockNumber)
        {
            LastBlockProcessed = blockNumber;
            //Debug.WriteLine(blockNumber.ToString());
            return Task.FromResult(0);
        }
    }
}
