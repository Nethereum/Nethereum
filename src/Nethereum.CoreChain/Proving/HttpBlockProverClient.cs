using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.CoreChain.Proving
{
    public class HttpBlockProverClient : IBlockProver
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public HttpBlockProverClient(string baseUrl, HttpClient httpClient = null)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _httpClient = httpClient ?? new HttpClient();
        }

        public async Task<BlockProofResult> ProveBlockAsync(byte[] witnessBytes,
            byte[] preStateRoot, byte[] postStateRoot, long blockNumber)
        {
            var request = new ProveBlockRequest
            {
                WitnessBytes = Convert.ToBase64String(witnessBytes ?? Array.Empty<byte>()),
                PreStateRoot = preStateRoot != null ? Convert.ToBase64String(preStateRoot) : null,
                PostStateRoot = postStateRoot != null ? Convert.ToBase64String(postStateRoot) : null,
                BlockNumber = blockNumber
            };

#if NET6_0_OR_GREATER
            var json = System.Text.Json.JsonSerializer.Serialize(request);
#else
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(request);
#endif
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_baseUrl}/prove", content).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

#if NET6_0_OR_GREATER
            var result = System.Text.Json.JsonSerializer.Deserialize<ProveBlockResponse>(responseJson);
#else
            var result = Newtonsoft.Json.JsonConvert.DeserializeObject<ProveBlockResponse>(responseJson);
#endif

            return new BlockProofResult
            {
                ProofBytes = !string.IsNullOrEmpty(result.ProofBytes) ? Convert.FromBase64String(result.ProofBytes) : null,
                PreStateRoot = !string.IsNullOrEmpty(result.PreStateRoot) ? Convert.FromBase64String(result.PreStateRoot) : null,
                PostStateRoot = !string.IsNullOrEmpty(result.PostStateRoot) ? Convert.FromBase64String(result.PostStateRoot) : null,
                WitnessHash = !string.IsNullOrEmpty(result.WitnessHash) ? Convert.FromBase64String(result.WitnessHash) : null,
                BlockNumber = result.BlockNumber,
                ProverMode = result.ProverMode
            };
        }
    }
}
