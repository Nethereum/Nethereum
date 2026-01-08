using Nethereum.DataServices.Sourcify.Responses;
using Nethereum.Util.Rest;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.DataServices.Sourcify
{
    public class Sourcify4ByteSignatureService
    {
        public const string BaseUrl = "https://api.4byte.sourcify.dev/";

        private readonly IRestHttpHelper restHttpHelper;

        public Sourcify4ByteSignatureService(HttpClient httpClient)
        {
            restHttpHelper = new RestHttpHelper(httpClient);
        }

        public Sourcify4ByteSignatureService()
        {
            restHttpHelper = new RestHttpHelper(new HttpClient());
        }

        public Sourcify4ByteSignatureService(IRestHttpHelper restHttpHelper)
        {
            this.restHttpHelper = restHttpHelper;
        }

        public Task<Sourcify4ByteResponse> LookupAsync(
            IEnumerable<string> functionSignatures = null,
            IEnumerable<string> eventSignatures = null,
            bool filter = true)
        {
            var headers = new Dictionary<string, string>
            {
                { "accept", "application/json" }
            };

            var url = new StringBuilder($"{BaseUrl}signature-database/v1/lookup");
            var queryParams = new List<string>();

            if (functionSignatures != null)
            {
                var functions = string.Join(",", functionSignatures);
                if (!string.IsNullOrEmpty(functions))
                {
                    queryParams.Add($"function={functions}");
                }
            }

            if (eventSignatures != null)
            {
                var events = string.Join(",", eventSignatures);
                if (!string.IsNullOrEmpty(events))
                {
                    queryParams.Add($"event={events}");
                }
            }

            queryParams.Add($"filter={filter.ToString().ToLowerInvariant()}");

            if (queryParams.Count > 0)
            {
                url.Append("?").Append(string.Join("&", queryParams));
            }

            return restHttpHelper.GetAsync<Sourcify4ByteResponse>(url.ToString(), headers);
        }

        public Task<Sourcify4ByteResponse> LookupFunctionAsync(string functionSignature, bool filter = true)
        {
            return LookupAsync(new[] { functionSignature }, null, filter);
        }

        public Task<Sourcify4ByteResponse> LookupEventAsync(string eventSignature, bool filter = true)
        {
            return LookupAsync(null, new[] { eventSignature }, filter);
        }

        public Task<Sourcify4ByteResponse> SearchAsync(string query, bool filter = true)
        {
            var headers = new Dictionary<string, string>
            {
                { "accept", "application/json" }
            };

            var url = $"{BaseUrl}signature-database/v1/search?query={query}&filter={filter.ToString().ToLowerInvariant()}";

            return restHttpHelper.GetAsync<Sourcify4ByteResponse>(url, headers);
        }
    }
}
