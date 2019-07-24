using Moq;
using Nethereum.BlockchainProcessing.Services;
using Nethereum.Contracts.Services;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.Blocks;
using Nethereum.RPC.Eth.Filters;
using Nethereum.RPC.Eth.Services;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.Web3;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Nethereum.BlockchainProcessing.IntegrationTests.TestUtils
{
    public class Web3Mock
    {
        Dictionary<string, VmStackMockResult> _mockVmStackResults = new Dictionary<string, VmStackMockResult>();

        public IWeb3 Web3 => Mock.Object;

        public Mock<IWeb3> Mock = new Mock<IWeb3>();
        public Mock<IEthApiContractService> EthApiContractServiceMock = new Mock<IEthApiContractService>();

        public Mock<IEthApiBlockService> BlocksServiceMock = new Mock<IEthApiBlockService>();

        public Mock<IEthBlockNumber> BlockNumberMock = new Mock<IEthBlockNumber>();

        public Mock<IEthGetLogs> GetLogsMock = new Mock<IEthGetLogs>();

        public Mock<IEthApiFilterService> FilterServiceMock = new Mock<IEthApiFilterService>();

        public Mock<IEthApiTransactionsService> TransactionServiceMock = new Mock<IEthApiTransactionsService>();

        public Mock<IEthGetTransactionByHash> GetTransactionByHashMock = new Mock<IEthGetTransactionByHash>();

        public Mock<IEthGetTransactionReceipt> GetTransactionReceiptMock = new Mock<IEthGetTransactionReceipt>();

        public Mock<IEthGetBlockWithTransactionsByNumber> GetBlockWithTransactionsByNumberMock = new Mock<IEthGetBlockWithTransactionsByNumber>();

        public Mock<IEthGetCode> GetCodeMock = new Mock<IEthGetCode>();

        public Mock<IBlockchainProcessingService> BlockchainProcessingServiceMock = new Mock<IBlockchainProcessingService>();

        public BlockchainBlockProcessingService BlockProcessingService;
        public IEthApiContractService Eth => EthApiContractServiceMock.Object;

        public Web3Mock()
        {
            Mock.Setup(m => m.Eth).Returns(EthApiContractServiceMock.Object);
            EthApiContractServiceMock.Setup(e => e.Blocks).Returns(BlocksServiceMock.Object);
            EthApiContractServiceMock.Setup(e => e.GetCode).Returns(GetCodeMock.Object);
            Mock.Setup(m => m.Processing).Returns(BlockchainProcessingServiceMock.Object);

            BlockProcessingService = new BlockchainBlockProcessingService(EthApiContractServiceMock.Object);
            BlockchainProcessingServiceMock.Setup(s => s.Blocks).Returns(BlockProcessingService);

            BlocksServiceMock.Setup(b => b.GetBlockNumber).Returns(BlockNumberMock.Object);
            BlocksServiceMock.Setup(b => b.GetBlockWithTransactionsByNumber).Returns(GetBlockWithTransactionsByNumberMock.Object);
            EthApiContractServiceMock.Setup(e => e.Filters).Returns(FilterServiceMock.Object);
            FilterServiceMock.Setup(f => f.GetLogs).Returns(GetLogsMock.Object);
            EthApiContractServiceMock.Setup(c => c.Transactions).Returns(TransactionServiceMock.Object);
            TransactionServiceMock.Setup(t => t.GetTransactionByHash).Returns(GetTransactionByHashMock.Object);
            TransactionServiceMock.Setup(t => t.GetTransactionReceipt).Returns(GetTransactionReceiptMock.Object);
        }

        public void ClearVmStackMocks()
        {
            _mockVmStackResults.Clear();
        }

        public void SetupMockForGetTransactionVmStack(string transactionHash, JObject vmStack) => GetOrAddVmStackMock(transactionHash).MockVmStack = vmStack;

        public void SetupMockForGetTransactionVmStack(string transactionHash, Exception exceptionToThrow) => GetOrAddVmStackMock(transactionHash).MockVmException = exceptionToThrow;

        private VmStackMockResult GetOrAddVmStackMock(string transactionHash)
        {
            if (!_mockVmStackResults.ContainsKey(transactionHash))
            {
                _mockVmStackResults.Add(transactionHash, new VmStackMockResult());
            }
            return _mockVmStackResults[transactionHash];
        }

        public JObject GetMockedTransactionVmStack(string transactionHash)
        {
            if (_mockVmStackResults.TryGetValue(transactionHash, out VmStackMockResult mockResult))
            {
                return mockResult.GetOrThrow();
            }
            return null;
        }

    }

    internal class VmStackMockResult
    {
        public JObject MockVmStack { get; set; }
        public Exception MockVmException { get; set; }

        public JObject GetOrThrow()
        {
            if (MockVmException != null) throw MockVmException;
            return MockVmStack;
        }
    }

}
