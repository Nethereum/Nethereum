using System.Numerics;

namespace Nethereum.BlockchainProcessing.LogProcessing
{
    public class BlockRangeRequestStrategy
    {
        private readonly int _defaultNumberOfBlocksPerRequest;

        public BlockRangeRequestStrategy(int defaultNumberOfBlocksPerRequest = 100)
        {
            _defaultNumberOfBlocksPerRequest = defaultNumberOfBlocksPerRequest;
        }

        public BigInteger GeBlockNumberToRequestTo(BigInteger lastBlockNumber, BigInteger maxBlockNumberToRequestTo, int retryRequestNumber = 0)
        {
            int maxNumberOfBlocks = _defaultNumberOfBlocksPerRequest / (retryRequestNumber + 1);

            return (lastBlockNumber + maxNumberOfBlocks) > maxBlockNumberToRequestTo
                ? maxBlockNumberToRequestTo
                : lastBlockNumber + maxNumberOfBlocks;
        }
    }
}