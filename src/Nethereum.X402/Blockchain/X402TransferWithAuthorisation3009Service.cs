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
        => eip3009Service.TransferWithAuthorizationRequestAndWaitForReceiptAsync(
            BuildFunction(authorization, signature), cancellationTokenSource);

    protected override Task SimulateSettlementAsync(
        Eip3009Service eip3009Service, Authorization authorization, EthECDSASignature signature)
        => eip3009Service.ContractHandler.EstimateGasAsync(BuildFunction(authorization, signature));

    private static TransferWithAuthorization1Function BuildFunction(Authorization authorization, EthECDSASignature signature) =>
        new()
        {
            AuthorisationFrom = authorization.From,
            AuthorisationTo = authorization.To,
            Value = BigInteger.Parse(authorization.Value),
            ValidAfter = BigInteger.Parse(authorization.ValidAfter),
            ValidBefore = BigInteger.Parse(authorization.ValidBefore),
            AuthorisationNonce = authorization.Nonce.HexToByteArray(),
            V = signature.V[0],
            R = signature.R,
            S = signature.S
        };

    /// <summary>
    /// Submits an EIP-3009 cancelAuthorization for the given authorizer/nonce, so a signed but
    /// unsettled authorization can be voided.
    /// </summary>
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
                return new CancelAuthorizationResponse
                {
                    Success = false,
                    ErrorReason = X402ErrorCodes.InvalidNetwork,
                    Transaction = null,
                    Network = network
                };
            }

            var web3 = new Nethereum.Web3.Web3(Account, client);
            var eip3009Service = new Eip3009Service(web3, tokenAddress);

            var cancelFunction = new CancelAuthorization1Function
            {
                Authorizer = authorizerAddress,
                AuthorisationNonce = nonce,
                V = 0,
                R = new byte[32],
                S = new byte[32]
            };

            var cancellationTokenSource = cancellationToken != default
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                : null;

            var receipt = await eip3009Service.CancelAuthorizationRequestAndWaitForReceiptAsync(
                cancelFunction,
                cancellationTokenSource
            );

            return new CancelAuthorizationResponse
            {
                Success = receipt.Status.Value == 1,
                ErrorReason = receipt.Status.Value == 1 ? null : X402ErrorCodes.InvalidTransactionState,
                Transaction = receipt.TransactionHash,
                Network = network
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "x402 eip3009 cancelAuthorization failed for {Authorizer}", authorizerAddress);
            return new CancelAuthorizationResponse
            {
                Success = false,
                ErrorReason = X402ErrorCodes.CancellationError,
                Transaction = null,
                Network = network
            };
        }
    }
}

public class CancelAuthorizationResponse
{
    public bool Success { get; set; }
    public string ErrorReason { get; set; }
    public string Transaction { get; set; }
    public string Network { get; set; }
}
