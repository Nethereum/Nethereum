using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.Consensus.LightClient.Tests.Live
{
    /// <summary>
    /// HTTP handler that captures outgoing requests and incoming responses.
    /// </summary>
    public class RecordingHttpHandler : HttpClientHandler
    {
        private readonly ConcurrentBag<RecordedHttpExchange> _exchanges = new ConcurrentBag<RecordedHttpExchange>();

        public IReadOnlyCollection<RecordedHttpExchange> Exchanges => _exchanges;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var requestBody = request.Content != null ? await request.Content.ReadAsStringAsync().ConfigureAwait(false) : string.Empty;
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            var clone = new HttpResponseMessage(response.StatusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, response.Content.Headers.ContentType?.MediaType ?? "application/json"),
                ReasonPhrase = response.ReasonPhrase,
                RequestMessage = request,
                Version = response.Version
            };

            foreach (var header in response.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            foreach (var header in response.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            _exchanges.Add(new RecordedHttpExchange
            {
                RequestUri = request.RequestUri?.ToString() ?? string.Empty,
                Method = request.Method.Method,
                RequestBody = requestBody ?? string.Empty,
                ResponseBody = responseBody
            });

            response.Dispose();
            return clone;
        }
    }

    public class RecordedHttpExchange
    {
        public string RequestUri { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public string RequestBody { get; set; } = string.Empty;
        public string ResponseBody { get; set; } = string.Empty;
    }
}
