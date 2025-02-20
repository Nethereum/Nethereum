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


        /// <summary>
        /// Use this method to create a batch of calls to be executed in a single request in combination with other rpc batch requests
        /// </summary>
        /// <param name="rpcObjectIdStartAt">This the rpc object id to start with</param>
        /// <param name="multiCalls"></param>
        /// <returns></returns>
        public MulticallInputOutputRpcBatchItem[] CreateMulticallInputOutputRpcBatchItems(int rpcObjectIdStartAt = 0,
            params IMulticallInputOutput[] multiCalls)
        {
            var batchItems = new List<MulticallInputOutputRpcBatchItem>();
            for (var i = 0; i < multiCalls.Length; i++)
            {
                var callInput = new CallInput
                {
                    Data = multiCalls[i].GetCallData().ToHex(),
                    To = multiCalls[i].Target,
                    Value = new HexBigInteger(multiCalls[i].Value),
                    From = DefaultAddressFrom
                };

                var ethCall = new EthCall(Client);
                var rpcRequestResponseBatchItem = ethCall.CreateBatchItem(callInput, rpcObjectIdStartAt + i);

                batchItems.Add(new MulticallInputOutputRpcBatchItem(multiCalls[i], rpcRequestResponseBatchItem, rpcObjectIdStartAt + i));
            }
            return batchItems.ToArray();
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