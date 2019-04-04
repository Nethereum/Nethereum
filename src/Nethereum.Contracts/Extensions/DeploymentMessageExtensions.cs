using Nethereum.Contracts.MessageEncodingServices;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts.Extensions
{
    /// <summary>
    /// Please use Nethereum.Contracts.DeploymentMessageExtensions instead
    /// </summary>
    public static class DeploymentMessageExtensions
    {
    //    public static DeploymentMessageEncodingService<TContractMessage> GetEncodingService<TContractMessage>(this TContractMessage contractMessage) where TContractMessage : ContractDeploymentMessage
    //    {
    //        return Nethereum.Contracts.DeploymentMessageExtensions.GetEncodingService<TContractMessage>(contractMessage);
    //    }

    //    public static CallInput CreateCallInput<TContractMessage>(this TContractMessage contractMessage) where TContractMessage : ContractDeploymentMessage
    //    {
    //        return Nethereum.Contracts.DeploymentMessageExtensions.CreateCallInput(contractMessage);
    //    }

    //    public static TransactionInput CreateTransactionInput<TContractMessage>(this TContractMessage contractMessage) where TContractMessage : ContractDeploymentMessage
    //    {
    //        return Nethereum.Contracts.DeploymentMessageExtensions.CreateTransactionInput(contractMessage);
    //    }

    //    public static TContractMessage DecodeTransaction<TContractMessage>(this TContractMessage contractMessage, Transaction transaction) where TContractMessage : ContractDeploymentMessage
    //    {
    //        return Nethereum.Contracts.DeploymentMessageExtensions.DecodeTransaction(contractMessage, transaction);
    //    }

    //    public static TContractMessage DecodeTransactionToDeploymentMessage<TContractMessage>(this Transaction transaction) where TContractMessage : ContractDeploymentMessage, new()
    //    {
    //        return Nethereum.Contracts.DeploymentMessageExtensions.DecodeTransactionToDeploymentMessage<TContractMessage>(transaction);
    //    }

    //    public static string GetSwarmAddressFromByteCode<TContractMessage>(this TContractMessage contractMessage) where TContractMessage : ContractDeploymentMessage
    //    {
    //        return Nethereum.Contracts.DeploymentMessageExtensions.GetSwarmAddressFromByteCode(contractMessage);
    //    }

    //    public static bool HasASwarmAddressTheByteCode<TContractMessage>(this TContractMessage contractMessage) where TContractMessage : ContractDeploymentMessage
    //    {
    //        return Nethereum.Contracts.DeploymentMessageExtensions.HasASwarmAddressTheByteCode(contractMessage);
    //    }

    //    public static void LinkLibraries<TContractMessage>(this TContractMessage contractMessage, params ByteCodeLibrary[] byteCodeLibraries) where TContractMessage : ContractDeploymentMessage, new()
    //    {
    //        Nethereum.Contracts.DeploymentMessageExtensions.LinkLibraries(contractMessage, byteCodeLibraries);
    //    }
    }
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