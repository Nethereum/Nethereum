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
                var newMax = retryWeightedMax / 2;
                if(newMax > 0)
                {
                    retryWeightedMax = newMax;
                }
                else
                {
                    retryWeightedMax = 1;
                    break;
                }
            }

            if(numberOfBlocksRequested <= retryWeightedMax) 
                return maxBlockNumberToRequestTo;

            if(retryWeightedMax == 1) return fromBlockNumber + 1;

            return fromBlockNumber + (retryWeightedMax -1);
           
        }
    }
}