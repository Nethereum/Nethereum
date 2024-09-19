using Nethereum.ABI.CompilationMetadata;
using Nethereum.DataServices.Sourcify.Responses;
using Nethereum.Util.Rest;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace Nethereum.DataServices.Sourcify
{
    public class SourcifyApiService
    {
        public const string BaseUrl = "https://sourcify.dev/server/";
        public const string BaseUrlMeta = "https://repo.sourcify.dev/";

        private IRestHttpHelper restHttpHelper;


        public SourcifyApiService(HttpClient httpClient)
        {
            restHttpHelper = new RestHttpHelper(httpClient);
        }

        public SourcifyApiService()
        {
            restHttpHelper = new RestHttpHelper(new HttpClient());
        }

        public SourcifyApiService(IRestHttpHelper restHttpHelper)
        {
            this.restHttpHelper = restHttpHelper;
        }

        public HttpClient HttpClient { get; }

        private string GetFullOrPartialMatch(bool fullMatch)
        {
            if (fullMatch) return "full_match";
            return "partial_match";
        }

        public Task<CompilationMetadata> GetCompilationMetadataAsync(long chain, string address, bool fullMatch = true)
        {
            var url = $"{BaseUrlMeta}/contracts/{GetFullOrPartialMatch(fullMatch)}/{chain}/{address}/metadata.json";
            return GetDataAsync<CompilationMetadata>(url);
        }

        public Task<List<SourcifyContentFile>> GetSourceFilesFullMatchAsync(long chain, string address)
        {
            var url = $"{BaseUrl}/files/{chain}/{address}";
            return GetDataAsync<List<SourcifyContentFile>>(url);
        }


        public Task<T> GetDataAsync<T>(string url)
        {
            var headers = new Dictionary<string, string>()
            {
                {"accept", "application/json"}
            };

            return restHttpHelper.GetAsync<T>(url, headers);
        }
    }
}
