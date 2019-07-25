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

        public Exception GetLogsException { get; set;}

        public LogProcessingRpcMock(Web3Mock web3Mock):base(web3Mock)
        {
            web3Mock.GetLogsMock.Setup(s => s.SendRequestAsync(It.IsAny<NewFilterInput>(), null))
                .Returns<NewFilterInput, object>((filter, id) => {

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

        public void SetupLogsToReturn(BigInteger blockNumber, int numberOfTransactions, int logsPerTransaction)
        {
            for(var t = 0; t < numberOfTransactions; t++)
            {
                for (var l = 0; l < logsPerTransaction; l++)
                {
                    var log = new FilterLog
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
