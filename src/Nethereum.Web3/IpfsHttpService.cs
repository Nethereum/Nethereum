using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Nethereum.Web3
{
    public class IPFSFileInfo
    {
        public string Name { get; set; }
        public string Size { get; set; }
        public string Hash { get; set; }

    }

    /// <summary>
    ///  Basic http ipfs service, for a complete implementation please use https://github.com/richardschneider/net-ipfs-http-client
    /// </summary>
    public class IpfsHttpService
    {
        public string Url { get; }
        public Uri Uri { get; }
        private AuthenticationHeaderValue _authHeaderValue;

        public IpfsHttpService(string url)
        {
            if (!url.EndsWith("api/v0"))
            {
                url = url.TrimEnd('/') + "/api/v0";
            }

            Url = url;
        }

        public IpfsHttpService(string url, string userName, string password) : this(url)
        {
            _authHeaderValue = GetBasicAuthenticationHeaderValue(userName, password);
        }

        public AuthenticationHeaderValue GetBasicAuthenticationHeaderValue(string userName, string password)
        {
            var byteArray = Encoding.UTF8.GetBytes(userName + ":" + password);
            return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
        }

        public async Task<IPFSFileInfo> AddAsync(byte[] fileBytes, string fileName, bool pin = true)
        {
            var content = new MultipartFormDataContent();
            var streamContent = new ByteArrayContent(fileBytes);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(streamContent, "file", fileName);
            using (var httpClient = new HttpClient())
            {

                httpClient.DefaultRequestHeaders.Authorization = _authHeaderValue;
                var query = pin ? "?pin=true&cid-version=1" : "?cid-version=1";
                var fullUrl = Url + "/add" + query;
                var httpResponseMessage = await httpClient.PostAsync(fullUrl, content).ConfigureAwait(false);
                httpResponseMessage.EnsureSuccessStatusCode();
                var stream = await httpResponseMessage.Content.ReadAsStreamAsync().ConfigureAwait(false);
                using (var streamReader = new StreamReader(stream))
                using (var reader = new JsonTextReader(streamReader))
                {
                    var serializer = JsonSerializer.Create();
                    var message = serializer.Deserialize<IPFSFileInfo>(reader);

                    return message;
                }
            }
        }

        /// <summary>
        /// Cat returns the file as it is stored
        /// </summary>
        public async Task<Stream> CatAsync(string path)
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Authorization = _authHeaderValue;
                var query = new StringBuilder();
                if (path != null)
                {
                    query.Append("?arg=");
                    query.Append(WebUtility.UrlEncode(path));
                }
                var fullUrl = Url + "/cat" + query.ToString();

                var httpResponseMessage = await httpClient.PostAsync(fullUrl, null).ConfigureAwait(false);
                httpResponseMessage.EnsureSuccessStatusCode();
                return await httpResponseMessage.Content.ReadAsStreamAsync();
            }
        }

        public async Task<IPFSFileInfo> AddObjectAsJson<T>(T objectToSerialise, string fileName, bool pin = true)
        {
            using (var ms = new MemoryStream())
            {
                var serializer = new JsonSerializer();
                var jsonTextWriter = new JsonTextWriter(new StreamWriter(ms));
                serializer.Serialize(jsonTextWriter, objectToSerialise);
                jsonTextWriter.Flush();
                ms.Position = 0;
                var node = await AddAsync(ms.ToArray(), fileName, true).ConfigureAwait(false);
                return node;
            }
        }

#if NET5_0_OR_GREATER
        public Task<IPFSFileInfo> AddFileAsync(string path, bool pin = true)
        {
            var fileBytes = File.ReadAllBytes(path);
            var fileName = Path.GetFileName(path);
            return AddAsync(fileBytes, fileName, pin);
        }
#endif
    }
}
