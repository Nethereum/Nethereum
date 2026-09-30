using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.X402.Blockchain;
using Nethereum.X402.Models;
using Nethereum.X402.Signers;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Blockchain;

public class X402TransferWithAuthorisation3009ServiceTests
{
    private const string BaseSepoliaRpc = "https://sepolia.base.org";
    private const string UsdcAddress = "0x036CbD53842c5426634e7929541eC2318f3dCF7e";
    private const int ChainId = 84532;

    private const string PayerAddress = "0x819961f93e6C808D932e723290450aA2686979C1";
    private const string RecipientAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";

    private const string FacilitatorPrivateKey = "0x7580e6fc491f1c871f00a0fae31c2224c6aba908e116b8da44ee8cd927b990b0";

    [Fact]
    public async Task VerifyPaymentAsync_WithRecipientMismatch_ReturnsInvalid()
    {
        var service = CreateService();
        var builder = new TransferWithAuthorisationBuilder();

        var requirements = CreatePaymentRequirements("100000");
        var authorization = builder.BuildFromPaymentRequirements(requirements, PayerAddress);

        authorization.To = PayerAddress;

        var paymentPayload = new PaymentPayload
        {
            X402Version = 2,
            Accepted = requirements,
            Payload = new ExactSchemePayload
            {
                Authorization = authorization,
                Signature = "0x" + new string('0', 130)
            }
        };

        var result = await service.VerifyPaymentAsync(paymentPayload, requirements);

        Assert.False(result.IsValid);
        Assert.Equal(X402ErrorCodes.RecipientMismatch, result.InvalidReason);
        Assert.Equal(PayerAddress, result.Payer);
    }

    [Fact]
    public async Task VerifyPaymentAsync_WithAmountBelowRequirement_ReturnsInvalid()
    {
        var service = CreateService();
        var builder = new TransferWithAuthorisationBuilder();

        var requirements = CreatePaymentRequirements("100000");
        var authorization = builder.BuildFromPaymentRequirements(requirements, PayerAddress);

        authorization.Value = "50000";

        var paymentPayload = new PaymentPayload
        {
            X402Version = 2,
            Accepted = requirements,
            Payload = new ExactSchemePayload
            {
                Authorization = authorization,
                Signature = "0x" + new string('0', 130)
            }
        };

        var result = await service.VerifyPaymentAsync(paymentPayload, requirements);

        Assert.False(result.IsValid);
        Assert.Equal(X402ErrorCodes.InvalidValue, result.InvalidReason);
        Assert.Equal(PayerAddress, result.Payer);
    }

    [Fact]
    public async Task VerifyPaymentAsync_WithUnconfiguredNetwork_ReturnsInvalidNetwork()
    {
        var service = CreateService();
        var builder = new TransferWithAuthorisationBuilder();

        var requirements = CreatePaymentRequirements("100000");
        requirements.Network = Caip2.FormatEip155(999999);
        var authorization = builder.BuildFromPaymentRequirements(requirements, PayerAddress);

        var paymentPayload = new PaymentPayload
        {
            X402Version = 2,
            Accepted = requirements,
            Payload = new ExactSchemePayload { Authorization = authorization, Signature = "0x" + new string('0', 130) }
        };

        var result = await service.VerifyPaymentAsync(paymentPayload, requirements);

        Assert.False(result.IsValid);
        Assert.Equal(X402ErrorCodes.InvalidNetwork, result.InvalidReason);
    }

    [Fact]
    public async Task VerifyPaymentAsync_WithMissingExtra_ReturnsInvalidPaymentRequirements()
    {
        var service = CreateService();
        var builder = new TransferWithAuthorisationBuilder();

        var requirements = CreatePaymentRequirements("100000");
        requirements.Extra = null;
        var authorization = builder.BuildFromPaymentRequirements(requirements, PayerAddress);

        var paymentPayload = new PaymentPayload
        {
            X402Version = 2,
            Accepted = requirements,
            Payload = new ExactSchemePayload { Authorization = authorization, Signature = "0x" + new string('0', 130) }
        };

        var result = await service.VerifyPaymentAsync(paymentPayload, requirements);

        Assert.False(result.IsValid);
        Assert.Equal(X402ErrorCodes.InvalidPaymentRequirements, result.InvalidReason);
    }

    [Fact]
    public async Task GetSupportedAsync_ReturnsConfiguredChain()
    {
        var service = CreateService();

        var result = await service.GetSupportedAsync();

        Assert.NotNull(result);
        Assert.NotEmpty(result.Kinds);
        Assert.Contains(result.Kinds, kind =>
            kind.X402Version == X402Protocol.Version &&
            kind.Scheme == "exact" &&
            kind.Network == Caip2.FormatEip155(ChainId)
        );
    }

    private X402TransferWithAuthorisation3009Service CreateService()
    {
        return new X402TransferWithAuthorisation3009Service(
            FacilitatorPrivateKey,
            new Dictionary<int, IClient> { [ChainId] = new RpcClient(new Uri(BaseSepoliaRpc)) }
        );
    }

    private PaymentRequirements CreatePaymentRequirements(string amount)
    {
        return new PaymentRequirements
        {
            Scheme = "exact",
            Network = Caip2.FormatEip155(ChainId),
            Amount = amount,
            Asset = UsdcAddress,
            PayTo = RecipientAddress,
            MaxTimeoutSeconds = 3600,
            Extra = new ExactSchemeExtra { Name = "USDC", Version = "2" }
        };
    }
}
