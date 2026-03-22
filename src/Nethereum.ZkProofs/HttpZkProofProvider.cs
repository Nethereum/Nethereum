using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#if NET6_0_OR_GREATER
using System.Text.Json;
#else
using Newtonsoft.Json;
#endif

namespace Nethereum.ZkProofs
{
    public class HttpZkProofProvider : IZkProofProvider
    {
        private readonly HttpClient _httpClient;
        private readonly string _proverEndpoint;
        private readonly ZkProofScheme _scheme;

        public ZkProofScheme Scheme => _scheme;

        public HttpZkProofProvider(HttpClient httpClient, string proverEndpoint,
            ZkProofScheme scheme = ZkProofScheme.Groth16)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            if (string.IsNullOrWhiteSpace(proverEndpoint))
                throw new ArgumentException("Prover endpoint is required.", nameof(proverEndpoint));
            _proverEndpoint = proverEndpoint;
            _scheme = scheme;
        }

        public async Task<ZkProofResult> FullProveAsync(ZkProofRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var httpRequest = new HttpProveRequest
            {
                circuitWasm = Convert.ToBase64String(request.CircuitWasm),
                circuitZkey = Convert.ToBase64String(request.CircuitZkey),
                inputJson = request.InputJson,
                scheme = request.Scheme.ToString()
            };

#if NET6_0_OR_GREATER
            var json = JsonSerializer.Serialize(httpRequest);
#else
            var json = JsonConvert.SerializeObject(httpRequest);
#endif
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_proverEndpoint, content, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

#if NET6_0_OR_GREATER
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var result = JsonSerializer.Deserialize<HttpProveResponse>(responseJson)
                ?? throw new InvalidOperationException("Failed to parse prover response");
#else
            var responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var result = JsonConvert.DeserializeObject<HttpProveResponse>(responseJson)
                ?? throw new InvalidOperationException("Failed to parse prover response");
#endif

            return ZkProofResult.BuildFromJson(_scheme, result.proofJson, result.publicSignalsJson);
        }

        private class HttpProveRequest
        {
            public string circuitWasm { get; set; } = "";
            public string circuitZkey { get; set; } = "";
            public string inputJson { get; set; } = "";
            public string scheme { get; set; } = "";
        }

        private class HttpProveResponse
        {
            public string proofJson { get; set; } = "";
            public string publicSignalsJson { get; set; } = "";
        }
    }
}
