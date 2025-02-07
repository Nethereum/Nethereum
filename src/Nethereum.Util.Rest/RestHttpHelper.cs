#if !DOTNET35

using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net.Http.Headers;


#if NET8_0_OR_GREATER
using System.Text.Json; // For System.Text.Json in .NET 5+
#else
using Newtonsoft.Json; // For Newtonsoft.Json in older versions
#endif

namespace Nethereum.Util.Rest
{
    public class RestHttpHelper : IRestHttpHelper
    {
        private readonly HttpClient _httpClient;

        public RestHttpHelper(HttpClient httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient();
        }

       
        public async Task<T> GetAsync<T>(string path, Dictionary<string, string> headers = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);

            if (headers != null)
            {
                foreach (var header in headers)
                {
                    request.Headers.Add(header.Key, header.Value);
                }
            }

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();

#if NET8_0_OR_GREATER
                return JsonSerializer.Deserialize<T>(content); 
#else
                return JsonConvert.DeserializeObject<T>(content); 
#endif
            }
            else
            {
                throw new Exception($"Error: {response.StatusCode}, {await response.Content.ReadAsStringAsync()}, {response.Headers}");
            }
        }

        public async Task<TResponse> PostAsync<TResponse, TRequest>(string path, TRequest request, Dictionary<string, string> headers = null)
        {
            var jsonContent = new StringContent(
#if NET8_0_OR_GREATER
                JsonSerializer.Serialize(request), Encoding.UTF8, "application/json"); 
#else
                JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json"); 
#endif

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = jsonContent
            };

           
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    requestMessage.Headers.Add(header.Key, header.Value);
                }
            }

            var response = await _httpClient.SendAsync(requestMessage);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();

#if NET8_0_OR_GREATER
                return JsonSerializer.Deserialize<TResponse>(content); 
#else
                return JsonConvert.DeserializeObject<TResponse>(content); 
#endif
            }
            else
            {
                throw new Exception($"Error: {response.StatusCode}, {await response.Content.ReadAsStringAsync()}");
            }
        }

        public async Task<TResponse> PutAsync<TResponse, TRequest>(string path, TRequest request, Dictionary<string, string> headers = null)
        {
            var jsonContent = new StringContent(
#if NET8_0_OR_GREATER
                JsonSerializer.Serialize(request), Encoding.UTF8, "application/json"); 
#else
                JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json"); 
#endif

            var requestMessage = new HttpRequestMessage(HttpMethod.Put, path)
            {
                Content = jsonContent
            };

            // Add custom headers if provided
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    requestMessage.Headers.Add(header.Key, header.Value);
                }
            }

            var response = await _httpClient.SendAsync(requestMessage);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();

#if NET8_0_OR_GREATER
                return JsonSerializer.Deserialize<TResponse>(content); 
#else
                return JsonConvert.DeserializeObject<TResponse>(content);
#endif
            }
            else
            {
                throw new Exception($"Error: {response.StatusCode}, {await response.Content.ReadAsStringAsync()}");
            }
        }

        public async Task DeleteAsync(string path, Dictionary<string, string> headers = null)
        {
            var requestMessage = new HttpRequestMessage(HttpMethod.Delete, path);

            // Add custom headers if provided
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    requestMessage.Headers.Add(header.Key, header.Value);
                }
            }

            var response = await _httpClient.SendAsync(requestMessage);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Error: {response.StatusCode}, {await response.Content.ReadAsStringAsync()}");
            }
        }

        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
        {
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Error: {response.StatusCode}, {await response.Content.ReadAsStringAsync()}");
            }

            return response;
        }
    }
}
#endif