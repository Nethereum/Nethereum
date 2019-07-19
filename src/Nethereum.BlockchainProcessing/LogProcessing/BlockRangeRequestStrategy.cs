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

        public BigInteger GeBlockNumberToRequestTo(BigInteger fromBlockNumber, BigInteger maxBlockNumberToRequestTo, int currentAttemptCount = 1)
        {
            if(currentAttemptCount < 1) currentAttemptCount = 1;

            var numberOfBlocksRequested = maxBlockNumberToRequestTo - fromBlockNumber + 1;

            int retryWeightedMax = _defaultNumberOfBlocksPerRequest;

            for (var attemptNumber = 1 ; attemptNumber < currentAttemptCount; attemptNumber ++)
            {
                //reduce by half for each retry
                retryWeightedMax = retryWeightedMax /2;
            }

            if(numberOfBlocksRequested <= retryWeightedMax) 
                return maxBlockNumberToRequestTo;

            if(retryWeightedMax == 1) return fromBlockNumber + 1;

            return fromBlockNumber + (retryWeightedMax -1);
           
        }
    }
}