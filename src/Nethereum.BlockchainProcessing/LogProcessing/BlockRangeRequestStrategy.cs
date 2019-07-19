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

        public BigInteger GeBlockNumberToRequestTo(BigInteger lastBlockNumber, BigInteger maxBlockNumberToRequestTo, int retryRequestNumber = 1)
        {
            var numberOfBlocks = maxBlockNumberToRequestTo - lastBlockNumber + 1;

            int maxNumberOfBlocks = _defaultNumberOfBlocksPerRequest;

            for (var retryNumber = 1 ; retryNumber < retryRequestNumber; retryNumber ++)
            {
                //reduce by half for each retry
                maxNumberOfBlocks = maxNumberOfBlocks /2;
            }

            if(numberOfBlocks <= maxNumberOfBlocks) 
                return maxBlockNumberToRequestTo;

            return numberOfBlocks == 1 ? lastBlockNumber + 1 : lastBlockNumber + (maxNumberOfBlocks - 1);
           
        }
    }
}