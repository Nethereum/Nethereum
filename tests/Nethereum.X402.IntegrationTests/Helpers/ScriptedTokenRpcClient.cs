using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.X402.IntegrationTests.Helpers;

public sealed class ScriptedTokenRpcClient : IClient
{
    private const string AllowanceSelector = "0xdd62ed3e";
    private const string NoncesSelector = "0x7ecebe00";

    public BigInteger Allowance { get; set; }

    public BigInteger Nonce { get; set; }

    public bool AllowanceReadFails { get; set; }

    public BigInteger PendingTransactionCount { get; set; }

    public List<string> Calls { get; } = new();

    public List<string> Methods { get; } = new();

    public RequestInterceptor OverridingRequestInterceptor { get; set; } = null!;

    public T DecodeResult<T>(RpcResponseMessage rpcResponseMessage) => rpcResponseMessage.GetResult<T>();

    public Task SendRequestAsync(RpcRequest request, string? route = null) =>
        Task.FromResult(Respond<string>(request));

    public Task SendRequestAsync(string method, string? route = null, params object[] paramList) =>
        Task.FromResult(Respond<string>(new RpcRequest(1, method, paramList)));

    public Task<T> SendRequestAsync<T>(RpcRequest request, string? route = null) =>
        Task.FromResult(Respond<T>(request));

    public Task<T> SendRequestAsync<T>(string method, string? route = null, params object[] paramList) =>
        Task.FromResult(Respond<T>(new RpcRequest(1, method, paramList)));

    public Task<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage, string? route = null) =>
        throw new NotSupportedException(rpcRequestMessage.Method);

    public Task<RpcRequestResponseBatch> SendBatchRequestAsync(RpcRequestResponseBatch rpcRequestResponseBatch) =>
        throw new NotSupportedException();

    private T Respond<T>(RpcRequest request)
    {
        Methods.Add(request.Method);
        if (request.Method == "eth_getTransactionCount")
            return (T)(object)new Nethereum.Hex.HexTypes.HexBigInteger(PendingTransactionCount);

        if (request.Method != "eth_call")
            throw new NotSupportedException(request.Method);

        var data = ((CallInput)request.RawParameters[0]).Data;
        var selector = data.Substring(0, 10);
        Calls.Add(selector);

        switch (selector)
        {
            case AllowanceSelector:
                if (AllowanceReadFails)
                    throw new RpcClientUnknownException("allowance read failed", new HttpRequestException("rpc unreachable"));
                return (T)(object)Word(Allowance);
            case NoncesSelector:
                return (T)(object)Word(Nonce);
            default:
                throw new NotSupportedException(selector);
        }
    }

    private static string Word(BigInteger value) =>
        "0x" + value.ToByteArray(isUnsigned: true, isBigEndian: true).ToHex().PadLeft(64, '0');
}
