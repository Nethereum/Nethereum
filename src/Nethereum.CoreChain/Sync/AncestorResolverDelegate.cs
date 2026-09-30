using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.CoreChain.Sync
{
    public delegate Task<ulong> AncestorResolverDelegate(
        ulong divergedBlock,
        ulong floorBlock,
        CancellationToken ct);
}
