using Nethereum.Contracts.MessageEncodingServices;
using Nethereum.Contracts.Services;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Contracts.QueryHandlers
{
    public abstract class QueryHandlerBase<TFunctionMessage> 
        where TFunctionMessage : FunctionMessage, new()
    {
        public EthApiContractService Eth { get; protected set; }
        public string DefaultAddressFrom { get; set; }
        public FunctionMessageEncodingService<TFunctionMessage> FunctionMessageEncodingService { get; } = new FunctionMessageEncodingService<TFunctionMessage>();

        public QueryHandlerBase(IClient client, string defaultAddressFrom)
        {
            DefaultAddressFrom = defaultAddressFrom;
            Eth = new EthApiContractService(client);
        }

        public QueryHandlerBase(EthApiContractService eth)
        {
            Eth = eth;
        }

        public virtual string GetAccountAddressFrom()
        {
           var address = Eth.TransactionManager?.Account?.Address;
           if (address == null)
           {
               return DefaultAddressFrom;
           }
           return address;
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