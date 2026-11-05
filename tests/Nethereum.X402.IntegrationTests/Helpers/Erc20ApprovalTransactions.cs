using System.Numerics;
using Nethereum.Contracts;
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.X402.Models;
using Nethereum.X402.Permit2;

namespace Nethereum.X402.IntegrationTests.Helpers;

public sealed record SignedApprovalSpec(
    int ChainId,
    string Token,
    BigInteger Nonce,
    BigInteger MaxFeePerGas,
    BigInteger Gas = default,
    BigInteger Value = default,
    string? Spender = null,
    BigInteger? Amount = null,
    string? Data = null,
    BigInteger? MaxPriorityFeePerGas = null)
{
    public static readonly BigInteger MaxUint256 = BigInteger.Pow(2, 256) - 1;

    public BigInteger EffectiveGas => Gas == default ? 70_000 : Gas;

    public BigInteger EffectiveAmount => Amount ?? MaxUint256;

    public string EffectiveSpender => Spender ?? X402Permit2Addresses.Permit2;
}

public static class Erc20ApprovalTransactions
{
    public static string ApproveData(string spender, BigInteger amount) =>
        new ApproveFunction { Spender = spender, Value = amount }.GetCallData().ToHex(true);

    public static string Sign(EthECKey key, SignedApprovalSpec spec)
    {
        var transaction = new Transaction1559(
            spec.ChainId,
            spec.Nonce,
            spec.MaxPriorityFeePerGas ?? spec.MaxFeePerGas / 10,
            spec.MaxFeePerGas,
            spec.EffectiveGas,
            spec.Token,
            spec.Value,
            spec.Data ?? ApproveData(spec.EffectiveSpender, spec.EffectiveAmount),
            new List<AccessListItem>());
        return "0x" + new Transaction1559Signer().SignTransaction(key, transaction);
    }

    public static string SignLegacy(EthECKey key, SignedApprovalSpec spec) =>
        "0x" + new LegacyTransactionSigner().SignTransaction(
            key.GetPrivateKey(),
            spec.ChainId,
            spec.Token,
            spec.Value,
            spec.Nonce,
            spec.MaxFeePerGas,
            spec.EffectiveGas,
            spec.Data ?? ApproveData(spec.EffectiveSpender, spec.EffectiveAmount));

    public static string Sign2930(EthECKey key, SignedApprovalSpec spec) =>
        "0x" + new TypeTransactionSigner<Transaction2930>().SignTransaction(
            key,
            new Transaction2930(
                spec.ChainId, spec.Nonce, spec.MaxFeePerGas, spec.EffectiveGas, spec.Token, spec.Value,
                spec.Data ?? ApproveData(spec.EffectiveSpender, spec.EffectiveAmount), new List<AccessListItem>()));

    public static string Sign7702(EthECKey key, SignedApprovalSpec spec) =>
        "0x" + new Transaction7702Signer().SignTransaction(
            key,
            new Transaction7702(
                spec.ChainId, spec.Nonce, spec.MaxFeePerGas / 10, spec.MaxFeePerGas, spec.EffectiveGas, spec.Token, spec.Value,
                spec.Data ?? ApproveData(spec.EffectiveSpender, spec.EffectiveAmount), new List<AccessListItem>(),
                new List<Authorisation7702Signed>
                {
                    new(spec.ChainId, spec.Token, 0, new byte[] { 1 }, new byte[] { 1 }, new byte[] { 0 })
                }));

    public static string Sign4844(EthECKey key, SignedApprovalSpec spec)
    {
        var blobHash = new byte[32];
        blobHash[0] = 0x01;
        return "0x" + new Transaction4844Signer().SignTransaction(
            key,
            new Transaction4844(
                spec.ChainId, spec.Nonce, spec.MaxFeePerGas / 10, spec.MaxFeePerGas, spec.EffectiveGas, spec.Token, spec.Value,
                spec.Data ?? ApproveData(spec.EffectiveSpender, spec.EffectiveAmount), new List<AccessListItem>(),
                spec.MaxFeePerGas, new List<byte[]> { blobHash }));
    }

    public static Erc20ApprovalGasSponsoringInfo Info(string payer, SignedApprovalSpec spec, string signedTransaction) => new()
    {
        From = payer,
        Asset = spec.Token,
        Spender = spec.EffectiveSpender,
        Amount = spec.EffectiveAmount.ToString(),
        SignedTransaction = signedTransaction,
        Version = "1"
    };

    public static Dictionary<string, object> Extension(Erc20ApprovalGasSponsoringInfo info) =>
        new() { [X402ExtensionKeys.Erc20ApprovalGasSponsoring] = new Dictionary<string, object> { ["info"] = info } };
}
