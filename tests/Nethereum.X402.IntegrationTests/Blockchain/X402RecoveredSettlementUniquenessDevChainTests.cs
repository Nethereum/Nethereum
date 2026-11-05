using System.Net;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.Contracts.EIP3009.EIP3009.ContractDefinition;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Web3.Accounts;
using Nethereum.X402.AspNetCore;
using Nethereum.X402.Blockchain;
using Nethereum.X402.Facilitator;
using Nethereum.X402.IntegrationTests.Helpers;
using Nethereum.X402.Models;
using Nethereum.X402.Server;

namespace Nethereum.X402.IntegrationTests.Blockchain;

[Collection("X402 DevChain E2E")]
public class X402RecoveredSettlementUniquenessDevChainTests : IClassFixture<X402DevChainFixture>
{
    private readonly X402DevChainFixture _chain;

    public X402RecoveredSettlementUniquenessDevChainTests(X402DevChainFixture chain)
    {
        _chain = chain;
    }

    [Fact]
    [Trait("Rule", "W10")]
    public async Task Given_TwoResourceServersWithSeparateReplayStoresAndOneFacilitator_When_TheSameUpfrontPaymentArrivesAtBoth_Then_TheHandlerRunsOnceAndOneRequestIsRefused()
    {
        var service = new X402TransferWithAuthorisation3009Service(
            X402DevChainFixture.FacilitatorKey, new Dictionary<int, IClient> { [X402DevChainFixture.ChainId] = _chain.Client });
        var facilitator = new BarrierFacilitator(service);
        var requirements = X402DevChainPayments.Requirements(_chain, "1000000", X402DevChainFixture.RecipientAddress);
        requirements.Extra = new ExactSchemeExtra
        {
            Name = X402DevChainFixture.TokenName,
            Version = X402DevChainFixture.TokenVersion,
            PaymentFlow = X402PaymentFlows.Upfront
        };
        var payload = await X402DevChainPayments.SignedTransfer(_chain, requirements);
        var header = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));
        var handlerRuns = 0;
        using var first = CreateServer(facilitator, requirements, () => Interlocked.Increment(ref handlerRuns));
        using var second = CreateServer(facilitator, requirements, () => Interlocked.Increment(ref handlerRuns));
        var before = await PayerBalanceAsync();

        var responses = await Task.WhenAll(Send(first, header), Send(second, header));

        var after = await PayerBalanceAsync();
        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, handlerRuns);
        Assert.Equal(BigInteger.Parse(requirements.Amount), before - after);
    }

    [Fact]
    [Trait("Rule", "W10")]
    public async Task Given_AThirdPartyFrontRanOneAuthorization_When_TwoSettlementsClaimIt_Then_TheFirstRecoversAndTheSecondIsRefused()
    {
        var requirements = X402DevChainPayments.Requirements(_chain, "1000000", X402DevChainFixture.RecipientAddress);
        var payload = await X402DevChainPayments.SignedTransfer(_chain, requirements);
        var estimates = 0;
        var nonceReads = 0;
        TransactionReceipt? thirdParty = null;
        var rpc = new InterceptingRpcClient(_chain.Client, async method =>
        {
            if (method == "eth_estimateGas")
                Interlocked.Increment(ref estimates);

            if (method == "eth_getTransactionCount" && Interlocked.Increment(ref nonceReads) == 1)
            {
                for (var i = 0; i < 100 && Volatile.Read(ref estimates) < 2; i++)
                    await Task.Delay(50);

                thirdParty = await ThirdPartySettleAsync(payload);
            }
        });
        var service = new X402TransferWithAuthorisation3009Service(
            X402DevChainFixture.FacilitatorKey, new Dictionary<int, IClient> { [X402DevChainFixture.ChainId] = rpc });

        var settles = await Task.WhenAll(
            service.SettlePaymentAsync(payload, requirements),
            service.SettlePaymentAsync(payload, requirements));

        Assert.NotNull(thirdParty);
        var recovered = settles.Single(settle => settle.Success);
        Assert.Equal(thirdParty!.TransactionHash, recovered.Transaction);
        Assert.Equal(1, settles.Count(settle => !settle.Success));
    }

    [Fact]
    [Trait("Rule", "W10")]
    public async Task Given_AClaimWhoseConfirmationWaitEndedPending_When_TheBlockIsMinedAndTheSameAuthorizationIsClaimedAgain_Then_TheRetryRecoversWithTheSameHash()
    {
        var options = new X402FacilitatorOptions { RequiredConfirmations = 2, MaxReceiptWaitSeconds = 2 };
        var requirements = X402DevChainPayments.Requirements(_chain, "1000000", X402DevChainFixture.RecipientAddress);
        var payload = await X402DevChainPayments.SignedTransfer(_chain, requirements);
        var estimates = 0;
        var nonceReads = 0;
        var retryMayProceed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TransactionReceipt? thirdParty = null;
        var rpc = new InterceptingRpcClient(_chain.Client, async method =>
        {
            if (method == "eth_estimateGas")
                Interlocked.Increment(ref estimates);

            if (method != "eth_getTransactionCount")
                return;

            var read = Interlocked.Increment(ref nonceReads);
            if (read == 1)
            {
                for (var i = 0; i < 100 && Volatile.Read(ref estimates) < 2; i++)
                    await Task.Delay(50);

                thirdParty = await ThirdPartySettleAsync(payload);
            }
            else if (read == 2)
            {
                await retryMayProceed.Task;
            }
        });
        var service = new X402TransferWithAuthorisation3009Service(
            X402DevChainFixture.FacilitatorKey, new Dictionary<int, IClient> { [X402DevChainFixture.ChainId] = rpc }, options);

        var claims = new[] { service.SettlePaymentAsync(payload, requirements), service.SettlePaymentAsync(payload, requirements) };
        var firstDone = await Task.WhenAny(claims);
        var pending = await firstDone;
        await MineBlockAsync();
        retryMayProceed.SetResult();
        var retried = await claims.Single(claim => claim != firstDone);

        Assert.False(pending.Success);
        Assert.Equal(X402ErrorCodes.SettlementPending, pending.ErrorReason);
        Assert.Equal(thirdParty!.TransactionHash, pending.Transaction);
        Assert.True(retried.Success, retried.ErrorReason);
        Assert.Equal(thirdParty.TransactionHash, retried.Transaction);
    }

    private async Task MineBlockAsync()
    {
        var payer = _chain.Node.CreateWeb3(new Account(X402DevChainFixture.PayerKey, X402DevChainFixture.ChainId));
        await payer.Eth.TransactionManager.SendTransactionAndWaitForReceiptAsync(new TransactionInput
        {
            From = X402DevChainFixture.PayerAddress,
            To = X402DevChainFixture.RecipientAddress,
            Value = new Nethereum.Hex.HexTypes.HexBigInteger(1),
            Gas = new Nethereum.Hex.HexTypes.HexBigInteger(100_000)
        });
    }

    private async Task<BigInteger> PayerBalanceAsync() =>
        await _chain.Node.CreateWeb3().Eth.ERC20.GetContractService(_chain.TokenAddress).BalanceOfQueryAsync(X402DevChainFixture.PayerAddress);

    private async Task<TransactionReceipt> ThirdPartySettleAsync(PaymentPayload payload)
    {
        var exact = (ExactSchemePayload)payload.Payload;
        var authorization = exact.Authorization;
        var signature = EthECDSASignatureFactory.ExtractECDSASignature(exact.Signature);
        var function = new TransferWithAuthorization1Function
        {
            AuthorisationFrom = authorization.From,
            AuthorisationTo = authorization.To,
            Value = BigInteger.Parse(authorization.Value),
            ValidAfter = BigInteger.Parse(authorization.ValidAfter),
            ValidBefore = BigInteger.Parse(authorization.ValidBefore),
            AuthorisationNonce = authorization.Nonce.HexToByteArray(),
            V = signature.V[0],
            R = signature.R.PadTo32Bytes(),
            S = signature.S.PadTo32Bytes()
        };
        var payer = _chain.Node.CreateWeb3(new Account(X402DevChainFixture.PayerKey, X402DevChainFixture.ChainId));
        return await payer.Eth.GetContractHandler(_chain.TokenAddress).SendRequestAndWaitForReceiptAsync(function);
    }

    private static Task<HttpResponseMessage> Send(TestServer server, string header)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/premium");
        request.Headers.Add(X402Headers.PaymentSignature, header);
        return server.CreateClient().SendAsync(request);
    }

    private static TestServer CreateServer(IFacilitatorClient facilitator, PaymentRequirements requirements, Action onHandler) =>
        new(new WebHostBuilder()
            .ConfigureServices(services => services.AddSingleton(facilitator))
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

    private sealed class BarrierFacilitator : IFacilitatorClient
    {
        private readonly X402TransferWithAuthorisation3009Service _service;
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrived;

        public BarrierFacilitator(X402TransferWithAuthorisation3009Service service)
        {
            _service = service;
        }

        public Task<VerificationResponse> VerifyAsync(PaymentPayload payload, PaymentRequirements requirements, CancellationToken cancellationToken = default) =>
            _service.VerifyPaymentAsync(payload, requirements, cancellationToken);

        public async Task<SettlementResponse> SettleAsync(PaymentPayload payload, PaymentRequirements requirements, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _arrived) >= 2)
                _gate.TrySetResult();

            await Task.WhenAny(_gate.Task, Task.Delay(5000, cancellationToken));
            return await _service.SettlePaymentAsync(payload, requirements, cancellationToken);
        }

        public Task<SupportedPaymentKindsResponse> GetSupportedAsync(CancellationToken cancellationToken = default) =>
            _service.GetSupportedAsync(cancellationToken);
    }
}
