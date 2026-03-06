using System;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.BlockchainProcessing.ProgressRepositories
{
    public class ReorgBufferedBlockProgressRepository : IBlockProgressRepository
    {
        private readonly IBlockProgressRepository _inner;
        private readonly BigInteger _reorgBuffer;

        public ReorgBufferedBlockProgressRepository(IBlockProgressRepository inner, BigInteger reorgBuffer)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            if (reorgBuffer < 0) throw new ArgumentOutOfRangeException(nameof(reorgBuffer));
            _reorgBuffer = reorgBuffer;
        }

        public async Task<BigInteger?> GetLastBlockNumberProcessedAsync()
        {
            var last = await _inner.GetLastBlockNumberProcessedAsync().ConfigureAwait(false);
            if (last == null) return null;

            var adjusted = last.Value - _reorgBuffer;
            return adjusted < 0 ? 0 : adjusted;
        }

        public Task UpsertProgressAsync(BigInteger blockNumber)
        {
            return _inner.UpsertProgressAsync(blockNumber);
        }
    }
}
