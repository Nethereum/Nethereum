using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.ABI.EIP712.Permit2;
using Nethereum.DevChain;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Nethereum.X402.AspNetCore;
using Nethereum.X402.Blockchain;
using Nethereum.X402.Facilitator;
using Nethereum.X402.IntegrationTests.AspNetCore;
using Nethereum.X402.IntegrationTests.Blockchain;
using Nethereum.X402.IntegrationTests.Helpers;
using Nethereum.X402.Models;
using Nethereum.X402.Permit2;
using Nethereum.X402.Processors;
using Nethereum.X402.Server;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Permit2;

[Collection("X402 DevChain E2E")]
public class X402Erc20ApprovalGasSponsoringTests : IClassFixture<X402DevChainFixture>
{
    private const string UnfundedPayerKey = "0x47e179ec197488593b187f80a00eb0da91f1b9d0b13f8733639f19c30a34926a";
    private const string Amount = "1000000";
    private const string Key = X402ExtensionKeys.Erc20ApprovalGasSponsoring;
    private const string OtherAddress = "0x000000000000000000000000000000000000dEaD";
    private static readonly BigInteger MaxSponsoredGasCost = BigInteger.Parse("10000000000000000");
    private static readonly BigInteger GoodMaxFee = 1_000_000_000;

    private const string ReferenceDeclaration = """
        {
          "erc20ApprovalGasSponsoring": {
            "info": {
              "description": "The facilitator broadcasts a pre-signed ERC-20 approve() transaction to grant Permit2 allowance.",
              "version": "1"
            },
            "schema": {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "type": "object",
              "properties": {
                "from": { "type": "string", "pattern": "^0x[a-fA-F0-9]{40}$", "description": "The address of the sender (token owner)." },
                "asset": { "type": "string", "pattern": "^0x[a-fA-F0-9]{40}$", "description": "The address of the ERC-20 token contract." },
                "spender": { "type": "string", "pattern": "^0x[a-fA-F0-9]{40}$", "description": "The address of the spender (Canonical Permit2)." },
                "amount": { "type": "string", "pattern": "^[0-9]+$", "description": "The amount approved (uint256). Always MaxUint256." },
                "signedTransaction": { "type": "string", "pattern": "^0x[a-fA-F0-9]+$", "description": "The RLP-encoded signed EIP-1559 transaction as a hex string." },
                "version": { "type": "string", "pattern": "^[0-9]+(\\.[0-9]+)*$", "description": "Schema version identifier." }
              },
              "required": ["from", "asset", "spender", "amount", "signedTransaction", "version"]
            }
          }
        }
        """;

    private readonly X402DevChainFixture _chain;
    private readonly Dictionary<int, IClient> _clients;
    private readonly X402ExactPermit2Service _service;
    private readonly EthECKey _payerKey = new(UnfundedPayerKey.Substring(2));

    public X402Erc20ApprovalGasSponsoringTests(X402DevChainFixture chain)
    {
        _chain = chain;
        _clients = new Dictionary<int, IClient> { [X402DevChainFixture.ChainId] = chain.Client };
        _service = new X402ExactPermit2Service(
            X402DevChainFixture.FacilitatorKey,
            _clients,
            new Erc20ApprovalGasSponsoringOptions { Enabled = true, MaxSponsoredGasCost = MaxSponsoredGasCost });
    }

    [Fact]
    public async Task Given_SponsoringDisabled_When_ApprovalExtensionArrives_Then_Permit2AllowanceRequired()
    {
        var service = new X402ExactPermit2Service(X402DevChainFixture.FacilitatorKey, _clients);
        var payload = Payload(_payerKey, Erc20ApprovalTransactions.Extension(SignedInfo(Spec(await PendingNonceAsync(_payerKey)))));

        var verify = await service.VerifyPaymentAsync(payload, Requirements());

        Assert.Equal(X402ErrorCodes.Permit2AllowanceRequired, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_OptionsPresentButNotEnabled_When_ApprovalExtensionArrives_Then_Permit2AllowanceRequired()
    {
        var service = new X402ExactPermit2Service(
            X402DevChainFixture.FacilitatorKey, _clients, new Erc20ApprovalGasSponsoringOptions());
        var payload = Payload(_payerKey, Erc20ApprovalTransactions.Extension(SignedInfo(Spec(await PendingNonceAsync(_payerKey)))));

        var verify = await service.VerifyPaymentAsync(payload, Requirements());

        Assert.Equal(X402ErrorCodes.Permit2AllowanceRequired, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_SponsoringDisabled_When_AskingWhatIsSupported_Then_TheErc20ExtensionIsNotListed()
    {
        var service = new X402ExactPermit2Service(X402DevChainFixture.FacilitatorKey, _clients);

        var supported = await service.GetSupportedAsync();

        Assert.DoesNotContain(Key, supported.Extensions);
        Assert.Contains(X402ExtensionKeys.Eip2612GasSponsoring, supported.Extensions);
    }

    [Fact]
    public async Task Given_SponsoringEnabled_When_AskingWhatIsSupported_Then_TheErc20ExtensionIsListed()
    {
        var supported = await _service.GetSupportedAsync();

        Assert.Contains(Key, supported.Extensions);
        Assert.Contains(X402ExtensionKeys.Eip2612GasSponsoring, supported.Extensions);
    }

    [Fact]
    public async Task Given_SponsoringEnabled_When_TheExactSchemeProcessorIsAskedWhatIsSupported_Then_TheExtensionIsListedOnce()
    {
        var eip3009 = new X402TransferWithAuthorisation3009Service(X402DevChainFixture.FacilitatorKey, _clients);
        var processor = new X402ExactSchemeProcessor(eip3009, _service);

        var supported = await processor.GetSupportedAsync();

        Assert.Single(supported.Extensions, Key);
    }

    [Fact]
    public void Given_SponsoringEnabledWithoutAGasCostCap_When_TheServiceIsCreated_Then_ItIsRejected()
    {
        var options = new Erc20ApprovalGasSponsoringOptions { Enabled = true };

        Assert.Throws<InvalidOperationException>(() =>
            new X402ExactPermit2Service(X402DevChainFixture.FacilitatorKey, _clients, options));
    }

    [Fact]
    public void Given_SponsoringEnabledWithANegativeGasCostCap_When_Validated_Then_ItIsRejected()
    {
        var options = new Erc20ApprovalGasSponsoringOptions { Enabled = true, MaxSponsoredGasCost = -1 };

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void Given_SponsoringDisabledWithoutAGasCostCap_When_Validated_Then_ItIsAccepted()
    {
        new Erc20ApprovalGasSponsoringOptions().Validate();
    }

    [Fact]
    public void Given_TheDeclarationHelper_When_Serialized_Then_ItEqualsTheReferenceDeclaration()
    {
        var declaration = new Dictionary<string, object> { [Key] = X402GasSponsoringExtensions.DeclareErc20Approval() };

        var actual = JsonSerializer.SerializeToNode(declaration);

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(ReferenceDeclaration), actual), actual!.ToJsonString());
    }

    [Fact]
    public async Task Given_ARouteWithTheDeclaration_When_NoPaymentIsSent_Then_ThePaymentRequiredServesTheExtension()
    {
        var route = new RoutePaymentConfig("/api/premium", Requirements())
        {
            Extensions = new Dictionary<string, object> { [Key] = X402GasSponsoringExtensions.DeclareErc20Approval() }
        };
        var builder = new WebHostBuilder()
            .ConfigureServices(services => services.AddSingleton<IFacilitatorClient>(new MockFacilitatorClient()))
            .Configure(app =>
            {
                app.UseX402(options =>
                {
                    options.FacilitatorUrl = "https://facilitator.test";
                    options.Routes.Add(route);
                });
                app.Run(context => context.Response.WriteAsync("premium"));
            });
        using var server = new TestServer(builder);

        var response = await server.CreateClient().GetAsync("/api/premium");

        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(ReferenceDeclaration)![Key], body["extensions"]![Key]));
    }

    [Fact]
    public async Task Given_NoAllowanceAndAnIncompleteApprovalExtension_When_Verifying_Then_Permit2AllowanceRequired()
    {
        var info = SignedInfo(Spec(await PendingNonceAsync(_payerKey)));
        info.SignedTransaction = "";

        var verify = await _service.VerifyPaymentAsync(Payload(_payerKey, Erc20ApprovalTransactions.Extension(info)), Requirements());

        Assert.Equal(X402ErrorCodes.Permit2AllowanceRequired, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_AnApprovalInfo_WithBadFormat_When_Verifying_Then_InvalidErc20ApprovalExtensionFormat()
    {
        var info = SignedInfo(Spec(await PendingNonceAsync(_payerKey)));
        info.SignedTransaction = "not-hex";

        var verify = await _service.VerifyPaymentAsync(Payload(_payerKey, Erc20ApprovalTransactions.Extension(info)), Requirements());

        Assert.Equal(X402ErrorCodes.InvalidErc20ApprovalExtensionFormat, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_AnApprovalInfo_WithANonNumericAmount_When_Verifying_Then_InvalidErc20ApprovalExtensionFormat()
    {
        var info = SignedInfo(Spec(await PendingNonceAsync(_payerKey)));
        info.Amount = "1e6";

        var verify = await _service.VerifyPaymentAsync(Payload(_payerKey, Erc20ApprovalTransactions.Extension(info)), Requirements());

        Assert.Equal(X402ErrorCodes.InvalidErc20ApprovalExtensionFormat, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_AnApprovalInfo_WithFromMismatch_When_Verifying_Then_Erc20ApprovalFromMismatch()
    {
        var info = SignedInfo(Spec(await PendingNonceAsync(_payerKey)));
        info.From = OtherAddress;

        var verify = await _service.VerifyPaymentAsync(Payload(_payerKey, Erc20ApprovalTransactions.Extension(info)), Requirements());

        Assert.Equal(X402ErrorCodes.Erc20ApprovalFromMismatch, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_AnApprovalInfo_WithAssetMismatch_When_Verifying_Then_Erc20ApprovalAssetMismatch()
    {
        var info = SignedInfo(Spec(await PendingNonceAsync(_payerKey)));
        info.Asset = OtherAddress;

        var verify = await _service.VerifyPaymentAsync(Payload(_payerKey, Erc20ApprovalTransactions.Extension(info)), Requirements());

        Assert.Equal(X402ErrorCodes.Erc20ApprovalAssetMismatch, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_AnApprovalInfo_WithSpenderOtherThanPermit2_When_Verifying_Then_Erc20ApprovalSpenderNotPermit2()
    {
        var info = SignedInfo(Spec(await PendingNonceAsync(_payerKey)));
        info.Spender = X402Permit2Addresses.ExactPermit2Proxy;

        var verify = await _service.VerifyPaymentAsync(Payload(_payerKey, Erc20ApprovalTransactions.Extension(info)), Requirements());

        Assert.Equal(X402ErrorCodes.Erc20ApprovalSpenderNotPermit2, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_AnApprovalTx_WhichCannotBeDecoded_When_Verifying_Then_Erc20ApprovalTxParseFailed()
    {
        var info = SignedInfo(Spec(await PendingNonceAsync(_payerKey)));
        info.SignedTransaction = "0x02c0";

        var verify = await _service.VerifyPaymentAsync(Payload(_payerKey, Erc20ApprovalTransactions.Extension(info)), Requirements());

        Assert.Equal(X402ErrorCodes.Erc20ApprovalTxParseFailed, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_AnApprovalTx_ToAnotherContract_When_Verifying_Then_Erc20ApprovalTxWrongTarget()
    {
        var spec = Spec(await PendingNonceAsync(_payerKey)) with { Token = OtherAddress };

        var verify = await VerifyApprovalAsync(spec, assetOfInfo: _chain.TokenAddress);

        Assert.Equal(X402ErrorCodes.Erc20ApprovalTxWrongTarget, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_AnApprovalTx_WithAnotherSelector_When_Verifying_Then_Erc20ApprovalTxWrongSelector()
    {
        var spec = Spec(await PendingNonceAsync(_payerKey)) with
        {
            Data = "0xa9059cbb" + X402Permit2Addresses.Permit2.Substring(2).PadLeft(64, '0') + "1".PadLeft(64, '0')
        };

        var verify = await VerifyApprovalAsync(spec);

        Assert.Equal(X402ErrorCodes.Erc20ApprovalTxWrongSelector, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_AnApprovalTx_WithTruncatedCalldata_When_Verifying_Then_Erc20ApprovalTxInvalidCalldata()
    {
        var spec = Spec(await PendingNonceAsync(_payerKey)) with { Data = "0x095ea7b3" };

        var verify = await VerifyApprovalAsync(spec);

        Assert.Equal(X402ErrorCodes.Erc20ApprovalTxInvalidCalldata, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_AnApprovalTx_ApprovingAnotherSpender_When_Verifying_Then_Erc20ApprovalTxWrongSpender()
    {
        var spec = Spec(await PendingNonceAsync(_payerKey)) with { Spender = OtherAddress };

        var verify = await VerifyApprovalAsync(spec, spenderOfInfo: X402Permit2Addresses.Permit2);

        Assert.Equal(X402ErrorCodes.Erc20ApprovalTxWrongSpender, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_AnApprovalTx_WhoseAmountDiffersFromTheInfo_When_Verifying_Then_Erc20ApprovalTxWrongAmount()
    {
        var spec = Spec(await PendingNonceAsync(_payerKey));

        var verify = await VerifyApprovalAsync(spec, amountOfInfo: (SignedApprovalSpec.MaxUint256 - 1).ToString());

        Assert.Equal(X402ErrorCodes.Erc20ApprovalTxWrongAmount, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_AnApprovalTx_SignedByAnotherKey_When_Verifying_Then_Erc20ApprovalTxSignerMismatch()
    {
        var spec = Spec(await PendingNonceAsync(_payerKey));

        var verify = await VerifyApprovalAsync(spec, signer: EthECKey.GenerateKey());

        Assert.Equal(X402ErrorCodes.Erc20ApprovalTxSignerMismatch, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_AnApprovalTx_ForAnotherChain_When_Verifying_Then_Erc20ApprovalTxChainIdMismatch()
    {
        var spec = Spec(await PendingNonceAsync(_payerKey)) with { ChainId = 1 };

        var verify = await VerifyApprovalAsync(spec);

        Assert.Equal(X402ErrorCodes.Erc20ApprovalTxChainIdMismatch, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_AnApprovalTx_CarryingValue_When_Verifying_Then_Erc20ApprovalTxValueNotZero()
    {
        var spec = Spec(await PendingNonceAsync(_payerKey)) with { Value = 1 };

        var verify = await VerifyApprovalAsync(spec);

        Assert.Equal(X402ErrorCodes.Erc20ApprovalTxValueNotZero, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_AnApprovalTx_WhoseGasCostExceedsTheCap_When_Verifying_Then_Erc20ApprovalTxGasCostExceedsCap()
    {
        var spec = Spec(await PendingNonceAsync(_payerKey)) with { Gas = 70_000 };
        var service = new X402ExactPermit2Service(
            X402DevChainFixture.FacilitatorKey,
            _clients,
            new Erc20ApprovalGasSponsoringOptions { Enabled = true, MaxSponsoredGasCost = 70_000 * spec.MaxFeePerGas - 1 });
        var payload = Payload(_payerKey, Erc20ApprovalTransactions.Extension(SignedInfo(spec)));

        var verify = await service.VerifyPaymentAsync(payload, Requirements());

        Assert.Equal(X402ErrorCodes.Erc20ApprovalTxGasCostExceedsCap, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_AnApprovalTx_WhoseGasCostEqualsTheCap_When_Verifying_Then_TheCapIsNotExceeded()
    {
        var spec = Spec(await PendingNonceAsync(_payerKey)) with { Gas = 70_000 };
        var service = new X402ExactPermit2Service(
            X402DevChainFixture.FacilitatorKey,
            _clients,
            new Erc20ApprovalGasSponsoringOptions { Enabled = true, MaxSponsoredGasCost = 70_000 * spec.MaxFeePerGas });
        var payload = Payload(_payerKey, Erc20ApprovalTransactions.Extension(SignedInfo(spec)));

        var verify = await service.VerifyPaymentAsync(payload, Requirements());

        Assert.Equal(X402ErrorCodes.Permit2InsufficientBalance, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_AnApprovalTx_ApprovingLessThanTheRequirement_When_Verifying_Then_Erc20ApprovalAmountBelowRequired()
    {
        var spec = Spec(await PendingNonceAsync(_payerKey)) with { Amount = BigInteger.Parse(Amount) - 1 };

        var verify = await VerifyApprovalAsync(spec);

        Assert.Equal(X402ErrorCodes.Erc20ApprovalAmountBelowRequired, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_AnApprovalTx_ApprovingExactlyTheRequirement_When_Verifying_Then_TheAmountRuleAccepts()
    {
        var spec = Spec(await PendingNonceAsync(_payerKey)) with { Amount = BigInteger.Parse(Amount) };

        var verify = await VerifyApprovalAsync(spec);

        Assert.Equal(X402ErrorCodes.Permit2InsufficientBalance, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_AnApprovalTx_WithAStaleNonce_When_Verifying_Then_Erc20ApprovalTxNonceMismatch()
    {
        var spec = Spec(await PendingNonceAsync(_payerKey) + 1);

        var verify = await VerifyApprovalAsync(spec);

        Assert.Equal(X402ErrorCodes.Erc20ApprovalTxNonceMismatch, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_AnApprovalTx_WithAMaxFeeBelowTheBaseFee_When_Verifying_Then_Erc20ApprovalTxMaxFeeBelowBaseFee()
    {
        var spec = Spec(await PendingNonceAsync(_payerKey)) with { MaxFeePerGas = 1 };

        var verify = await VerifyApprovalAsync(spec);

        Assert.Equal(X402ErrorCodes.Erc20ApprovalTxMaxFeeBelowBaseFee, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_AValidApprovalTx_And_NoTokenBalance_When_Verifying_Then_Permit2InsufficientBalance()
    {
        var verify = await VerifyApprovalAsync(Spec(await PendingNonceAsync(_payerKey)));

        Assert.Equal(X402ErrorCodes.Permit2InsufficientBalance, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_AValidApprovalTx_And_AFundedPayerWithoutTheProxy_When_Verifying_Then_Permit2ProxyNotDeployed()
    {
        var fundedKey = new EthECKey(X402DevChainFixture.PayerKey.Substring(2));
        var spec = Spec(await PendingNonceAsync(fundedKey));

        var verify = await VerifyApprovalAsync(spec, payerKey: fundedKey);

        Assert.Equal(X402ErrorCodes.Permit2ProxyNotDeployed, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_AnApprovalInfoOverTheWire_When_Verifying_Then_TheSameRejectionIsReported()
    {
        var info = SignedInfo(Spec(await PendingNonceAsync(_payerKey)));
        info.From = OtherAddress;
        var payload = Payload(_payerKey, Erc20ApprovalTransactions.Extension(info));
        var wire = JsonSerializer.Deserialize<PaymentPayload>(JsonSerializer.Serialize(payload))!;

        var verify = await _service.VerifyPaymentAsync(wire, Requirements());

        Assert.Equal(X402ErrorCodes.Erc20ApprovalFromMismatch, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_BothExtensions_And_AnInvalidEip2612Info_When_Verifying_Then_TheEip2612RejectionWinsOverAValidApproval()
    {
        var extensions = Erc20ApprovalTransactions.Extension(SignedInfo(Spec(await PendingNonceAsync(_payerKey))));
        extensions[X402ExtensionKeys.Eip2612GasSponsoring] = new Dictionary<string, object>
        {
            ["info"] = new Eip2612GasSponsoringInfo
            {
                From = OtherAddress,
                Asset = _chain.TokenAddress,
                Spender = X402Permit2Addresses.Permit2,
                Amount = Amount,
                Nonce = "0",
                Deadline = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600).ToString(),
                Signature = "0x" + new string('1', 130),
                Version = "1"
            }
        };

        var verify = await _service.VerifyPaymentAsync(Payload(_payerKey, extensions), Requirements());

        Assert.Equal(X402ErrorCodes.Eip2612FromMismatch, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_BothExtensions_And_AnIncompleteEip2612Info_When_Verifying_Then_TheApprovalIsValidated()
    {
        var info = SignedInfo(Spec(await PendingNonceAsync(_payerKey)));
        info.From = OtherAddress;
        var extensions = Erc20ApprovalTransactions.Extension(info);
        extensions[X402ExtensionKeys.Eip2612GasSponsoring] = new Dictionary<string, object>
        {
            ["info"] = new Eip2612GasSponsoringInfo { From = OtherAddress }
        };

        var verify = await _service.VerifyPaymentAsync(Payload(_payerKey, extensions), Requirements());

        Assert.Equal(X402ErrorCodes.Erc20ApprovalFromMismatch, verify.InvalidReason);
    }

    private SignedApprovalSpec Spec(BigInteger nonce) => new(
        X402DevChainFixture.ChainId, _chain.TokenAddress, nonce, GoodMaxFee);

    private async Task<BigInteger> PendingNonceAsync(EthECKey key)
    {
        var web3 = _chain.Node.CreateWeb3();
        var count = await web3.Eth.Transactions.GetTransactionCount.SendRequestAsync(
            key.GetPublicAddress(), BlockParameter.CreatePending());
        return count.Value;
    }

    private Erc20ApprovalGasSponsoringInfo SignedInfo(SignedApprovalSpec spec) =>
        Erc20ApprovalTransactions.Info(
            _payerKey.GetPublicAddress(), spec, Erc20ApprovalTransactions.Sign(_payerKey, spec));

    private async Task<VerificationResponse> VerifyApprovalAsync(
        SignedApprovalSpec spec,
        EthECKey? payerKey = null,
        EthECKey? signer = null,
        string? assetOfInfo = null,
        string? spenderOfInfo = null,
        string? amountOfInfo = null)
    {
        payerKey ??= _payerKey;
        var info = Erc20ApprovalTransactions.Info(
            payerKey.GetPublicAddress(), spec, Erc20ApprovalTransactions.Sign(signer ?? payerKey, spec));
        info.Asset = assetOfInfo ?? info.Asset;
        info.Spender = spenderOfInfo ?? info.Spender;
        info.Amount = amountOfInfo ?? info.Amount;

        return await _service.VerifyPaymentAsync(
            Payload(payerKey, Erc20ApprovalTransactions.Extension(info)), Requirements());
    }

    private PaymentRequirements Requirements() => new()
    {
        Scheme = X402Schemes.Exact,
        Network = Caip2.FormatEip155(X402DevChainFixture.ChainId),
        Amount = Amount,
        Asset = _chain.TokenAddress,
        PayTo = X402DevChainFixture.RecipientAddress,
        MaxTimeoutSeconds = 3600,
        Extra = new ExactSchemeExtra { AssetTransferMethod = X402AssetTransferMethods.Permit2, Name = "USDC", Version = "2" }
    };

    private PaymentPayload Payload(EthECKey payerKey, Dictionary<string, object>? extensions)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var authorization = new Permit2Authorization
        {
            From = payerKey.GetPublicAddress(),
            Permitted = new Permit2TokenPermissions { Token = _chain.TokenAddress, Amount = Amount },
            Spender = X402Permit2Addresses.ExactPermit2Proxy,
            Nonce = new BigInteger(Guid.NewGuid().ToByteArray(), isUnsigned: true).ToString(),
            Deadline = (now + 3600).ToString(),
            Witness = new Permit2WitnessData
            {
                To = X402DevChainFixture.RecipientAddress,
                ValidAfter = (now - 600).ToString()
            }
        };
        var signature = new Permit2WitnessSigner().Sign(
            authorization.ToWitnessMessage(),
            X402DevChainFixture.ChainId,
            X402Permit2Addresses.Permit2,
            payerKey);

        return new PaymentPayload
        {
            X402Version = X402Protocol.Version,
            Accepted = Requirements(),
            Payload = new Permit2SchemePayload { Signature = signature, Permit2Authorization = authorization },
            Extensions = extensions
        };
    }
}
