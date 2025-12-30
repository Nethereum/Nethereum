using Nethereum.DataServices.Sourcify.Responses;
using Nethereum.Util.Rest;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Nethereum.DataServices.Sourcify
{
    public class SourcifyApiServiceV2
    {
        public const string BaseUrl = "https://sourcify.dev/server/v2/";

        private readonly IRestHttpHelper restHttpHelper;

        public SourcifyApiServiceV2(HttpClient httpClient)
        {
            restHttpHelper = new RestHttpHelper(httpClient);
        }

        public SourcifyApiServiceV2()
        {
            restHttpHelper = new RestHttpHelper(new HttpClient());
        }

        public SourcifyApiServiceV2(IRestHttpHelper restHttpHelper)
        {
            this.restHttpHelper = restHttpHelper;
        }

        /// <summary>
        /// POST /v2/metadata — submits a contract's address, chainId, and source files for metadata verification.
        /// </summary>
        public Task<SourcifyMetadataResponse> PostMetadataAsync(long chainId, string address, Dictionary<string, string> sourceFiles)
        {
            var request = new MultipartFormDataRequest
            {
                Fields = new List<MultipartField>
            {
                new MultipartField { Name = "chainId", Value = chainId.ToString() },
                new MultipartField { Name = "address", Value = address }
            },
                Files = sourceFiles.Select(f => new MultipartFile
                {
                    FieldName = "files",
                    FileName = f.Key,
                    Content = f.Value,
                    ContentType = "text/plain"
                }).ToList()
            };

            var headers = new Dictionary<string, string>
        {
            { "accept", "application/json" }
        };

            return restHttpHelper.PostMultipartAsync<SourcifyMetadataResponse>($"{BaseUrl}metadata", request, headers);
        }

        public Task<SourcifyVerifyResponse> PostVerifyAsync(long chainId, string address, Dictionary<string, string> sourceFiles)
        {
            var request = new MultipartFormDataRequest
            {
                Fields = new List<MultipartField>
        {
            new MultipartField { Name = "chainId", Value = chainId.ToString() },
            new MultipartField { Name = "address", Value = address }
        },
                Files = sourceFiles.Select(f => new MultipartFile
                {
                    FieldName = "files",
                    FileName = f.Key,
                    Content = f.Value,
                    ContentType = "text/plain"
                }).ToList()
            };

            var headers = new Dictionary<string, string>
            {
                { "accept", "application/json" }
            };

            return restHttpHelper.PostMultipartAsync<SourcifyVerifyResponse>($"{BaseUrl}verify", request, headers);
        }

        public Task<SourcifyCheckByAddressResponse> GetCheckByAddressAsync(long chainId, string address)
        {
            var headers = new Dictionary<string, string>
            {
                { "accept", "application/json" }
            };

            var url = $"{BaseUrl}check-by-address?chainId={chainId}&address={address}";
            return restHttpHelper.GetAsync<SourcifyCheckByAddressResponse>(url, headers);
        }

        public Task<List<SourcifyChainInfo>> GetChainsAsync()
        {
            var headers = new Dictionary<string, string>
            {
                { "accept", "application/json" }
            };

            return restHttpHelper.GetAsync<List<SourcifyChainInfo>>($"{BaseUrl}chains", headers);
        }


    }
}
