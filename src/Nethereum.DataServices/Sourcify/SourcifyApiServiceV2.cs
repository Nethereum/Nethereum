using Nethereum.DataServices.Sourcify.Responses;
using Nethereum.Util.Rest;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
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

        public Task<SourcifyContractResponse> GetContractAsync(long chainId, string address, string fields = null, string omit = null)
        {
            var headers = new Dictionary<string, string>
            {
                { "accept", "application/json" }
            };

            var url = new StringBuilder($"{BaseUrl}contract/{chainId}/{address}");
            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(fields)) queryParams.Add($"fields={fields}");
            if (!string.IsNullOrEmpty(omit)) queryParams.Add($"omit={omit}");
            if (queryParams.Count > 0) url.Append("?").Append(string.Join("&", queryParams));

            return restHttpHelper.GetAsync<SourcifyContractResponse>(url.ToString(), headers);
        }

        public async Task<string> GetContractAbiAsync(long chainId, string address)
        {
            var response = await GetContractAsync(chainId, address, fields: "abi").ConfigureAwait(false);
            return response?.GetAbiString();
        }

        public Task<SourcifyContractsListResponse> GetContractsAsync(long chainId, int limit = 200, string sort = "desc", string afterMatchId = null)
        {
            var headers = new Dictionary<string, string>
            {
                { "accept", "application/json" }
            };

            var url = new StringBuilder($"{BaseUrl}contracts/{chainId}");
            var queryParams = new List<string>();
            if (limit > 0) queryParams.Add($"limit={limit}");
            if (!string.IsNullOrEmpty(sort)) queryParams.Add($"sort={sort}");
            if (!string.IsNullOrEmpty(afterMatchId)) queryParams.Add($"afterMatchId={afterMatchId}");
            if (queryParams.Count > 0) url.Append("?").Append(string.Join("&", queryParams));

            return restHttpHelper.GetAsync<SourcifyContractsListResponse>(url.ToString(), headers);
        }

        public Task<SourcifyAllChainsResponse> GetContractAllChainsAsync(string address)
        {
            var headers = new Dictionary<string, string>
            {
                { "accept", "application/json" }
            };

            return restHttpHelper.GetAsync<SourcifyAllChainsResponse>($"{BaseUrl}contract/all-chains/{address}", headers);
        }

        public Task<SourcifyVerificationJobResponse> GetVerificationStatusAsync(string verificationId)
        {
            var headers = new Dictionary<string, string>
            {
                { "accept", "application/json" }
            };

            return restHttpHelper.GetAsync<SourcifyVerificationJobResponse>($"{BaseUrl}verify/{verificationId}", headers);
        }

        public Task<SourcifyVerificationJobResponse> VerifyFromEtherscanAsync(long chainId, string address, string apiKey = null)
        {
            var headers = new Dictionary<string, string>
            {
                { "accept", "application/json" }
            };

            var url = new StringBuilder($"{BaseUrl}verify/etherscan/{chainId}/{address}");
            if (!string.IsNullOrEmpty(apiKey)) url.Append($"?apiKey={apiKey}");

            return restHttpHelper.PostAsync<SourcifyVerificationJobResponse, object>(url.ToString(), new { }, headers);
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
