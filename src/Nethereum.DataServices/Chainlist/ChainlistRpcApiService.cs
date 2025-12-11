using Nethereum.Util.Rest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Nethereum.DataServices.Chainlist.Responses;

namespace Nethereum.DataServices.Chainlist
{
    public class ChainlistRpcApiService
    {
        public const string RpcJsonUrl = "https://chainlist.org/rpcs.json";

        private readonly IRestHttpHelper restHttpHelper;

        public ChainlistRpcApiService(HttpClient httpClient)
        {
            restHttpHelper = new RestHttpHelper(httpClient);
        }

        public ChainlistRpcApiService()
        {
            restHttpHelper = new RestHttpHelper(new HttpClient());
        }

        public ChainlistRpcApiService(IRestHttpHelper restHttpHelper)
        {
            this.restHttpHelper = restHttpHelper;
        }

        public Task<List<ChainlistChainInfo>> GetAllChainsAsync()
        {
            var headers = new Dictionary<string, string> { { "accept", "application/json" } };
            return restHttpHelper.GetAsync<List<ChainlistChainInfo>>(RpcJsonUrl, headers);
        }

        public async Task<ChainlistChainInfo> GetChainByIdAsync(long chainId)
        {
            var allChains = await GetAllChainsAsync();
            return allChains?.FirstOrDefault(chain => chain.ChainId == chainId);
        }
    }
}
