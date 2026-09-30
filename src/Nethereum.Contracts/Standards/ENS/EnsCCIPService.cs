using System;
using System.Collections.Generic;
#if !DOTNET35
using System.Net.Http;
#endif
using System.Linq;
using System.Threading.Tasks;
using Nethereum.ABI;
using Nethereum.Contracts.Services;
using Nethereum.Contracts.Standards.ENS.BatchGateway.ContractDefinition;
using Nethereum.Contracts.Standards.ENS.OffchainResolver.ContractDefinition;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.Util.Rest;
using Newtonsoft.Json;

namespace Nethereum.Contracts.Standards.ENS
{
#if !DOTNET35
    public class EnsCCIPService : IEnsCCIPService
    {
        private static readonly HttpClient _ccipClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });

        private static readonly IRestHttpHelper _ccipRestHttpHelper = new RestHttpHelper(_ccipClient);

        public bool AllowHttp { get; set; } = false;

        public bool AllowPrivateDestinations { get; set; } = false;

        public const string LocalBatchGatewayUrl = "x-batch-gateway:true";

        /// <summary>
        /// Handle the ENSIP-21 BGOLP batch locally (decode the query, perform each inner EIP-3668 read
        /// through the SSRF-validated fetch, bundle the results) instead of calling the external batch
        /// gateway. On by default. Each inner request is still validated, so this does NOT relax SSRF
        /// protection — it is the path where that protection matters most.
        /// </summary>
        public bool AllowLocalBatchGateway { get; set; } = true;

        /// <summary>
        /// Validates a CCIP-Read gateway URL before it is requested. The URL is supplied by an untrusted
        /// resolver contract (EIP-3668 OffchainLookup), so by default only https is permitted and any host
        /// that resolves to a private/reserved/link-local/loopback address is refused (CWE-918, SSRF).
        /// </summary>
        protected virtual void ValidateCcipUrl(string url)
        {
            var uri = new Uri(url);

            if (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && AllowHttp))
                throw new CCIPReadUrlValidationException($"CCIP Read refused non-https gateway URL: {url}");

            if (AllowPrivateDestinations) return;

            System.Net.IPAddress[] addresses;
            try
            {
                addresses = System.Net.Dns.GetHostAddresses(uri.DnsSafeHost);
            }
            catch (Exception ex)
            {
                throw new CCIPReadUrlValidationException($"CCIP Read could not resolve gateway host '{uri.DnsSafeHost}' for URL: {url}", ex);
            }
            foreach (var ip in addresses)
            {
                if (IsPrivateOrReserved(ip))
                    throw new CCIPReadUrlValidationException($"CCIP Read refused private/reserved destination {ip} for gateway URL: {url}");
            }
        }

        private static bool IsPrivateOrReserved(System.Net.IPAddress ip)
        {
            if (System.Net.IPAddress.IsLoopback(ip)) return true;

            if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();

            var bytes = ip.GetAddressBytes();
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                return bytes[0] == 10
                    || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                    || (bytes[0] == 192 && bytes[1] == 168)
                    || (bytes[0] == 169 && bytes[1] == 254)
                    || (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127)
                    || bytes[0] == 0
                    || bytes[0] >= 224;
            }

            if (ip.Equals(System.Net.IPAddress.IPv6Any)) return true;
            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || (bytes[0] & 0xFE) == 0xFC;
        }

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

        public async Task<byte[]> ResolveCCIPReadGenericAsync(IEthApiContractService ethApiContractService, OffchainLookupError offchainLookup, int maxLookupRedirects)
        {
            if (offchainLookup.Urls == null || offchainLookup.Urls.Count == 0) throw new Exception("No urls provided to resolve CCIP read");

            var responseData = await FetchGatewayDataAsync(offchainLookup).ConfigureAwait(false);

            // EIP-3668 callback: sender.callbackFunction(response, extraData)
            var encodedParams = new ABIEncode().GetABIEncoded(
                new ABIValue("bytes", responseData.HexToByteArray()),
                new ABIValue("bytes", offchainLookup.ExtraData));
            var callbackData = ByteUtil.Merge(offchainLookup.CallbackFunction, encodedParams);
            var callInput = new CallInput(callbackData.ToHex(true), offchainLookup.Sender);

            string resultHex;
            try
            {
                resultHex = await ethApiContractService.Transactions.Call.SendRequestAsync(callInput).ConfigureAwait(false);
            }
            catch (RpcResponseException rpcException)
            {
                var encodedErrorData = rpcException.RpcError.GetDataAsString();
                if (encodedErrorData != null && encodedErrorData.IsHex() && encodedErrorData.IsExceptionEncodedDataForError<OffchainLookupError>())
                {
                    return await RedirectGenericCCIPReadAsync(ethApiContractService, encodedErrorData, offchainLookup, maxLookupRedirects).ConfigureAwait(false);
                }
                throw;
            }

            if (resultHex.IsExceptionEncodedDataForError<OffchainLookupError>())
            {
                return await RedirectGenericCCIPReadAsync(ethApiContractService, resultHex, offchainLookup, maxLookupRedirects).ConfigureAwait(false);
            }
            return resultHex.HexToByteArray();
        }

        private Task<byte[]> RedirectGenericCCIPReadAsync(IEthApiContractService ethApiContractService, string encodedError, OffchainLookupError previous, int maxLookupRedirects)
        {
            if (maxLookupRedirects <= 0) throw new Exception("Too many CCIP read redirects");
            var customError = new SmartContractCustomErrorRevertException(encodedError);
            var nested = customError.DecodeError<OffchainLookupError>();
            if (!nested.Sender.IsTheSameAddress(previous.Sender))
                throw new Exception("Cannot handle OffchainLookup raised inside nested call");
            return ResolveCCIPReadGenericAsync(ethApiContractService, nested, maxLookupRedirects - 1);
        }

        protected virtual Task<string> FetchGatewayDataAsync(OffchainLookupError offchainLookup)
        {
            if (AllowLocalBatchGateway && offchainLookup.Urls != null && offchainLookup.Urls.Contains(LocalBatchGatewayUrl))
            {
                return ProcessLocalBatchGatewayAsync(offchainLookup);
            }
            return FetchFromGatewayUrlsAsync(offchainLookup);
        }

        private async Task<string> FetchFromGatewayUrlsAsync(OffchainLookupError offchainLookup)
        {
            var errors = new List<CCIPReadUrlDataResolvingException>();
            CCIPReadResponse response = null;
            foreach (var url in offchainLookup.Urls)
            {
                if (url == LocalBatchGatewayUrl) continue;
                try
                {
                    var hexCallData = offchainLookup.CallData.ToHex(true);
                    var formattedUrl = BuildCCIPReadUrl(url, offchainLookup.Sender, hexCallData);
                    if (url.Contains("{data}"))
                    {
                        response = await HttpGet<CCIPReadResponse>(formattedUrl).ConfigureAwait(false);
                    }
                    else
                    {
                        response = await HttpPost<CCIPReadRequest, CCIPReadResponse>(formattedUrl, new CCIPReadRequest { Sender = offchainLookup.Sender, Data = hexCallData }).ConfigureAwait(false);
                    }
                    if (response != null) break;
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
            return response.Data;
        }

        /// <summary>
        /// Performs an ENSIP-21 BGOLP batch locally: decodes query((address,string[],bytes)[]) from the
        /// batch call data, runs each inner EIP-3668 read through the SSRF-validated per-URL fetch, and
        /// ABI-encodes (bool[] failures, bytes[] responses) for the Universal Resolver callback.
        /// </summary>
        private async Task<string> ProcessLocalBatchGatewayAsync(OffchainLookupError offchainLookup)
        {
            var query = new QueryFunction().DecodeInput(offchainLookup.CallData.ToHex());
            var requests = query.Requests ?? new List<BatchGatewayRequest>();

            var failures = new List<bool>();
            var responses = new List<byte[]>();
            foreach (var request in requests)
            {
                try
                {
                    var innerLookup = new OffchainLookupError { Sender = request.Sender, Urls = request.Urls, CallData = request.Data };
                    var data = await FetchFromGatewayUrlsAsync(innerLookup).ConfigureAwait(false);
                    failures.Add(false);
                    responses.Add(data.HexToByteArray());
                }
                catch (Exception)
                {
                    failures.Add(true);
                    responses.Add(new byte[0]);
                }
            }

            var encoded = new ABIEncode().GetABIEncoded(
                new ABIValue("bool[]", failures.ToArray()),
                new ABIValue("bytes[]", responses.ToArray()));
            return encoded.ToHex(true);
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
                : base(message + (urlReadExceptions != null && urlReadExceptions.Length > 0
                    ? " | " + string.Join(" | ", urlReadExceptions.Select(e => e.Url + ": " + e.Message))
                    : ""))
            {
                UrlReadExceptions = urlReadExceptions;
            }

        }

        public class CCIPReadUrlValidationException : Exception
        {
            public CCIPReadUrlValidationException(string message) : base(message)
            {
            }

            public CCIPReadUrlValidationException(string message, Exception innerException) : base(message, innerException)
            {
            }
        }

        public virtual Task<T> HttpGet<T>(string url)
        {
            ValidateCcipUrl(url);
            return _ccipRestHttpHelper.GetAsync<T>(url);
        }

        public virtual Task<TResponse> HttpPost<TRequest, TResponse>(string url, TRequest request)
        {
            ValidateCcipUrl(url);
            return _ccipRestHttpHelper.PostAsync<TResponse, TRequest>(url, request);
        }

        public class CCIPReadResponse
        {
            [JsonProperty(PropertyName = "data")]
#if NET6_0_OR_GREATER
            [System.Text.Json.Serialization.JsonPropertyName("data")]
#endif
            public string Data { get; set; }
        }

        public class CCIPReadRequest
        {
            // EIP-3668 gateways expect lower-case JSON members.
            [JsonProperty(PropertyName = "data")]
#if NET6_0_OR_GREATER
            [System.Text.Json.Serialization.JsonPropertyName("data")]
#endif
            public string Data { get; set; }

            [JsonProperty(PropertyName = "sender")]
#if NET6_0_OR_GREATER
            [System.Text.Json.Serialization.JsonPropertyName("sender")]
#endif
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
