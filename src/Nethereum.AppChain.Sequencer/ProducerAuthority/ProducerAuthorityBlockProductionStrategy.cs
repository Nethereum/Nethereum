using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Consensus;
using Nethereum.Model;

namespace Nethereum.AppChain.Sequencer.ProducerAuthority
{
    public class ProducerAuthorityBlockProductionStrategy : IBlockProductionStrategy
    {
        private readonly IProducerAuthority _authority;
        private readonly string _ourNodeId;
        private readonly IBlockProductionStrategy _inner;
        private readonly ILogger<ProducerAuthorityBlockProductionStrategy>? _logger;

        public ProducerAuthorityBlockProductionStrategy(
            IProducerAuthority authority,
            string ourNodeId,
            IBlockProductionStrategy inner,
            ILogger<ProducerAuthorityBlockProductionStrategy>? logger = null)
        {
            _authority = authority ?? throw new ArgumentNullException(nameof(authority));
            _ourNodeId = ourNodeId ?? throw new ArgumentNullException(nameof(ourNodeId));
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _logger = logger;
        }

        public bool CanProduceBlock(long blockNumber)
        {
            var currentProducer = _authority.CurrentProducer();
            var mayProduce = string.Equals(currentProducer, _ourNodeId, StringComparison.OrdinalIgnoreCase);

            if (!mayProduce)
            {
                _logger?.LogWarning(
                    "CanProduceBlock: producer authority names {CurrentProducer}, this node is {OurNodeId}",
                    currentProducer ?? "<none>", _ourNodeId);
            }

            return mayProduce;
        }

        public Task<TimeSpan> GetSigningDelayAsync(long blockNumber, CancellationToken cancellationToken = default)
            => Task.FromResult(TimeSpan.Zero);

        public BlockProductionOptions PrepareBlockOptions(long blockNumber, BlockHeader? parentHeader)
            => _inner.PrepareBlockOptions(blockNumber, parentHeader);

        public Task FinalizeBlockAsync(BlockHeader header, byte[] blockHash, BlockProductionResult result)
            => _inner.FinalizeBlockAsync(header, blockHash, result);
    }
}
