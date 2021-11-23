using System.Numerics;

namespace Nethereum.BlockchainProcessing.LogProcessing
{
    public class BlockRangeRequestStrategy
    {
        private const int DefaultNumberOfBlocksPerRequest = 100;
        private const int DefaultRetryWeight = 0;

        private readonly int _defaultNumberOfBlocksPerRequest;
        private readonly int _retryWeight;

        public BlockRangeRequestStrategy(int defaultNumberOfBlocksPerRequest = DefaultNumberOfBlocksPerRequest, int retryWeight = DefaultRetryWeight)
        {
            _defaultNumberOfBlocksPerRequest = defaultNumberOfBlocksPerRequest;
            _retryWeight = retryWeight;
        }

        public BigInteger GeBlockNumberToRequestTo(BigInteger fromBlockNumber, BigInteger maxBlockNumberToRequestTo, int retryRequestNumber = 0)
        {
            var totalNumberOfBlocksRequested = (int)(maxBlockNumberToRequestTo - fromBlockNumber) + 1;

            var maxNumberOfBlocks = _defaultNumberOfBlocksPerRequest;

            //we start with the smaller batch instead if we only need less than the total batch
            if (totalNumberOfBlocksRequested < maxNumberOfBlocks)
            {
                maxNumberOfBlocks = totalNumberOfBlocksRequested;
            }

            if (retryRequestNumber > 0)
            {
                maxNumberOfBlocks = GetMaxNumberOfBlocksToRequestToRetryAttempt(retryRequestNumber, maxNumberOfBlocks);
            }

            if ((fromBlockNumber + maxNumberOfBlocks) > maxBlockNumberToRequestTo)
            {
                return maxBlockNumberToRequestTo;
            }
            else
            {
                if (maxNumberOfBlocks > 1)
                {
                    // including fromBlockNumber in the count of max number of blocks
                    return fromBlockNumber + maxNumberOfBlocks - 1;
                }
                else
                {
                    //if maxNumber of blocks is 1 or less, we can only retrieve the current block (from block number)
                    return fromBlockNumber;
                }
            }
        }

        protected virtual int GetMaxNumberOfBlocksToRequestToRetryAttempt(int retryRequestNumber, int numberOfBlocksPerRequest)
        {
            return numberOfBlocksPerRequest / (retryRequestNumber + 1) + (_retryWeight * retryRequestNumber);
        }
    }
}