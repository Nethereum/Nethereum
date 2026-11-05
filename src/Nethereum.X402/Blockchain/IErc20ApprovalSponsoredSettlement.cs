using System.Numerics;
using Nethereum.Web3;
using Nethereum.X402.Models;

namespace Nethereum.X402.Blockchain;

public interface IErc20ApprovalSponsoredSettlement
{
    Task<SettlementResponse> SettleAsync(
        Erc20ApprovalSponsoredSettlementRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class Erc20ApprovalSponsoredSettlementRequest
{
    public Erc20ApprovalSponsoredSettlementRequest(
        IWeb3 facilitator,
        string network,
        string payer,
        string signedApprovalTransaction,
        BigInteger approvalGasCost,
        Func<CancellationToken, Task<SettlementResponse>> settleThroughProxyAsync)
    {
        Facilitator = facilitator;
        Network = network;
        Payer = payer;
        SignedApprovalTransaction = signedApprovalTransaction;
        ApprovalGasCost = approvalGasCost;
        SettleThroughProxyAsync = settleThroughProxyAsync;
    }

    public IWeb3 Facilitator { get; }

    public string Network { get; }

    public string Payer { get; }

    public string SignedApprovalTransaction { get; }

    public BigInteger ApprovalGasCost { get; }

    public Func<CancellationToken, Task<SettlementResponse>> SettleThroughProxyAsync { get; }

    public int MaxTimeoutSeconds { get; init; }

    internal FacilitatorTransactionSender? Sender { get; init; }
}
