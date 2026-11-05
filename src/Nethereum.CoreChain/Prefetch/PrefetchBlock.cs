using System;
using Nethereum.EVM;
using Nethereum.Model;

namespace Nethereum.CoreChain.Prefetch
{
    internal sealed class PrefetchBlock
    {
        private readonly BlockHeader _header;
        private readonly HardforkName _fork;
        private readonly Func<BlockHeader, string>? _authorResolver;

        public PrefetchBlock(
            BlockHeader header, HardforkName fork, ChainConfig chainConfig, HardforkConfig hardforkConfig,
            Func<BlockHeader, string>? authorResolver)
        {
            _header = header;
            _fork = fork;
            ChainConfig = chainConfig;
            HardforkConfig = hardforkConfig;
            _authorResolver = authorResolver;
        }

        public ChainConfig ChainConfig { get; }

        public HardforkConfig HardforkConfig { get; }

        public bool Eip158EmptyAccountPruning => _fork >= HardforkName.SpuriousDragon;

        public BlockContext NewContext() => BlockExecutor.BuildBlockContext(_header, ChainConfig, _fork, _authorResolver);
    }
}
