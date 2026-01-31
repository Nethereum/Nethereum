using System.Threading;
using System.Threading.Tasks;
using Nethereum.EVM.StateChanges;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Wallet.Services.Transaction
{
    public interface IStateChangesPreviewService
    {
        Task<StateChangesResult> PreviewStateChangesAsync(
            CallInput callInput,
            long chainId,
            string currentUserAddress,
            CancellationToken ct = default);

        Task<StateChangesResult> PreviewStateChangesAsync(
            CallInput callInput,
            long chainId,
            string currentUserAddress,
            bool enableTracing,
            CancellationToken ct = default);
    }
}
