using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.X402.Blockchain;
using Nethereum.X402.IntegrationTests.Blockchain;
using Nethereum.X402.IntegrationTests.Helpers;
using Nethereum.X402.Models;
using Xunit;

namespace Nethereum.X402.IntegrationTests.SecurityAudit71.Round4;

[Collection("X402 DevChain E2E")]
public class X402BroadcastClassificationDevChainTests : IClassFixture<X402DevChainFixture>
{
    private readonly X402DevChainFixture _chain;

    public X402BroadcastClassificationDevChainTests(X402DevChainFixture chain)
    {
        _chain = chain;
    }

    private sealed class FailingBroadcastClient : IClient
    {
        private readonly IClient _inner;
        private readonly Func<Exception> _failure;

        public FailingBroadcastClient(IClient inner, Func<Exception> failure)
        {
            _inner = inner;
            _failure = failure;
        }

        public RequestInterceptor OverridingRequestInterceptor
        {
            get => _inner.OverridingRequestInterceptor;
            set => _inner.OverridingRequestInterceptor = value;
        }

        public T DecodeResult<T>(RpcResponseMessage rpcResponseMessage) => _inner.DecodeResult<T>(rpcResponseMessage);

        public Task SendRequestAsync(RpcRequest request, string? route = null) =>
            IsBroadcast(request.Method) ? throw _failure() : _inner.SendRequestAsync(request, route);

        public Task SendRequestAsync(string method, string? route = null, params object[] paramList) =>
            IsBroadcast(method) ? throw _failure() : _inner.SendRequestAsync(method, route, paramList);

        public Task<T> SendRequestAsync<T>(RpcRequest request, string? route = null) =>
            IsBroadcast(request.Method) ? throw _failure() : _inner.SendRequestAsync<T>(request, route);

        public Task<T> SendRequestAsync<T>(string method, string? route = null, params object[] paramList) =>
            IsBroadcast(method) ? throw _failure() : _inner.SendRequestAsync<T>(method, route, paramList);

        public Task<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage, string? route = null) =>
            IsBroadcast(rpcRequestMessage.Method) ? throw _failure() : _inner.SendAsync(rpcRequestMessage, route);

        public Task<RpcRequestResponseBatch> SendBatchRequestAsync(RpcRequestResponseBatch rpcRequestResponseBatch) =>
            _inner.SendBatchRequestAsync(rpcRequestResponseBatch);

        private static bool IsBroadcast(string method) => method == InterceptingRpcClient.SendRawMethod;
    }

    private async Task<SettlementResponse> SettleWith(Func<Exception> failure)
    {
        var service = new X402TransferWithAuthorisation3009Service(
            X402DevChainFixture.FacilitatorKey,
            new Dictionary<int, IClient> { [X402DevChainFixture.ChainId] = new FailingBroadcastClient(_chain.Client, failure) });
        var requirements = X402DevChainPayments.Requirements(_chain, "1000000", X402DevChainFixture.RecipientAddress);
        var payload = await X402DevChainPayments.SignedTransfer(_chain, requirements);

        return await service.SettlePaymentAsync(payload, requirements).WaitAsync(TimeSpan.FromSeconds(60));
    }

    [Fact(Timeout = 120000)]
    [Trait("Rule", "D4")]
    public async Task Given_TheNodeAnswersTheBroadcastWithAnInsufficientFundsError_When_Settling_Then_TheFailureIsDefiniteNotPending()
    {
        var settle = await SettleWith(() => new RpcResponseException(new Nethereum.JsonRpc.Client.RpcError(-32000, "insufficient funds for gas * price + value")));

        Assert.False(settle.Success);
        Assert.NotEqual(X402ErrorCodes.SettlementPending, settle.ErrorReason);
    }

    [Theory(Timeout = 120000)]
    [Trait("Rule", "D4")]
    [InlineData("already known")]
    [InlineData("Known transaction")]
    [InlineData("existing txn with same hash")]
    public async Task Given_TheNodeAnswersTheBroadcastWithAnAlreadyKnownError_When_Settling_Then_TheOutcomeIsPending(string message)
    {
        var settle = await SettleWith(() => new RpcResponseException(new Nethereum.JsonRpc.Client.RpcError(-32000, message)));

        Assert.Equal(X402ErrorCodes.SettlementPending, settle.ErrorReason);
    }

    [Fact(Timeout = 120000)]
    [Trait("Rule", "D4")]
    public async Task Given_TheBroadcastAnswerWasLostToAConnectionReset_When_Settling_Then_TheOutcomeStaysPending()
    {
        var settle = await SettleWith(() => new HttpRequestException(HttpRequestError.ResponseEnded, "reset", new IOException("connection reset")));

        Assert.Equal(X402ErrorCodes.SettlementPending, settle.ErrorReason);
    }
}
