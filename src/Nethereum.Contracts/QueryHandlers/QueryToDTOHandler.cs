using Nethereum.Contracts.Services;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Contracts.QueryHandlers
{
#if !DOTNET35

    public class QueryToDTOHandler<TFunctionMessage, TFunctionOutput> :
        QueryDecoderBaseHandler<TFunctionMessage, TFunctionOutput> where TFunctionMessage : FunctionMessage, new()
        where TFunctionOutput: IFunctionOutputDTO, new()
    {

        public QueryToDTOHandler(IClient client, string defaultAddressFrom) : base(client, defaultAddressFrom)
        {
              
        }

        public QueryToDTOHandler(EthApiContractService eth) : base(eth)
        {

        }

        protected override TFunctionOutput DecodeOutput(string output)
        {
            return QueryRawHandler.FunctionMessageEncodingService.DecodeDTOTypeOutput<TFunctionOutput>(output);
        }
    }
#endif
}