using System.Numerics;
using Nethereum.Contracts.EIP3009.EIP3009;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Extensions;
using Nethereum.Signer;
using Nethereum.Testing;
using Nethereum.Util;
using Nethereum.Web3.Accounts;
using Nethereum.X402.Blockchain;
using Nethereum.X402.IntegrationTests.Helpers;
using Nethereum.X402.Models;
using Nethereum.X402.Signers;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Permit2;

[Trait("Category", "LiveNetwork")]
public class X402Eip3009CancelForkTests
{
    private const int ChainId = 84532;
    private const string Network = "eip155:84532";
    private const string Usdc = "0x036CbD53842c5426634e7929541eC2318f3dCF7e";
    private const int UsdcBalanceSlot = 9;
    private static readonly BigInteger Amount = 1_000_000;

    private static string? ForkRpc => TestEnvironment.ServiceOrNull("X402ForkRpc");

    [SkippableFact]
    public async Task Given_RealUsdcOnBaseSepoliaFork_When_TheAuthorizerSignsACancellation_Then_TheNonceIsUsedAndSettlementFails()
    {
        var rpc = ForkRpc;
        Skip.If(string.IsNullOrEmpty(rpc),
            "Services:X402ForkRpc is not set in appsettings.test.local.json; set it to an anvil fork of Base Sepolia to run this test.");

        var facilitatorKey = EthECKey.GenerateKey();
        var payerKey = EthECKey.GenerateKey();
        var facilitatorAccount = new Account(facilitatorKey.GetPrivateKey(), ChainId);
        var payerAddress = payerKey.GetPublicAddress();
        var web3 = new Nethereum.Web3.Web3(facilitatorAccount, rpc);
        await web3.Eth.Anvil().SetBalance.SendRequestAsync(
            facilitatorAccount.Address, new HexBigInteger(BigInteger.Parse("100000000000000000000")));
        var balanceSlot = new HexBigInteger(
            new BigInteger(Sha3Keccack.Current.CalculateHash(
                payerAddress.HexToByteArray().PadTo32Bytes()
                    .Concat(new BigInteger(UsdcBalanceSlot).ToByteArray(isUnsigned: true, isBigEndian: true).PadTo32Bytes())
                    .ToArray()),
                isUnsigned: true, isBigEndian: true));
        await web3.Eth.Anvil().SetStorageAt.SendRequestAsync(
            Usdc, balanceSlot, (Amount * 10).ToByteArray(isUnsigned: true, isBigEndian: true));

        var service = new X402TransferWithAuthorisation3009Service(
            facilitatorKey.GetPrivateKey(), new Dictionary<int, IClient> { [ChainId] = web3.Client });
        var requirements = new PaymentRequirements
        {
            Scheme = X402Schemes.Exact,
            Network = Network,
            Amount = Amount.ToString(),
            Asset = Usdc,
            PayTo = EthECKey.GenerateKey().GetPublicAddress(),
            MaxTimeoutSeconds = 3600,
            Extra = new ExactSchemeExtra { Name = "USDC", Version = "2" }
        };
        var authorization = new TransferWithAuthorisationBuilder().BuildFromPaymentRequirements(requirements, payerAddress);
        var signer = new TransferWithAuthorisationSigner();
        var transferSignature = await signer.SignWithPrivateKeyAsync(
            authorization, "USDC", "2", ChainId, Usdc, payerKey.GetPrivateKey());
        var payload = new PaymentPayload
        {
            X402Version = 2,
            Accepted = requirements,
            Payload = new ExactSchemePayload
            {
                Authorization = authorization,
                Signature = EthECDSASignature.CreateStringSignature(transferSignature)
            }
        };
        var nonce = authorization.Nonce.HexToByteArray();
        var token = new Eip3009Service(web3, Usdc);
        Assert.True((await service.VerifyPaymentAsync(payload, requirements)).IsValid);

#pragma warning disable CS0618
        var zeroSignature = await service.CancelAuthorizationAsync(payerAddress, nonce, Network, Usdc);
#pragma warning restore CS0618
        Assert.False(zeroSignature.Success);
        Assert.False(await token.AuthorizationStateQueryAsync(payerAddress, nonce));

        var cancelSignature = EthECDSASignature.CreateStringSignature(await signer.SignCancelWithPrivateKeyAsync(
            payerAddress, nonce, "USDC", "2", ChainId, Usdc, payerKey.GetPrivateKey()));
        var cancel = await service.CancelAuthorizationAsync(payerAddress, nonce, cancelSignature, Network, Usdc);

        Assert.True(cancel.Success, cancel.ErrorReason);
        Assert.True(await token.AuthorizationStateQueryAsync(payerAddress, nonce));
        Assert.False((await service.VerifyPaymentAsync(payload, requirements)).IsValid);
        Assert.False((await service.SettlePaymentAsync(payload, requirements)).Success);
    }
}
