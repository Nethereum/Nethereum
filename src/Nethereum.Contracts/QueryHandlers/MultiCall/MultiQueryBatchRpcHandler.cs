using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.Util;

namespace Nethereum.Contracts.QueryHandlers.MultiCall
{


#if !DOTNET35
    /// <summary>
    /// Creates a multi query handler, to enable execute a single request combining multiple queries to multiple contracts using batches
    ///
    public class MultiQueryBatchRpcHandler
    {
        public const int DEFAULT_CALLS_PER_REQUEST = 3000;
        public MultiQueryBatchRpcHandler(IClient client, string defaultAddressFrom = null, BlockParameter defaultBlockParameter = null)
        {
            Client = client;
            DefaultAddressFrom = defaultAddressFrom;
            DefaultBlockParameter = defaultBlockParameter;
        }

        public IClient Client { get; }
        public string DefaultAddressFrom { get; set; }
        public BlockParameter DefaultBlockParameter { get; set; }

        public Task<IMulticallInputOutput[]> MultiCallAsync(
         params IMulticallInputOutput[] multiCalls)
        {
            return MultiCallAsync(null, DEFAULT_CALLS_PER_REQUEST, multiCalls);
        }

        public Task<IMulticallInputOutput[]> MultiCallAsync(int pageSize = DEFAULT_CALLS_PER_REQUEST,
            params IMulticallInputOutput[] multiCalls)
        {
            return MultiCallAsync(null, pageSize, multiCalls);
        }

        public async Task<IMulticallInputOutput[]> MultiCallAsync(BlockParameter block, int pageSize = DEFAULT_CALLS_PER_REQUEST,
            params IMulticallInputOutput[] multiCalls)
        {
                if (block == null) block = DefaultBlockParameter;
                var results = new List<string>();
                foreach (var page in multiCalls.Batch(pageSize))
                {
                    var contractCalls = new List<CallInput>();
                    foreach (var multiCall in page)
                    {
       
                        contractCalls.Add(new CallInput { Data = multiCall.GetCallData().ToHex(), 
                                                         To = multiCall.Target,
                                                         Value = new HexBigInteger(multiCall.Value),
                                                         From = DefaultAddressFrom
                         });
                    }

                    var ethCall = new EthCall(Client);
                    var returnCalls = await ethCall.SendBatchRequestAsync(contractCalls.ToArray(), block);
                    results.AddRange(returnCalls);
                }

                for (var i = 0; i < results.Count; i++)
                {
                    
                        multiCalls[i].Decode(results[i].HexToByteArray());
                        multiCalls[i].Success = true;
                }

                return multiCalls;

            }
           
     }
#endif
}