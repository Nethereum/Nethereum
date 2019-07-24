using Nethereum.BlockchainProcessing.IntegrationTests.TestUtils;
using Nethereum.Web3;

namespace Nethereum.BlockchainProcessing.IntegrationTests.BlockProcessing
{
    public class BlockProcessingTestBase
    {
        protected Web3Mock Web3Mock;
        protected IWeb3 Web3 => Web3Mock.Web3;

        public BlockProcessingTestBase()
        {
            Web3Mock = new Web3Mock();
        }

    }
}
