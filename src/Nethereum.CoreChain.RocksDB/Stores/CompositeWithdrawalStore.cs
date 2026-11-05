using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class CompositeWithdrawalStore : IWithdrawalStore
    {
        private readonly IWithdrawalStore _history;
        private readonly IWithdrawalStore _hot;
        private readonly RocksDbHotBlockWindowStore _hotWindow;

        public CompositeWithdrawalStore(IWithdrawalStore history, IWithdrawalStore hot, RocksDbHotBlockWindowStore hotWindow)
        {
            _history = history ?? throw new ArgumentNullException(nameof(history));
            _hot = hot ?? throw new ArgumentNullException(nameof(hot));
            _hotWindow = hotWindow ?? throw new ArgumentNullException(nameof(hotWindow));
        }

        public Task<IList<Withdrawal>> GetByBlockNumberAsync(BigInteger blockNumber)
            => HotThenHistory.ReadAsync(
                _hotWindow.ContainsBlock((ulong)blockNumber),
                () => _hot.GetByBlockNumberAsync(blockNumber),
                HotThenHistory.NotEmpty,
                () => _history.GetByBlockNumberAsync(blockNumber));

        public async Task<IList<Withdrawal>> GetByBlockHashAsync(byte[] blockHash)
        {
            var number = _hotWindow.TryGetBlockNumberByHash(blockHash);
            return await HotThenHistory.ReadAsync(
                number.HasValue,
                () => _hot.GetByBlockNumberAsync((BigInteger)number.Value),
                HotThenHistory.NotEmpty,
                () => _history.GetByBlockHashAsync(blockHash)).ConfigureAwait(false);
        }

        public Task SaveAsync(byte[] blockHash, IList<Withdrawal> withdrawals) => _hot.SaveAsync(blockHash, withdrawals);

        public Task DeleteByBlockHashAsync(byte[] blockHash) => _hot.DeleteByBlockHashAsync(blockHash);

        public Task DeleteByBlockNumberAsync(BigInteger blockNumber) => _hot.DeleteByBlockNumberAsync(blockNumber);
    }
}
