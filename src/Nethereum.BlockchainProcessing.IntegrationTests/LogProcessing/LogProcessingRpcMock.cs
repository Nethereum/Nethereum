using Moq;
using Nethereum.BlockchainProcessing.IntegrationTests.TestUtils;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.BlockchainProcessing.IntegrationTests.LogProcessing
{
    public class LogProcessingRpcMock : ProcessingRpcMockBase
    {
        public List<FilterLog> Logs { get; set;} = new List<FilterLog>();

        public List<NewFilterInput> GetLogsFiltersInvoked { get;set;} = new List<NewFilterInput>();

        public Exception GetLogsException { get; set;}

        public LogProcessingRpcMock(Web3Mock web3Mock):base(web3Mock)
        {
            web3Mock.GetLogsMock.Setup(s => s.SendRequestAsync(It.IsAny<NewFilterInput>(), null))
                .Returns<NewFilterInput, object>((filter, id) => {

                    GetLogsFiltersInvoked.Add(filter);
                    GetLogRequestCount ++;

                    if(GetLogsException != null) throw GetLogsException;

                    var logs = Logs.Where(l => 
                        l.BlockNumber.Value >= 
                            filter.FromBlock.BlockNumber.Value && 
                            l.BlockNumber.Value <= 
                            filter.ToBlock.BlockNumber.Value).ToArray();

                    return Task.FromResult(logs);
                    });
        }

        public int GetLogRequestCount { get; set; }

        public void SetupLogsToReturn(
            BigInteger blockNumber, int numberOfTransactions, int logsPerTransaction, Func<BigInteger, int, int, FilterLog> createLog = null)
        {
            for(var t = 0; t < numberOfTransactions; t++)
            {
                for (var l = 0; l < logsPerTransaction; l++)
                {
                    var log = createLog?.Invoke(blockNumber, t, l) ?? new FilterLog
                    {
                        BlockNumber = new HexBigInteger(blockNumber),
                        LogIndex = new HexBigInteger(l),
                        TransactionIndex = new HexBigInteger(t)
                    };

                    Logs.Add(log);
                }
            }

        }

        public void SetupGetLogsToReturnDummyERC20Transfers(
            BigInteger blockFrom, 
            BigInteger blockTo, 
            int logsPerTransaction, 
            int transactionsPerBlock)
        {
            for (var block = blockFrom; block <= blockTo; block++)
            {
                SetupLogsToReturn(
                    block,
                    transactionsPerBlock,
                    logsPerTransaction,
                    (bNum, txIndex, logIndex) =>
                    CreateDummyTransfer(bNum, txIndex, logIndex));
            }
        }

        private static FilterLog CreateDummyTransfer(BigInteger blockNumber, BigInteger transactionIndex, BigInteger logIndex)
        {
            var sample = SampleTransferLog();
            sample.BlockNumber = blockNumber.ToHexBigInteger();
            sample.TransactionIndex = transactionIndex.ToHexBigInteger();
            sample.LogIndex = logIndex.ToHexBigInteger();
            return sample;
        }

        private static FilterLog SampleTransferLog()
        {
            return SampleTransferLogAsJObject.ToObject<FilterLog>();
        }

        private static readonly JObject SampleTransferLogAsJObject = JObject.Parse(
    $@"{{
  'address': '0x243e72b69141f6af525a9a5fd939668ee9f2b354',
  'topics': [
    '0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef',
    '0x00000000000000000000000012890d2cce102216644c59dae5baed380d84830c',
    '0x00000000000000000000000013f022d72158410433cbd66f5dd8bf6d2d129924'
  ],
  'data': '0x00000000000000000000000000000000000000000000000000000000000003e8',
  'blockNumber': '0x36',
  'transactionHash': '0x19ce02e0b4fdf5cfee0ed21141b38c2d88113c58828c771e813ce2624af127cd',
  'transactionIndex': '0x0',
  'blockHash': '0x58dab5a71037752b36e0a6af02f290fbc3dc5b2abf88d88f2c04defd9b8fb03b',
  'logIndex': '0x0',
  'removed': false
}}");
    
    }
}
