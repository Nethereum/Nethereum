using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Consensus;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.Consensus.Clique
{
    public class CliqueBlockProductionStrategy : IBlockProductionStrategy
    {
        private readonly ChainConfig _chainConfig;
        private readonly CliqueEngine _cliqueEngine;
        private readonly ILogger<CliqueBlockProductionStrategy>? _logger;
        private readonly ICliqueProposalStore? _proposalStore;
        private readonly Random _random = new();

        public event EventHandler<BlockFinalizedEventArgs>? BlockFinalized;

        public CliqueBlockProductionStrategy(
            ChainConfig chainConfig,
            CliqueEngine cliqueEngine,
            ILogger<CliqueBlockProductionStrategy>? logger = null,
            ICliqueProposalStore? proposalStore = null)
        {
            _chainConfig = chainConfig ?? throw new ArgumentNullException(nameof(chainConfig));
            _cliqueEngine = cliqueEngine ?? throw new ArgumentNullException(nameof(cliqueEngine));
            _logger = logger;
            _proposalStore = proposalStore;
        }

        public CliqueEngine CliqueEngine => _cliqueEngine;

        public bool CanProduceBlock(long blockNumber)
        {
            return _cliqueEngine.CanProduceBlock(blockNumber);
        }

        public async Task<TimeSpan> GetSigningDelayAsync(long blockNumber, CancellationToken cancellationToken = default)
        {
            return await _cliqueEngine.GetSigningDelayAsync(blockNumber, cancellationToken);
        }

        public BlockProductionOptions PrepareBlockOptions(long blockNumber, BlockHeader? parentHeader)
        {
            var signerAddress = _cliqueEngine.SignerAddress;
            var difficulty = _cliqueEngine.GetDifficulty(blockNumber, signerAddress);
            var extraData = _cliqueEngine.PrepareExtraData(blockNumber);
            var vote = ChooseVote(blockNumber);

            return new BlockProductionOptions
            {
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Coinbase = vote.Target,
                Difficulty = difficulty,
                ExtraData = extraData,
                BlockGasLimit = _chainConfig.BlockGasLimit,
                BaseFee = _chainConfig.BaseFee,
                ChainId = _chainConfig.ChainId,
                Nonce = vote.Nonce,
                ApplyConsensusSeal = SealWithThisSigner
            };
        }

        public string ResolveFeeRecipient(BlockHeader header) => _cliqueEngine.SignerAddress;

        private (string Target, byte[] Nonce) ChooseVote(long blockNumber)
        {
            if (_proposalStore == null || IsCheckpoint(blockNumber))
                return (AddressUtil.ZERO_ADDRESS, new byte[8]);

            var snapshot = _cliqueEngine.CurrentSnapshot;
            var candidates = _proposalStore.Proposals
                .Where(p => snapshot.ValidVote(p.Key, p.Value))
                .ToList();

            if (candidates.Count == 0)
                return (AddressUtil.ZERO_ADDRESS, new byte[8]);

            var chosen = candidates[_random.Next(candidates.Count)];
            var nonce = chosen.Value ? CliqueEngine.NONCE_AUTH : CliqueEngine.NONCE_DROP;
            return (chosen.Key, (byte[])nonce.Clone());
        }

        private bool IsCheckpoint(long blockNumber) => blockNumber % _cliqueEngine.Config.EpochLength == 0;

        public Task FinalizeBlockAsync(BlockHeader header, byte[] blockHash, BlockProductionResult result)
        {
            _logger?.LogDebug("FinalizeBlockAsync called for block {Number}, ParentHash: {ParentHash}",
                header.BlockNumber,
                header.ParentHash != null ? BitConverter.ToString(header.ParentHash).Replace("-", "").ToLowerInvariant() : "null");

            _cliqueEngine.ApplyBlock(header, _cliqueEngine.SignerAddress, blockHash);

            _logger?.LogInformation("Clique signed block {Number} by {Signer}",
                header.BlockNumber, _cliqueEngine.SignerAddress);

            BlockFinalized?.Invoke(this, new BlockFinalizedEventArgs(header, blockHash, result));

            return Task.CompletedTask;
        }

        private void SealWithThisSigner(BlockHeader header)
        {
            var signature = _cliqueEngine.SignBlock(header);
            _cliqueEngine.InsertSignature(header.ExtraData!, signature);
        }
    }

    public class BlockFinalizedEventArgs : EventArgs
    {
        public BlockHeader Header { get; }
        public byte[] BlockHash { get; }
        public BlockProductionResult Result { get; }

        public BlockFinalizedEventArgs(BlockHeader header, byte[] blockHash, BlockProductionResult result)
        {
            Header = header;
            BlockHash = blockHash;
            Result = result;
        }
    }
}
