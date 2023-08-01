using Nethereum.ABI.CompilationMetadata;
using Nethereum.DataServices.FourByteDirectory.Responses;
using Nethereum.DataServices.Sourcify.Responses;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.DataServices.FourByteDirectory
{
    public class FourByteDirectoryService
    {
        public const string BaseUrl = "https://www.4byte.directory";
        public FourByteDirectoryService(HttpClient httpClient)
        {
            HttpClient = httpClient;
        }

        public FourByteDirectoryService()
        {
            HttpClient = new HttpClient();
        }

        public HttpClient HttpClient { get; }



        public Task<FourByteDirectoryResponse> GetFunctionSignatureByHexSignatureAsync(string hexSignature)
        {
            var url = $"{BaseUrl}/api/v1/signatures/?hex_signature={hexSignature}";
            return GetDataAsync<FourByteDirectoryResponse>(url);
        }

        public Task<FourByteDirectoryResponse> GetFunctionSignatureByTextSignatureAsync(string textSignature)
        {
            var url = $"{BaseUrl}/api/v1/signatures/?text_signature={textSignature}";
            return GetDataAsync<FourByteDirectoryResponse>(url);
        }

        public Task<FourByteDirectoryResponse> GetFunctionSignatureByTextSignatureInsensitiveAsync(string textSignature)
        {
            var url = $"{BaseUrl}/api/v1/signatures/?text_signature__iexact={textSignature}";
            return GetDataAsync<FourByteDirectoryResponse>(url);
        }



        public Task<FourByteDirectoryResponse> GetEventSignatureByHexSignatureAsync(string hexSignature)
        {
            var url = $"{BaseUrl}/api/v1/event-signatures/?hex_signature={hexSignature}";
            return GetDataAsync<FourByteDirectoryResponse>(url);
        }

        public Task<FourByteDirectoryResponse> GetEventSignatureByTextSignatureAsync(string textSignature)
        {
            var url = $"{BaseUrl}/api/v1/event-signatures/?text_signature={textSignature}";
            return GetDataAsync<FourByteDirectoryResponse>(url);
        }

        public Task<FourByteDirectoryResponse> GetEventSignatureByTextSignatureInsensitiveAsync(string textSignature)
        {
            var url = $"{BaseUrl}/api/v1/event-signatures/?text_signature__iexact={textSignature}";
            return GetDataAsync<FourByteDirectoryResponse>(url);
        }
        public Task<FourByteDirectoryResponse> GetNextPageAsync(string next)
        {
            var url = $"{BaseUrl}{next}";
            return GetDataAsync<FourByteDirectoryResponse>(url);
        }

        public Task<FourByteDirectoryResponse> GetPreviousPageAsync(string previous)
        {
            var url = $"{BaseUrl}{previous}";
            return GetDataAsync<FourByteDirectoryResponse>(url);
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
