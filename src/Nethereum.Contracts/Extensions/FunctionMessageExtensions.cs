using Nethereum.Contracts.MessageEncodingServices;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts
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
            return GetEncodingService<TContractMessage>(contractMessage).DecodeInput(contractMessage, data);
        }

        public static bool IsTransactionForFunctionMessage<TContractMessage>(this
            Transaction transaction) where TContractMessage : FunctionMessage, new()
        {
            var contractMessage = new TContractMessage();
            return GetEncodingService<TContractMessage>(contractMessage).IsTransactionForFunction(transaction);
        }

        public static TContractMessage DecodeTransactionToFunctionMessage<TContractMessage>(this
            Transaction transaction) where TContractMessage : FunctionMessage, new()
        {
            var contractMessage = new TContractMessage();
            return GetEncodingService<TContractMessage>(contractMessage).DecodeTransactionInput(contractMessage, transaction);
        }

        public static TContractMessage DecodeTransaction<TContractMessage>(this TContractMessage contractMessage,
            Transaction transaction) where TContractMessage : FunctionMessage
        {
            return GetEncodingService<TContractMessage>(contractMessage).DecodeTransactionInput(contractMessage, transaction);
        }

        public static byte[] GetCallData<TContractMessage>(this TContractMessage contractMessage
            ) where TContractMessage : FunctionMessage
        {
            return GetEncodingService<TContractMessage>(contractMessage).GetCallData(contractMessage);
        }

        public static byte[] GetCallDataHash<TContractMessage>(this TContractMessage contractMessage)
            where TContractMessage : FunctionMessage
        {
            return GetEncodingService<TContractMessage>(contractMessage).GetCallDataHash(contractMessage);
        }

        public static bool IsForFunction<TFunctionMessage>(this TransactionReceiptVO transactionWithReceipt) where TFunctionMessage : FunctionMessage, new()
        {
            return transactionWithReceipt.Transaction?.IsTransactionForFunctionMessage<TFunctionMessage>() ?? false;
        }

        public static TFunctionMessage Decode<TFunctionMessage>(this TransactionReceiptVO transactionWithReceipt) where TFunctionMessage : FunctionMessage, new()
        {
            return transactionWithReceipt.Transaction?.DecodeTransactionToFunctionMessage<TFunctionMessage>();
        }
    }
}