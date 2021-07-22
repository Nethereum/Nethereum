using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

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

#if !DOTNET35
    /// <summary>
    /// Creates a multi query handler, to enable execute a single request combining multiple queries to multiple contracts using the multicall contract https://github.com/makerdao/multicall/blob/master/src/Multicall.sol
    /// This is deployed at https://etherscan.io/address/0xeefBa1e63905eF1D7ACbA5a8513c70307C1cE441#code
    /// </summary>
    /// <param name="multiContractAdress">The address of the deployed multicall contract</param>
    public class MultiQueryHandler
    {
        public string ContractAddress { get; set; }
        private readonly QueryToDTOHandler<AggregateFunction, AggregateOutputDTO> _multiQueryToDtoHandler;
        public MultiQueryHandler(IClient client, string multiCallContractAdress = "0xeefBa1e63905eF1D7ACbA5a8513c70307C1cE441", string defaultAddressFrom = null, BlockParameter defaultBlockParameter = null)
        {
            ContractAddress = multiCallContractAdress;
            _multiQueryToDtoHandler = new QueryToDTOHandler<AggregateFunction, AggregateOutputDTO>(client, defaultAddressFrom, defaultBlockParameter);
        }

      
        public Task<IMulticallInputOutput[]> MultiCallAsync(
            params IMulticallInputOutput[] multiCalls)
        {
            return MultiCallAsync(null, multiCalls);
        }

        public async Task<IMulticallInputOutput[]> MultiCallAsync(BlockParameter block,
            params IMulticallInputOutput[] multiCalls)
        {
            var contractCalls = new List<Call>();
            foreach (var multiCall in multiCalls)
            {
                contractCalls.Add(new Call { CallData = multiCall.GetCallData(), Target = multiCall.Target });
            }

            var aggregateFunction = new AggregateFunction();
            aggregateFunction.Calls = contractCalls;
            var returnCalls = await _multiQueryToDtoHandler
                .QueryAsync(ContractAddress, aggregateFunction, block)
                .ConfigureAwait(false);

            for (var i = 0; i < returnCalls.ReturnData.Count; i++)
            {
                multiCalls[i].Decode(returnCalls.ReturnData[i]);
            }

            return multiCalls;
        }
    }
#endif
}