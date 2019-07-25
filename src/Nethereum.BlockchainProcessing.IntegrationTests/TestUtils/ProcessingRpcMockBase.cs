using Nethereum.Hex.HexTypes;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.BlockchainProcessing.IntegrationTests.TestUtils
{
    public class ProcessingRpcMockBase
    {
        protected Queue<HexBigInteger> blockNumberQueue;

        protected HexBigInteger currentBlockNumber;

        protected ProcessingRpcMockBase(Web3Mock web3Mock)
        {
            blockNumberQueue = new Queue<HexBigInteger>();

            web3Mock
                .BlockNumberMock
                .Setup(m => m.SendRequestAsync(null))
                .Returns(() => {
                    BlockNumberRequestCount++;
                    var blockNumberToReturn = currentBlockNumber ?? blockNumberQueue.Dequeue();
                    return Task.FromResult(blockNumberToReturn);
                });
        }

        public int BlockNumberRequestCount { get; set; }

        public virtual void AddToGetBlockNumberRequestQueue(BigInteger blockNumberToReturn)
        {
            blockNumberQueue.Enqueue(new HexBigInteger(blockNumberToReturn));
        }

        public virtual void SetupGetCurrentBlockNumber(BigInteger blockNumberToReturn)
        {
            currentBlockNumber = new HexBigInteger(blockNumberToReturn);
        }
    }
}
