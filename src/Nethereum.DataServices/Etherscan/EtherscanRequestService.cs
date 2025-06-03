using Nethereum.DataServices.Etherscan.Responses;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Nethereum.Util.Rest;

namespace Nethereum.DataServices.Etherscan
{
    public class EtherscanRequestService
    {
        public const string DefaultToken = "YourApiKeyToken";
        private readonly IRestHttpHelper _restHttpHelper;


        public EtherscanRequestService(HttpClient httpClient, string baseUrl, string apiKey = DefaultToken)
        {
            _restHttpHelper = new RestHttpHelper(httpClient);
            BaseUrl = baseUrl;
            ApiKey = apiKey;
        }


        public EtherscanRequestService(HttpClient httpClient, long chain, string apiKey = DefaultToken)
        {
            _restHttpHelper = new RestHttpHelper(httpClient);
            BaseUrl = GetBaseUrl(chain);
            ApiKey = apiKey;
        }


        public EtherscanRequestService(long chain = 1, string apiKey = DefaultToken)
        {
            _restHttpHelper = new RestHttpHelper(new HttpClient());
            BaseUrl = GetBaseUrl(chain);
            ApiKey = apiKey;
        }

        public EtherscanRequestService(IRestHttpHelper restHttpHelper, long chain = 1, string apiKey = DefaultToken)
        {
            _restHttpHelper = restHttpHelper;
            BaseUrl = GetBaseUrl(chain);
            ApiKey = apiKey;
        }



        public string BaseUrl { get; }
        public string ApiKey { get; }

        public static string GetBaseUrl(long chain)
        {
            return $"https://api.etherscan.io/v2/api?chainid={chain}";
        }

  
        public async Task<EtherscanResponse<T>> GetDataAsync<T>(string url)
        {
            return await _restHttpHelper.GetAsync<EtherscanResponse<T>>(url).ConfigureAwait(false);
        }

        public async Task<EtherscanResponse<TResponse>> PostAsync<TResponse, TRequest>(string path, TRequest request)
        {
            return await _restHttpHelper.PostAsync<EtherscanResponse<TResponse>, TRequest>(path, request).ConfigureAwait(false);
        }
    }
}

