using System.Numerics;

namespace Nethereum.BlockchainProcessing.LogProcessing
{
    public interface IBlockRangeRequestStrategy
    {
        BigInteger GeBlockNumberToRequestTo(BigInteger fromBlockNumber, BigInteger maxBlockNumberToRequestTo, int retryRequestNumber = 0);
    }
}