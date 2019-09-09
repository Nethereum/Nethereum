using System.Numerics;

namespace Nethereum.BlockchainProcessing.LogProcessing
{
    public class BlockRangeRequestStrategy : IBlockRangeRequestStrategy
    {
        public int DefaultNumberOfBlocksPerRequest {get; }
        public int RetryWeight {get; }

        public BlockRangeRequestStrategy(int defaultNumberOfBlocksPerRequest = 100, int retryWeight = 0)
        {
            DefaultNumberOfBlocksPerRequest = defaultNumberOfBlocksPerRequest;
            RetryWeight = retryWeight;
        }

        public BigInteger GeBlockNumberToRequestTo(BigInteger fromBlockNumber, BigInteger maxBlockNumberToRequestTo, int retryRequestNumber = 0)
        {
            var blocksRequested = (int)(maxBlockNumberToRequestTo - fromBlockNumber) + 1;

            var maxNumberOfBlocks = DefaultNumberOfBlocksPerRequest;

            // block size is lower than max
            if (blocksRequested < maxNumberOfBlocks)
            {
                maxNumberOfBlocks = blocksRequested;
            }

            if (retryRequestNumber > 0)
            {
                //reduce block range relative to retry number
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
            return numberOfBlocksPerRequest / (retryRequestNumber + 1) + (RetryWeight * retryRequestNumber);
        }
    }
}