using Nethereum.Contracts.QueryHandlers;
using Nethereum.Contracts.Services;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;

namespace Nethereum.Contracts.ContractHandlers
{

#if !DOTNET35
    public class ContractQueryEthCallHandler<TFunctionMessage> : ContractQueryHandlerBase<TFunctionMessage>
       where TFunctionMessage : FunctionMessage, new()
    {
        public string DefaultAddressFrom { get; set; }
        public BlockParameter DefaultBlockParameter { get; set; }

        private EthCall EthCall { get; }

        public ContractQueryEthCallHandler(EthCall ethCall, string defaultAddressFrom = null, BlockParameter defaultBlockParameter = null)
        {
            DefaultAddressFrom = defaultAddressFrom;
            DefaultBlockParameter = defaultBlockParameter;
            EthCall = ethCall;
        }

        protected override QueryToDTOHandler<TFunctionMessage, TFunctionOutput> GetQueryDTOHandler<TFunctionOutput>()
        {
            return new QueryToDTOHandler<TFunctionMessage, TFunctionOutput>(EthCall, DefaultAddressFrom, DefaultBlockParameter);
        }

        protected override QueryToSimpleTypeHandler<TFunctionMessage, TFunctionOutput> GetQueryToSimpleTypeHandler<TFunctionOutput>()
        {
            return new QueryToSimpleTypeHandler<TFunctionMessage, TFunctionOutput>(EthCall, DefaultAddressFrom, DefaultBlockParameter);
        }

        protected override QueryRawHandler<TFunctionMessage> GetQueryRawHandler()
        {
            return new QueryRawHandler<TFunctionMessage>(EthCall, DefaultAddressFrom, DefaultBlockParameter);
        }
    }
#endif
}