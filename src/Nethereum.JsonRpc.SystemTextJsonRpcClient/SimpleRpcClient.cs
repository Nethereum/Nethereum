#if NET7_0_OR_GREATER
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.JsonRpc.SystemTextJsonRpcClient
{
    public class SimpleRpcClient : ClientBase
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _serializerOptions;
        private readonly JsonTypeInfo<RpcRequestMessage> _requestTypeInfo;
        private readonly JsonTypeInfo<RpcRequestMessage[]> _requestArrayTypeInfo;
        private readonly JsonTypeInfo<RpcResponseMessage> _responseTypeInfo;
        private readonly JsonTypeInfo<RpcResponseMessage[]> _responseArrayTypeInfo;

        public SimpleRpcClient(
            Uri baseUrl,
            HttpClient httpClient,
            JsonSerializerOptions serializerOptions = null,
            JsonSerializerContext context = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _httpClient.BaseAddress = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));

            _serializerOptions = serializerOptions ?? new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            if (context != null)
            {
                _serializerOptions.TypeInfoResolver = context;
                _requestTypeInfo = context.GetTypeInfo(typeof(RpcRequestMessage)) as JsonTypeInfo<RpcRequestMessage>;
                _requestArrayTypeInfo = context.GetTypeInfo(typeof(RpcRequestMessage[])) as JsonTypeInfo<RpcRequestMessage[]>;
                _responseTypeInfo = context.GetTypeInfo(typeof(RpcResponseMessage)) as JsonTypeInfo<RpcResponseMessage>;
                _responseArrayTypeInfo = context.GetTypeInfo(typeof(RpcResponseMessage[])) as JsonTypeInfo<RpcResponseMessage[]>;
            }
        }

        public SimpleRpcClient(string url)
                                           : this(
                                               new Uri(url),
                                               new HttpClient(),
                                               serializerOptions: null,
                                               context: NethereumRpcJsonContext.Default)
        {
        }

        public override T DecodeResult<T>(RpcResponseMessage rpcResponseMessage)
        {
            return rpcResponseMessage.GetResultSTJ<T>(true, _serializerOptions);
        }

        protected override async Task<RpcResponseMessage> SendAsync(RpcRequestMessage request, string route = null)
        {
            try
            {
                var json = JsonSerializer.Serialize(request, typeof(RpcRequestMessage), _serializerOptions);
                
                var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

                using var cts = new CancellationTokenSource(ConnectionTimeout);
                var response = await _httpClient.PostAsync(route ?? string.Empty, httpContent, cts.Token).ConfigureAwait(false);

                response.EnsureSuccessStatusCode();
                var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

                return _responseTypeInfo != null
                    ? await JsonSerializer.DeserializeAsync(stream, _responseTypeInfo, cts.Token).ConfigureAwait(false)
                    : await JsonSerializer.DeserializeAsync<RpcResponseMessage>(stream, _serializerOptions, cts.Token).ConfigureAwait(false);
            }
            catch (TaskCanceledException ex)
            {
                throw new RpcClientTimeoutException($"Rpc timeout after {ConnectionTimeout.TotalMilliseconds} milliseconds", ex);
            }
            catch (Exception ex)
            {
                throw new RpcClientUnknownException("Error occurred when trying to send rpc request: " + request.Method, ex);
            }
        }

        protected override async Task<RpcResponseMessage[]> SendAsync(RpcRequestMessage[] requests)
        {
            try
            {
                var json = JsonSerializer.Serialize(requests, typeof(RpcRequestMessage[]), _serializerOptions);
             
                var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

                using var cts = new CancellationTokenSource(ConnectionTimeout);
                var response = await _httpClient.PostAsync(string.Empty, httpContent, cts.Token).ConfigureAwait(false);

                response.EnsureSuccessStatusCode();
                var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

                return _responseArrayTypeInfo != null
                    ? await JsonSerializer.DeserializeAsync(stream, _responseArrayTypeInfo, cts.Token).ConfigureAwait(false)
                    : await JsonSerializer.DeserializeAsync<RpcResponseMessage[]>(stream, _serializerOptions, cts.Token).ConfigureAwait(false);
            }
            catch (TaskCanceledException ex)
            {
                throw new RpcClientTimeoutException($"Rpc timeout after {ConnectionTimeout.TotalMilliseconds} milliseconds", ex);
            }
            catch (Exception ex)
            {
                throw new RpcClientUnknownException("Error occurred when trying to send rpc requests", ex);
            }
        }
    }
}
#endif
