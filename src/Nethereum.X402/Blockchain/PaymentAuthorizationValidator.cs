using Nethereum.Documentation;
using System;
using Nethereum.Contracts;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Contracts.EIP3009.EIP3009;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Web3;
using Nethereum.X402.Models;
using Nethereum.X402.Signers;

namespace Nethereum.X402.Blockchain;

[NethereumDocExample(DocSection.DeFi, "x402-payments", "PaymentAuthorizationValidator - EIP-3009 signature, funds, nonce and time checks")]
public class PaymentAuthorizationValidator
{
    internal const int BlockTimeBufferSeconds = 6;

    private readonly TransferWithAuthorisationSigner _signer;

    public PaymentAuthorizationValidator()
    {
        _signer = new TransferWithAuthorisationSigner();
    }

    public async Task<ValidationResult> ValidateAsync(
        Authorization authorization,
        string signatureHex,
        string tokenAddress,
        string tokenName,
        string tokenVersion,
        int chainId,
        IClient client,
        CancellationToken cancellationToken = default)
    {
        var signature = EthECDSASignatureFactory.ExtractECDSASignature(signatureHex);

        var recoveredAddress = _signer.RecoverAddress(
            authorization,
            tokenName,
            tokenVersion,
            chainId,
            tokenAddress,
            signature
        );

        if (!authorization.From.IsTheSameAddress(recoveredAddress))
        {
            return ValidationResult.Invalid(X402ErrorCodes.InvalidSignature);
        }

        return await ValidateFundsNonceTimeAsync(authorization, tokenAddress, client, cancellationToken);
    }

    public async Task<ValidationResult> ValidateFundsNonceTimeAsync(
        Authorization authorization,
        string tokenAddress,
        IClient client,
        CancellationToken cancellationToken = default)
    {
        var web3 = new Nethereum.Web3.Web3(client);
        var latestBlock = await web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(
            BlockParameter.CreateLatest()
        );
        var erc20Service = web3.Eth.ERC20.GetContractService(tokenAddress);

        var balance = await erc20Service.BalanceOfQueryAsync(authorization.From);
        var requiredValue = X402Number.Parse(authorization.Value);

        if (balance < requiredValue)
        {
            return ValidationResult.Invalid(X402ErrorCodes.InsufficientFunds);
        }

        var eip3009Service = new Eip3009Service(web3, tokenAddress);
        var nonceBytes = authorization.Nonce.HexToByteArray();
        bool nonceState;
        try
        {
            nonceState = await eip3009Service.AuthorizationStateQueryAsync(
                authorization.From,
                nonceBytes
            );
        }
        catch (Exception ex) when (ex is Nethereum.ABI.FunctionEncoding.SmartContractRevertException or SmartContractCustomErrorRevertException or RpcResponseException)
        {
            return ValidationResult.Invalid(X402ErrorCodes.Eip3009NotSupported);
        }

        if (nonceState)
        {
            return ValidationResult.Invalid(X402ErrorCodes.NonceAlreadyUsed);
        }

        var blockTimestamp = (long)latestBlock.Timestamp.Value;

        var validAfter = X402Number.Parse(authorization.ValidAfter);
        var validBefore = X402Number.Parse(authorization.ValidBefore);

        if (blockTimestamp < validAfter)
        {
            return ValidationResult.Invalid(X402ErrorCodes.InvalidValidAfter);
        }

        if (validBefore <= blockTimestamp + BlockTimeBufferSeconds)
        {
            return ValidationResult.Invalid(X402ErrorCodes.InvalidValidBefore);
        }

        return ValidationResult.Valid(latestBlock.Number.Value);
    }
}

[NethereumDocExample(DocSection.DeFi, "x402-payments", "ValidationResult - the outcome of an authorization check")]
public class ValidationResult
{
    public bool IsValid { get; set; }
    public string InvalidReason { get; set; }

    public BigInteger BlockNumber { get; set; }

    public static ValidationResult Valid() => new ValidationResult { IsValid = true };

    public static ValidationResult Valid(BigInteger blockNumber) =>
        new ValidationResult { IsValid = true, BlockNumber = blockNumber };

    public static ValidationResult Invalid(string reason) =>
        new ValidationResult { IsValid = false, InvalidReason = reason };
}
