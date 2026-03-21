using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.PrivacyPools.Processing
{
    public class InMemoryPrivacyPoolRepository : IPrivacyPoolRepository
    {
        private readonly List<PoolDepositEventData> _deposits = new List<PoolDepositEventData>();
        private readonly List<PoolWithdrawalEventData> _withdrawals = new List<PoolWithdrawalEventData>();
        private readonly List<PoolRagequitEventData> _ragequits = new List<PoolRagequitEventData>();
        private readonly List<PoolLeafEventData> _leaves = new List<PoolLeafEventData>();
        private readonly object _lock = new object();

        public Task AddDepositAsync(PoolDepositEventData deposit)
        {
            lock (_lock) { _deposits.Add(deposit); }
            return Task.FromResult(0);
        }

        public Task AddWithdrawalAsync(PoolWithdrawalEventData withdrawal)
        {
            lock (_lock) { _withdrawals.Add(withdrawal); }
            return Task.FromResult(0);
        }

        public Task AddRagequitAsync(PoolRagequitEventData ragequit)
        {
            lock (_lock) { _ragequits.Add(ragequit); }
            return Task.FromResult(0);
        }

        public Task AddLeafAsync(PoolLeafEventData leaf)
        {
            lock (_lock) { _leaves.Add(leaf); }
            return Task.FromResult(0);
        }

        public Task<List<PoolDepositEventData>> GetDepositsAsync()
        {
            lock (_lock) { return Task.FromResult(_deposits.ToList()); }
        }

        public Task<List<PoolWithdrawalEventData>> GetWithdrawalsAsync()
        {
            lock (_lock) { return Task.FromResult(_withdrawals.ToList()); }
        }

        public Task<List<PoolRagequitEventData>> GetRagequitsAsync()
        {
            lock (_lock) { return Task.FromResult(_ragequits.ToList()); }
        }

        public Task<List<PoolLeafEventData>> GetLeavesAsync()
        {
            lock (_lock) { return Task.FromResult(_leaves.ToList()); }
        }

        public Task<int> GetLeafCountAsync()
        {
            lock (_lock) { return Task.FromResult(_leaves.Count); }
        }

        public Task<List<PoolLeafEventData>> GetLeavesSinceAsync(int fromIndex)
        {
            lock (_lock)
            {
                return Task.FromResult(
                    _leaves.Where(l => (int)l.Index >= fromIndex)
                           .OrderBy(l => (int)l.Index)
                           .ToList());
            }
        }
    }
}
