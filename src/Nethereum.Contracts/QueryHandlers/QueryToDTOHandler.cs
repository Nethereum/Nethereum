using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;

namespace Nethereum.Contracts.QueryHandlers
{
#if !DOTNET35

    public class QueryToDTOHandler<TFunctionMessage, TFunctionOutput> :
        QueryDecoderBaseHandler<TFunctionMessage, TFunctionOutput> where TFunctionMessage : FunctionMessage, new()
        where TFunctionOutput: IFunctionOutputDTO, new()
    {

        public QueryToDTOHandler(IClient client, string defaultAddressFrom = null, BlockParameter defaultBlockParameter = null) : base(client, defaultAddressFrom, defaultBlockParameter)
        {
            QueryRawHandler = new QueryRawHandler<TFunctionMessage>(client, defaultAddressFrom, defaultBlockParameter);
        }

        public QueryToDTOHandler(EthCall ethCall, string defaultAddressFrom = null, BlockParameter defaultBlockParameter = null) : base(ethCall, defaultAddressFrom, defaultBlockParameter)
        {
            QueryRawHandler = new QueryRawHandler<TFunctionMessage>(ethCall, defaultAddressFrom, defaultBlockParameter);
        }

        protected override TFunctionOutput DecodeOutput(string output)
        {
            return QueryRawHandler.FunctionMessageEncodingService.DecodeDTOTypeOutput<TFunctionOutput>(output);
        }
    }
#endif
}