using Nethereum.Contracts.MessageEncodingServices;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;

namespace Nethereum.Contracts.QueryHandlers
{
    public abstract class QueryHandlerBase<TFunctionMessage> 
        where TFunctionMessage : FunctionMessage, new()
    {
        protected IEthCall EthCall { get; set; }
        public string DefaultAddressFrom { get; set; }
        protected BlockParameter DefaultBlockParameter { get; set; }
        public FunctionMessageEncodingService<TFunctionMessage> FunctionMessageEncodingService { get; } = new FunctionMessageEncodingService<TFunctionMessage>();

        protected QueryHandlerBase(IEthCall ethCall, string defaultAddressFrom = null, BlockParameter defaultBlockParameter = null)
        {
            EthCall = ethCall;
            DefaultAddressFrom = defaultAddressFrom;
            DefaultBlockParameter = defaultBlockParameter ?? BlockParameter.CreateLatest();
        }

        protected QueryHandlerBase(IClient client, string defaultAddressFrom = null, BlockParameter defaultBlockParameter = null):this(new EthCall(client), defaultAddressFrom, defaultBlockParameter)
        {
            
        }

        public virtual string GetAccountAddressFrom()
        {
            return DefaultAddressFrom;
        }

        protected void EnsureInitialiseAddress()
        {
            if (FunctionMessageEncodingService != null)
            {
                FunctionMessageEncodingService.DefaultAddressFrom = GetAccountAddressFrom();
            }
        }
    }
}