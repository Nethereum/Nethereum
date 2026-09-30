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

public class X402ReceiveWithAuthorisation3009Service : ExactScheme3009ServiceBase
{
    public X402ReceiveWithAuthorisation3009Service(
        string receiverPrivateKey,
        Dictionary<int, IClient> clientsByChainId,
        ILogger logger = null)
        : base(new Account(receiverPrivateKey), clientsByChainId, logger)
    {
    }

    public X402ReceiveWithAuthorisation3009Service(
        IAccount receiverAccount,
        Dictionary<int, IClient> clientsByChainId,
        ILogger logger = null)
        : base(receiverAccount, clientsByChainId, logger)
    {
    }

    protected override bool IsRecipientValid(Authorization authorization, PaymentRequirements requirements)
        => authorization.To.IsTheSameAddress(requirements.PayTo) && authorization.To.IsTheSameAddress(Account.Address);

    protected override string RecoverSigner(Authorization authorization, ResolvedRequirement resolved, EthECDSASignature signature)
        => Signer.RecoverReceiveAddress(
            authorization, resolved.TokenName, resolved.TokenVersion, resolved.ChainId, resolved.TokenAddress, signature);

    protected override byte[] ComputeSignedDigest(Authorization authorization, ResolvedRequirement resolved)
        => Signer.ComputeReceiveDigest(
            authorization, resolved.TokenName, resolved.TokenVersion, resolved.ChainId, resolved.TokenAddress);

    protected override Task<TransactionReceipt> SubmitSettlementAsync(
        Eip3009Service eip3009Service, Authorization authorization, EthECDSASignature signature, CancellationTokenSource cancellationTokenSource)
        => eip3009Service.ReceiveWithAuthorizationRequestAndWaitForReceiptAsync(
            BuildFunction(authorization, signature), cancellationTokenSource);

    protected override Task SimulateSettlementAsync(
        Eip3009Service eip3009Service, Authorization authorization, EthECDSASignature signature)
        => eip3009Service.ContractHandler.EstimateGasAsync(BuildFunction(authorization, signature));

    private static ReceiveWithAuthorization1Function BuildFunction(Authorization authorization, EthECDSASignature signature) =>
        new()
        {
            From = authorization.From,
            To = authorization.To,
            Value = BigInteger.Parse(authorization.Value),
            ValidAfter = BigInteger.Parse(authorization.ValidAfter),
            ValidBefore = BigInteger.Parse(authorization.ValidBefore),
            AuthorisationNonce = authorization.Nonce.HexToByteArray(),
            V = signature.V[0],
            R = signature.R,
            S = signature.S
        };
}
