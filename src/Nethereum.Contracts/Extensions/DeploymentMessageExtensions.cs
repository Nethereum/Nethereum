using Nethereum.ABI.FunctionEncoding;
using Nethereum.Contracts.MessageEncodingServices;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;

namespace Nethereum.Contracts.Extensions
{
    /// <summary>
    /// Please use Nethereum.Contracts.DeploymentMessageExtensions instead (keeping this class as a small workaround to move extensions to contracts namespaces)
    /// </summary>
    public static class DeploymentMessageExtensions
    {
    }
}

namespace Nethereum.Contracts
{

    public static class DeploymentMessageExtensions
    {
        public static DeploymentMessageEncodingService<TContractMessage> GetEncodingService<TContractMessage>(this TContractMessage contractMessage) where TContractMessage : ContractDeploymentMessage, new()
        {
            return new DeploymentMessageEncodingService<TContractMessage>();
        }

        public static CallInput CreateCallInput<TContractMessage>(this TContractMessage contractMessage) where TContractMessage : ContractDeploymentMessage, new()
        {
            return GetEncodingService<TContractMessage>(contractMessage).CreateCallInput(contractMessage);
        }

        public static TransactionInput CreateTransactionInput<TContractMessage>(this TContractMessage contractMessage) where TContractMessage : ContractDeploymentMessage, new()
        {
            return GetEncodingService<TContractMessage>(contractMessage).CreateTransactionInput(contractMessage);
        }

        public static TContractMessage DecodeTransaction<TContractMessage>(this TContractMessage contractMessage, Transaction transaction) where TContractMessage : ContractDeploymentMessage, new()
        {
            return GetEncodingService<TContractMessage>(contractMessage).DecodeTransaction(contractMessage, transaction);
        }

        public static TContractMessage DecodeTransactionToDeploymentMessage<TContractMessage>(this Transaction transaction) where TContractMessage : ContractDeploymentMessage, new()
        {
            var contractMessage = new TContractMessage();
            return GetEncodingService<TContractMessage>(contractMessage).DecodeTransaction(contractMessage, transaction);
        }

        public static string GetSwarmAddressFromByteCode<TContractMessage>(this TContractMessage contractMessage) where TContractMessage : ContractDeploymentMessage, new()
        {
            return GetEncodingService<TContractMessage>(contractMessage).GetSwarmAddressFromByteCode(contractMessage);
        }

        public static bool HasASwarmAddressTheByteCode<TContractMessage>(this TContractMessage contractMessage) where TContractMessage : ContractDeploymentMessage, new()
        {
            return GetEncodingService<TContractMessage>(contractMessage).HasASwarmAddressTheByteCode(contractMessage);
        }

        public static void LinkLibraries<TContractMessage>(this TContractMessage contractMessage, params ByteCodeLibrary[] byteCodeLibraries) where TContractMessage : ContractDeploymentMessage, new()
        {
            contractMessage.ByteCode = ByteCodeLibraryLinker.LinkByteCode(contractMessage.ByteCode, byteCodeLibraries);
        }

        public static string CalculateCreate2Address<TContractMessage>(this TContractMessage contractMessage, string deployerAddress, string salt) where TContractMessage : ContractDeploymentMessage, new()
        {
            var data = GetDeploymentData(contractMessage);
            return ContractUtils.CalculateCreate2Address(deployerAddress, salt, data.ToHex());
        }

        public static string CalculateCreate2Address<TContractMessage>(this TContractMessage contractMessage, string deployerAddress, string salt, params ByteCodeLibrary[] byteCodeLibraries) where TContractMessage : ContractDeploymentMessage, new()
        {
            LinkLibraries<TContractMessage>(contractMessage, byteCodeLibraries);
            var data = GetDeploymentData(contractMessage);
            return ContractUtils.CalculateCreate2Address(deployerAddress, salt, data.ToHex());
        }

        public static byte[] GetDeploymentData<TContractMessage>(this TContractMessage contractMessage
        ) where TContractMessage : ContractDeploymentMessage, new()
        {
            return GetEncodingService<TContractMessage>(contractMessage).GetDeploymentData(contractMessage).HexToByteArray();
        }

        public static byte[] GetDeploymentDataHash<TContractMessage>(this TContractMessage contractMessage)
            where TContractMessage : ContractDeploymentMessage, new()
        {
            return GetEncodingService<TContractMessage>(contractMessage).GetDeploymentDataHash(contractMessage);
        }
    }
}