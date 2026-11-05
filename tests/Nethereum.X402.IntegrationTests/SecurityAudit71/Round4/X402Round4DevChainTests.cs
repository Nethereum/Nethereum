using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.X402.Blockchain;
using Nethereum.X402.IntegrationTests.Blockchain;
using Nethereum.X402.IntegrationTests.Helpers;
using Nethereum.X402.Models;
using Xunit;

namespace Nethereum.X402.IntegrationTests.SecurityAudit71.Round4;

[Collection("X402 DevChain E2E")]
public class X402Round4DevChainTests : IClassFixture<X402DevChainFixture>
{
    private readonly X402DevChainFixture _chain;

    public X402Round4DevChainTests(X402DevChainFixture chain)
    {
        _chain = chain;
    }

    private sealed class LostBroadcastAnswerClient : IClient
    {
        private readonly IClient _inner;

        public LostBroadcastAnswerClient(IClient inner) => _inner = inner;

        public RequestInterceptor OverridingRequestInterceptor
        {
            get => _inner.OverridingRequestInterceptor;
            set => _inner.OverridingRequestInterceptor = value;
        }

        public T DecodeResult<T>(RpcResponseMessage rpcResponseMessage) => _inner.DecodeResult<T>(rpcResponseMessage);

        public Task SendRequestAsync(RpcRequest request, string? route = null) => Run(request.Method, () => _inner.SendRequestAsync(request, route));

        public Task SendRequestAsync(string method, string? route = null, params object[] paramList) => Run(method, () => _inner.SendRequestAsync(method, route, paramList));

        public Task<T> SendRequestAsync<T>(RpcRequest request, string? route = null) => Run(request.Method, () => _inner.SendRequestAsync<T>(request, route));

        public Task<T> SendRequestAsync<T>(string method, string? route = null, params object[] paramList) => Run(method, () => _inner.SendRequestAsync<T>(method, route, paramList));

        public Task<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage, string? route = null) => Run(rpcRequestMessage.Method, () => _inner.SendAsync(rpcRequestMessage, route));

        public Task<RpcRequestResponseBatch> SendBatchRequestAsync(RpcRequestResponseBatch rpcRequestResponseBatch) => _inner.SendBatchRequestAsync(rpcRequestResponseBatch);

        private async Task Run(string method, Func<Task> call)
        {
            await call();
            if (method == InterceptingRpcClient.SendRawMethod)
                throw new HttpRequestException("The response was lost.", new IOException("connection reset"));
        }

        private async Task<T> Run<T>(string method, Func<Task<T>> call)
        {
            var result = await call();
            if (method == InterceptingRpcClient.SendRawMethod)
                throw new HttpRequestException("The response was lost.", new IOException("connection reset"));
            return result;
        }
    }

    [Fact(Timeout = 120000)]
    [Trait("Rule", "R4-4")]
    public async Task Given_TheBroadcastWasAcceptedButItsAnswerWasLost_When_Settling_Then_TheOutcomeIsPendingNotADefiniteFailure()
    {
        var service = new X402TransferWithAuthorisation3009Service(
            X402DevChainFixture.FacilitatorKey,
            new Dictionary<int, IClient> { [X402DevChainFixture.ChainId] = new LostBroadcastAnswerClient(_chain.Client) });
        var requirements = X402DevChainPayments.Requirements(_chain, "1000000", X402DevChainFixture.RecipientAddress);
        var payload = await X402DevChainPayments.SignedTransfer(_chain, requirements);

        var settle = await service.SettlePaymentAsync(payload, requirements).WaitAsync(TimeSpan.FromSeconds(60));

        Assert.Equal(X402ErrorCodes.SettlementPending, settle.ErrorReason);
    }
}
