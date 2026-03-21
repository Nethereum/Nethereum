using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.PrivacyPools.Processing
{
    public interface IPrivacyPoolRepository
    {
        Task AddDepositAsync(PoolDepositEventData deposit);
        Task AddWithdrawalAsync(PoolWithdrawalEventData withdrawal);
        Task AddRagequitAsync(PoolRagequitEventData ragequit);
        Task AddLeafAsync(PoolLeafEventData leaf);

        Task<List<PoolDepositEventData>> GetDepositsAsync();
        Task<List<PoolWithdrawalEventData>> GetWithdrawalsAsync();
        Task<List<PoolRagequitEventData>> GetRagequitsAsync();
        Task<List<PoolLeafEventData>> GetLeavesAsync();

        Task<int> GetLeafCountAsync();
        Task<List<PoolLeafEventData>> GetLeavesSinceAsync(int fromIndex);
    }
}
