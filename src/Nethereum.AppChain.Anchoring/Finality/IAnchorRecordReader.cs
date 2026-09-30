using System.Threading;
using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.AppChain.Anchoring.Finality
{
    public interface IAnchorRecordReader
    {
        Task<AnchorRecord?> GetLatestAnchorAsync(BlockParameter blockParameter, CancellationToken ct);
    }
}
