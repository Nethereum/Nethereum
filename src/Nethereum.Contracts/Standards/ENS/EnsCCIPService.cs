using System;
using System.Collections.Generic;
using System.IO;
#if !DOTNET35
using System.Net.Http;
#endif
using System.Text;
using System.Threading.Tasks;
using Nethereum.Contracts.Standards.ENS.OffchainResolver.ContractDefinition;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Newtonsoft.Json;

namespace Nethereum.Contracts.Standards.ENS
{
#if !DOTNET35
    public class EnsCCIPService : IEnsCCIPService
    {

        public async Task<byte[]> ResolveCCIPRead(OffchainResolverService offchainResolver, OffchainLookupError offchainLookup, int maxLookupRedirects)
        {
            if (offchainLookup.Urls == null || offchainLookup.Urls.Count == 0) throw new Exception("No urls provided to resolve CCIP read");
            var errors = new List<CCIPReadUrlDataResolvingException>();
            CCIPReadResponse response = null;
            foreach (var url in offchainLookup.Urls)
            {
                try
                {
                    var hexCallData = offchainLookup.CallData.ToHex(true);
                    var formattedUrl = BuildCCIPReadUrl(url, offchainLookup.Sender, hexCallData);
                    if (url.Contains("{data}"))
                    {
                        response = await HttpGet<CCIPReadResponse>(formattedUrl);
                    }
                    else
                    {
                        response = await HttpPost<CCIPReadRequest, CCIPReadResponse>(formattedUrl, new CCIPReadRequest() { Sender = offchainLookup.Sender, Data = hexCallData });
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(new CCIPReadUrlDataResolvingException(url, ex.Message, ex));
                }
            }

            if (response == null)
            {
                throw new CCIPReadUrlsDataResolvingException("Error retrieving CCIP read from urls: " + string.Join(", ", offchainLookup.Urls), errors.ToArray());
            }

            try
            {
                var result = await offchainResolver.ResolveWithProofQueryAsync(response.Data.HexToByteArray(), offchainLookup.ExtraData);
                var resultHex = result.ToHex();
                if (resultHex.IsExceptionEncodedDataForError<OffchainLookupError>())
                {
                    if (maxLookupRedirects > 0)
                    {
                        return await ResolveCCIPRead(offchainResolver, offchainLookup, maxLookupRedirects - 1);
                    }
                    else
                    {
                        throw new Exception("Too many CCIP read redirects");
                    }
                }
                return result;
            }
            catch (SmartContractCustomErrorRevertException customError)
            {
                if (customError.IsCustomErrorFor<OffchainLookupError>())
                {

                    var decoded = customError.DecodeError<OffchainLookupError>();
                    if (!decoded.Sender.IsTheSameAddress(offchainResolver.ContractAddress))
                    {
                        throw new Exception("Cannot handle OffchainLookup raised inside nested call");
                    }

                    if (maxLookupRedirects > 0)
                    {
                        return await ResolveCCIPRead(offchainResolver, offchainLookup, maxLookupRedirects - 1);
                    }
                    else
                    {
                        throw new Exception("Too many CCIP read redirects");
                    }

                }
                else
                {
                    throw customError;
                }
            }
        }

        public class CCIPReadUrlDataResolvingException : Exception
        {
            public string Url { get; }
            public CCIPReadUrlDataResolvingException(string url, string message, Exception innerException) : base(message, innerException)
            {
                Url = url;
            }
        }

        public class CCIPReadUrlsDataResolvingException : Exception
        {
            public CCIPReadUrlDataResolvingException[] UrlReadExceptions { get; }

            public CCIPReadUrlsDataResolvingException(string message, params CCIPReadUrlDataResolvingException[] urlReadExceptions)
            {
                UrlReadExceptions = urlReadExceptions;
            }

        }
        public virtual async Task<T> HttpGet<T>(string url)
        {
            var client = new HttpClient();
            var json = await client.GetStringAsync(url).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<T>(json);
        }

        public virtual async Task<TResponse> HttpPost<TRequest, TResponse>(string url, TRequest request)
        {
            var httpClient = new HttpClient();
            var rpcRequestJson = JsonConvert.SerializeObject(request);
            var httpContent = new StringContent(rpcRequestJson, Encoding.UTF8, "application/json");
            var httpResponseMessage = await httpClient.PostAsync(url, httpContent)
                .ConfigureAwait(false);
            httpResponseMessage.EnsureSuccessStatusCode();
            var stream = await httpResponseMessage.Content.ReadAsStreamAsync().ConfigureAwait(false);

            using (var streamReader = new StreamReader(stream))
            using (var reader = new JsonTextReader(streamReader))
            {
                var serializer = JsonSerializer.Create();
                var message = serializer.Deserialize<TResponse>(reader);
                return message;
            }
        }

        public class CCIPReadResponse
        {
            public string Data { get; set; }
        }

        public class CCIPReadRequest
        {
            public string Data { get; set; }
            public string Sender { get; set; }
        }

        public static string BuildCCIPReadUrl(string url, string sender, string dataInHex)
        {
            var formattedUrl = url.Replace("{sender}", sender.EnsureHexPrefix().ToLower());
            formattedUrl = formattedUrl.Replace("{data}", dataInHex.EnsureHexPrefix().ToLower());
            return formattedUrl;
        }

    }
#endif

}
