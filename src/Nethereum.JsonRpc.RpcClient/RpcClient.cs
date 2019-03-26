using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Nethereum.JsonRpc.Client.RpcMessages;
using Newtonsoft.Json;

namespace Nethereum.JsonRpc.Client
{
    public class RpcClient : ClientBase
    {
        private const int NUMBER_OF_SECONDS_TO_RECREATE_HTTP_CLIENT = 60;
        private readonly AuthenticationHeaderValue _authHeaderValue;
        private readonly Uri _baseUrl;
        private readonly HttpClientHandler _httpClientHandler;
        private readonly ILog _log;
        private readonly JsonSerializerSettings _jsonSerializerSettings;
        private volatile bool _firstHttpClient;
        private HttpClient _httpClient;
        private HttpClient _httpClient2;
        private DateTime _httpClientLastCreatedAt;
        private readonly object _lockObject = new object();

        public RpcClient(Uri baseUrl, AuthenticationHeaderValue authHeaderValue = null,
            JsonSerializerSettings jsonSerializerSettings = null, HttpClientHandler httpClientHandler = null, ILog log = null)
        {
            _baseUrl = baseUrl;

            if (authHeaderValue == null)
            {
                authHeaderValue = UserAuthentication.FromUri(baseUrl).GetBasicAuthenticationHeaderValue();
            }

            _authHeaderValue = authHeaderValue;
            if (jsonSerializerSettings == null)
                jsonSerializerSettings = DefaultJsonSerializerSettingsFactory.BuildDefaultJsonSerializerSettings();
            _jsonSerializerSettings = jsonSerializerSettings;
            _httpClientHandler = httpClientHandler;
            _log = log;
            CreateNewHttpClient();
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

                var stream = await httpResponseMessage.Content.ReadAsStreamAsync();
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
                throw new RpcClientTimeoutException($"Rpc timeout after {ConnectionTimeout.TotalMilliseconds} milliseconds", ex);
            }
            catch (Exception ex)
            {
                logger.LogException(ex);
                throw new RpcClientUnknownException("Error occurred when trying to send rpc requests(s)", ex);
            }
        }

       

        private HttpClient GetOrCreateHttpClient()
        {
            lock (_lockObject)
            {
                var timeSinceCreated = DateTime.UtcNow - _httpClientLastCreatedAt;
                if (timeSinceCreated.TotalSeconds > NUMBER_OF_SECONDS_TO_RECREATE_HTTP_CLIENT)
                    CreateNewHttpClient();
                return GetClient();
            }
        }

        private HttpClient GetClient()
        {
            lock (_lockObject)
            {
                return _firstHttpClient ? _httpClient : _httpClient2;
            }
        }

        private void CreateNewHttpClient()
        {
            var httpClient = _httpClientHandler != null ? new HttpClient(_httpClientHandler) : new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = _authHeaderValue;
            httpClient.BaseAddress = _baseUrl;
            _httpClientLastCreatedAt = DateTime.UtcNow;
            if (_firstHttpClient)
                lock (_lockObject)
                {
                    _firstHttpClient = false;
                    _httpClient2 = httpClient;
                }
            else
                lock (_lockObject)
                {
                    _firstHttpClient = true;
                    _httpClient = httpClient;
                }
        }
    }
}