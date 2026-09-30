using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.DevP2P.Sync.Snap.CatchUp
{
    public interface IBlockAccessListFetcher
    {
        Task<IReadOnlyList<IReadOnlyList<AccountChanges>>> FetchAsync(
            IReadOnlyList<BalBlockRef> blocks, CancellationToken ct);
    }
}
