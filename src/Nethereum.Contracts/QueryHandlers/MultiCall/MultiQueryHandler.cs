using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts.Constants;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;

namespace Nethereum.Contracts.QueryHandlers.MultiCall
{
    public partial class Call : CallBase { }

    public class CallBase
    {
        [Parameter("address", "target", 1)]
        public virtual string Target { get; set; }
        [Parameter("bytes", "callData", 2)]
        public virtual byte[] CallData { get; set; }
    }

    public partial class AggregateFunction : AggregateFunctionBase { }

    [Function("aggregate", typeof(AggregateOutputDTO))]
    public class AggregateFunctionBase : FunctionMessage
    {
        [Parameter("tuple[]", "calls", 1)]
        public virtual List<Call> Calls { get; set; }
    }

    public partial class AggregateOutputDTO : AggregateOutputDTOBase { }

    [FunctionOutput]
    public class AggregateOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("uint256", "blockNumber", 1)]
        public virtual BigInteger BlockNumber { get; set; }
        [Parameter("bytes[]", "returnData", 2)]
        public virtual List<byte[]> ReturnData { get; set; }
    }

    public partial class Result : ResultBase { }

    public class ResultBase
    {
        [Parameter("bool", "success", 1)]
        public virtual bool Success { get; set; }
        [Parameter("bytes", "returnData", 2)]
        public virtual byte[] ReturnData { get; set; }
    }

    public partial class Call3 : Call3Base { }

    public class Call3Base
    {
        [Parameter("address", "target", 1)]
        public virtual string Target { get; set; }
        [Parameter("bool", "allowFailure", 2)]
        public virtual bool AllowFailure { get; set; }
        [Parameter("bytes", "callData", 3)]
        public virtual byte[] CallData { get; set; }
    }

    public partial class Call3Value : Call3ValueBase { }

    public class Call3ValueBase
    {
        [Parameter("address", "target", 1)]
        public virtual string Target { get; set; }
        [Parameter("bool", "allowFailure", 2)]
        public virtual bool AllowFailure { get; set; }
        [Parameter("uint256", "value", 3)]
        public virtual BigInteger Value { get; set; }
        [Parameter("bytes", "callData", 4)]
        public virtual byte[] CallData { get; set; }
    }

    public partial class Aggregate3Function : Aggregate3FunctionBase { }

    [Function("aggregate3", typeof(Aggregate3OutputDTO))]
    public class Aggregate3FunctionBase : FunctionMessage
    {
        [Parameter("tuple[]", "calls", 1)]
        public virtual List<Call3> Calls { get; set; }
    }

    public partial class Aggregate3ValueFunction : Aggregate3ValueFunctionBase { }

    [Function("aggregate3Value", typeof(Aggregate3ValueOutputDTO))]
    public class Aggregate3ValueFunctionBase : FunctionMessage
    {
        [Parameter("tuple[]", "calls", 1)]
        public virtual List<Call3Value> Calls { get; set; }
    }

    public partial class Aggregate3OutputDTO : Aggregate3OutputDTOBase { }

    [FunctionOutput]
    public class Aggregate3OutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("tuple[]", "returnData", 1)]
        public virtual List<Result> ReturnData { get; set; }
    }

    public partial class Aggregate3ValueOutputDTO : Aggregate3ValueOutputDTOBase { }

    [FunctionOutput]
    public class Aggregate3ValueOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("tuple[]", "returnData", 1)]
        public virtual List<Result> ReturnData { get; set; }
    }

#if !DOTNET35
    /// <summary>
    /// Creates a multi query handler, to enable execute a single request combining multiple queries to multiple contracts using the multicall contract https://github.com/makerdao/multicall/blob/master/src/Multicall.sol
    /// This is deployed at https://etherscan.io/address/0xeefBa1e63905eF1D7ACbA5a8513c70307C1cE441#code
    /// </summary>
    /// <param name="multiContractAdress">The address of the deployed multicall contract</param>
    public class MultiQueryHandler
    {
        public const int DEFAULT_CALLS_PER_REQUEST = 3000;
        public string ContractAddress { get; set; }
        private readonly QueryToDTOHandler<AggregateFunction, AggregateOutputDTO> _multiQueryV1ToDtoHandler;
        private readonly QueryToDTOHandler<Aggregate3Function, Aggregate3OutputDTO> _multiQueryToDtoHandler;
        private readonly QueryToDTOHandler<Aggregate3ValueFunction, Aggregate3ValueOutputDTO> _multiQueryToValueDtoHandler;

        public MultiQueryHandler(IClient client, string multiCallContractAdress = CommonAddresses.MULTICALL_ADDRESS, string defaultAddressFrom = null, BlockParameter defaultBlockParameter = null)
        {
            ContractAddress = multiCallContractAdress;
            _multiQueryV1ToDtoHandler =
                new QueryToDTOHandler<AggregateFunction, AggregateOutputDTO>(client, defaultAddressFrom,
                    defaultBlockParameter);
            _multiQueryToDtoHandler = new QueryToDTOHandler<Aggregate3Function, Aggregate3OutputDTO>(client, defaultAddressFrom, defaultBlockParameter);
            _multiQueryToValueDtoHandler = new QueryToDTOHandler<Aggregate3ValueFunction, Aggregate3ValueOutputDTO>(client, defaultAddressFrom, defaultBlockParameter);
        }

        public Task<IMulticallInputOutput[]> MultiCallV1Async(
            params IMulticallInputOutput[] multiCalls)
        {
            return MultiCallV1Async(null, multiCalls);
        }

        public async Task<IMulticallInputOutput[]> MultiCallV1Async(BlockParameter block,
            params IMulticallInputOutput[] multiCalls)
        {
            var contractCalls = new List<Call>();
            foreach (var multiCall in multiCalls)
            {
                contractCalls.Add(new Call { CallData = multiCall.GetCallData(), Target = multiCall.Target });
            }

            var aggregateFunction = new AggregateFunction();
            aggregateFunction.Calls = contractCalls;
            var returnCalls = await _multiQueryV1ToDtoHandler
                .QueryAsync(ContractAddress, aggregateFunction, block)
                .ConfigureAwait(false);

            for (var i = 0; i < returnCalls.ReturnData.Count; i++)
            {
                multiCalls[i].Decode(returnCalls.ReturnData[i]);
            }

            return multiCalls;
        }

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

            if (multiCalls.Any(x => x.Value > 0))
            {
                var results = new List<Result>();
                foreach (var page in multiCalls.Batch(pageSize))
                {
                    var contractCalls = new List<Call3Value>();
                    foreach (var multiCall in page)
                    {
                        contractCalls.Add(new Call3Value { CallData = multiCall.GetCallData(), Target = multiCall.Target, AllowFailure = multiCall.AllowFailure, Value = multiCall.Value });
                    }

                    var aggregateFunction = new Aggregate3ValueFunction();
                    aggregateFunction.Calls = contractCalls;
                    var returnCalls = await _multiQueryToValueDtoHandler
                        .QueryAsync(ContractAddress, aggregateFunction, block)
                        .ConfigureAwait(false);
                    results.AddRange(returnCalls.ReturnData);
                }

                for (var i = 0; i < results.Count; i++)
                {
                    if (results[i].Success)
                    {
                        multiCalls[i].Decode(results[i].ReturnData);
                        multiCalls[i].Success = true;
                    }
                    else
                    {
                        multiCalls[i].Success = false;
                    }
                }

                return multiCalls;

            }
            else
            {
                var results = new List<Result>();
                foreach (var page in multiCalls.Batch(pageSize))
                {
                    var contractCalls = new List<Call3>();
                    foreach (var multiCall in page)
                    {
                        contractCalls.Add(new Call3 { CallData = multiCall.GetCallData(), Target = multiCall.Target, AllowFailure = multiCall.AllowFailure });
                    }

                    var aggregateFunction = new Aggregate3Function();
                    aggregateFunction.Calls = contractCalls;
                    var returnCalls = await _multiQueryToDtoHandler
                        .QueryAsync(ContractAddress, aggregateFunction, block)
                        .ConfigureAwait(false);
                    results.AddRange(returnCalls.ReturnData);
                }

                for (var i = 0; i < results.Count; i++)
                {
                    if (results[i].Success)
                    {
                        multiCalls[i].Decode(results[i].ReturnData);
                        multiCalls[i].Success = true;
                    }
                    else
                    {
                        multiCalls[i].Success = false;
                    }
                }

                return multiCalls;
            }
            
        }

        
    }
#endif
}