using Nethereum.Documentation;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Contracts.EIP3009.EIP3009;
using Nethereum.Contracts.EIP3009.EIP3009.ContractDefinition;
using Microsoft.Extensions.Logging;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Accounts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Web3.Accounts;
using Nethereum.X402.Facilitator;
using Nethereum.X402.Models;

namespace Nethereum.X402.Blockchain;

public class X402TransferWithAuthorisation3009Service : ExactScheme3009ServiceBase
{
    public X402TransferWithAuthorisation3009Service(
        string facilitatorPrivateKey,
        Dictionary<int, IClient> clientsByChainId,
        ILogger logger = null)
        : base(new Account(facilitatorPrivateKey), clientsByChainId, logger)
    {
    }

    public X402TransferWithAuthorisation3009Service(
        IAccount facilitatorAccount,
        Dictionary<int, IClient> clientsByChainId,
        ILogger logger = null)
        : base(facilitatorAccount, clientsByChainId, logger)
    {
    }

    public X402TransferWithAuthorisation3009Service(
        string facilitatorPrivateKey,
        Dictionary<int, IClient> clientsByChainId,
        X402FacilitatorOptions options,
        ILogger logger = null)
        : base(new Account(facilitatorPrivateKey), clientsByChainId, options, logger)
    {
    }

    public X402TransferWithAuthorisation3009Service(
        IAccount facilitatorAccount,
        Dictionary<int, IClient> clientsByChainId,
        X402FacilitatorOptions options,
        ILogger logger = null)
        : base(facilitatorAccount, clientsByChainId, options, logger)
    {
    }

    protected override bool IsRecipientValid(Authorization authorization, PaymentRequirements requirements)
        => authorization.To.IsTheSameAddress(requirements.PayTo);

    protected override string RecoverSigner(Authorization authorization, ResolvedRequirement resolved, EthECDSASignature signature)
        => Signer.RecoverAddress(
            authorization, resolved.TokenName, resolved.TokenVersion, resolved.ChainId, resolved.TokenAddress, signature);

    protected override byte[] ComputeSignedDigest(Authorization authorization, ResolvedRequirement resolved)
        => Signer.ComputeTransferDigest(
            authorization, resolved.TokenName, resolved.TokenVersion, resolved.ChainId, resolved.TokenAddress);

    protected override Task<TransactionReceipt> SubmitSettlementAsync(
        Eip3009Service eip3009Service, Authorization authorization, EthECDSASignature signature, CancellationTokenSource cancellationTokenSource)
        => Sender.SendAndWaitForReceiptAsync(
            eip3009Service.Web3,
            eip3009Service.ContractHandler,
            BuildFunction(authorization, signature),
            cancellationTokenSource);

    protected override Task SimulateSettlementAsync(
        Eip3009Service eip3009Service, Authorization authorization, EthECDSASignature signature)
        => eip3009Service.ContractHandler.EstimateGasAsync(BuildFunction(authorization, signature));

    protected override async Task<BigInteger> EstimateSettlementGasAsync(
        Eip3009Service eip3009Service, Authorization authorization, EthECDSASignature signature)
        => (await eip3009Service.ContractHandler.EstimateGasAsync(BuildFunction(authorization, signature))).Value;

    private static TransferWithAuthorization1Function BuildFunction(Authorization authorization, EthECDSASignature signature) =>
        new()
        {
            AuthorisationFrom = authorization.From,
            AuthorisationTo = authorization.To,
            Value = X402Number.Parse(authorization.Value),
            ValidAfter = X402Number.Parse(authorization.ValidAfter),
            ValidBefore = X402Number.Parse(authorization.ValidBefore),
            AuthorisationNonce = authorization.Nonce.HexToByteArray(),
            V = signature.V[0],
            R = signature.R.PadTo32Bytes(),
            S = signature.S.PadTo32Bytes()
        };

    protected override Task<TransactionReceipt> SubmitContractSignatureSettlementAsync(
        Eip3009Service eip3009Service, Authorization authorization, byte[] signature, CancellationTokenSource cancellationTokenSource)
        => Sender.SendAndWaitForReceiptAsync(
            eip3009Service.Web3,
            eip3009Service.ContractHandler,
            BuildBytesFunction(authorization, signature),
            cancellationTokenSource);

    protected override async Task<BigInteger> EstimateContractSignatureSettlementGasAsync(
        Eip3009Service eip3009Service, Authorization authorization, byte[] signature)
        => (await eip3009Service.ContractHandler.EstimateGasAsync(BuildBytesFunction(authorization, signature))).Value;

    private static TransferWithAuthorizationBytesSignatureFunction BuildBytesFunction(Authorization authorization, byte[] signature) =>
        new()
        {
            AuthorisationFrom = authorization.From,
            AuthorisationTo = authorization.To,
            Value = X402Number.Parse(authorization.Value),
            ValidAfter = X402Number.Parse(authorization.ValidAfter),
            ValidBefore = X402Number.Parse(authorization.ValidBefore),
            AuthorisationNonce = authorization.Nonce.HexToByteArray(),
            Signature = signature
        };

    [Obsolete("EIP-3009 cancelAuthorization requires the authorizer's EIP-712 signature, so this overload always reverts on a conformant token. Use the overload that takes the authorizer's signature.")]
    public async Task<CancelAuthorizationResponse> CancelAuthorizationAsync(
        string authorizerAddress,
        byte[] nonce,
        string network,
        string tokenAddress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Caip2.TryParseEip155ChainId(network, out var chainId) ||
                !ClientsByChainId.TryGetValue(chainId, out var client))
            {
                return CancelFailure(network, X402ErrorCodes.InvalidNetwork);
            }

            var web3 = new Nethereum.Web3.Web3(AccountFor(chainId), client);
            var eip3009Service = new Eip3009Service(web3, tokenAddress);

            var cancelFunction = new CancelAuthorization1Function
            {
                Authorizer = authorizerAddress,
                AuthorisationNonce = nonce,
                V = 0,
                R = new byte[32],
                S = new byte[32]
            };

            return await SendCancelAsync(web3, eip3009Service, cancelFunction, network, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "x402 eip3009 cancelAuthorization failed for {Authorizer}", authorizerAddress);
            return CancelFailure(network, X402ErrorCodes.CancellationError);
        }
    }

    [NethereumDocExample(DocSection.DeFi, "x402-payments", "CancelAuthorizationAsync - cancel an unsettled authorization with the authorizer signature")]
    public async Task<CancelAuthorizationResponse> CancelAuthorizationAsync(
        string authorizerAddress,
        byte[] nonce,
        string authorizerSignature,
        string network,
        string tokenAddress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Caip2.TryParseEip155ChainId(network, out var chainId) ||
                !ClientsByChainId.TryGetValue(chainId, out var client))
            {
                return CancelFailure(network, X402ErrorCodes.InvalidNetwork);
            }

            var signature = EthECDSASignatureFactory.ExtractECDSASignature(authorizerSignature);
            var cancelFunction = new CancelAuthorization1Function
            {
                Authorizer = authorizerAddress,
                AuthorisationNonce = nonce,
                V = signature.V[0],
                R = signature.R.PadTo32Bytes(),
                S = signature.S.PadTo32Bytes()
            };

            var web3 = new Nethereum.Web3.Web3(AccountFor(chainId), client);
            var eip3009Service = new Eip3009Service(web3, tokenAddress);

            try
            {
                await eip3009Service.ContractHandler.EstimateGasAsync(cancelFunction);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "x402 eip3009 cancelAuthorization simulation reverted for {Authorizer}", authorizerAddress);
                return CancelFailure(network, X402ErrorCodes.InvalidTransactionState);
            }

            return await SendCancelAsync(web3, eip3009Service, cancelFunction, network, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "x402 eip3009 cancelAuthorization failed for {Authorizer}", authorizerAddress);
            return CancelFailure(network, X402ErrorCodes.CancellationError);
        }
    }

    private async Task<CancelAuthorizationResponse> SendCancelAsync(
        Nethereum.Web3.Web3 web3,
        Eip3009Service eip3009Service,
        CancelAuthorization1Function cancelFunction,
        string network,
        CancellationToken cancellationToken)
    {
        using var receiptBound = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        receiptBound.CancelAfter(FacilitatorTransactionSender.BoundFor(0));
        var receipt = await Sender.SendAndWaitForReceiptAsync(
            web3,
            eip3009Service.ContractHandler,
            cancelFunction,
            receiptBound.Token);

        return CancelReceipt(receipt, network);
    }

    private static CancelAuthorizationResponse CancelReceipt(TransactionReceipt receipt, string network) =>
        new()
        {
            Success = receipt.Status.Value == 1,
            ErrorReason = receipt.Status.Value == 1 ? null : X402ErrorCodes.InvalidTransactionState,
            Transaction = receipt.TransactionHash,
            Network = network
        };

    private static CancelAuthorizationResponse CancelFailure(string network, string errorReason) =>
        new()
        {
            Success = false,
            ErrorReason = errorReason,
            Transaction = null,
            Network = network
        };
}

[NethereumDocExample(DocSection.DeFi, "x402-payments", "CancelAuthorizationResponse - the result of cancelling an authorization")]
public class CancelAuthorizationResponse
{
    public bool Success { get; set; }
    public string ErrorReason { get; set; }
    public string Transaction { get; set; }
    public string Network { get; set; }
}
