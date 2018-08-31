using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client.RpcMessages;
using Newtonsoft.Json;

namespace Nethereum.JsonRpc.Client
{
    public class SimpleRpcClient : ClientBase
    {
        private readonly JsonSerializerSettings _jsonSerializerSettings;
        private readonly HttpClient _httpClient;

        public SimpleRpcClient(Uri baseUrl, HttpClient httpClient,
            JsonSerializerSettings jsonSerializerSettings = null)
        { 
            if (jsonSerializerSettings == null)
                jsonSerializerSettings = DefaultJsonSerializerSettingsFactory.BuildDefaultJsonSerializerSettings();
            _jsonSerializerSettings = jsonSerializerSettings;
            _httpClient = httpClient;
            _httpClient.BaseAddress = baseUrl;
        }

        protected override async Task<RpcResponseMessage> SendAsync(RpcRequestMessage request, string route = null)
        {
            try
            {
                var rpcRequestJson = JsonConvert.SerializeObject(request, _jsonSerializerSettings);
                var httpContent = new StringContent(rpcRequestJson, Encoding.UTF8, "application/json");

                var cancellationTokenSource = new CancellationTokenSource();
                cancellationTokenSource.CancelAfter(ConnectionTimeout);

                var httpResponseMessage = await _httpClient.PostAsync(route, httpContent, cancellationTokenSource.Token)
                    .ConfigureAwait(false);
                httpResponseMessage.EnsureSuccessStatusCode();

                var stream = await httpResponseMessage.Content.ReadAsStreamAsync();
                using (var streamReader = new StreamReader(stream))
                using (var reader = new JsonTextReader(streamReader))
                {
                    var serializer = JsonSerializer.Create(_jsonSerializerSettings);
                    var message = serializer.Deserialize<RpcResponseMessage>(reader);

                    return message;
                }
            }
            catch (TaskCanceledException ex)
            {
                throw new RpcClientTimeoutException($"Rpc timeout after {ConnectionTimeout.TotalMilliseconds} milliseconds", ex);
            }
            catch (Exception ex)
            {
                throw new RpcClientUnknownException("Error occurred when trying to send rpc requests(s)", ex);
            }
        }
    }
}