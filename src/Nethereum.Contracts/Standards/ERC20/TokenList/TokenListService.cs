using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
#if NETSTANDARD1_0_OR_GREATER || NETCOREAPP2_1_OR_GREATER
using System.Net.Http;
using Nethereum.Util.Rest;
#endif

namespace Nethereum.Contracts.Standards.ERC20.TokenList
{

#if NETSTANDARD1_0_OR_GREATER || NETCOREAPP2_1_OR_GREATER
    /// <summary>
    /// Helper service to get tokens from https://tokenlists.org/
    /// </summary>
    public class TokenListService
    {
        private readonly IRestHttpHelper _restHttpHelper;

        // Constructor for dependency injection
        public TokenListService(IRestHttpHelper restHttpHelper)
        {
            _restHttpHelper = restHttpHelper;
        }

        // Default constructor if no RestHttpHelper is provided
        public TokenListService() : this(new RestHttpHelper(new HttpClient()))
        {
        }

        // Method to load tokens from a URL using RestHttpHelper
        public async Task<List<Token>> LoadFromUrlAsync(string url)
        {
            // Use the RestHttpHelper to fetch the JSON from the URL
            var root = await _restHttpHelper.GetAsync<Root>(url);
            return root.Tokens;
    }

    }
#endif
}