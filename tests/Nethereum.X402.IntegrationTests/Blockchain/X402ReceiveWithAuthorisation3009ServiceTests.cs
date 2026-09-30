using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.X402.Blockchain;
using Nethereum.X402.Models;
using Nethereum.X402.Signers;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Blockchain;

public class X402ReceiveWithAuthorisation3009ServiceTests
{
    private const string BaseSepoliaRpc = "https://sepolia.base.org";
    private const int ChainId = 84532;

    private const string PayerAddress = "0x819961f93e6C808D932e723290450aA2686979C1";

    private const string ReceiverPrivateKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
    private const string ReceiverAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";

    [Fact]
    public async Task VerifyPaymentAsync_WithRecipientNotReceiver_ReturnsRecipientMismatch()
    {
        var service = CreateService();
        var builder = new ReceiveWithAuthorisationBuilder();

        var requirements = CreatePaymentRequirements("100000", payTo: "0x0000000000000000000000000000000000000001");
        var authorization = builder.BuildFromPaymentRequirements(requirements, PayerAddress);

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
    public async Task VerifyPaymentAsync_WithAmountBelowRequirement_ReturnsInvalidValue()
    {
        var service = CreateService();
        var builder = new ReceiveWithAuthorisationBuilder();

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

    private X402ReceiveWithAuthorisation3009Service CreateService()
    {
        return new X402ReceiveWithAuthorisation3009Service(
            ReceiverPrivateKey,
            new Dictionary<int, IClient> { [ChainId] = new RpcClient(new Uri(BaseSepoliaRpc)) }
        );
    }

    private PaymentRequirements CreatePaymentRequirements(string amount, string payTo = ReceiverAddress)
    {
        return new PaymentRequirements
        {
            Scheme = "exact",
            Network = Caip2.FormatEip155(ChainId),
            Amount = amount,
            Asset = "0x036CbD53842c5426634e7929541eC2318f3dCF7e",
            PayTo = payTo,
            MaxTimeoutSeconds = 3600,
            Extra = new ExactSchemeExtra { Name = "USDC", Version = "2" }
        };
    }
}
