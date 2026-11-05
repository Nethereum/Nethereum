using System.Net;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.Signer;
using Nethereum.X402.AspNetCore;
using Nethereum.X402.Blockchain;
using Nethereum.X402.IntegrationTests.Blockchain;
using Nethereum.X402.Facilitator;
using Nethereum.X402.IntegrationTests.Helpers;
using Nethereum.X402.Models;
using Nethereum.X402.Server;
using Nethereum.X402.Signers;
using Authorization = Nethereum.X402.Models.Authorization;

namespace Nethereum.X402.IntegrationTests.AspNetCore;

[Collection("X402 DevChain E2E")]
public class X402ReplayReservationRound3Tests : IClassFixture<X402DevChainFixture>
{
    private const string Network = "eip155:84532";
    private const string Token = "0x036CbD53842c5426634e7929541eC2318f3dCF7e";
    private const string Payee = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C";
    private const string Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66";

    private readonly X402DevChainFixture _chain;

    public X402ReplayReservationRound3Tests(X402DevChainFixture chain)
    {
        _chain = chain;
    }

    [Fact]
    [Trait("Rule", "W9")]
    public async Task Given_ABogusUpfrontPaymentThatFailsVerification_When_TheSameRequestIsSentAgain_Then_ItIsNeverSettledNorReserved()
    {
        var service = new X402TransferWithAuthorisation3009Service(
            X402DevChainFixture.FacilitatorKey,
            new Dictionary<int, Nethereum.JsonRpc.Client.IClient> { [X402DevChainFixture.ChainId] = _chain.Client });
        var facilitator = new CountingFacilitator(service);
        var requirements = X402DevChainPayments.Requirements(_chain, "1000000", X402DevChainFixture.RecipientAddress);
        requirements.Extra = new ExactSchemeExtra
        {
            Name = X402DevChainFixture.TokenName,
            Version = X402DevChainFixture.TokenVersion,
            PaymentFlow = X402PaymentFlows.Upfront
        };
        var bogus = await SignedByAnotherKeyAsync(requirements);
        var handlerRuns = 0;
        using var server = CreateServer(facilitator, requirements, new InMemoryPaymentReplayStore(), () => handlerRuns++);
        var client = server.CreateClient();

        var first = await client.SendAsync(PaymentRequest(bogus));
        var second = await client.SendAsync(PaymentRequest(bogus));

        Assert.Equal(HttpStatusCode.PaymentRequired, first.StatusCode);
        Assert.Equal(HttpStatusCode.PaymentRequired, second.StatusCode);
        Assert.Contains(X402ErrorCodes.InvalidSignature, await first.Content.ReadAsStringAsync());
        Assert.Contains(X402ErrorCodes.InvalidSignature, await second.Content.ReadAsStringAsync());
        Assert.Equal(0, facilitator.Settles);
        Assert.Equal(0, handlerRuns);
    }

    [Fact]
    [Trait("Rule", "W9")]
    public async Task Given_AnUpfrontSettlementThatThrows_When_TheSameRequestIsSentAgain_Then_TheUnknownOutcomeKeepsTheReservationAndItIsRejectedAsAReplay()
    {
        var facilitator = new ScriptedFacilitator(
            () => throw new InvalidOperationException("facilitator down"),
            () => new SettlementResponse { Success = true, Transaction = "0xabc", Network = Network, Payer = Payer });
        var requirements = Requirements(upfront: true);
        var payment = Payment(requirements, 1);
        var handlerRuns = 0;
        using var server = CreateServer(facilitator, requirements, new InMemoryPaymentReplayStore(), () => handlerRuns++);
        var client = server.CreateClient();

        var first = await client.SendAsync(PaymentRequest(payment));
        var second = await client.SendAsync(PaymentRequest(payment));

        Assert.Equal(HttpStatusCode.PaymentRequired, first.StatusCode);
        Assert.Equal(HttpStatusCode.PaymentRequired, second.StatusCode);
        Assert.Contains(X402ErrorCodes.NonceAlreadyUsed, await second.Content.ReadAsStringAsync());
        Assert.Equal(1, facilitator.Settles);
        Assert.Equal(0, handlerRuns);
    }

    [Fact]
    [Trait("Rule", "W9")]
    public async Task Given_AnUpfrontPaymentThatSettled_When_TheSameRequestIsSentAgain_Then_ItIsStillRejectedAsAReplay()
    {
        var facilitator = new ScriptedFacilitator(
            () => new SettlementResponse { Success = true, Transaction = "0xabc", Network = Network, Payer = Payer });
        var requirements = Requirements(upfront: true);
        var payment = Payment(requirements, 1);
        var handlerRuns = 0;
        using var server = CreateServer(facilitator, requirements, new InMemoryPaymentReplayStore(), () => handlerRuns++);
        var client = server.CreateClient();

        var first = await client.SendAsync(PaymentRequest(payment));
        var second = await client.SendAsync(PaymentRequest(payment));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.PaymentRequired, second.StatusCode);
        Assert.Contains(X402ErrorCodes.NonceAlreadyUsed, await second.Content.ReadAsStringAsync());
        Assert.Equal(1, facilitator.Settles);
        Assert.Equal(1, handlerRuns);
    }

    [Fact]
    [Trait("Rule", "W9")]
    public async Task Given_AStoreHoldingItsMaximumOfLiveReservations_When_AnotherPaymentIsReserved_Then_TheStoreRefusesAndAFreedSlotIsReusable()
    {
        var store = new InMemoryPaymentReplayStore(2);

        Assert.True(await store.TryReserveAsync("a", TimeSpan.FromMinutes(5)));
        Assert.True(await store.TryReserveAsync("b", TimeSpan.FromMinutes(5)));
        Assert.False(await store.TryReserveAsync("a", TimeSpan.FromMinutes(5)));
        var full = await Assert.ThrowsAsync<PaymentReplayStoreFullException>(
            () => store.TryReserveAsync("c", TimeSpan.FromMinutes(5)));
        Assert.Equal(2, full.MaxEntries);

        await store.ReleaseAsync("a");

        Assert.True(await store.TryReserveAsync("c", TimeSpan.FromMinutes(5)));
    }

    [Fact]
    [Trait("Rule", "W9")]
    public async Task Given_AFullStoreWhoseReservationsHaveExpired_When_AnotherPaymentIsReserved_Then_TheExpiredEntriesAreEvictedFirst()
    {
        var store = new InMemoryPaymentReplayStore(2);
        Assert.True(await store.TryReserveAsync("a", TimeSpan.FromMilliseconds(30)));
        Assert.True(await store.TryReserveAsync("b", TimeSpan.FromMilliseconds(30)));
        await Task.Delay(120);

        Assert.True(await store.TryReserveAsync("c", TimeSpan.FromMinutes(5)));
        Assert.True(await store.TryReserveAsync("d", TimeSpan.FromMinutes(5)));
        await Assert.ThrowsAsync<PaymentReplayStoreFullException>(() => store.TryReserveAsync("e", TimeSpan.FromMinutes(5)));
    }

    [Fact]
    [Trait("Rule", "W9")]
    public void Given_ADefaultStore_When_ItIsCreated_Then_ItHoldsAHundredThousandReservations()
    {
        Assert.Equal(100000, new InMemoryPaymentReplayStore().MaxEntries);
        Assert.Throws<ArgumentOutOfRangeException>(() => new InMemoryPaymentReplayStore(0));
    }

    [Fact]
    [Trait("Rule", "W9")]
    public async Task Given_AFullReplayStore_When_ADifferentPaymentArrives_Then_TheRequestFailsClosedWith503AndTheHandlerDoesNotRun()
    {
        var facilitator = new ScriptedFacilitator(
            () => new SettlementResponse { Success = true, Transaction = "0xabc", Network = Network, Payer = Payer });
        var requirements = Requirements(upfront: false);
        var handlerRuns = 0;
        using var server = CreateServer(facilitator, requirements, new InMemoryPaymentReplayStore(1), () => handlerRuns++);
        var client = server.CreateClient();

        var first = await client.SendAsync(PaymentRequest(Payment(requirements, 1)));
        var second = await client.SendAsync(PaymentRequest(Payment(requirements, 2)));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, second.StatusCode);
        Assert.Contains(X402ErrorCodes.ReplayStoreFull, await second.Content.ReadAsStringAsync());
        Assert.Equal(1, handlerRuns);
        Assert.Equal(1, facilitator.Settles);
    }

    private async Task<PaymentPayload> SignedByAnotherKeyAsync(PaymentRequirements requirements)
    {
        var authorization = new TransferWithAuthorisationBuilder().BuildFromPaymentRequirements(
            requirements, X402DevChainFixture.PayerAddress);
        var impostor = EthECKey.GenerateKey();
        var signature = await new TransferWithAuthorisationSigner().SignWithPrivateKeyAsync(
            authorization, X402DevChainFixture.TokenName, X402DevChainFixture.TokenVersion,
            X402DevChainFixture.ChainId, _chain.TokenAddress, impostor.GetPrivateKey());
        return new PaymentPayload
        {
            X402Version = X402Protocol.Version,
            Accepted = requirements,
            Payload = new ExactSchemePayload
            {
                Authorization = authorization,
                Signature = EthECDSASignature.CreateStringSignature(signature)
            }
        };
    }

    private static PaymentRequirements Requirements(bool upfront) => new()
    {
        Scheme = X402Schemes.Exact,
        Network = Network,
        Amount = "100000",
        PayTo = Payee,
        Asset = Token,
        MaxTimeoutSeconds = 300,
        Extra = new ExactSchemeExtra
        {
            Name = "USDC",
            Version = "2",
            PaymentFlow = upfront ? X402PaymentFlows.Upfront : null
        }
    };

    private static PaymentPayload Payment(PaymentRequirements requirements, int nonce) => new()
    {
        X402Version = X402Protocol.Version,
        Accepted = requirements,
        Payload = new ExactSchemePayload
        {
            Signature = "0x" + new string('1', 130),
            Authorization = new Authorization
            {
                From = Payer,
                To = Payee,
                Value = "100000",
                ValidAfter = "0",
                ValidBefore = "4102444800",
                Nonce = "0x" + nonce.ToString("x").PadLeft(64, '0')
            }
        }
    };

    private static HttpRequestMessage PaymentRequest(PaymentPayload payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/premium");
        request.Headers.Add(X402Headers.PaymentSignature, Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload))));
        return request;
    }

    private static string? SettlementReason(HttpResponseMessage response)
    {
        var header = response.Headers.GetValues(X402Headers.PaymentResponse).Single();
        return JsonSerializer.Deserialize<SettlementResponse>(Encoding.UTF8.GetString(Convert.FromBase64String(header)))!.ErrorReason;
    }

    private static TestServer CreateServer(
        IFacilitatorClient facilitator, PaymentRequirements requirements, IPaymentReplayStore store, Action onHandler) =>
        new(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(facilitator);
                services.AddSingleton(store);
            })
            .Configure(app =>
            {
                app.UseX402(options =>
                {
                    options.FacilitatorUrl = "http://facilitator.local";
                    options.Routes.Add(new RoutePaymentConfig("/premium", requirements));
                });
                app.Run(context =>
                {
                    onHandler();
                    return context.Response.WriteAsync("premium");
                });
            }));

    private sealed class CountingFacilitator : IFacilitatorClient
    {
        private readonly Nethereum.X402.Processors.IX402PaymentProcessor _inner;
        private int _settles;

        public CountingFacilitator(Nethereum.X402.Processors.IX402PaymentProcessor inner)
        {
            _inner = inner;
        }

        public int Settles => _settles;

        public Task<VerificationResponse> VerifyAsync(PaymentPayload payload, PaymentRequirements requirements, CancellationToken cancellationToken = default) =>
            _inner.VerifyPaymentAsync(payload, requirements, cancellationToken);

        public Task<SettlementResponse> SettleAsync(PaymentPayload payload, PaymentRequirements requirements, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _settles);
            return _inner.SettlePaymentAsync(payload, requirements, cancellationToken);
        }

        public Task<SupportedPaymentKindsResponse> GetSupportedAsync(CancellationToken cancellationToken = default) =>
            _inner.GetSupportedAsync(cancellationToken);
    }

    private sealed class ScriptedFacilitator : IFacilitatorClient
    {
        private readonly Func<SettlementResponse>[] _settles;
        private int _calls;

        public ScriptedFacilitator(params Func<SettlementResponse>[] settles)
        {
            _settles = settles;
        }

        public int Settles => _calls;

        public Task<VerificationResponse> VerifyAsync(PaymentPayload payload, PaymentRequirements requirements, CancellationToken cancellationToken = default) =>
            Task.FromResult(new VerificationResponse { IsValid = true, Payer = Payer });

        public Task<SettlementResponse> SettleAsync(PaymentPayload payload, PaymentRequirements requirements, CancellationToken cancellationToken = default)
        {
            var index = Math.Min(Interlocked.Increment(ref _calls) - 1, _settles.Length - 1);
            return Task.FromResult(_settles[index]());
        }

        public Task<SupportedPaymentKindsResponse> GetSupportedAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SupportedPaymentKindsResponse());
    }
}
