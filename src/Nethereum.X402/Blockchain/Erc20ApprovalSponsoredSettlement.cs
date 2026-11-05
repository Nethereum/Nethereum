using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.X402.Facilitator;
using Nethereum.X402.Models;

namespace Nethereum.X402.Blockchain;

public class Erc20ApprovalSponsoredSettlement : IErc20ApprovalSponsoredSettlement
{
    private readonly ILogger _logger;
    private readonly FacilitatorTransactionSender _fallbackSender = new();

    public Erc20ApprovalSponsoredSettlement(ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    public async Task<SettlementResponse> SettleAsync(
        Erc20ApprovalSponsoredSettlementRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var funding = await FundPayerAsync(request);
        if (funding != null)
            return funding;

        var approval = await RelayApprovalAsync(request);
        if (approval != null)
            return approval;

        var settlement = await request.SettleThroughProxyAsync(CancellationToken.None);
        if (!settlement.Success)
            _logger.LogWarning(
                "x402 exact/permit2 ERC-20 approval for payer {Payer} is mined but settlement failed ({Reason}); the approval is not undone",
                request.Payer, settlement.ErrorReason);

        return settlement;
    }

    private async Task<SettlementResponse?> FundPayerAsync(Erc20ApprovalSponsoredSettlementRequest request)
    {
        try
        {
            var web3 = request.Facilitator;
            var balance = (await web3.Eth.GetBalance.SendRequestAsync(request.Payer)).Value;
            if (balance >= request.ApprovalGasCost)
                return null;

            var receipt = await (request.Sender ?? _fallbackSender).TransferAndWaitForReceiptAsync(
                web3, request.Payer, request.ApprovalGasCost - balance, request.MaxTimeoutSeconds);

            return receipt.Status.Value == 1
                ? null
                : SettlementResponse.Failure(request.Network, X402ErrorCodes.GasFundingFailed, request.Payer, receipt.TransactionHash);
        }
        catch (X402SettlementPendingException ex)
        {
            _logger.LogWarning(ex, "x402 exact/permit2 gas funding {Transaction} broadcast but receipt not obtained for payer {Payer}", ex.TransactionHash, request.Payer);
            return SettlementResponse.Failure(request.Network, X402ErrorCodes.SettlementPending, request.Payer, ex.TransactionHash);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "x402 exact/permit2 gas funding failed for payer {Payer}", request.Payer);
            return SettlementResponse.Failure(request.Network, X402ErrorCodes.GasFundingFailed, request.Payer);
        }
    }

    private async Task<SettlementResponse?> RelayApprovalAsync(Erc20ApprovalSponsoredSettlementRequest request)
    {
        var web3 = request.Facilitator;

        string transactionHash;
        try
        {
            transactionHash = await web3.Eth.Transactions.SendRawTransaction.SendRequestAsync(request.SignedApprovalTransaction);
        }
        catch (Exception ex) when (!X402SettleOutcomes.BroadcastOutcomeIsUnknown(ex))
        {
            _logger.LogWarning(ex, "x402 exact/permit2 ERC-20 approval broadcast failed for payer {Payer}", request.Payer);
            return SettlementResponse.Failure(request.Network, X402ErrorCodes.Erc20ApprovalBroadcastFailed, request.Payer);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "x402 exact/permit2 ERC-20 approval broadcast outcome unknown for payer {Payer}", request.Payer);
            return SettlementResponse.Failure(request.Network, X402ErrorCodes.SettlementPending, request.Payer);
        }

        TransactionReceipt receipt;
        try
        {
            receipt = await (request.Sender ?? _fallbackSender).AwaitReceiptAsync(web3, transactionHash, request.MaxTimeoutSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "x402 exact/permit2 ERC-20 approval {Transaction} broadcast but receipt not obtained for payer {Payer}", transactionHash, request.Payer);
            return SettlementResponse.Failure(request.Network, X402ErrorCodes.SettlementPending, request.Payer, transactionHash);
        }

        return receipt.Status.Value == 1
            ? null
            : SettlementResponse.Failure(request.Network, X402ErrorCodes.Erc20ApprovalTxFailed, request.Payer, transactionHash);
    }
}
