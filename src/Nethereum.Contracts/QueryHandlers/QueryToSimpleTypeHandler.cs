using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;

namespace Nethereum.Contracts.QueryHandlers
{
#if !DOTNET35

    public class QueryToSimpleTypeHandler<TFunctionMessage, TFunctionOutput> :
        QueryDecoderBaseHandler<TFunctionMessage, TFunctionOutput> where TFunctionMessage : FunctionMessage, new()
    {


        public QueryToSimpleTypeHandler(IClient client, string defaultAddressFrom = null, BlockParameter defaultBlockParameter = null):base(client, defaultAddressFrom, defaultBlockParameter)
        {
            QueryRawHandler = new QueryRawHandler<TFunctionMessage>(client, defaultAddressFrom, defaultBlockParameter);
        }

        public QueryToSimpleTypeHandler(IEthCall ethCall, string defaultAddressFrom = null, BlockParameter defaultBlockParameter = null) : base(ethCall, defaultAddressFrom, defaultBlockParameter)
        {
            QueryRawHandler = new QueryRawHandler<TFunctionMessage>(ethCall, defaultAddressFrom, defaultBlockParameter);
        }


        protected override TFunctionOutput DecodeOutput(string output)
        {
            return QueryRawHandler.FunctionMessageEncodingService.DecodeSimpleTypeOutput<TFunctionOutput>(output);
        }
    }
#endif
}