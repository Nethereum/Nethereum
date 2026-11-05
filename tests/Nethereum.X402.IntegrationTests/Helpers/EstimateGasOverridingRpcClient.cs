using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Newtonsoft.Json.Linq;

namespace Nethereum.X402.IntegrationTests.Helpers;

public sealed class EstimateGasOverridingRpcClient : IClient
{
    private const string EstimateGasMethod = "eth_estimateGas";

    private readonly IClient _inner;
    private readonly BigInteger _estimate;

    public EstimateGasOverridingRpcClient(IClient inner, BigInteger estimate)
    {
        _inner = inner;
        _estimate = estimate;
    }

    public RequestInterceptor OverridingRequestInterceptor
    {
        get => _inner.OverridingRequestInterceptor;
        set => _inner.OverridingRequestInterceptor = value;
    }

    public T DecodeResult<T>(RpcResponseMessage rpcResponseMessage) => _inner.DecodeResult<T>(rpcResponseMessage);

    public Task SendRequestAsync(RpcRequest request, string? route = null) => _inner.SendRequestAsync(request, route);

    public Task SendRequestAsync(string method, string? route = null, params object[] paramList) =>
        _inner.SendRequestAsync(method, route, paramList);

    public Task<T> SendRequestAsync<T>(RpcRequest request, string? route = null) =>
        request.Method == EstimateGasMethod ? Task.FromResult(Estimate<T>()) : _inner.SendRequestAsync<T>(request, route);

    public Task<T> SendRequestAsync<T>(string method, string? route = null, params object[] paramList) =>
        method == EstimateGasMethod ? Task.FromResult(Estimate<T>()) : _inner.SendRequestAsync<T>(method, route, paramList);

    public Task<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage, string? route = null) =>
        rpcRequestMessage.Method == EstimateGasMethod
            ? Task.FromResult(new RpcResponseMessage(rpcRequestMessage.Id, JToken.FromObject(new HexBigInteger(_estimate).HexValue)))
            : _inner.SendAsync(rpcRequestMessage, route);

    public Task<RpcRequestResponseBatch> SendBatchRequestAsync(RpcRequestResponseBatch rpcRequestResponseBatch) =>
        _inner.SendBatchRequestAsync(rpcRequestResponseBatch);

    private T Estimate<T>() => (T)(object)new HexBigInteger(_estimate);
}
