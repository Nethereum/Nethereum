using Nethereum.DataServices.Etherscan.Responses;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Nethereum.DataServices.Etherscan
{
    public class EtherscanRequestService
    {
        public const string DefaultToken = "YourApiKeyToken";

        public static string GetBaseUrl(EtherscanChain chain)
        {
            switch (chain) 
            {
                case EtherscanChain.Mainnet:
                    return "https://api.etherscan.io/";
            }
            throw new NotImplementedException();
        }

        public EtherscanRequestService(HttpClient httpClient, string baseUrl, string apiKey= DefaultToken)
        {
            HttpClient = httpClient;
            BaseUrl = baseUrl;
            ApiKey = apiKey;
        }

        public EtherscanRequestService(HttpClient httpClient, EtherscanChain chain, string apiKey = DefaultToken)
        {
            HttpClient = httpClient;
            BaseUrl = GetBaseUrl(chain);
            ApiKey = apiKey;
        }

        public EtherscanRequestService(EtherscanChain chain = EtherscanChain.Mainnet, string apiKey = DefaultToken)
        {
            HttpClient = new HttpClient();
            BaseUrl = GetBaseUrl(chain);
            ApiKey = apiKey;
        }

        public HttpClient HttpClient { get; }
        public string BaseUrl { get; }
        public string ApiKey { get; }



        public async Task<EtherscanResponse<T>> GetDataAsync<T>(string url)
        {
            var httpResponseMessage = await HttpClient.GetAsync(url).ConfigureAwait(false);
            httpResponseMessage.EnsureSuccessStatusCode();
            var stream = await httpResponseMessage.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using (var streamReader = new StreamReader(stream))
            using (var reader = new JsonTextReader(streamReader))
            {
                var serializer = JsonSerializer.Create();
                var message = serializer.Deserialize<EtherscanResponse<T>>(reader);

                return message;
            }
        }
    }
}
