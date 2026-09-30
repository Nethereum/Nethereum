using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.Model;

namespace Nethereum.Consensus.Clique
{
    /// <summary>
    /// Refuses a block whose seal does not belong to an authorised signer, before it is executed.
    /// EIP-225 puts the seal in extraData so that "anyone obtaining a block [can] verify it against a
    /// list of authorized signers"; a follower that imports without checking accepts a chain anyone
    /// can write.
    /// </summary>
    public sealed class CliqueConsensusBlockGate : IConsensusBlockGate
    {
        private readonly CliqueEngine _engine;
        private readonly IBlockStore _blocks;
        private readonly ILogger? _logger;

        public CliqueConsensusBlockGate(CliqueEngine engine, IBlockStore blocks, ILogger? logger = null)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _blocks = blocks ?? throw new ArgumentNullException(nameof(blocks));
            _logger = logger;
        }

        public async Task<ConsensusBlockGateResult> IsBlockCanonicalAsync(
            BlockHeader header, byte[] computedBlockHash, CancellationToken ct)
        {
            if (header == null) throw new ArgumentNullException(nameof(header));

            var verdict = await VerdictForAsync(header, ct).ConfigureAwait(false);
            if (!verdict.IsValid)
            {
                _logger?.LogWarning(
                    "Clique refused block {BlockNumber}: {Reason}", (long)header.BlockNumber, verdict.Error);
                return ConsensusBlockGateResult.Reject(verdict.Error ?? "clique validation failed");
            }

            return ConsensusBlockGateResult.Accept();
        }

        public async Task OnBlockImportedAsync(BlockHeader header, byte[] blockHash, CancellationToken ct)
        {
            if (header == null) throw new ArgumentNullException(nameof(header));

            var verdict = await VerdictForAsync(header, ct).ConfigureAwait(false);
            if (!verdict.IsValid) return;

            _engine.ApplyBlock(header, verdict.Signer!, blockHash);
        }

        private async Task<CliqueValidationResult> VerdictForAsync(BlockHeader header, CancellationToken ct)
        {
            var parent = await ParentOfAsync(header, ct).ConfigureAwait(false);
            return _engine.ValidateBlockInternal(header, parent);
        }

        private async Task<BlockHeader?> ParentOfAsync(BlockHeader header, CancellationToken ct)
        {
            if (header.ParentHash == null || (long)header.BlockNumber == 0) return null;
            try
            {
                return await _blocks.GetByHashAsync(header.ParentHash).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Clique gate could not load the parent of block {BlockNumber}",
                    (long)header.BlockNumber);
                return null;
            }
        }
    }
}
