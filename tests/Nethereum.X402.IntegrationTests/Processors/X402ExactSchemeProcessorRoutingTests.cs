using System.Text.Json;
using Nethereum.JsonRpc.Client;
using Nethereum.X402.Blockchain;
using Nethereum.X402.Models;
using Nethereum.X402.Processors;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Processors;

public class X402ExactSchemeProcessorRoutingTests
{
    private const string FacilitatorKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
    private const string Payer = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
    private const string PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C";
    private const string Asset = "0x036CbD53842c5426634e7929541eC2318f3dCF7e";
    private const string Network = "eip155:84532";

    [Fact]
    public async Task Given_Permit2Payload_When_Verifying_Then_ThePermit2ProcessorHandlesIt()
    {
        var (processor, eip3009, permit2) = CreateRouted();

        await processor.VerifyPaymentAsync(Payload(JsonSerializer.SerializeToElement(Permit2Payload())), Requirements("permit2"));

        Assert.Equal(1, permit2.VerifyCalls);
        Assert.Equal(0, eip3009.VerifyCalls);
    }

    [Fact]
    public async Task Given_Eip3009Payload_When_Settling_Then_TheEip3009ProcessorHandlesIt()
    {
        var (processor, eip3009, permit2) = CreateRouted();

        await processor.SettlePaymentAsync(Payload(ExactPayload()), Requirements(null));

        Assert.Equal(1, eip3009.SettleCalls);
        Assert.Equal(0, permit2.SettleCalls);
    }

    [Fact]
    public async Task Given_Permit2PayloadAgainstEip3009Requirement_When_Verifying_Then_UnsupportedPayloadType()
    {
        var processor = new X402ExactSchemeProcessor(Eip3009Service(), Permit2Service());

        var onTheWire = JsonSerializer.SerializeToElement(Permit2Payload());

        var verify = await processor.VerifyPaymentAsync(Payload(onTheWire), Requirements("eip3009"));

        Assert.False(verify.IsValid);
        Assert.Equal(X402ErrorCodes.UnsupportedPayloadType, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_Eip3009PayloadAgainstPermit2Requirement_When_Settling_Then_UnsupportedPayloadTypeWithEmptyTransaction()
    {
        var processor = new X402ExactSchemeProcessor(Eip3009Service(), Permit2Service());

        var settle = await processor.SettlePaymentAsync(Payload(ExactPayload()), Requirements("permit2"));

        Assert.False(settle.Success);
        Assert.Equal(X402ErrorCodes.UnsupportedPayloadType, settle.ErrorReason);
        Assert.Equal(string.Empty, settle.Transaction);
        Assert.Equal(Network, settle.Network);
    }

    [Fact]
    public async Task Given_UnknownAssetTransferMethod_When_Verifying_Then_UnsupportedPayloadType()
    {
        var processor = new X402ExactSchemeProcessor(Eip3009Service(), Permit2Service());

        var verify = await processor.VerifyPaymentAsync(Payload(ExactPayload()), Requirements("erc7710"));

        Assert.False(verify.IsValid);
        Assert.Equal(X402ErrorCodes.UnsupportedPayloadType, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_UnknownAssetTransferMethod_When_Settling_Then_UnsupportedPayloadTypeWithEmptyTransactionAndRequirementNetwork()
    {
        var processor = new X402ExactSchemeProcessor(Eip3009Service(), Permit2Service());

        var settle = await processor.SettlePaymentAsync(Payload(ExactPayload()), Requirements("erc7710"));

        Assert.False(settle.Success);
        Assert.Equal(X402ErrorCodes.UnsupportedPayloadType, settle.ErrorReason);
        Assert.Equal(string.Empty, settle.Transaction);
        Assert.Equal(Network, settle.Network);
    }

    [Fact]
    public async Task Given_Eip3009ServiceAndPermit2Requirement_When_Verifying_Then_UnsupportedPayloadType()
    {
        var verify = await Eip3009Service().VerifyPaymentAsync(Payload(ExactPayload()), Requirements("permit2"));

        Assert.Equal(X402ErrorCodes.UnsupportedPayloadType, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_Permit2ServiceAndEip3009Requirement_When_Verifying_Then_UnsupportedPayloadType()
    {
        var verify = await Permit2Service().VerifyPaymentAsync(Payload(Permit2Payload()), Requirements("eip3009"));

        Assert.Equal(X402ErrorCodes.UnsupportedPayloadType, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_NonExactScheme_When_Eip3009ServiceVerifies_Then_InvalidSchemeNotUnsupportedScheme()
    {
        var requirements = Requirements(null);
        requirements.Scheme = "upto";

        var verify = await Eip3009Service().VerifyPaymentAsync(Payload(ExactPayload()), requirements);

        Assert.Equal(X402ErrorCodes.InvalidScheme, verify.InvalidReason);
        Assert.Equal("invalid_exact_evm_scheme", verify.InvalidReason);
    }

    [Fact]
    public async Task Given_PayloadOfAnotherProtocolVersion_When_Eip3009ServiceVerifies_Then_InvalidX402Version()
    {
        var payload = Payload(ExactPayload());
        payload.X402Version = 1;

        var verify = await Eip3009Service().VerifyPaymentAsync(payload, Requirements(null));

        Assert.False(verify.IsValid);
        Assert.Equal(X402ErrorCodes.InvalidX402Version, verify.InvalidReason);
    }

    [Fact]
    public async Task Given_PayloadOfAnotherProtocolVersion_When_Permit2ServiceVerifies_Then_InvalidX402Version()
    {
        var payload = Payload(Permit2Payload());
        payload.X402Version = 1;

        var verify = await Permit2Service().VerifyPaymentAsync(payload, Requirements("permit2"));

        Assert.False(verify.IsValid);
        Assert.Equal(X402ErrorCodes.InvalidX402Version, verify.InvalidReason);
    }

    private static (X402ExactSchemeProcessor Processor, RecordingProcessor Eip3009, RecordingProcessor Permit2) CreateRouted()
    {
        var eip3009 = new RecordingProcessor();
        var permit2 = new RecordingProcessor();
        return (new X402ExactSchemeProcessor(eip3009, permit2), eip3009, permit2);
    }

    private static X402TransferWithAuthorisation3009Service Eip3009Service() =>
        new(FacilitatorKey, new Dictionary<int, IClient> { [84532] = new RpcClient(new Uri("http://127.0.0.1:1")) });

    private static X402ExactPermit2Service Permit2Service() =>
        new(FacilitatorKey, new Dictionary<int, IClient> { [84532] = new RpcClient(new Uri("http://127.0.0.1:1")) });

    private static PaymentRequirements Requirements(string? assetTransferMethod) => new()
    {
        Scheme = X402Schemes.Exact,
        Network = Network,
        Amount = "1000",
        Asset = Asset,
        PayTo = PayTo,
        MaxTimeoutSeconds = 300,
        Extra = new ExactSchemeExtra { AssetTransferMethod = assetTransferMethod, Name = "USDC", Version = "2" }
    };

    private static PaymentPayload Payload(object payload) => new()
    {
        Accepted = Requirements(null),
        Payload = payload
    };

    private static ExactSchemePayload ExactPayload() => new()
    {
        Signature = "0x00",
        Authorization = new Authorization
        {
            From = Payer,
            To = PayTo,
            Value = "1000",
            ValidAfter = "0",
            ValidBefore = "99999999999",
            Nonce = "0x" + new string('1', 64)
        }
    };

    private static Permit2SchemePayload Permit2Payload() => new()
    {
        Signature = "0x00",
        Permit2Authorization = new Permit2Authorization
        {
            From = Payer,
            Permitted = new Permit2TokenPermissions { Token = Asset, Amount = "1000" },
            Spender = PayTo,
            Nonce = "1",
            Deadline = "99999999999",
            Witness = new Permit2WitnessData { To = PayTo, ValidAfter = "0" }
        }
    };

    private sealed class RecordingProcessor : IX402PaymentProcessor
    {
        public int VerifyCalls { get; private set; }
        public int SettleCalls { get; private set; }

        public Task<VerificationResponse> VerifyPaymentAsync(
            PaymentPayload paymentPayload, PaymentRequirements requirements, CancellationToken cancellationToken = default)
        {
            VerifyCalls++;
            return Task.FromResult(new VerificationResponse { IsValid = true, Payer = Payer });
        }

        public Task<SettlementResponse> SettlePaymentAsync(
            PaymentPayload paymentPayload, PaymentRequirements requirements, CancellationToken cancellationToken = default)
        {
            SettleCalls++;
            return Task.FromResult(new SettlementResponse { Success = true, Transaction = "0x1", Network = Network, Payer = Payer });
        }

        public Task<SupportedPaymentKindsResponse> GetSupportedAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SupportedPaymentKindsResponse());
    }
}
