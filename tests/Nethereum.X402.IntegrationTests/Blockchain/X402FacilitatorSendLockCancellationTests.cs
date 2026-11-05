using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Signer;
using Nethereum.X402.Blockchain;
using Nethereum.X402.IntegrationTests.Helpers;
using Nethereum.X402.Models;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Blockchain;

[Collection("X402 DevChain E2E")]
public class X402FacilitatorSendLockCancellationTests : IClassFixture<X402DevChainFixture>
{
    private const string PendingNonceMethod = "eth_getTransactionCount";
    private const string BroadcastMethod = "eth_sendRawTransaction";

    private readonly X402DevChainFixture _chain;

    public X402FacilitatorSendLockCancellationTests(X402DevChainFixture chain)
    {
        _chain = chain;
    }

    [Fact]
    [Trait("Rule", "G2")]
    public async Task Given_TheFacilitatorSendLockIsHeld_When_ACancelledCallerWaitsForIt_Then_ItStopsWaitingAndBroadcastsNothing()
    {
        var gated = new LockHoldingClient(_chain.Client, PendingNonceMethod, BroadcastMethod);
        var service = new X402TransferWithAuthorisation3009Service(
            EthECKey.GenerateKey().GetPrivateKey(),
            new Dictionary<int, IClient> { [X402DevChainFixture.ChainId] = gated });
        var network = Caip2.FormatEip155(X402DevChainFixture.ChainId);
        var nonce = new byte[32];

#pragma warning disable CS0618
        var holder = service.CancelAuthorizationAsync(X402DevChainFixture.PayerAddress, nonce, network, _chain.TokenAddress);
        try
        {
            await gated.Held.Task.WaitAsync(TimeSpan.FromSeconds(30));

            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            var waiter = service.CancelAuthorizationAsync(
                X402DevChainFixture.PayerAddress, nonce, network, _chain.TokenAddress, cancelled.Token);

            var finished = await Task.WhenAny(waiter, Task.Delay(TimeSpan.FromSeconds(5)));

            Assert.Same(waiter, finished);
            Assert.False((await waiter).Success);
            Assert.Equal(0, gated.Seen(BroadcastMethod));
        }
        finally
        {
            gated.Release();
            await holder;
        }
#pragma warning restore CS0618
    }

    private sealed class LockHoldingClient : IClient
    {
        private readonly IClient _inner;
        private readonly string _holdMethod;
        private readonly string _countedMethod;
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _held;
        private int _counted;

        public LockHoldingClient(IClient inner, string holdMethod, string countedMethod)
        {
            _inner = inner;
            _holdMethod = holdMethod;
            _countedMethod = countedMethod;
        }

        public TaskCompletionSource Held { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Seen(string method) => method == _countedMethod ? Volatile.Read(ref _counted) : 0;

        public void Release() => _release.TrySetResult();

        public RequestInterceptor OverridingRequestInterceptor
        {
            get => _inner.OverridingRequestInterceptor;
            set => _inner.OverridingRequestInterceptor = value;
        }

        public T DecodeResult<T>(RpcResponseMessage rpcResponseMessage) => _inner.DecodeResult<T>(rpcResponseMessage);

        public Task SendRequestAsync(RpcRequest request, string? route = null) => _inner.SendRequestAsync(request, route);

        public Task SendRequestAsync(string method, string? route = null, params object[] paramList) =>
            _inner.SendRequestAsync(method, route, paramList);

        public async Task<T> SendRequestAsync<T>(RpcRequest request, string? route = null)
        {
            await Gate(request.Method);
            return await _inner.SendRequestAsync<T>(request, route);
        }

        public async Task<T> SendRequestAsync<T>(string method, string? route = null, params object[] paramList)
        {
            await Gate(method);
            return await _inner.SendRequestAsync<T>(method, route, paramList);
        }

        public async Task<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage, string? route = null)
        {
            await Gate(rpcRequestMessage.Method);
            return await _inner.SendAsync(rpcRequestMessage, route);
        }

        public Task<RpcRequestResponseBatch> SendBatchRequestAsync(RpcRequestResponseBatch rpcRequestResponseBatch) =>
            _inner.SendBatchRequestAsync(rpcRequestResponseBatch);

        private async Task Gate(string method)
        {
            if (method == _countedMethod)
                Interlocked.Increment(ref _counted);

            if (method == _holdMethod && Interlocked.Exchange(ref _held, 1) == 0)
            {
                Held.TrySetResult();
                await _release.Task;
            }
        }
    }
}
