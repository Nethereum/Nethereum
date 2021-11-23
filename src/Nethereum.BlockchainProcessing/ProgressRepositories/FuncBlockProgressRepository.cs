using System;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.BlockchainProcessing.ProgressRepositories
{
    /// <summary>
    /// When the Block Progress checkpoint occurs, you may need to persist other calculated data to the database in chunks.
    /// This allows you to invoke functions to save Units of Work.
    /// </summary>
    public class FuncBlockProgressRepository : IBlockProgressRepository
    {
        private readonly Func<Task<BigInteger?>> getLastBlockNumberFunc;
        private readonly Func<BigInteger, Task> upsertProgressAsyncFunc;

        public FuncBlockProgressRepository(Func<Task<BigInteger?>> getLastBlockNumberFunc, Func<BigInteger, Task> upsertProgressAsyncFunc)
        {
            if (getLastBlockNumberFunc == null)
                throw new ArgumentNullException(nameof(getLastBlockNumberFunc));

            if (upsertProgressAsyncFunc == null)
                throw new ArgumentNullException(nameof(upsertProgressAsyncFunc));

            this.getLastBlockNumberFunc = getLastBlockNumberFunc;
            this.upsertProgressAsyncFunc = upsertProgressAsyncFunc;
        }

        public Task<BigInteger?> GetLastBlockNumberProcessedAsync()
        {
            return getLastBlockNumberFunc();
        }

        public Task UpsertProgressAsync(BigInteger blockNumber)
        {
            return upsertProgressAsyncFunc(blockNumber);
        }
    }
}
