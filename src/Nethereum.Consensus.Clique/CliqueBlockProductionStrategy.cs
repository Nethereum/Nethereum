using System;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Consensus;
using Nethereum.Model;

namespace Nethereum.Consensus.Clique
{
    public class CliqueBlockProductionStrategy : IBlockProductionStrategy
    {
        private readonly ChainConfig _chainConfig;
        private readonly CliqueEngine _cliqueEngine;
        private readonly ILogger<CliqueBlockProductionStrategy>? _logger;

        public event EventHandler<BlockFinalizedEventArgs>? BlockFinalized;

        public CliqueBlockProductionStrategy(
            ChainConfig chainConfig,
            CliqueEngine cliqueEngine,
            ILogger<CliqueBlockProductionStrategy>? logger = null)
        {
            _chainConfig = chainConfig ?? throw new ArgumentNullException(nameof(chainConfig));
            _cliqueEngine = cliqueEngine ?? throw new ArgumentNullException(nameof(cliqueEngine));
            _logger = logger;
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

            return new BlockProductionOptions
            {
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Coinbase = signerAddress,
                Difficulty = difficulty,
                ExtraData = extraData,
                BlockGasLimit = _chainConfig.BlockGasLimit,
                BaseFee = _chainConfig.BaseFee,
                ChainId = _chainConfig.ChainId,
                Nonce = new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 }
            };
        }

        public Task FinalizeBlockAsync(BlockHeader header, byte[] blockHash, BlockProductionResult result)
        {
            _logger?.LogDebug("FinalizeBlockAsync called for block {Number}, ParentHash: {ParentHash}",
                header.BlockNumber,
                header.ParentHash != null ? BitConverter.ToString(header.ParentHash).Replace("-", "").ToLowerInvariant() : "null");

            // Sign the block with Clique signature
            var signature = _cliqueEngine.SignBlock(header);
            _cliqueEngine.InsertSignature(header.ExtraData!, signature);

            // Update Clique consensus state
            _cliqueEngine.ApplyBlock(header, _cliqueEngine.SignerAddress, blockHash);

            _logger?.LogInformation("Clique signed block {Number} by {Signer}",
                header.BlockNumber, _cliqueEngine.SignerAddress);

            // Raise event for P2P layer to broadcast
            BlockFinalized?.Invoke(this, new BlockFinalizedEventArgs(header, blockHash, result));

            return Task.CompletedTask;
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
