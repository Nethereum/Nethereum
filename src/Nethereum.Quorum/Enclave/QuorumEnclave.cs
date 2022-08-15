using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json;

namespace Nethereum.Quorum.Enclave
{
    public class QuorumEnclave
    {
        private readonly string _privateEndPoint;
        private AuthenticationHeaderValue _authHeaderValue;

        public QuorumEnclave(string privateEndPoint, AuthenticationHeaderValue authHeaderValue = null)
        {
            _privateEndPoint = privateEndPoint;
            if (authHeaderValue == null)
            {
                authHeaderValue = BasicAuthenticationHeaderHelper.GetBasicAuthenticationHeaderValueFromUri(new Uri(privateEndPoint));
            }
            _authHeaderValue = authHeaderValue;

        }

        public async Task<string> StoreRawAsync(string payload, string from)
        {
            var response = await SendRequestAsync<StoreRawRequest, StoreRawResponse>(new StoreRawRequest() { Payload = payload, From = from },
                "storeraw").ConfigureAwait(false);
            return response.Key;
        }

        public async Task<TResponse> SendRequestAsync<TRequest, TResponse>(TRequest request, string path)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Authorization = _authHeaderValue;
                    httpClient.BaseAddress = new Uri(_privateEndPoint);
                    var jsonRequest = JsonConvert.SerializeObject(request);
                    var httpContent = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                    var httpResponseMessage = await httpClient
                        .PostAsync(path, httpContent).ConfigureAwait(false);
                    httpResponseMessage.EnsureSuccessStatusCode();
                    var response = await httpResponseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return JsonConvert.DeserializeObject<TResponse>(response);
                }
            }
            catch (Exception ex)
            {
                throw new QuorumEnclaveRequestException($"Quorum Enclave request Exception: {ex.Message}", ex);
            }
        }

        public async Task<bool> UpCheckAsync()
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Authorization = _authHeaderValue;
                    httpClient.BaseAddress = new Uri(_privateEndPoint);
                    var httpResponseMessage = await httpClient
                        .GetAsync("upcheck").ConfigureAwait(false);
                    httpResponseMessage.EnsureSuccessStatusCode();
                    var response = await httpResponseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return response == "I'm up!";
                }
            }
            catch (Exception ex)
            {
                throw new QuorumEnclaveRequestException($"Quorum Enclave request Exception: {ex.Message}", ex);
            }
        }
    }
}