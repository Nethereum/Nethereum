using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;

namespace Nethereum.CoreChain.Sync
{
    public delegate Task<bool> BodyRepairDelegate(
        ulong fromBlock,
        ulong toBlock,
        IChainStoreBundle bundle,
        CancellationToken ct);
}
