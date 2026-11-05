using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.X402.IntegrationTests.Helpers;

public sealed class ReceiptUnavailableClient : IClient
{
    private const string ReceiptMethod = "eth_getTransactionReceipt";

    private readonly IClient _inner;

    public ReceiptUnavailableClient(IClient inner)
    {
        _inner = inner;
    }

    public RequestInterceptor OverridingRequestInterceptor
    {
        get => _inner.OverridingRequestInterceptor;
        set => _inner.OverridingRequestInterceptor = value;
    }

    public T DecodeResult<T>(RpcResponseMessage rpcResponseMessage) => _inner.DecodeResult<T>(rpcResponseMessage);

    public Task SendRequestAsync(RpcRequest request, string? route = null) =>
        Guard(request.Method, () => _inner.SendRequestAsync(request, route));

    public Task SendRequestAsync(string method, string? route = null, params object[] paramList) =>
        Guard(method, () => _inner.SendRequestAsync(method, route, paramList));

    public Task<T> SendRequestAsync<T>(RpcRequest request, string? route = null) =>
        Guard(request.Method, () => _inner.SendRequestAsync<T>(request, route));

    public Task<T> SendRequestAsync<T>(string method, string? route = null, params object[] paramList) =>
        Guard(method, () => _inner.SendRequestAsync<T>(method, route, paramList));

    public Task<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage, string? route = null) =>
        Guard(rpcRequestMessage.Method, () => _inner.SendAsync(rpcRequestMessage, route));

    public Task<RpcRequestResponseBatch> SendBatchRequestAsync(RpcRequestResponseBatch rpcRequestResponseBatch) =>
        Guard(
            rpcRequestResponseBatch.GetRpcRequests().Select(request => request.Method).FirstOrDefault(m => m == ReceiptMethod),
            () => _inner.SendBatchRequestAsync(rpcRequestResponseBatch));

    private static Task Guard(string? method, Func<Task> call) =>
        method == ReceiptMethod ? throw Unavailable() : call();

    private static Task<T> Guard<T>(string? method, Func<Task<T>> call) =>
        method == ReceiptMethod ? throw Unavailable() : call();

    private static RpcClientUnknownException Unavailable() =>
        new($"{ReceiptMethod} is unavailable", new HttpRequestException("receipt endpoint unreachable"));
}
