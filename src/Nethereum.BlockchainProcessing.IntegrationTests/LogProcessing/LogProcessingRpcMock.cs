using Moq;
using Nethereum.BlockchainProcessing.IntegrationTests.TestUtils;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
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
    }
}
