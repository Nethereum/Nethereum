#if !DOTNET35

using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.IO;



#if NET8_0_OR_GREATER
using System.Text.Json; // For System.Text.Json in .NET 5+
#else
using Newtonsoft.Json; // For Newtonsoft.Json in older versions
#endif

namespace Nethereum.Util.Rest
{
    public class RestHttpHelper : IRestHttpHelper
    {
        public const long DefaultMaxResponseBytes = 32L * 1024 * 1024;

        private readonly HttpClient _httpClient;

        public long MaxResponseBytes { get; set; } = DefaultMaxResponseBytes;

        public RestHttpHelper(HttpClient httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient();
        }

       
        private async Task<string> ReadBodyAsync(HttpContent content)
        {
            var declared = content.Headers.ContentLength;
            if (declared.HasValue && declared.Value > MaxResponseBytes)
            {
                throw new HttpRequestException($"Response of {declared.Value} bytes exceeds the {MaxResponseBytes} byte limit.");
            }

            using (var stream = await content.ReadAsStreamAsync())
            using (var buffer = new MemoryStream())
            {
                var chunk = new byte[81920];
                int read;
                while ((read = await stream.ReadAsync(chunk, 0, chunk.Length)) > 0)
                {
                    if (buffer.Length + read > MaxResponseBytes)
                    {
                        throw new HttpRequestException($"Response exceeds the {MaxResponseBytes} byte limit.");
                    }
                    buffer.Write(chunk, 0, read);
                }
                buffer.Position = 0;
                using (var reader = new StreamReader(buffer, ResolveEncoding(content), true))
                {
                    return await reader.ReadToEndAsync();
                }
            }
        }

        private static Encoding ResolveEncoding(HttpContent content)
        {
            var charset = content.Headers.ContentType?.CharSet;
            if (string.IsNullOrWhiteSpace(charset))
            {
                return Encoding.UTF8;
            }
            try
            {
                return Encoding.GetEncoding(charset.Trim('"'));
            }
            catch (ArgumentException)
            {
                return Encoding.UTF8;
            }
        }

        private async Task<string> SendForBodyAsync(HttpRequestMessage request, bool errorIncludesHeaders = false)
        {
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await ReadBodyAsync(response.Content);
                throw new Exception(errorIncludesHeaders
                    ? $"Error: {response.StatusCode}, {errorBody}, {response.Headers}"
                    : $"Error: {response.StatusCode}, {errorBody}");
            }

            return await ReadBodyAsync(response.Content);
        }

        private static T Deserialize<T>(string content)
        {
#if NET8_0_OR_GREATER
            return JsonSerializer.Deserialize<T>(content);
#else
            return JsonConvert.DeserializeObject<T>(content);
#endif
        }

        public async Task<T> GetAsync<T>(string path, Dictionary<string, string> headers = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);

            if (headers != null)
            {
                foreach (var header in headers)
                {
                    request.Headers.Add(header.Key, header.Value);
                }
            }

            return Deserialize<T>(await SendForBodyAsync(request, true));
        }

        public async Task<TResponse> PostAsync<TResponse, TRequest>(string path, TRequest request, Dictionary<string, string> headers = null)
        {
            var jsonContent = new StringContent(
#if NET8_0_OR_GREATER
                JsonSerializer.Serialize(request), Encoding.UTF8, "application/json"); 
#else
                JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json"); 
#endif

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = jsonContent
            };

           
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    requestMessage.Headers.Add(header.Key, header.Value);
                }
            }

            return Deserialize<TResponse>(await SendForBodyAsync(requestMessage));
        }

        public async Task<TResponse> PutAsync<TResponse, TRequest>(string path, TRequest request, Dictionary<string, string> headers = null)
        {
            var jsonContent = new StringContent(
#if NET8_0_OR_GREATER
                JsonSerializer.Serialize(request), Encoding.UTF8, "application/json"); 
#else
                JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json"); 
#endif

            var requestMessage = new HttpRequestMessage(HttpMethod.Put, path)
            {
                Content = jsonContent
            };

            // Add custom headers if provided
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    requestMessage.Headers.Add(header.Key, header.Value);
                }
            }

            return Deserialize<TResponse>(await SendForBodyAsync(requestMessage));
        }

        public async Task DeleteAsync(string path, Dictionary<string, string> headers = null)
        {
            var requestMessage = new HttpRequestMessage(HttpMethod.Delete, path);

            // Add custom headers if provided
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    requestMessage.Headers.Add(header.Key, header.Value);
                }
            }

            await SendForBodyAsync(requestMessage);
        }

        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
        {
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Error: {response.StatusCode}, {await response.Content.ReadAsStringAsync()}");
            }

            return response;
        }

        public async Task<TResponse> PostMultipartAsync<TResponse>(
            string path,
            MultipartFormDataRequest request,
            Dictionary<string, string> headers = null)
        {
            var content = new MultipartFormDataContent();

            foreach (var field in request.Fields)
            {
                content.Add(new StringContent(field.Value), field.Name);
            }

            foreach (var file in request.Files)
            {
                var fileContent = new StringContent(file.Content, Encoding.UTF8, file.ContentType);
                content.Add(fileContent, file.FieldName, file.FileName);
            }

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = content
            };

            if (headers != null)
            {
                foreach (var header in headers)
                {
                    httpRequest.Headers.Add(header.Key, header.Value);
                }
            }

            return Deserialize<TResponse>(await SendForBodyAsync(httpRequest));
        }
    }

    public class MultipartFormDataRequest
    {
        public List<MultipartField> Fields { get; set; } = new();
        public List<MultipartFile> Files { get; set; } = new();
    }

    public class MultipartField
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

    public class MultipartFile
    {
        public string FieldName { get; set; } = "file"; // Default to "file", can be "files"
        public string FileName { get; set; }             // e.g., "contracts/Token.sol"
        public string Content { get; set; }              // File content as string
        public string ContentType { get; set; } = "text/plain"; // MIME type
    }
}
#endif