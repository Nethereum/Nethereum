using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.JsonRpc.Client;
using Nethereum.Signer;
using Nethereum.X402.Blockchain;
using Nethereum.X402.Extensions;
using Nethereum.X402.Facilitator;
using Nethereum.X402.IntegrationTests.Helpers;
using Nethereum.X402.Models;
using Nethereum.X402.Permit2;
using Nethereum.X402.Processors;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Blockchain;

[Collection("X402 DevChain E2E")]
public class X402HugeNumberDevChainTests : IClassFixture<X402DevChainFixture>, IAsyncLifetime
{
    private const int ChainId = X402DevChainFixture.ChainId;
    private const string FacilitatorKey = X402DevChainFixture.FacilitatorKey;
    private const int Digits = 1_000_000;

    private readonly X402DevChainFixture _chain;
    private readonly EthECKey _payer = new(X402DevChainFixture.PayerKey.Substring(2));

    public X402HugeNumberDevChainTests(X402DevChainFixture chain)
    {
        _chain = chain;
    }

    public async Task InitializeAsync()
    {
        await X402Permit2DevChain.InstallPermit2StubsAsync(_chain.Node);
        await X402Permit2DevChain.ApprovePermit2Async(_chain.Node, _payer, ChainId, _chain.TokenAddress);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [Trait("Rule", "V9")]
    [InlineData("Value")]
    [InlineData("ValidAfter")]
    [InlineData("ValidBefore")]
    [InlineData("RequirementAmount")]
    public async Task Given_AMillionDigitEip3009Number_When_PostedToVerify_Then_InvalidPayloadIsReturnedInUnderASecond(string field)
    {
        using var server = CreateFacilitator();
        var client = server.CreateClient();
        await VerifyAsync(client, await Eip3009RequestAsync(null));

        var elapsed = Stopwatch.StartNew();
        var verify = await VerifyAsync(client, await Eip3009RequestAsync(field));
        elapsed.Stop();

        Assert.False(verify.IsValid);
        Assert.Equal(X402ErrorCodes.InvalidPayload, verify.InvalidReason);
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(1), elapsed.Elapsed.ToString());
    }

    [Theory]
    [Trait("Rule", "V9")]
    [InlineData("Amount")]
    [InlineData("Nonce")]
    [InlineData("Deadline")]
    [InlineData("ValidAfter")]
    [InlineData("RequirementAmount")]
    public async Task Given_AMillionDigitPermit2Number_When_PostedToVerify_Then_InvalidPayloadIsReturnedInUnderASecond(string field)
    {
        using var server = CreateFacilitator();
        var client = server.CreateClient();
        await VerifyAsync(client, Permit2Request(null));

        var elapsed = Stopwatch.StartNew();
        var verify = await VerifyAsync(client, Permit2Request(field));
        elapsed.Stop();

        Assert.False(verify.IsValid);
        Assert.Equal(X402ErrorCodes.InvalidPayload, verify.InvalidReason);
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(1), elapsed.Elapsed.ToString());
    }

    [Theory]
    [Trait("Rule", "V9")]
    [InlineData("Amount")]
    [InlineData("Nonce")]
    [InlineData("Deadline")]
    public async Task Given_AMillionDigitEip2612ExtensionNumber_When_Verifying_Then_TheFormatIsRejectedInUnderASecond(string field)
    {
        var service = Service();
        await service.VerifyPaymentAsync(Eip2612Payload(null), Eip2612Payload(null).Accepted);
        var payload = Eip2612Payload(field);

        var elapsed = Stopwatch.StartNew();
        var verify = await service.VerifyPaymentAsync(payload, payload.Accepted);
        elapsed.Stop();

        Assert.Equal(X402ErrorCodes.InvalidEip2612ExtensionFormat, verify.InvalidReason);
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(1), elapsed.Elapsed.ToString());
    }

    [Fact]
    [Trait("Rule", "V9")]
    public async Task Given_AMillionDigitErc20ApprovalAmount_When_Verifying_Then_TheFormatIsRejectedInUnderASecond()
    {
        var service = Service();
        await service.VerifyPaymentAsync(Erc20Payload(false), Erc20Payload(false).Accepted);
        var payload = Erc20Payload(true);

        var elapsed = Stopwatch.StartNew();
        var verify = await service.VerifyPaymentAsync(payload, payload.Accepted);
        elapsed.Stop();

        Assert.Equal(X402ErrorCodes.InvalidErc20ApprovalExtensionFormat, verify.InvalidReason);
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(1), elapsed.Elapsed.ToString());
    }

    private X402ExactPermit2Service Service() => new(
        FacilitatorKey, new Dictionary<int, IClient> { [ChainId] = _chain.Client },
        new Erc20ApprovalGasSponsoringOptions { Enabled = true, MaxSponsoredGasCost = System.Numerics.BigInteger.Pow(10, 20) });

    private PaymentPayload Eip2612Payload(string? hugeField)
    {
        var payer = EthECKey.GenerateKey();
        var huge = new string('9', Digits);
        var info = new Eip2612GasSponsoringInfo
        {
            From = payer.GetPublicAddress(),
            Asset = _chain.TokenAddress,
            Spender = X402Permit2Addresses.Permit2,
            Amount = hugeField == "Amount" ? huge : X402Permit2DevChain.Amount,
            Nonce = hugeField == "Nonce" ? huge : "0",
            Deadline = hugeField == "Deadline" ? huge : (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600).ToString(),
            Signature = "0x" + new string('1', 130),
            Version = "1"
        };
        return X402Permit2DevChain.Payload(payer, ChainId, _chain.TokenAddress, new Dictionary<string, object>
        {
            [X402ExtensionKeys.Eip2612GasSponsoring] = new Dictionary<string, object> { ["info"] = info }
        });
    }

    private PaymentPayload Erc20Payload(bool huge)
    {
        var payer = EthECKey.GenerateKey();
        var spec = new SignedApprovalSpec(ChainId, _chain.TokenAddress, 0, 1_000_000_000);
        var info = Erc20ApprovalTransactions.Info(payer.GetPublicAddress(), spec, Erc20ApprovalTransactions.Sign(payer, spec));
        if (huge)
            info.Amount = new string('9', Digits);
        return X402Permit2DevChain.Payload(payer, ChainId, _chain.TokenAddress, Erc20ApprovalTransactions.Extension(info));
    }

    private async Task<FacilitatorVerifyRequest> Eip3009RequestAsync(string? hugeField)
    {
        var requirements = X402TokenPayments.Requirements(_chain.TokenAddress, "1000000");
        var payload = await X402TokenPayments.SignedTransferAsync(_chain.TokenAddress, requirements);
        var authorization = ((ExactSchemePayload)payload.Payload).Authorization;
        var huge = new string('9', Digits);
        switch (hugeField)
        {
            case "Value": authorization.Value = huge; break;
            case "ValidAfter": authorization.ValidAfter = huge; break;
            case "ValidBefore": authorization.ValidBefore = huge; break;
            case "RequirementAmount": requirements.Amount = huge; break;
        }

        return new FacilitatorVerifyRequest { PaymentPayload = payload, PaymentRequirements = requirements };
    }

    private FacilitatorVerifyRequest Permit2Request(string? hugeField)
    {
        var payload = X402Permit2DevChain.Payload(_payer, ChainId, _chain.TokenAddress);
        var requirements = payload.Accepted;
        var authorization = ((Permit2SchemePayload)payload.Payload).Permit2Authorization;
        var huge = new string('9', Digits);
        switch (hugeField)
        {
            case "Amount": authorization.Permitted.Amount = huge; break;
            case "Nonce": authorization.Nonce = huge; break;
            case "Deadline": authorization.Deadline = huge; break;
            case "ValidAfter": authorization.Witness.ValidAfter = huge; break;
            case "RequirementAmount": requirements = X402Permit2DevChain.Requirements(ChainId, _chain.TokenAddress); requirements.Amount = huge; break;
        }

        return new FacilitatorVerifyRequest { PaymentPayload = payload, PaymentRequirements = requirements };
    }

    private TestServer CreateFacilitator() => new(new WebHostBuilder()
        .ConfigureServices(services =>
        {
            var rpc = new Dictionary<int, IClient> { [ChainId] = _chain.Client };
            services.AddSingleton<IX402PaymentProcessor>(new X402ExactSchemeProcessor(
                new X402TransferWithAuthorisation3009Service(FacilitatorKey, rpc),
                new X402ExactPermit2Service(FacilitatorKey, rpc)));
            services.AddControllers().AddX402FacilitatorControllers();
        })
        .Configure(app =>
        {
            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        }));

    private static async Task<VerificationResponse> VerifyAsync(HttpClient client, FacilitatorVerifyRequest request)
    {
        using var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/facilitator/verify", content);
        response.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<VerificationResponse>(await response.Content.ReadAsStringAsync())!;
    }
}
