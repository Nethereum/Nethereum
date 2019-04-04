using Nethereum.Contracts.MessageEncodingServices;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts.Extensions
{

}

namespace Nethereum.Contracts
{

    public static class DeploymentMessageExtensions
    {
        public static DeploymentMessageEncodingService<TContractMessage> GetEncodingService<TContractMessage>(this TContractMessage contractMessage) where TContractMessage : ContractDeploymentMessage
        {
            return new DeploymentMessageEncodingService<TContractMessage>();
        }

        public static CallInput CreateCallInput<TContractMessage>(this TContractMessage contractMessage) where TContractMessage : ContractDeploymentMessage
        {
            return GetEncodingService<TContractMessage>(contractMessage).CreateCallInput(contractMessage);
        }

        public static TransactionInput CreateTransactionInput<TContractMessage>(this TContractMessage contractMessage) where TContractMessage : ContractDeploymentMessage
        {
            return GetEncodingService<TContractMessage>(contractMessage).CreateTransactionInput(contractMessage);
        }

        public static TContractMessage DecodeTransaction<TContractMessage>(this TContractMessage contractMessage, Transaction transaction) where TContractMessage : ContractDeploymentMessage
        {
            return GetEncodingService<TContractMessage>(contractMessage).DecodeTransaction(contractMessage, transaction);
        }

        public static TContractMessage DecodeTransactionToDeploymentMessage<TContractMessage>(this Transaction transaction) where TContractMessage : ContractDeploymentMessage, new()
        {
            var contractMessage = new TContractMessage();
            return GetEncodingService<TContractMessage>(contractMessage).DecodeTransaction(contractMessage, transaction);
        }

        public static string GetSwarmAddressFromByteCode<TContractMessage>(this TContractMessage contractMessage) where TContractMessage : ContractDeploymentMessage
        {
            return GetEncodingService<TContractMessage>(contractMessage).GetSwarmAddressFromByteCode(contractMessage);
        }

        public static bool HasASwarmAddressTheByteCode<TContractMessage>(this TContractMessage contractMessage) where TContractMessage : ContractDeploymentMessage
        {
            return GetEncodingService<TContractMessage>(contractMessage).HasASwarmAddressTheByteCode(contractMessage);
        }

        public static void LinkLibraries<TContractMessage>(this TContractMessage contractMessage, params ByteCodeLibrary[] byteCodeLibraries) where TContractMessage : ContractDeploymentMessage, new()
        {
            var libraryLinker = new ByteCodeLibraryLinker();
            contractMessage.ByteCode = libraryLinker.LinkByteCode(contractMessage.ByteCode, byteCodeLibraries);
        }
    }
}