using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.CoreChain.Storage
{
    public interface IStateCompaction
    {
        Task CompactStateAsync(Action<string> progress, CancellationToken ct);

        Task CompactAllAsync(Action<string> progress, CancellationToken ct);
    }
}
