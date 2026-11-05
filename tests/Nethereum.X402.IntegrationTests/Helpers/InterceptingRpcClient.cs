using System.Collections.Concurrent;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.X402.IntegrationTests.Helpers;

public sealed class InterceptingRpcClient : IClient
{
    public static readonly Task Never = new TaskCompletionSource().Task;

    public const string ReceiptMethod = "eth_getTransactionReceipt";
    public const string SendRawMethod = "eth_sendRawTransaction";

    private readonly IClient _inner;
    private readonly Func<string, Task> _before;
    private readonly ConcurrentQueue<string> _methods = new();

    public InterceptingRpcClient(IClient inner, Func<string, Task>? before = null)
    {
        _inner = inner;
        _before = before ?? (_ => Task.CompletedTask);
    }

    public IReadOnlyCollection<string> Methods => _methods.ToArray();

    public RequestInterceptor OverridingRequestInterceptor
    {
        get => _inner.OverridingRequestInterceptor;
        set => _inner.OverridingRequestInterceptor = value;
    }

    public T DecodeResult<T>(RpcResponseMessage rpcResponseMessage) => _inner.DecodeResult<T>(rpcResponseMessage);

    public Task SendRequestAsync(RpcRequest request, string? route = null) =>
        Run(request.Method, () => _inner.SendRequestAsync(request, route));

    public Task SendRequestAsync(string method, string? route = null, params object[] paramList) =>
        Run(method, () => _inner.SendRequestAsync(method, route, paramList));

    public Task<T> SendRequestAsync<T>(RpcRequest request, string? route = null) =>
        Run(request.Method, () => _inner.SendRequestAsync<T>(request, route));

    public Task<T> SendRequestAsync<T>(string method, string? route = null, params object[] paramList) =>
        Run(method, () => _inner.SendRequestAsync<T>(method, route, paramList));

    public Task<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage, string? route = null) =>
        Run(rpcRequestMessage.Method, () => _inner.SendAsync(rpcRequestMessage, route));

    public Task<RpcRequestResponseBatch> SendBatchRequestAsync(RpcRequestResponseBatch rpcRequestResponseBatch) =>
        _inner.SendBatchRequestAsync(rpcRequestResponseBatch);

    private async Task Run(string method, Func<Task> call)
    {
        _methods.Enqueue(method);
        await _before(method).ConfigureAwait(false);
        await call().ConfigureAwait(false);
    }

    private async Task<T> Run<T>(string method, Func<Task<T>> call)
    {
        _methods.Enqueue(method);
        await _before(method).ConfigureAwait(false);
        return await call().ConfigureAwait(false);
    }
}
