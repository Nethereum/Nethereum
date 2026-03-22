using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace Nethereum.ZkProofs.Snarkjs.Blazor
{
    public class SnarkjsBlazorProvider : IZkProofProvider, IAsyncDisposable
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly string _snarkjsUrl;
        private IJSObjectReference _module;

        public ZkProofScheme Scheme => ZkProofScheme.Groth16;

        public SnarkjsBlazorProvider(IJSRuntime jsRuntime, string snarkjsUrl)
        {
            _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
            if (string.IsNullOrWhiteSpace(snarkjsUrl))
                throw new ArgumentException(
                    "snarkjsUrl is required. Provide a local path (e.g. \"./js/snarkjs.min.mjs\") " +
                    "or a CDN URL. To self-host: npm install snarkjs, then copy build/snarkjs.min.mjs to your wwwroot.",
                    nameof(snarkjsUrl));
            _snarkjsUrl = snarkjsUrl;
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            _module = await _jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", cancellationToken,
                new object[] { "./_content/Nethereum.ZkProofs.Snarkjs.Blazor/snarkjsInterop.js" })
                .ConfigureAwait(false);

            await _module.InvokeVoidAsync("initialize", cancellationToken,
                new object[] { _snarkjsUrl }).ConfigureAwait(false);
        }

        public async Task<ZkProofResult> FullProveAsync(ZkProofRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (_module == null)
                throw new InvalidOperationException("Provider not initialized. Call InitializeAsync() first.");

            var wasmBase64 = Convert.ToBase64String(request.CircuitWasm);
            var zkeyBase64 = Convert.ToBase64String(request.CircuitZkey);

            var resultJson = await _module.InvokeAsync<string>(
                "fullProve", cancellationToken,
                new object[] { request.InputJson, wasmBase64, zkeyBase64 })
                .ConfigureAwait(false);

            var wrapper = JsonSerializer.Deserialize<SnarkjsJsResult>(resultJson)
                ?? throw new InvalidOperationException("Failed to parse snarkjs JS result");

            return ZkProofResult.BuildFromJson(ZkProofScheme.Groth16, wrapper.proof, wrapper.publicSignals);
        }

        public async ValueTask DisposeAsync()
        {
            if (_module != null)
            {
                await _module.DisposeAsync().ConfigureAwait(false);
                _module = null;
            }
        }

        private class SnarkjsJsResult
        {
            public string proof { get; set; } = "";
            public string publicSignals { get; set; } = "";
        }
    }
}
