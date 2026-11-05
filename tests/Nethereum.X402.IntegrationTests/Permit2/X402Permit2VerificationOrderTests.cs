using System.Numerics;
using Nethereum.ABI.EIP712.Permit2;
using Nethereum.DevChain;
using Nethereum.JsonRpc.Client;
using Nethereum.Signer;
using Nethereum.X402.Blockchain;
using Nethereum.X402.IntegrationTests.Blockchain;
using Nethereum.X402.Models;
using Nethereum.X402.Permit2;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Permit2;

[Collection("X402 DevChain E2E")]
public class X402Permit2VerificationOrderTests : IClassFixture<X402DevChainFixture>
{
    private const string UnapprovedPayerKey = "0x47e179ec197488593b187f80a00eb0da91f1b9d0b13f8733639f19c30a34926a";
    private const string UnrelatedAsset = "0x000000000000000000000000000000000000dEaD";
    private const string Amount = "1000000";

    private readonly X402DevChainFixture _chain;
    private readonly X402ExactPermit2Service _service;

    public X402Permit2VerificationOrderTests(X402DevChainFixture chain)
    {
        _chain = chain;
        _service = new X402ExactPermit2Service(
            X402DevChainFixture.FacilitatorKey,
            new Dictionary<int, IClient> { [X402DevChainFixture.ChainId] = chain.Client });
    }

    [Fact]
    public async Task Given_BadSignatureAndEveryLaterCheckFailing_When_Verifying_Then_SignatureIsReportedFirst()
    {
        var payload = Signed(
            X402DevChainFixture.PayerKey, X402DevChainFixture.PayerAddress, signWithKey: UnapprovedPayerKey,
            amount: "1", deadline: Expired(), validAfter: Future());

        var verify = await _service.VerifyPaymentAsync(payload, Requirements());

        Assert.Equal(X402ErrorCodes.Permit2InvalidSignature, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_NoPermit2AllowanceAndLaterChecksFailing_When_Verifying_Then_AllowanceRequiredIsReportedBeforeBalanceAmountAndDeadline()
    {
        var payload = Signed(UnapprovedPayerKey, new EthECKey(UnapprovedPayerKey.Substring(2)).GetPublicAddress(),
            amount: "99999999999", deadline: Expired());

        var verify = await _service.VerifyPaymentAsync(payload, Requirements());

        Assert.Equal(X402ErrorCodes.Permit2AllowanceRequired, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_PayloadAmountBeyondBalanceAndNotTheRequiredAmount_When_Verifying_Then_InsufficientBalanceIsReportedBeforeAmountMismatch()
    {
        await ApprovePermit2Async();
        var payload = Signed(X402DevChainFixture.PayerKey, X402DevChainFixture.PayerAddress,
            amount: "100000000", deadline: Expired());

        var verify = await _service.VerifyPaymentAsync(payload, Requirements());

        Assert.Equal(X402ErrorCodes.Permit2InsufficientBalance, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_AmountMismatchAndExpiredDeadline_When_Verifying_Then_AmountMismatchIsReportedBeforeDeadline()
    {
        await ApprovePermit2Async();
        var payload = Signed(X402DevChainFixture.PayerKey, X402DevChainFixture.PayerAddress,
            amount: "2000000", deadline: Expired());

        var verify = await _service.VerifyPaymentAsync(payload, Requirements());

        Assert.Equal(X402ErrorCodes.Permit2AmountMismatch, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_ExpiredDeadlineAndFutureValidAfter_When_Verifying_Then_DeadlineIsReportedBeforeValidAfter()
    {
        await ApprovePermit2Async();
        var payload = Signed(X402DevChainFixture.PayerKey, X402DevChainFixture.PayerAddress,
            deadline: Expired(), validAfter: Future());

        var verify = await _service.VerifyPaymentAsync(payload, Requirements(asset: UnrelatedAsset));

        Assert.Equal(X402ErrorCodes.Permit2DeadlineExpired, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_FutureValidAfterAndTokenMismatch_When_Verifying_Then_NotYetValidIsReportedBeforeToken()
    {
        await ApprovePermit2Async();
        var payload = Signed(X402DevChainFixture.PayerKey, X402DevChainFixture.PayerAddress, validAfter: Future());

        var verify = await _service.VerifyPaymentAsync(payload, Requirements(asset: UnrelatedAsset));

        Assert.Equal(X402ErrorCodes.Permit2NotYetValid, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_TokenMismatchAndAcceptedNetworkMismatch_When_Verifying_Then_TokenIsReportedBeforeNetwork()
    {
        await ApprovePermit2Async();
        var payload = Signed(X402DevChainFixture.PayerKey, X402DevChainFixture.PayerAddress, acceptedNetwork: "eip155:1");

        var verify = await _service.VerifyPaymentAsync(payload, Requirements(asset: UnrelatedAsset));

        Assert.Equal(X402ErrorCodes.Permit2TokenMismatch, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_AcceptedNetworkMismatch_When_Verifying_Then_NetworkMismatch()
    {
        await ApprovePermit2Async();
        var payload = Signed(X402DevChainFixture.PayerKey, X402DevChainFixture.PayerAddress, acceptedNetwork: "eip155:1");

        var verify = await _service.VerifyPaymentAsync(payload, Requirements());

        Assert.Equal(X402ErrorCodes.NetworkMismatch, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_EverySpecifiedCheckPassing_When_ProxyIsNotDeployed_Then_ProxyNotDeployedIsReportedLast()
    {
        await ApprovePermit2Async();
        var payload = Signed(X402DevChainFixture.PayerKey, X402DevChainFixture.PayerAddress);

        var verify = await _service.VerifyPaymentAsync(payload, Requirements());

        Assert.Equal(X402ErrorCodes.Permit2ProxyNotDeployed, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_RequirementNamingEip3009_When_PermitServiceVerifies_Then_UnsupportedPayloadType()
    {
        var payload = Signed(X402DevChainFixture.PayerKey, X402DevChainFixture.PayerAddress);
        var requirements = Requirements();
        requirements.Extra = new ExactSchemeExtra { AssetTransferMethod = X402AssetTransferMethods.Eip3009, Name = "USDC", Version = "2" };

        var verify = await _service.VerifyPaymentAsync(payload, requirements);

        Assert.Equal(X402ErrorCodes.UnsupportedPayloadType, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_NonExactScheme_When_PermitServiceVerifies_Then_InvalidScheme()
    {
        var payload = Signed(X402DevChainFixture.PayerKey, X402DevChainFixture.PayerAddress);
        var requirements = Requirements();
        requirements.Scheme = "upto";

        var verify = await _service.VerifyPaymentAsync(payload, requirements);

        Assert.Equal(X402ErrorCodes.InvalidScheme, verify.InvalidReason);
    }

    private PaymentRequirements Requirements(string? asset = null) => new()
    {
        Scheme = X402Schemes.Exact,
        Network = Caip2.FormatEip155(X402DevChainFixture.ChainId),
        Amount = Amount,
        Asset = asset ?? _chain.TokenAddress,
        PayTo = X402DevChainFixture.RecipientAddress,
        MaxTimeoutSeconds = 3600,
        Extra = new ExactSchemeExtra { AssetTransferMethod = X402AssetTransferMethods.Permit2 }
    };

    private PaymentPayload Signed(
        string payerKey,
        string payerAddress,
        string? signWithKey = null,
        string? amount = null,
        BigInteger? deadline = null,
        BigInteger? validAfter = null,
        string? acceptedNetwork = null)
    {
        var authorization = new Permit2Authorization
        {
            From = payerAddress,
            Permitted = new Permit2TokenPermissions { Token = _chain.TokenAddress, Amount = amount ?? Amount },
            Spender = X402Permit2Addresses.ExactPermit2Proxy,
            Nonce = new BigInteger(Guid.NewGuid().ToByteArray(), isUnsigned: true).ToString(),
            Deadline = (deadline ?? Now() + 3600).ToString(),
            Witness = new Permit2WitnessData
            {
                To = X402DevChainFixture.RecipientAddress,
                ValidAfter = (validAfter ?? Now() - 600).ToString()
            }
        };
        var key = new EthECKey((signWithKey ?? payerKey).Substring(2));
        var signature = new Permit2WitnessSigner().Sign(
            authorization.ToWitnessMessage(), X402DevChainFixture.ChainId, X402Permit2Addresses.Permit2, key);

        var accepted = Requirements();
        if (acceptedNetwork != null) accepted.Network = acceptedNetwork;

        return new PaymentPayload
        {
            X402Version = X402Protocol.Version,
            Accepted = accepted,
            Payload = new Permit2SchemePayload { Signature = signature, Permit2Authorization = authorization }
        };
    }

    private async Task ApprovePermit2Async()
    {
        var web3 = _chain.Node.CreateWeb3(new Nethereum.Web3.Accounts.Account(X402DevChainFixture.PayerKey, X402DevChainFixture.ChainId));
        await web3.Eth.ERC20.GetContractService(_chain.TokenAddress)
            .ApproveRequestAndWaitForReceiptAsync(X402Permit2Addresses.Permit2, BigInteger.Pow(2, 200));
    }

    private static BigInteger Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static BigInteger Expired() => Now() - 3600;

    private static BigInteger Future() => Now() + 7200;
}
