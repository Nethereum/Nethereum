using Nethereum.Contracts.QueryHandlers;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts.ContractHandlers
{

#if !DOTNET35

    public class ContractQueryHandler<TFunctionMessage>: ContractQueryHandlerBase<TFunctionMessage>
        where TFunctionMessage : FunctionMessage, new()
    {
        private readonly IClient _client;
        private readonly BlockParameter _defaultBlockParameter;
        public string DefaultAddressFrom { get; set; }

        public ContractQueryHandler(IClient client, string defaultAddressFrom = null, BlockParameter defaultBlockParameter = null)
        {
            _client = client;
            _defaultBlockParameter = defaultBlockParameter;
            DefaultAddressFrom = defaultAddressFrom;
        }

        protected override QueryToDTOHandler<TFunctionMessage, TFunctionOutput> GetQueryDTOHandler<TFunctionOutput>()
        {
            return new QueryToDTOHandler<TFunctionMessage, TFunctionOutput>(_client, DefaultAddressFrom, _defaultBlockParameter);
        }

        protected override QueryToSimpleTypeHandler<TFunctionMessage, TFunctionOutput> GetQueryToSimpleTypeHandler<TFunctionOutput>()
        {
            return new QueryToSimpleTypeHandler<TFunctionMessage, TFunctionOutput>(_client, DefaultAddressFrom, _defaultBlockParameter);
        }

        protected override QueryRawHandler<TFunctionMessage> GetQueryRawHandler()
        {
            return new QueryRawHandler<TFunctionMessage>(_client, DefaultAddressFrom, _defaultBlockParameter);
        }
    }
#endif
}