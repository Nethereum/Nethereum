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


        public EtherscanRequestService(HttpClient httpClient, EtherscanChain chain, string apiKey = DefaultToken)
        {
            _restHttpHelper = new RestHttpHelper(httpClient);
            BaseUrl = GetBaseUrl(chain);
            ApiKey = apiKey;
        }


        public EtherscanRequestService(EtherscanChain chain = EtherscanChain.Mainnet, string apiKey = DefaultToken)
        {
            _restHttpHelper = new RestHttpHelper(new HttpClient());
            BaseUrl = GetBaseUrl(chain);
            ApiKey = apiKey;
        }

        public EtherscanRequestService(IRestHttpHelper restHttpHelper, EtherscanChain chain = EtherscanChain.Mainnet, string apiKey = DefaultToken)
        {
            _restHttpHelper = restHttpHelper;
            BaseUrl = GetBaseUrl(chain);
            ApiKey = apiKey;
        }



        public string BaseUrl { get; }
        public string ApiKey { get; }

        public static string GetBaseUrl(EtherscanChain chain)
        {
            switch (chain)
            {
                case EtherscanChain.Mainnet:
                    return "https://api.etherscan.io/";
                case EtherscanChain.Binance:
                    return "https://api.bscscan.com/";
                case EtherscanChain.Optimism:
                    return "https://api-optimistic.etherscan.io/";
                case EtherscanChain.Polygon:
                    return "https://api.polygonscan.com/";
                case EtherscanChain.Arbitrum:
                    return "https://api.arbiscan.io/";
            }
            throw new NotImplementedException();
        }

  
        public async Task<EtherscanResponse<T>> GetDataAsync<T>(string url)
        {
            return await _restHttpHelper.GetAsync<EtherscanResponse<T>>(url).ConfigureAwait(false);
        }
    }
}

