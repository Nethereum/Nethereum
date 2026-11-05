using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Consensus;
using Nethereum.Model;

namespace Nethereum.AppChain.Sequencer
{
    public class AppChainBlockProductionStrategy : IBlockProductionStrategy
    {
        private readonly AppChainConfig _config;

        public AppChainBlockProductionStrategy(AppChainConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public bool CanProduceBlock(long blockNumber) => true;

        public Task<TimeSpan> GetSigningDelayAsync(long blockNumber, CancellationToken cancellationToken = default)
            => Task.FromResult(TimeSpan.Zero);

        public BlockProductionOptions PrepareBlockOptions(long blockNumber, BlockHeader? parentHeader)
        {
            return new BlockProductionOptions
            {
                Timestamp = parentHeader == null
                    ? DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    : Math.Max(DateTimeOffset.UtcNow.ToUnixTimeSeconds(), parentHeader.Timestamp + 1),
                Coinbase = _config.Coinbase,
                BaseFee = _config.BaseFee,
                FeeFreeBaseFee = _config.FeeFreeBaseFee,
                BlockGasLimit = parentHeader == null
                    ? _config.BlockGasLimit
                    : GasLimitBound.StepToward(parentHeader.GasLimit, (long)_config.BlockGasLimit),
                ChainId = _config.ChainId,
                Difficulty = 0,
                ExtraData = ExtraDataFor(_config.AppChainName)
            };
        }

        private static byte[] ExtraDataFor(string chainName)
        {
            var tag = System.Text.Encoding.UTF8.GetBytes($"AppChain:{chainName}");
            return tag.Length <= HeaderValidator.MaximumExtraDataSize
                ? tag
                : tag.AsSpan(0, HeaderValidator.MaximumExtraDataSize).ToArray();
        }

        public Task FinalizeBlockAsync(BlockHeader header, byte[] blockHash, BlockProductionResult result)
            => Task.CompletedTask;
    }
}
