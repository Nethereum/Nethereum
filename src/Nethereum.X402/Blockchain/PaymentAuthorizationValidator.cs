using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Contracts.EIP3009.EIP3009;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Web3;
using Nethereum.X402.Models;
using Nethereum.X402.Signers;

namespace Nethereum.X402.Blockchain;

public class PaymentAuthorizationValidator
{
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
        string rpcUrl,
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

        var web3 = new Nethereum.Web3.Web3(rpcUrl);
        var erc20Service = web3.Eth.ERC20.GetContractService(tokenAddress);

        var balance = await erc20Service.BalanceOfQueryAsync(authorization.From);
        var requiredValue = BigInteger.Parse(authorization.Value);

        if (balance < requiredValue)
        {
            return ValidationResult.Invalid(X402ErrorCodes.InsufficientFunds);
        }

        var eip3009Service = new Eip3009Service(web3, tokenAddress);
        var nonceBytes = authorization.Nonce.HexToByteArray();
        var nonceState = await eip3009Service.AuthorizationStateQueryAsync(
            authorization.From,
            nonceBytes
        );

        if (nonceState)
        {
            return ValidationResult.Invalid(X402ErrorCodes.NonceAlreadyUsed);
        }

        var latestBlock = await web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(
            BlockParameter.CreateLatest()
        );
        var blockTimestamp = (long)latestBlock.Timestamp.Value;

        var validAfter = BigInteger.Parse(authorization.ValidAfter);
        var validBefore = BigInteger.Parse(authorization.ValidBefore);

        if (blockTimestamp < validAfter)
        {
            return ValidationResult.Invalid(X402ErrorCodes.InvalidValidAfter);
        }

        if (blockTimestamp > validBefore)
        {
            return ValidationResult.Invalid(X402ErrorCodes.InvalidValidBefore);
        }

        return ValidationResult.Valid();
    }
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public string InvalidReason { get; set; }

    public static ValidationResult Valid() => new ValidationResult { IsValid = true };

    public static ValidationResult Invalid(string reason) =>
        new ValidationResult { IsValid = false, InvalidReason = reason };
}
