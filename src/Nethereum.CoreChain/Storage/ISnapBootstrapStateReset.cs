using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.CoreChain.Storage
{
    public interface ISnapBootstrapStateReset
    {
        Task ResetSnapBootstrapStateAsync(Action<string> progress, CancellationToken ct);
    }
}
