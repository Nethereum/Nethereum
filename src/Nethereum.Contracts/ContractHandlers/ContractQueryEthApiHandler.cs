using Nethereum.Contracts.QueryHandlers;
using Nethereum.Contracts.Services;

namespace Nethereum.Contracts.ContractHandlers
{

#if !DOTNET35
    public class ContractQueryEthApiHandler<TFunctionMessage> : ContractQueryHandlerBase<TFunctionMessage>
       where TFunctionMessage : FunctionMessage, new()
    {
        public EthApiContractService EthApi { get; }

        public ContractQueryEthApiHandler(EthApiContractService ethApi)
        {
            EthApi = ethApi;
        }

        protected override QueryToDTOHandler<TFunctionMessage, TFunctionOutput> GetQueryDTOHandler<TFunctionOutput>()
        {
            return new QueryToDTOHandler<TFunctionMessage, TFunctionOutput>(EthApi);
        }

        protected override QueryToSimpleTypeHandler<TFunctionMessage, TFunctionOutput> GetQueryToSimpleTypeHandler<TFunctionOutput>()
        {
            return new QueryToSimpleTypeHandler<TFunctionMessage, TFunctionOutput>(EthApi);
        }

        protected override QueryRawHandler<TFunctionMessage> GetQueryRawHandler()
        {
            return new QueryRawHandler<TFunctionMessage>(EthApi);
        }
    }
#endif
}