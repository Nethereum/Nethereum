using Nethereum.DataServices.FourByteDirectory.Responses;
using Nethereum.Util.Rest;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace Nethereum.DataServices.FourByteDirectory
{
    public class FourByteDirectoryService
    {
        public const string BaseUrl = "https://www.4byte.directory";
        private IRestHttpHelper _restHttpHelper;
        public FourByteDirectoryService(HttpClient httpClient)
        {
            _restHttpHelper = new RestHttpHelper(httpClient);
        }

        public FourByteDirectoryService()
        {
            _restHttpHelper = new RestHttpHelper(new HttpClient());
        }

        public FourByteDirectoryService(IRestHttpHelper restHttpHelper)
        {
            _restHttpHelper = restHttpHelper;
        }

     

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
            var headers = new Dictionary<string, string>
            {
                { "accept", "application/json" }
            };

            return await _restHttpHelper.GetAsync<T>(url, headers);
        }


    }
}
