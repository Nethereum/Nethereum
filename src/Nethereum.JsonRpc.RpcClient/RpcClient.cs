using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
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
        private readonly int CONNECTION_LIMIT = 2000; //max 2000 connections
        private readonly int CONNECTION_LEASE_TIMEOUT = 10000; //10 seconds for reconnect

        private readonly AuthenticationHeaderValue _authHeaderValue;
        private readonly Uri _baseUrl;
        private readonly HttpClientHandler _httpClientHandler;
        private readonly ILog _log;
        private readonly JsonSerializerSettings _jsonSerializerSettings;
        private static readonly object _lockObject = new object();
        private readonly HttpClient _httpClient;
        private static volatile Dictionary<string, HttpClient> _httpClients = new Dictionary<string, HttpClient>();

        public RpcClient(Uri baseUrl, AuthenticationHeaderValue authHeaderValue = null,
            JsonSerializerSettings jsonSerializerSettings = null, HttpClientHandler httpClientHandler = null, ILog log = null)
        {
            _baseUrl = baseUrl;

            if (authHeaderValue == null)
            {
                authHeaderValue = UserAuthentication.FromUri(baseUrl)?.GetBasicAuthenticationHeaderValue();
            }

            _authHeaderValue = authHeaderValue;
            if (jsonSerializerSettings == null)
                jsonSerializerSettings = DefaultJsonSerializerSettingsFactory.BuildDefaultJsonSerializerSettings();
            _jsonSerializerSettings = jsonSerializerSettings;
            _httpClientHandler = httpClientHandler;
            _log = log;
            _httpClient = GetClient();
        }

        protected override async Task<RpcResponseMessage> SendAsync(RpcRequestMessage request, string route = null)
        {
            var logger = new RpcLogger(_log);
            var cancellationTokenSource = new CancellationTokenSource();
            try
            {
                var rpcRequestJson = JsonConvert.SerializeObject(request, _jsonSerializerSettings);
                var httpContent = new StringContent(rpcRequestJson, Encoding.UTF8, "application/json");
                cancellationTokenSource.CancelAfter(ConnectionTimeout);

                logger.LogRequest(rpcRequestJson);

                using (var httpResponseMessage = await _httpClient.PostAsync(route, httpContent, cancellationTokenSource.Token).ConfigureAwait(false))
                {
                    httpResponseMessage.EnsureSuccessStatusCode();

                    var stream = await httpResponseMessage.Content.ReadAsStreamAsync();
                    using (var streamReader = new StreamReader(stream))
                    using (var reader = new JsonTextReader(streamReader))
                    {
                        var serializer = JsonSerializer.Create(_jsonSerializerSettings);
                        var message = serializer.Deserialize<RpcResponseMessage>(reader);

                        logger.LogResponse(message);

                        return message;
                    }
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
                var exception = new RpcClientUnknownException("Error occurred when trying to send rpc requests(s)", ex);
                logger.LogException(exception);
                throw exception;
            }
            finally
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();
            }
        }

        private HttpClient GetClient()
        {
            lock (_lockObject)
            {
                if (_httpClients.ContainsKey(_baseUrl.AbsoluteUri))
                {
                    return _httpClients[_baseUrl.AbsoluteUri];
                }
                var httpClient = _httpClientHandler != null ? new HttpClient(_httpClientHandler) : new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = _authHeaderValue;
                httpClient.BaseAddress = _baseUrl;
                httpClient.DefaultRequestHeaders.ConnectionClose = false; //keepAlive by default for reuse connection
                var servicePoint = ServicePointManager.FindServicePoint(_baseUrl);
                servicePoint.ConnectionLimit = CONNECTION_LIMIT;
                servicePoint.ConnectionLeaseTimeout = CONNECTION_LEASE_TIMEOUT;
                _httpClients.Add(_baseUrl.AbsoluteUri, httpClient); //store for reuse
                return httpClient;
            }
        }
    }
}