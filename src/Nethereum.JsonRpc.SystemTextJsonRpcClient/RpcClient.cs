#if NET7_0_OR_GREATER
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.JsonRpc.SystemTextJsonRpcClient
{

    public class RpcClient : ClientBase
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger? _logger;
        private readonly JsonSerializerOptions _serializerOptions;
        private readonly JsonTypeInfo<RpcRequestMessage>? _requestTypeInfo;
        private readonly JsonTypeInfo<RpcRequestMessage[]>? _requestArrayTypeInfo;
        private readonly JsonTypeInfo<RpcResponseMessage>? _responseTypeInfo;
        private readonly JsonTypeInfo<RpcResponseMessage[]>? _responseArrayTypeInfo;

        public RpcClient(
            Uri baseUrl,
            ILogger? logger = null,
            AuthenticationHeaderValue? authHeader = null,
            HttpMessageHandler? handler = null,
            JsonSerializerContext? context = null,
            JsonSerializerOptions? serializerOptions = null)
        {
            _logger = logger;

            if (authHeader == null)
            {
                authHeader = BasicAuthenticationHeaderHelper.GetBasicAuthenticationHeaderValueFromUri(baseUrl);
            }

            handler ??= RpcHttpHandlerFactory.Create();

            _httpClient = new HttpClient(handler)
            {
                BaseAddress = baseUrl
            };

            if (authHeader != null)
            {
                _httpClient.DefaultRequestHeaders.Authorization = authHeader;
            }

            _serializerOptions = serializerOptions ?? new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
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

        public RpcClient(string url, ILogger? logger = null)
            : this(new Uri(url), logger, null, null, NethereumRpcJsonContext.Default) { }

        public void AddBearerToken(string token)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        public override T DecodeResult<T>(RpcResponseMessage rpcResponseMessage)
        {
            return rpcResponseMessage.GetResultSTJ<T>(true, _serializerOptions);
        }

        protected override async Task<RpcResponseMessage> SendAsync(RpcRequestMessage request, string? route = null)
        {
            try
            {
                var json = JsonSerializer.Serialize(request, typeof(RpcRequestMessage), _serializerOptions);

                _logger?.LogDebug("Sending request: {Json}", json);

                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var cts = new CancellationTokenSource(ConnectionTimeout);
                using var response = await _httpClient.PostAsync(route ?? string.Empty, content, cts.Token).ConfigureAwait(false);

                response.EnsureSuccessStatusCode();

                var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

                var rpcResponse = _responseTypeInfo != null
                    ? await JsonSerializer.DeserializeAsync(stream, _responseTypeInfo, cts.Token).ConfigureAwait(false)
                    : await JsonSerializer.DeserializeAsync<RpcResponseMessage>(stream, _serializerOptions, cts.Token).ConfigureAwait(false);

                _logger?.LogDebug("Received response: {Response}", JsonSerializer.Serialize(rpcResponse, _serializerOptions));
               // Debug.WriteLine(string.Format("Received response: {0}", JsonSerializer.Serialize(rpcResponse, _serializerOptions)));
                return rpcResponse!;
            }
            catch (TaskCanceledException ex)
            {
                var timeoutEx = new RpcClientTimeoutException($"RPC timeout after {ConnectionTimeout.TotalMilliseconds} ms", ex);
                _logger?.LogError(timeoutEx, "Timeout error");
                throw timeoutEx;
            }
            catch (Exception ex)
            {
                var rpcEx = new RpcClientUnknownException($"Error calling RPC method '{request.Method}'", ex);
                _logger?.LogError(rpcEx, "RPC call error");
                throw rpcEx;
            }
        }

        protected override async Task<RpcResponseMessage[]> SendAsync(RpcRequestMessage[] requests)
        {
            try
            {
                var json = JsonSerializer.Serialize(requests, typeof(RpcRequestMessage[]), _serializerOptions);

                _logger?.LogDebug("Sending batch request: {Json}", json);

                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var cts = new CancellationTokenSource(ConnectionTimeout);
                using var response = await _httpClient.PostAsync(string.Empty, content, cts.Token).ConfigureAwait(false);

                response.EnsureSuccessStatusCode();

                var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

                var rpcResponses = _responseArrayTypeInfo != null
                    ? await JsonSerializer.DeserializeAsync(stream, _responseArrayTypeInfo, cts.Token).ConfigureAwait(false)
                    : await JsonSerializer.DeserializeAsync<RpcResponseMessage[]>(stream, _serializerOptions, cts.Token).ConfigureAwait(false);

                _logger?.LogDebug("Received batch response: {Response}", JsonSerializer.Serialize(rpcResponses, _serializerOptions));
                return rpcResponses!;
            }
            catch (TaskCanceledException ex)
            {
                var timeoutEx = new RpcClientTimeoutException($"RPC timeout after {ConnectionTimeout.TotalMilliseconds} ms", ex);
                _logger?.LogError(timeoutEx, "Batch timeout error");
                throw timeoutEx;
            }
            catch (Exception ex)
            {
                var rpcEx = new RpcClientUnknownException("Error calling batch RPC methods", ex);
                _logger?.LogError(rpcEx, "Batch RPC call error");
                throw rpcEx;
            }
        }
    }
}
#endif