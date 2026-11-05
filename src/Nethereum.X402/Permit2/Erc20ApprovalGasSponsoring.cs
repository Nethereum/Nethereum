using System.Numerics;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Contracts;
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.X402.Models;

namespace Nethereum.X402.Permit2;

internal static class Erc20ApprovalGasSponsoring
{
    private const int ApproveCalldataLength = 4 + 32 + 32;

    public static readonly BigInteger MaxApprovalGasLimit = 16_777_216;

    private static readonly BigInteger HalfCurveOrder = BigInteger.Parse(
        "7FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF5D576E7357A4501DDFE92F46681B20A0",
        System.Globalization.NumberStyles.HexNumber);

    public static BigInteger MinimumMaxFeePerGas(BigInteger baseFee) => (baseFee * 9 + 7) / 8;

    public static readonly BigInteger ReferenceApprovalGasLimit = 70_000;

    public static readonly BigInteger MinimumPriorityFeeCeiling = 100_000_000;

    public static readonly BigInteger MinimumMaxFeeCeiling = 1_000_000_000;

    public static BigInteger MaximumGasLimit(BigInteger estimate) =>
        BigInteger.Max(ReferenceApprovalGasLimit, (estimate * 3 + 1) / 2);

    public static BigInteger MaximumPriorityFee(BigInteger suggestedPriorityFee) =>
        BigInteger.Max(suggestedPriorityFee * 2, MinimumPriorityFeeCeiling);

    public static BigInteger MaximumMaxFeePerGas(BigInteger baseFee, BigInteger maxPriorityFeePerGas) =>
        BigInteger.Max(baseFee * 3 + maxPriorityFeePerGas, MinimumMaxFeeCeiling);

    public readonly record struct SignedApproval(BigInteger Nonce, BigInteger MaxFeePerGas, BigInteger GasCost, BigInteger GasLimit, BigInteger MaxPriorityFeePerGas);

    public readonly record struct Inspection(string? Rejection, SignedApproval Approval);

    public static Erc20ApprovalGasSponsoringInfo? Extract(object? extensions)
    {
        if (!X402ExtensionInfoReader.TryGetInfo(extensions, X402ExtensionKeys.Erc20ApprovalGasSponsoring, out var info))
            return null;

        var parsed = new Erc20ApprovalGasSponsoringInfo
        {
            From = X402ExtensionInfoReader.ReadString(info, "from")!,
            Asset = X402ExtensionInfoReader.ReadString(info, "asset")!,
            Spender = X402ExtensionInfoReader.ReadString(info, "spender")!,
            Amount = X402ExtensionInfoReader.ReadString(info, "amount")!,
            SignedTransaction = X402ExtensionInfoReader.ReadString(info, "signedTransaction")!,
            Version = X402ExtensionInfoReader.ReadString(info, "version")!
        };

        return string.IsNullOrEmpty(parsed.From) || string.IsNullOrEmpty(parsed.Asset) ||
               string.IsNullOrEmpty(parsed.Spender) || string.IsNullOrEmpty(parsed.Amount) ||
               string.IsNullOrEmpty(parsed.SignedTransaction) || string.IsNullOrEmpty(parsed.Version)
            ? null
            : parsed;
    }

    public static bool HasValidFormat(Erc20ApprovalGasSponsoringInfo info) =>
        X402GasSponsoringFormat.IsAddress(info.From) &&
        X402GasSponsoringFormat.IsAddress(info.Asset) &&
        X402GasSponsoringFormat.IsAddress(info.Spender) &&
        X402GasSponsoringFormat.IsNumber(info.Amount) &&
        X402GasSponsoringFormat.IsHex(info.SignedTransaction) &&
        X402GasSponsoringFormat.IsVersion(info.Version);

    public static Inspection Inspect(
        Erc20ApprovalGasSponsoringInfo info,
        string payer,
        string token,
        int chainId,
        BigInteger requiredAmount,
        BigInteger maxSponsoredGasCost)
    {
        var rejection = ValidateFields(info, payer, token);
        if (rejection != null)
            return Rejected(rejection);

        ISignedTransaction transaction;
        try
        {
            transaction = TransactionFactory.CreateTransaction(info.SignedTransaction);
        }
        catch (Exception)
        {
            return Rejected(X402ErrorCodes.Erc20ApprovalTxParseFailed);
        }

        if (transaction.TransactionType != TransactionType.EIP1559)
            return Rejected(X402ErrorCodes.Erc20ApprovalTxUnsupportedType);

        var target = transaction.GetReceiverAddress();
        if (string.IsNullOrEmpty(target) || !target.IsTheSameAddress(token))
            return Rejected(X402ErrorCodes.Erc20ApprovalTxWrongTarget);

        var dataBytes = transaction.GetData() ?? Array.Empty<byte>();
        var data = dataBytes.ToHex(true);
        if (!SignatureEncoder.IsDataForSignature(ABITypedRegistry.GetFunctionABI<ApproveFunction>().Sha3Signature, data))
            return Rejected(X402ErrorCodes.Erc20ApprovalTxWrongSelector);

        if (dataBytes.Length < ApproveCalldataLength)
            return Rejected(X402ErrorCodes.Erc20ApprovalTxInvalidCalldata);

        for (var i = 4; i < 16; i++)
        {
            if (dataBytes[i] != 0)
                return Rejected(X402ErrorCodes.Erc20ApprovalTxInvalidCalldata);
        }

        ApproveFunction approve;
        try
        {
            approve = new ApproveFunction().DecodeInput(data);
        }
        catch (Exception)
        {
            return Rejected(X402ErrorCodes.Erc20ApprovalTxInvalidCalldata);
        }

        if (!approve.Spender.IsTheSameAddress(X402Permit2Addresses.Permit2))
            return Rejected(X402ErrorCodes.Erc20ApprovalTxWrongSpender);

        if (approve.Value != X402Number.Parse(info.Amount))
            return Rejected(X402ErrorCodes.Erc20ApprovalTxWrongAmount);

        string signer;
        try
        {
            signer = transaction.GetSenderAddress();
        }
        catch (Exception)
        {
            return Rejected(X402ErrorCodes.Erc20ApprovalTxInvalidSignature);
        }

        if (string.IsNullOrEmpty(signer))
            return Rejected(X402ErrorCodes.Erc20ApprovalTxInvalidSignature);

        if (!HasLowS(transaction))
            return Rejected(X402ErrorCodes.Erc20ApprovalTxInvalidSignature);

        if (!signer.IsTheSameAddress(payer))
            return Rejected(X402ErrorCodes.Erc20ApprovalTxSignerMismatch);

        if (!transaction.DeclaresItsOwnChainId() || (BigInteger)transaction.GetChainId() != chainId)
            return Rejected(X402ErrorCodes.Erc20ApprovalTxChainIdMismatch);

        if (!transaction.GetValue().IsZero)
            return Rejected(X402ErrorCodes.Erc20ApprovalTxValueNotZero);

        var maxPriorityFeePerGas = (BigInteger)transaction.GetMaxPriorityFeePerGas();
        var maxFeePerGas = (BigInteger)transaction.GetMaxFeePerGas();
        var gasCost = (BigInteger)transaction.GetGasLimit() * maxFeePerGas;
        if (gasCost > maxSponsoredGasCost)
            return Rejected(X402ErrorCodes.Erc20ApprovalTxGasCostExceedsCap);

        if (approve.Value < requiredAmount)
            return Rejected(X402ErrorCodes.Erc20ApprovalAmountBelowRequired);

        return new Inspection(null, new SignedApproval((BigInteger)transaction.GetNonce(), maxFeePerGas, gasCost, (BigInteger)transaction.GetGasLimit(), maxPriorityFeePerGas));
    }

    private static bool HasLowS(ISignedTransaction transaction)
    {
        var s = transaction.Signature?.S;
        return s != null && new BigInteger(s, isUnsigned: true, isBigEndian: true) <= HalfCurveOrder;
    }

    private static string? ValidateFields(Erc20ApprovalGasSponsoringInfo info, string payer, string token)
    {
        if (!HasValidFormat(info))
            return X402ErrorCodes.InvalidErc20ApprovalExtensionFormat;

        if (!info.From.IsTheSameAddress(payer))
            return X402ErrorCodes.Erc20ApprovalFromMismatch;

        if (!info.Asset.IsTheSameAddress(token))
            return X402ErrorCodes.Erc20ApprovalAssetMismatch;

        if (!info.Spender.IsTheSameAddress(X402Permit2Addresses.Permit2))
            return X402ErrorCodes.Erc20ApprovalSpenderNotPermit2;

        return null;
    }

    private static Inspection Rejected(string rejection) => new(rejection, default);
}
