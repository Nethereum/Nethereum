using Nethereum.Contracts.MessageEncodingServices;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts.Extensions
{
    public static class FunctionMessageExtensions
    {
        public static FunctionMessageEncodingService<TContractMessage> GetEncodingService<TContractMessage>(this TContractMessage contractMessage, string contractAddress=null, string defaultAddressFrom = null) where TContractMessage: FunctionMessage
        {
            return new FunctionMessageEncodingService<TContractMessage>(contractAddress, defaultAddressFrom);
        }

        public static CallInput CreateCallInput<TContractMessage>(this TContractMessage contractMessage,
            string contractAddress) where TContractMessage : FunctionMessage
        {
            return GetEncodingService<TContractMessage>(contractMessage, contractAddress).CreateCallInput(contractMessage);
        }

        public static TransactionInput CreateTransactionInput<TContractMessage>(this TContractMessage contractMessage,
            string contractAddress) where TContractMessage : FunctionMessage
        {
            return GetEncodingService<TContractMessage>(contractMessage, contractAddress).CreateTransactionInput(contractMessage);
        }

        public static TContractMessage DecodeInput<TContractMessage>(this TContractMessage contractMessage,
            string data) where TContractMessage : FunctionMessage
        {     
            return GetEncodingService<TContractMessage>(contractMessage, data).DecodeInput(contractMessage, data);
        }

        public static byte[] GetCallData<TContractMessage>(this TContractMessage contractMessage
            ) where TContractMessage : FunctionMessage
        {
            return GetEncodingService<TContractMessage>(contractMessage).GetCallData(contractMessage);
        }
    }
}