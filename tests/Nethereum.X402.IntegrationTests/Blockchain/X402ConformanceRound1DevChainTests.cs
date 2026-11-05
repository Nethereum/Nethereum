using System.Numerics;
using System.Text.Json;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Web3.Accounts;
using Nethereum.X402.Blockchain;
using Nethereum.X402.IntegrationTests.Helpers;
using Nethereum.X402.Models;
using Nethereum.X402.Processors;
using Nethereum.X402.Signers;
using Xunit;
using Authorization = Nethereum.X402.Models.Authorization;

namespace Nethereum.X402.IntegrationTests.Blockchain;

[Collection("X402 DevChain E2E")]
public class X402ConformanceRound1DevChainTests : IClassFixture<X402DevChainFixture>
{
    private const int ChainId = X402DevChainFixture.ChainId;
    private const string OtherKey = "0x5de4111afa1a4b94908f83103eb1f1706367c2e68ca870fc3fb9a804cdab365a";
    private const string NoAuthorizationStateToken = "0x000000000000000000000000000000000000beef";

    private static readonly byte[] BalanceOnlyToken =
        "60003560e01c63e94a010214601c57633b9aca0060005260206000f35b600080fd".HexToByteArray();

    private readonly X402DevChainFixture _chain;
    private readonly EthECKey _payerKey = new(X402DevChainFixture.PayerKey.Substring(2));

    public X402ConformanceRound1DevChainTests(X402DevChainFixture chain)
    {
        _chain = chain;
    }

    private Dictionary<int, IClient> Clients(IClient? client = null) => new() { [ChainId] = client ?? _chain.Client };

    private X402TransferWithAuthorisation3009Service TransferService(IClient? client = null) =>
        new(X402DevChainFixture.FacilitatorKey, Clients(client));

    private async Task<PaymentPayload> SignedAs(
        PaymentRequirements requirements, string name, string version, string signerKey = X402DevChainFixture.PayerKey,
        Action<Authorization>? mutate = null)
    {
        var signerAddress = new EthECKey(signerKey.Substring(2)).GetPublicAddress();
        var authorization = new TransferWithAuthorisationBuilder().BuildFromPaymentRequirements(requirements, signerAddress);
        mutate?.Invoke(authorization);
        var signature = await new TransferWithAuthorisationSigner().SignWithPrivateKeyAsync(
            authorization, name, version, ChainId, requirements.Asset, signerKey);
        return new PaymentPayload
        {
            X402Version = 2,
            Accepted = requirements,
            Payload = new ExactSchemePayload { Authorization = authorization, Signature = EthECDSASignature.CreateStringSignature(signature) }
        };
    }

    private PaymentRequirements Requirements(string amount = "1000") =>
        X402DevChainPayments.Requirements(_chain, amount, X402DevChainFixture.RecipientAddress);

    [Fact]
    [Trait("Rule", "K3")]
    public async Task Given_AnAuthorizationValueThatDiffersFromTheRequiredAmount_When_Verifying_Then_TheReferenceAuthorizationValueReasonIsReturned()
    {
        var requirements = Requirements("1000");
        var payload = await SignedAs(requirements, X402DevChainFixture.TokenName, X402DevChainFixture.TokenVersion);
        requirements.Amount = "999";

        var verify = await TransferService().VerifyPaymentAsync(payload, requirements);

        Assert.False(verify.IsValid);
        Assert.Equal("invalid_exact_evm_authorization_value", verify.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "K3")]
    public async Task Given_RequirementsWhoseExtraHasNoNameOrVersion_When_Verifying_Then_TheReferenceMissingEip712DomainReasonIsReturned()
    {
        var service = TransferService();
        var requirements = Requirements();
        var payload = await SignedAs(requirements, X402DevChainFixture.TokenName, X402DevChainFixture.TokenVersion);

        requirements.Extra = new ExactSchemeExtra { Name = null!, Version = null! };
        var withoutNameAndVersion = await service.VerifyPaymentAsync(payload, requirements);
        requirements.Extra = null;
        var withoutExtra = await service.VerifyPaymentAsync(payload, requirements);

        Assert.Equal("invalid_exact_evm_missing_eip712_domain", withoutNameAndVersion.InvalidReason);
        Assert.Equal("invalid_exact_evm_missing_eip712_domain", withoutExtra.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "K3")]
    public async Task Given_ASignatureOverTheRealDomainAndRequirementsNamingAnotherTokenName_When_Verifying_Then_InvalidSignatureIsReturnedWithoutReadingTheTokenDomain()
    {
        var requirements = Requirements();
        var payload = await SignedAs(requirements, X402DevChainFixture.TokenName, X402DevChainFixture.TokenVersion);
        requirements.Extra = new ExactSchemeExtra { Name = "Other Coin", Version = X402DevChainFixture.TokenVersion };

        var verify = await TransferService().VerifyPaymentAsync(payload, requirements);

        Assert.False(verify.IsValid);
        Assert.Equal("invalid_exact_evm_signature", verify.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "K3")]
    public async Task Given_ASignatureOverTheRealDomainAndRequirementsNamingAnotherTokenVersion_When_Verifying_Then_InvalidSignatureIsReturnedWithoutReadingTheTokenDomain()
    {
        var requirements = Requirements();
        var payload = await SignedAs(requirements, X402DevChainFixture.TokenName, X402DevChainFixture.TokenVersion);
        requirements.Extra = new ExactSchemeExtra { Name = X402DevChainFixture.TokenName, Version = "9" };

        var verify = await TransferService().VerifyPaymentAsync(payload, requirements);

        Assert.False(verify.IsValid);
        Assert.Equal("invalid_exact_evm_signature", verify.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "K3")]
    public async Task Given_ASignatureThatRecoversUnderRequirementsNamingAnotherTokenName_When_TheSimulationFails_Then_TokenNameMismatchIsReturned()
    {
        var requirements = Requirements();
        requirements.Extra = new ExactSchemeExtra { Name = "Other Coin", Version = X402DevChainFixture.TokenVersion };
        var payload = await SignedAs(requirements, "Other Coin", X402DevChainFixture.TokenVersion);

        var verify = await TransferService().VerifyPaymentAsync(payload, requirements);

        Assert.False(verify.IsValid);
        Assert.Equal("invalid_exact_evm_token_name_mismatch", verify.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "K3")]
    public async Task Given_ASignatureThatRecoversUnderRequirementsNamingAnotherTokenVersion_When_TheSimulationFails_Then_TokenVersionMismatchIsReturned()
    {
        var requirements = Requirements();
        requirements.Extra = new ExactSchemeExtra { Name = X402DevChainFixture.TokenName, Version = "9" };
        var payload = await SignedAs(requirements, X402DevChainFixture.TokenName, "9");

        var verify = await TransferService().VerifyPaymentAsync(payload, requirements);

        Assert.False(verify.IsValid);
        Assert.Equal("invalid_exact_evm_token_version_mismatch", verify.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "K3")]
    public async Task Given_ASignatureFromAnotherKeyOverTheRealDomain_When_Verifying_Then_InvalidSignatureIsReturned()
    {
        var requirements = Requirements();
        var payload = await SignedAs(requirements, X402DevChainFixture.TokenName, X402DevChainFixture.TokenVersion);
        var forged = await SignedAs(requirements, X402DevChainFixture.TokenName, X402DevChainFixture.TokenVersion, OtherKey);
        ((ExactSchemePayload)payload.Payload).Signature = ((ExactSchemePayload)forged.Payload).Signature;

        var verify = await TransferService().VerifyPaymentAsync(payload, requirements);

        Assert.False(verify.IsValid);
        Assert.Equal("invalid_exact_evm_signature", verify.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "K3")]
    public async Task Given_ATokenWithoutAuthorizationState_When_Verifying_Then_Eip3009NotSupportedIsReturned()
    {
        await _chain.Node.SetCodeAsync(NoAuthorizationStateToken, BalanceOnlyToken);
        var requirements = Requirements();
        requirements.Asset = NoAuthorizationStateToken;
        var payload = await SignedAs(requirements, X402DevChainFixture.TokenName, X402DevChainFixture.TokenVersion);

        var verify = await TransferService().VerifyPaymentAsync(payload, requirements);

        Assert.False(verify.IsValid);
        Assert.Equal("invalid_exact_evm_eip3009_not_supported", verify.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "K3")]
    public async Task Given_ASettlementTransactionThatIsMinedAndReverts_When_Settling_Then_TheReferenceTransactionFailedReasonCarriesTheHash()
    {
        var requirements = Requirements();
        var payload = await SignedAs(requirements, X402DevChainFixture.TokenName, X402DevChainFixture.TokenVersion);
        var service = TransferService(new EstimateGasOverridingRpcClient(_chain.Client, 200_000));
        await X402DevChainPayments.SetBlocklistedAsync(_chain, X402DevChainFixture.PayerAddress, true);
        try
        {
            var settle = await service.SettlePaymentAsync(payload, requirements);

            Assert.False(settle.Success);
            Assert.Equal("invalid_exact_evm_transaction_failed", settle.ErrorReason);
            Assert.False(string.IsNullOrEmpty(settle.Transaction));
        }
        finally
        {
            await X402DevChainPayments.SetBlocklistedAsync(_chain, X402DevChainFixture.PayerAddress, false);
        }
    }

    [Theory]
    [InlineData(64)]
    [InlineData(1)]
    [Trait("Rule", "K3")]
    [Trait("Rule", "K4")]
    [Trait("Rule", "K12")]
    public async Task Given_AnEoaSignatureOf65BytesOrFewer_When_Verifying_Then_InvalidSignatureIsReturnedNeverUnexpectedVerifyError(int length)
    {
        var requirements = Requirements();
        var payload = await SignedAs(requirements, X402DevChainFixture.TokenName, X402DevChainFixture.TokenVersion);
        var exact = (ExactSchemePayload)payload.Payload;
        var bytes = exact.Signature.HexToByteArray();
        exact.Signature = bytes.Concat(new byte[Math.Max(0, length - bytes.Length)]).Take(length).ToArray().ToHex(true);

        var verify = await TransferService().VerifyPaymentAsync(payload, requirements);

        Assert.False(verify.IsValid);
        Assert.Equal("invalid_exact_evm_signature", verify.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "K12")]
    public async Task Given_ASignatureLongerThan65BytesFromAnUndeployedPayer_When_Verifying_Then_UndeployedSmartWalletIsReturned()
    {
        var requirements = Requirements();
        var payload = await SignedAs(requirements, X402DevChainFixture.TokenName, X402DevChainFixture.TokenVersion);
        var exact = (ExactSchemePayload)payload.Payload;
        exact.Signature = exact.Signature.HexToByteArray().Concat(new byte[1]).ToArray().ToHex(true);

        var verify = await TransferService().VerifyPaymentAsync(payload, requirements);

        Assert.False(verify.IsValid);
        Assert.Equal("invalid_exact_evm_payload_undeployed_smart_wallet", verify.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "K12")]
    public async Task Given_ADeployedContractWalletThatRejectsA66ByteSignature_When_Verifying_Then_InvalidSignatureIsReturned()
    {
        var (token, wallet) = await DeployBytesTokenAndWalletAsync(5000);
        var requirements = WalletRequirements(token, X402DevChainFixture.RecipientAddress);
        var payload = WalletSigned(requirements, wallet, receive: false, extraBytes: 1, signerKey: OtherKey);
        Assert.Equal(66, ((ExactSchemePayload)payload.Payload).Signature.HexToByteArray().Length);

        var verify = await TransferService().VerifyPaymentAsync(payload, requirements);

        Assert.False(verify.IsValid);
        Assert.Equal("invalid_exact_evm_signature", verify.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "K3")]
    public async Task Given_A65ByteSignatureWithAnUnrecoverableV_When_Verifying_Then_InvalidSignatureIsReturnedNeverUnexpectedVerifyError()
    {
        var requirements = Requirements();
        var payload = await SignedAs(requirements, X402DevChainFixture.TokenName, X402DevChainFixture.TokenVersion);
        var exact = (ExactSchemePayload)payload.Payload;
        var bytes = exact.Signature.HexToByteArray();
        bytes[64] = 9;
        exact.Signature = bytes.ToHex(true);

        var verify = await TransferService().VerifyPaymentAsync(payload, requirements);

        Assert.False(verify.IsValid);
        Assert.Equal("invalid_exact_evm_signature", verify.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "K8")]
    public async Task Given_ATokenNameMismatch_When_VerifyingAndSettling_Then_TheHumanReadableCauseIsInInvalidMessageAndErrorMessage()
    {
        var requirements = Requirements();
        requirements.Extra = new ExactSchemeExtra { Name = "Other Coin", Version = X402DevChainFixture.TokenVersion };
        var payload = await SignedAs(requirements, "Other Coin", X402DevChainFixture.TokenVersion);
        var service = TransferService();

        var verify = await service.VerifyPaymentAsync(payload, requirements);
        var settle = await service.SettlePaymentAsync(payload, requirements);

        Assert.False(string.IsNullOrWhiteSpace(verify.InvalidMessage));
        Assert.Contains(X402DevChainFixture.TokenName, verify.InvalidMessage);
        Assert.Equal(verify.InvalidMessage, settle.ErrorMessage);
    }

    [Fact]
    [Trait("Rule", "K7")]
    public async Task Given_TheExactSchemeProcessor_When_GettingSupported_Then_EachNetworkAppearsOnceAndNoNullsAreEmitted()
    {
        var processor = new X402ExactSchemeProcessor(
            TransferService(),
            new X402ExactPermit2Service(X402DevChainFixture.FacilitatorKey, Clients()));

        var supported = await processor.GetSupportedAsync();

        var kind = Assert.Single(supported.Kinds);
        Assert.Equal("eip155:31337", kind.Network);
        Assert.Equal("exact", kind.Scheme);
        Assert.Equal(2, kind.X402Version);
        Assert.DoesNotContain("null", JsonSerializer.Serialize(supported));
    }

    [Fact]
    [Trait("Rule", "K7")]
    public void Given_AnExactSchemeExtraWithoutNameOrVersion_When_Serialized_Then_NameAndVersionAreOmitted()
    {
        var json = JsonSerializer.Serialize(new ExactSchemeExtra { AssetTransferMethod = "permit2" });

        Assert.Equal("{\"assetTransferMethod\":\"permit2\"}", json);
    }

    [Function("mint")]
    private class MintFunction : FunctionMessage
    {
        [Parameter("address", "to", 1)]
        public string To { get; set; } = string.Empty;

        [Parameter("uint256", "amount", 2)]
        public BigInteger Amount { get; set; }
    }

    private async Task<(string Token, string Wallet)> DeployBytesTokenAndWalletAsync(BigInteger walletBalance)
    {
        var deployer = new Account(X402DevChainFixture.FacilitatorKey, ChainId);
        var web3 = _chain.Node.CreateWeb3(deployer);
        var tokenReceipt = await web3.Eth.GetContractDeploymentHandler<Eip3009BytesSignatureTokenDeployment>()
            .SendRequestAndWaitForReceiptAsync(new Eip3009BytesSignatureTokenDeployment
            {
                TokenName = X402DevChainFixture.TokenName,
                Version = X402DevChainFixture.TokenVersion,
                Gas = 5_000_000
            });
        var walletReceipt = await web3.Eth.GetContractDeploymentHandler<Erc1271WrappedSignatureWalletDeployment>()
            .SendRequestAndWaitForReceiptAsync(new Erc1271WrappedSignatureWalletDeployment
            {
                Owner = _payerKey.GetPublicAddress(),
                Gas = 2_000_000
            });
        await web3.Eth.GetContractHandler(tokenReceipt.ContractAddress)
            .SendRequestAndWaitForReceiptAsync(new MintFunction { To = walletReceipt.ContractAddress, Amount = walletBalance });
        return (tokenReceipt.ContractAddress, walletReceipt.ContractAddress);
    }

    private async Task<BigInteger> BalanceAsync(string token, string holder) =>
        await _chain.Node.CreateWeb3().Eth.ERC20.GetContractService(token).BalanceOfQueryAsync(holder);

    private PaymentRequirements WalletRequirements(string token, string payTo) => new()
    {
        Scheme = X402Schemes.Exact,
        Network = Caip2.FormatEip155(ChainId),
        Amount = "1000",
        Asset = token,
        PayTo = payTo,
        MaxTimeoutSeconds = 3600,
        Extra = new ExactSchemeExtra { Name = X402DevChainFixture.TokenName, Version = X402DevChainFixture.TokenVersion }
    };

    private PaymentPayload WalletSigned(PaymentRequirements requirements, string wallet, bool receive, int extraBytes, string? signerKey = null)
    {
        var signer = new TransferWithAuthorisationSigner();
        Authorization authorization;
        byte[] digest;
        if (receive)
        {
            authorization = new ReceiveWithAuthorisationBuilder().BuildFromPaymentRequirements(requirements, wallet);
            digest = signer.ComputeReceiveDigest(authorization, X402DevChainFixture.TokenName, X402DevChainFixture.TokenVersion, ChainId, requirements.Asset);
        }
        else
        {
            authorization = new TransferWithAuthorisationBuilder().BuildFromPaymentRequirements(requirements, wallet);
            digest = signer.ComputeTransferDigest(authorization, X402DevChainFixture.TokenName, X402DevChainFixture.TokenVersion, ChainId, requirements.Asset);
        }

        var signature = (signerKey == null ? _payerKey : new EthECKey(signerKey.Substring(2))).SignAndCalculateV(digest);
        var bytes = EthECDSASignature.CreateStringSignature(signature).HexToByteArray().Concat(new byte[extraBytes]).ToArray();
        return new PaymentPayload
        {
            X402Version = 2,
            Accepted = requirements,
            Payload = new ExactSchemePayload { Authorization = authorization, Signature = bytes.ToHex(true) }
        };
    }

    [Fact]
    [Trait("Rule", "K4")]
    public async Task Given_AnErc1271WalletSigningWithAnExtendedSignature_When_VerifyingAndSettling_Then_ItSettlesThroughTheBytesOverload()
    {
        var (token, wallet) = await DeployBytesTokenAndWalletAsync(5000);
        var requirements = WalletRequirements(token, X402DevChainFixture.RecipientAddress);
        var payload = WalletSigned(requirements, wallet, receive: false, extraBytes: 1);
        Assert.Equal(66, ((ExactSchemePayload)payload.Payload).Signature.HexToByteArray().Length);
        var service = TransferService();
        var recipientBefore = await BalanceAsync(token, X402DevChainFixture.RecipientAddress);

        var verify = await service.VerifyPaymentAsync(payload, requirements);
        var settle = await service.SettlePaymentAsync(payload, requirements);

        Assert.True(verify.IsValid, verify.InvalidReason);
        Assert.True(settle.Success, settle.ErrorReason);
        Assert.Equal(recipientBefore + 1000, await BalanceAsync(token, X402DevChainFixture.RecipientAddress));
        Assert.Equal(4000, await BalanceAsync(token, wallet));
    }

    [Fact]
    [Trait("Rule", "K4")]
    public async Task Given_AnErc1271WalletSigningWithAnExtendedSignature_When_TheReceiveServiceSettles_Then_ItSettlesThroughTheReceiveBytesOverload()
    {
        var (token, wallet) = await DeployBytesTokenAndWalletAsync(5000);
        var requirements = WalletRequirements(token, X402DevChainFixture.FacilitatorAddress);
        var payload = WalletSigned(requirements, wallet, receive: true, extraBytes: 3);
        var service = new X402ReceiveWithAuthorisation3009Service(X402DevChainFixture.FacilitatorKey, Clients());

        var verify = await service.VerifyPaymentAsync(payload, requirements);
        var settle = await service.SettlePaymentAsync(payload, requirements);

        Assert.True(verify.IsValid, verify.InvalidReason);
        Assert.True(settle.Success, settle.ErrorReason);
        Assert.Equal(4000, await BalanceAsync(token, wallet));
    }

    [Fact]
    [Trait("Rule", "K4")]
    public async Task Given_AnErc1271WalletWithAnExtendedSignatureAndATokenWhoseBytesOverloadWantsExactly65Bytes_When_Verifying_Then_TheSimulationFailureIsReported()
    {
        var (_, wallet) = await DeployBytesTokenAndWalletAsync(1);
        var payerWeb3 = _chain.Node.CreateWeb3(new Account(X402DevChainFixture.PayerKey, ChainId));
        await payerWeb3.Eth.ERC20.GetContractService(_chain.TokenAddress).TransferRequestAndWaitForReceiptAsync(wallet, 5000);
        var requirements = WalletRequirements(_chain.TokenAddress, X402DevChainFixture.RecipientAddress);
        var payload = WalletSigned(requirements, wallet, receive: false, extraBytes: 1);

        var verify = await TransferService().VerifyPaymentAsync(payload, requirements);

        Assert.False(verify.IsValid);
        Assert.Equal("invalid_exact_evm_transaction_simulation_failed", verify.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "K4")]
    public async Task Given_A65ByteContractSignatureOnATokenWithOnlyTheBytesOverload_When_Verifying_Then_TheVrsPathIsUsedAndTheSimulationFails()
    {
        var (token, wallet) = await DeployBytesTokenAndWalletAsync(5000);
        var requirements = WalletRequirements(token, X402DevChainFixture.RecipientAddress);
        var payload = WalletSigned(requirements, wallet, receive: false, extraBytes: 0);
        Assert.Equal(65, ((ExactSchemePayload)payload.Payload).Signature.HexToByteArray().Length);

        var verify = await TransferService().VerifyPaymentAsync(payload, requirements);

        Assert.False(verify.IsValid);
        Assert.Equal("invalid_exact_evm_transaction_simulation_failed", verify.InvalidReason);
    }
}
