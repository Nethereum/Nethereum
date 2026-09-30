using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.DevP2P.Sync.Snap.CatchUp
{
    public interface IBlockAccessListApplier
    {
        Task ApplyAsync(IReadOnlyList<AccountChanges> blockAccessList, ISnapTaskFrontier frontier, CancellationToken ct = default);
    }
}
