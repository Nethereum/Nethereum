using System.Collections.Generic;
#if NETSTANDARD1_0_OR_GREATER || NETCOREAPP2_1_OR_GREATER
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
#endif
using Newtonsoft.Json;

namespace Nethereum.Contracts.Standards.ERC20
{
    /// <summary>
    /// Helper service to get tokens from https://tokenlists.org/
    /// </summary>
    public class TokenListService
    {
        public List<Token> DeserialiseFromJson(string json)
        {
            var root = JsonConvert.DeserializeObject<Root>(json);
            return root.Tokens;
        }

#if NETSTANDARD1_0_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        public async  Task<List<Token>> LoadFromUrl(string url)
        {
            var client = new HttpClient();
            var json = await client.GetStringAsync(url);
            return DeserialiseFromJson(json);
        }
#endif
    }
}