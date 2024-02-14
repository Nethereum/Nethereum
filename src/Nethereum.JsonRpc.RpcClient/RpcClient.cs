using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#if NETSTANDARD2_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER || NET461_OR_GREATER || NET5_0_OR_GREATER
using Microsoft.Extensions.Logging;
#endif
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json;

namespace Nethereum.JsonRpc.Client
{
    public class RpcClient : ClientBase
    {
        private const int NUMBER_OF_SECONDS_TO_RECREATE_HTTP_CLIENT = 60;
        private const int NUMBER_OF_MINUTES_TO_POOL_CONNECTIONS = 10;
        private const int NUMBER_OF_MINUTES_TO_IDLE_CONNECTIONS = 5;
        public static int MaximumConnectionsPerServer { get; set; } = 20;
        private readonly AuthenticationHeaderValue _authHeaderValue;
        private readonly Uri _baseUrl;
        private readonly HttpClientHandler _httpClientHandler;
        private readonly ILogger _log;
        private readonly JsonSerializerSettings _jsonSerializerSettings;
        private volatile bool _firstHttpClient;
        private HttpClient _httpClient;
        private HttpClient _httpClient2;
        private bool _rotateHttpClients = true;
        private DateTime _httpClientLastCreatedAt;
        private readonly object _lockObject = new object();

        public RpcClient(Uri baseUrl, AuthenticationHeaderValue authHeaderValue = null,
            JsonSerializerSettings jsonSerializerSettings = null, HttpClientHandler httpClientHandler = null, ILogger log = null)
        {
            _baseUrl = baseUrl;

            if (authHeaderValue == null)
            {
                authHeaderValue = BasicAuthenticationHeaderHelper.GetBasicAuthenticationHeaderValueFromUri(baseUrl);
            }

            _authHeaderValue = authHeaderValue;

            if (jsonSerializerSettings == null)
                jsonSerializerSettings = DefaultJsonSerializerSettingsFactory.BuildDefaultJsonSerializerSettings();
            
            _jsonSerializerSettings = jsonSerializerSettings;
            _httpClientHandler = httpClientHandler;
            _log = log;

#if NETCOREAPP2_1 || NETCOREAPP3_1 || NET5_0_OR_GREATER
            _httpClient = CreateNewHttpClient();
            _rotateHttpClients = false;
#else
            CreateNewRotatedHttpClient();
#endif
        }

        private static HttpMessageHandler GetDefaultHandler()
        {
            try
            {
#if NETSTANDARD2_0
                return new HttpClientHandler
                {
                    MaxConnectionsPerServer = MaximumConnectionsPerServer
                };
           
#elif NETCOREAPP2_1 || NETCOREAPP3_1 || NET5_0_OR_GREATER
                return new SocketsHttpHandler
                {
                    PooledConnectionLifetime = TimeSpan.FromMinutes(NUMBER_OF_MINUTES_TO_POOL_CONNECTIONS),
                    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(NUMBER_OF_MINUTES_TO_IDLE_CONNECTIONS),
                    MaxConnectionsPerServer = MaximumConnectionsPerServer
                };
#else
                return null;
#endif
            }
            catch
            {
                return null;
            }
        }

        public RpcClient(Uri baseUrl, HttpClient httpClient, AuthenticationHeaderValue authHeaderValue = null,
           JsonSerializerSettings jsonSerializerSettings = null, ILogger log = null)
        {
            _baseUrl = baseUrl;

            if (authHeaderValue == null)
            {
                authHeaderValue = BasicAuthenticationHeaderHelper.GetBasicAuthenticationHeaderValueFromUri(baseUrl);
            }

            _authHeaderValue = authHeaderValue;
            if (jsonSerializerSettings == null)
                jsonSerializerSettings = DefaultJsonSerializerSettingsFactory.BuildDefaultJsonSerializerSettings();
            _jsonSerializerSettings = jsonSerializerSettings;
            _log = log;
            InitialiseHttpClient(httpClient);
            _httpClient = httpClient;
            _rotateHttpClients = false;
        }


        protected override async Task<RpcResponseMessage[]> SendAsync(RpcRequestMessage[] requests)
        {
            var logger = new RpcLogger(_log);
            try
            {
                var httpClient = GetOrCreateHttpClient();
                var rpcRequestJson = JsonConvert.SerializeObject(requests, _jsonSerializerSettings);
                var httpContent = new StringContent(rpcRequestJson, Encoding.UTF8, "application/json");
                var cancellationTokenSource = new CancellationTokenSource();
                cancellationTokenSource.CancelAfter(ConnectionTimeout);

                logger.LogRequest(rpcRequestJson);

                var httpResponseMessage = await httpClient.PostAsync(String.Empty, httpContent, cancellationTokenSource.Token).ConfigureAwait(false);
                httpResponseMessage.EnsureSuccessStatusCode();

                var stream = await httpResponseMessage.Content.ReadAsStreamAsync().ConfigureAwait(false);
                using (var streamReader = new StreamReader(stream))
                using (var reader = new JsonTextReader(streamReader))
                {
                    var serializer = JsonSerializer.Create(_jsonSerializerSettings);
                    var messages = serializer.Deserialize<RpcResponseMessage[]>(reader);

                    return messages;
                }
            }
            catch (TaskCanceledException ex)
            {
                var exception = new RpcClientTimeoutException($"Rpc timeout after {ConnectionTimeout.TotalMilliseconds} milliseconds", ex);
                logger.LogException(exception);
                throw exception;
            }
            catch (Exception ex)
            {
                var exception = new RpcClientUnknownException("Error occurred when trying to send multiple rpc requests(s)", ex);
                logger.LogException(exception);
                throw exception;
            }
        }

        protected override async Task<RpcResponseMessage> SendAsync(RpcRequestMessage request, string route = null)
        {
            var logger = new RpcLogger(_log);
            try
            {
                var httpClient = GetOrCreateHttpClient();
                var rpcRequestJson = JsonConvert.SerializeObject(request, _jsonSerializerSettings);
                var httpContent = new StringContent(rpcRequestJson, Encoding.UTF8, "application/json");
                var cancellationTokenSource = new CancellationTokenSource();
                cancellationTokenSource.CancelAfter(ConnectionTimeout);

                logger.LogRequest(rpcRequestJson);

                var httpResponseMessage = await httpClient.PostAsync(route, httpContent, cancellationTokenSource.Token).ConfigureAwait(false);
                httpResponseMessage.EnsureSuccessStatusCode();

                var stream = await httpResponseMessage.Content.ReadAsStreamAsync().ConfigureAwait(false);
                using (var streamReader = new StreamReader(stream))
                using (var reader = new JsonTextReader(streamReader))
                {
                    var serializer = JsonSerializer.Create(_jsonSerializerSettings);
                    var message =  serializer.Deserialize<RpcResponseMessage>(reader);

                    logger.LogResponse(message);
                    
                    return message;
                }
            }
            catch(TaskCanceledException ex)
            {
                 var exception = new RpcClientTimeoutException($"Rpc timeout after {ConnectionTimeout.TotalMilliseconds} milliseconds", ex);
                 logger.LogException(exception);
                 throw exception;
            }
            catch (Exception ex)
            {
                var exception = new RpcClientUnknownException("Error occurred when trying to send rpc requests(s): " + request.Method, ex);
                logger.LogException(exception);
                throw exception;
            }
        }

        private HttpClient GetOrCreateHttpClient()
        {
            if (_rotateHttpClients) //already created if not rotated
            {
                lock (_lockObject)
                {
                    var timeSinceCreated = DateTime.UtcNow - _httpClientLastCreatedAt;
                    if (timeSinceCreated.TotalSeconds > NUMBER_OF_SECONDS_TO_RECREATE_HTTP_CLIENT)
                        CreateNewRotatedHttpClient();
                    return GetClient();
                }
            }
            else
            {
                return GetClient();
            }
        }

        private HttpClient GetClient()
        {
            if (_rotateHttpClients)
            {
                lock (_lockObject)
                {

                    return _firstHttpClient ? _httpClient : _httpClient2;
                }
            }
            else
            {
                return _httpClient;
            }
        }

        private void CreateNewRotatedHttpClient()
        {
            var httpClient = CreateNewHttpClient();
            _httpClientLastCreatedAt = DateTime.UtcNow;
            
            if (_firstHttpClient)
            {
                lock (_lockObject)
                {
                    _firstHttpClient = false;
                    _httpClient2 = httpClient;
                }
            }
            else
            {
                lock (_lockObject)
                {
                    _firstHttpClient = true;
                    _httpClient = httpClient;
                }
            }
        }

        private HttpClient CreateNewHttpClient()
        {
            HttpClient httpClient = new HttpClient();
            
            if (_httpClientHandler != null)
            {
                httpClient = new HttpClient(_httpClientHandler);
            }
            else
            {
                var handler = GetDefaultHandler();
                if(handler != null)
                {
                    httpClient = new HttpClient(handler);
                }
            }

            InitialiseHttpClient(httpClient);
            return httpClient;
        }

        private void InitialiseHttpClient(HttpClient httpClient)
        {
            httpClient.DefaultRequestHeaders.Authorization = _authHeaderValue;
            httpClient.BaseAddress = _baseUrl;
        }

    
    }
}