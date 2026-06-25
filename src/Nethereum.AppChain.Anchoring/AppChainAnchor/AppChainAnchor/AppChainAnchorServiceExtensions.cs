using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.AppChain.Anchoring.AppChainAnchor.ContractDefinition;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.AppChain.Anchoring.AppChainAnchor
{
    public partial class AppChainAnchorService
    {
        public async Task<TransactionReceipt> SubmitAnchorRequestAndWaitForReceiptAsync(
            AnchorBuildResult buildResult, CancellationTokenSource cancellationToken = null)
        {
            return await SubmitAnchorRequestAndWaitForReceiptAsync(
                new SubmitAnchorFunction
                {
                    A = buildResult.Anchor,
                    Proof = buildResult.ProofBytes ?? Array.Empty<byte>()
                }, cancellationToken).ConfigureAwait(false);
        }
    }
}
