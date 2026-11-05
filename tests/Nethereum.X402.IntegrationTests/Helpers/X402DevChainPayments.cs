using System.Numerics;
using Nethereum.Contracts;
using Nethereum.DevChain;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.X402.Blockchain;
using Nethereum.X402.IntegrationTests.Blockchain;
using Nethereum.X402.Models;
using Nethereum.X402.Signers;

namespace Nethereum.X402.IntegrationTests.Helpers;

public static class X402DevChainPayments
{
    public static PaymentRequirements Requirements(X402DevChainFixture chain, string amount, string payTo) => new()
    {
        Scheme = X402Schemes.Exact,
        Network = Caip2.FormatEip155(X402DevChainFixture.ChainId),
        Amount = amount,
        Asset = chain.TokenAddress,
        PayTo = payTo,
        MaxTimeoutSeconds = 3600,
        Extra = new ExactSchemeExtra { Name = X402DevChainFixture.TokenName, Version = X402DevChainFixture.TokenVersion }
    };

    public static async Task<PaymentPayload> SignedTransfer(
        X402DevChainFixture chain,
        PaymentRequirements requirements,
        BigInteger? validAfter = null,
        BigInteger? validBefore = null)
    {
        var authorization = new TransferWithAuthorisationBuilder().BuildFromPaymentRequirements(
            requirements, X402DevChainFixture.PayerAddress, validAfter, validBefore);
        var signature = await new TransferWithAuthorisationSigner().SignWithPrivateKeyAsync(
            authorization, X402DevChainFixture.TokenName, X402DevChainFixture.TokenVersion,
            X402DevChainFixture.ChainId, chain.TokenAddress, X402DevChainFixture.PayerKey);
        return Payload(requirements, authorization, signature);
    }

    public static async Task<PaymentPayload> SignedWithShortSignatureComponent(
        X402DevChainFixture chain, PaymentRequirements requirements, bool receive)
    {
        var signer = new TransferWithAuthorisationSigner();
        for (var candidate = 1; candidate <= 5000; candidate++)
        {
            var authorization = receive
                ? new ReceiveWithAuthorisationBuilder().BuildFromPaymentRequirements(requirements, X402DevChainFixture.PayerAddress)
                : new TransferWithAuthorisationBuilder().BuildFromPaymentRequirements(requirements, X402DevChainFixture.PayerAddress);
            authorization.Nonce = new BigInteger(candidate).ToByteArray(isUnsigned: true, isBigEndian: true).PadTo32Bytes().ToHex(true);

            var signature = receive
                ? await signer.SignReceiveWithPrivateKeyAsync(
                    authorization, X402DevChainFixture.TokenName, X402DevChainFixture.TokenVersion,
                    X402DevChainFixture.ChainId, chain.TokenAddress, X402DevChainFixture.PayerKey)
                : await signer.SignWithPrivateKeyAsync(
                    authorization, X402DevChainFixture.TokenName, X402DevChainFixture.TokenVersion,
                    X402DevChainFixture.ChainId, chain.TokenAddress, X402DevChainFixture.PayerKey);

            if (signature.R.Length < 32 || signature.S.Length < 32)
                return Payload(requirements, authorization, signature);
        }

        throw new InvalidOperationException("No signature with a leading zero byte in r or s was found in 5000 candidates.");
    }

    public static async Task<PaymentPayload> SignedReceive(X402DevChainFixture chain, PaymentRequirements requirements)
    {
        var authorization = new ReceiveWithAuthorisationBuilder().BuildFromPaymentRequirements(
            requirements, X402DevChainFixture.PayerAddress);
        var signature = await new TransferWithAuthorisationSigner().SignReceiveWithPrivateKeyAsync(
            authorization, X402DevChainFixture.TokenName, X402DevChainFixture.TokenVersion,
            X402DevChainFixture.ChainId, chain.TokenAddress, X402DevChainFixture.PayerKey);
        return Payload(requirements, authorization, signature);
    }

    public static Task<Nethereum.RPC.Eth.DTOs.TransactionReceipt> SetBlocklistedAsync(
        X402DevChainFixture chain, string account, bool blocked) =>
        Admin(chain).SendRequestAndWaitForReceiptAsync(new SetBlocklistedFunction { Account = account, Blocked = blocked });

    public static Task<Nethereum.RPC.Eth.DTOs.TransactionReceipt> SetSuppressTransferEventAsync(
        X402DevChainFixture chain, bool suppress) =>
        Admin(chain).SendRequestAndWaitForReceiptAsync(new SetSuppressTransferEventFunction { Suppress = suppress });

    private static Nethereum.Contracts.ContractHandlers.ContractHandler Admin(X402DevChainFixture chain)
    {
        var account = new Nethereum.Web3.Accounts.Account(X402DevChainFixture.FacilitatorKey, X402DevChainFixture.ChainId);
        return chain.Node.CreateWeb3(account).Eth.GetContractHandler(chain.TokenAddress);
    }

    private static PaymentPayload Payload(PaymentRequirements requirements, Authorization authorization, EthECDSASignature signature) => new()
    {
        X402Version = X402Protocol.Version,
        Accepted = requirements,
        Payload = new ExactSchemePayload
        {
            Authorization = authorization,
            Signature = EthECDSASignature.CreateStringSignature(signature)
        }
    };

    [Function("setBlocklisted")]
    public class SetBlocklistedFunction : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public string Account { get; set; } = string.Empty;

        [Parameter("bool", "blocked", 2)]
        public bool Blocked { get; set; }
    }

    [Function("setSuppressTransferEvent")]
    public class SetSuppressTransferEventFunction : FunctionMessage
    {
        [Parameter("bool", "suppress", 1)]
        public bool Suppress { get; set; }
    }
}
