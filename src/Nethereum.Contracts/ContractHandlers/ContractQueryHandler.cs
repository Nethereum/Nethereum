using Nethereum.Contracts.QueryHandlers;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Contracts.ContractHandlers
{

#if !DOTNET35

    public class ContractQueryHandler<TFunctionMessage>: ContractQueryHandlerBase<TFunctionMessage>
        where TFunctionMessage : FunctionMessage, new()
    {
        private readonly IClient _client;
        public string DefaultAddressFrom { get; set; }

        public ContractQueryHandler(IClient client, string defaultAddressFrom = null)
        {
            _client = client;
            DefaultAddressFrom = defaultAddressFrom;
        }

        protected override QueryToDTOHandler<TFunctionMessage, TFunctionOutput> GetQueryDTOHandler<TFunctionOutput>()
        {
            return new QueryToDTOHandler<TFunctionMessage, TFunctionOutput>(_client, DefaultAddressFrom);
        }

        protected override QueryToSimpleTypeHandler<TFunctionMessage, TFunctionOutput> GetQueryToSimpleTypeHandler<TFunctionOutput>()
        {
            return new QueryToSimpleTypeHandler<TFunctionMessage, TFunctionOutput>(_client, DefaultAddressFrom);
        }

        protected override QueryRawHandler<TFunctionMessage> GetQueryRawHandler()
        {
            return new QueryRawHandler<TFunctionMessage>(_client, DefaultAddressFrom);
        }
    }
#endif
}