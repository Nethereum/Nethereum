using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;

namespace Nethereum.CoreChain.Engine
{
    public sealed class EngineForkchoiceHead
    {
        public EngineForkchoiceHead(BlockHeader header, byte[] hash)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            Hash = hash ?? throw new ArgumentNullException(nameof(hash));
        }

        public BlockHeader Header { get; }
        public byte[] Hash { get; }
    }

    public interface IEngineForkchoiceSink
    {
        void Publish(EngineForkchoiceHead head, EngineForkchoiceHead finalized);
    }

    public delegate Task<BlockHeader> HeaderByHashFetcher(byte[] hash, CancellationToken ct);

    public sealed class EngineHeaderResolver
    {
        private readonly IBlockStore _blocks;
        private readonly EnginePayloadStash _stash;
        private readonly HeaderByHashFetcher _peers;

        public EngineHeaderResolver(IBlockStore blocks, EnginePayloadStash stash, HeaderByHashFetcher peers)
        {
            _blocks = blocks ?? throw new ArgumentNullException(nameof(blocks));
            _stash = stash ?? throw new ArgumentNullException(nameof(stash));
            _peers = peers;
        }

        public async Task<BlockHeader> ResolveAsync(byte[] hash, CancellationToken ct)
        {
            var stored = await _blocks.GetByHashAsync(hash).ConfigureAwait(false);
            if (stored != null) return stored;

            if (_stash.TryGet(hash, out var stashed)) return stashed.Header;

            if (_peers == null) return null;
            var fetched = await _peers(hash, ct).ConfigureAwait(false);
            return BlockHashCalculator.HashesTo(fetched, hash) ? fetched : null;
        }
    }
}
