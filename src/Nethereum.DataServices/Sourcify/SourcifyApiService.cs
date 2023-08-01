using Nethereum.ABI.CompilationMetadata;
using Nethereum.DataServices.Sourcify.Responses;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.DataServices.Sourcify
{
    public class SourcifyApiService
    {
        public const string BaseUrl = "https://sourcify.dev/server/";
        public const string BaseUrlMeta = "https://repo.sourcify.dev/";
        public SourcifyApiService(HttpClient httpClient)
        {
            HttpClient = httpClient;
        }

        public SourcifyApiService()
        {
            HttpClient = new HttpClient();
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


        public async Task<T> GetDataAsync<T>(string url)
        {
            var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    url);
            request.Headers.Add("accept", "application/json");

            var httpResponseMessage = await HttpClient.SendAsync(request).ConfigureAwait(false);
            httpResponseMessage.EnsureSuccessStatusCode();
            var stream = await httpResponseMessage.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using (var streamReader = new StreamReader(stream))
            using (var reader = new JsonTextReader(streamReader))
            {
                var serializer = JsonSerializer.Create();
                var message = serializer.Deserialize<T>(reader);

                return message;
            }
        }


    }
}
