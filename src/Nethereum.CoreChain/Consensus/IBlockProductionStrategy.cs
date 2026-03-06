using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.CoreChain.Consensus
{
    public interface IBlockProductionStrategy
    {
        bool CanProduceBlock(long blockNumber);

        Task<TimeSpan> GetSigningDelayAsync(long blockNumber, CancellationToken cancellationToken = default);

        BlockProductionOptions PrepareBlockOptions(long blockNumber, BlockHeader? parentHeader);

        Task FinalizeBlockAsync(BlockHeader header, byte[] blockHash, BlockProductionResult result);
    }

    public class DefaultBlockProductionStrategy : IBlockProductionStrategy
    {
        private readonly ChainConfig _config;

        public DefaultBlockProductionStrategy(ChainConfig config)
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
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Coinbase = _config.Coinbase,
                Difficulty = 1,
                ExtraData = Array.Empty<byte>(),
                BlockGasLimit = _config.BlockGasLimit,
                BaseFee = _config.BaseFee,
                ChainId = _config.ChainId
            };
        }

        public Task FinalizeBlockAsync(BlockHeader header, byte[] blockHash, BlockProductionResult result)
            => Task.CompletedTask;
    }
}
