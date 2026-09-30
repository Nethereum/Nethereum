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

        public HttpBlockProverClient(string baseUrl, HttpClient httpClient = null, TimeSpan? timeout = null)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _httpClient = httpClient ?? new HttpClient();
            _httpClient.Timeout = timeout ?? TimeSpan.FromMinutes(30);
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
                ProverComputedStateRoot = !string.IsNullOrEmpty(result.ProverComputedStateRoot) ? Convert.FromBase64String(result.ProverComputedStateRoot) : null,
                ProverComputedBlockHash = !string.IsNullOrEmpty(result.ProverComputedBlockHash) ? Convert.FromBase64String(result.ProverComputedBlockHash) : null,
                StateRootVerified = result.StateRootVerified,
                BlockHashVerified = result.BlockHashVerified,
                WitnessHash = !string.IsNullOrEmpty(result.WitnessHash) ? Convert.FromBase64String(result.WitnessHash) : null,
                ElfHash = !string.IsNullOrEmpty(result.ElfHash) ? Convert.FromBase64String(result.ElfHash) : null,
                BlockNumber = result.BlockNumber,
                GasUsed = result.GasUsed,
                ProverMode = result.ProverMode
            };
        }
    }
}
