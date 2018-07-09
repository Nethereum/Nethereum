using Nethereum.Contracts.Services;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Contracts.QueryHandlers
{
#if !DOTNET35

    public class QueryToSimpleTypeHandler<TFunctionMessage, TFunctionOutput> :
        QueryDecoderBaseHandler<TFunctionMessage, TFunctionOutput> where TFunctionMessage : FunctionMessage, new()
    {
        public QueryToSimpleTypeHandler(IClient client, string defaultAddressFrom) : base(client, defaultAddressFrom)
        {
            
        }

        public QueryToSimpleTypeHandler(EthApiContractService eth) : base(eth)
        {

        }

        protected override TFunctionOutput DecodeOutput(string output)
        {
            return QueryRawHandler.FunctionMessageEncodingService.DecodeSimpleTypeOutput<TFunctionOutput>(output);
        }
    }
#endif
}