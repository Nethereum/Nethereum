using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.DevP2P.Sync.Snap.CatchUp
{
    public interface IBlockAccessListPeer
    {
        string Id { get; }

        Task<IReadOnlyList<byte[]>> RequestBlockAccessListsAsync(
            IReadOnlyList<byte[]> blockHashes, ulong responseBytes, CancellationToken ct);
    }
}
