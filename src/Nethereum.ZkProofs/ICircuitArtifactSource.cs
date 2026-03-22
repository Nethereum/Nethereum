using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.ZkProofs
{
    public interface ICircuitArtifactSource
    {
        Task<byte[]> GetWasmAsync(string circuitName, CancellationToken cancellationToken = default);
        Task<byte[]> GetZkeyAsync(string circuitName, CancellationToken cancellationToken = default);
    }

    public class FileCircuitArtifactSource : ICircuitArtifactSource
    {
        private readonly CircuitArtifactLocator _locator;

        public FileCircuitArtifactSource(CircuitArtifactLocator locator)
        {
            _locator = locator;
        }

        public FileCircuitArtifactSource(string? baseDir = null)
        {
            _locator = new CircuitArtifactLocator(baseDir);
        }

        public async Task<byte[]> GetWasmAsync(string circuitName, CancellationToken cancellationToken = default)
        {
            var path = _locator.GetWasmPath(circuitName);
#if NET6_0_OR_GREATER
            return await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
#else
            return await Task.FromResult(File.ReadAllBytes(path)).ConfigureAwait(false);
#endif
        }

        public async Task<byte[]> GetZkeyAsync(string circuitName, CancellationToken cancellationToken = default)
        {
            var path = _locator.GetZkeyPath(circuitName);
#if NET6_0_OR_GREATER
            return await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
#else
            return await Task.FromResult(File.ReadAllBytes(path)).ConfigureAwait(false);
#endif
        }
    }

    public class EmbeddedCircuitArtifactSource : ICircuitArtifactSource
    {
        private readonly byte[] _wasm;
        private readonly byte[] _zkey;

        public EmbeddedCircuitArtifactSource(byte[] wasm, byte[] zkey)
        {
            _wasm = wasm;
            _zkey = zkey;
        }

        public Task<byte[]> GetWasmAsync(string circuitName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_wasm);
        }

        public Task<byte[]> GetZkeyAsync(string circuitName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_zkey);
        }
    }
}
