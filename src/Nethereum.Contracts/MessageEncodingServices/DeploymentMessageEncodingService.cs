using Nethereum.Contracts.Extensions;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts.MessageEncodingServices
{
    public class DeploymentMessageEncodingService<TContractDeployment> : 
        IContractMessageTransactionInputCreator<TContractDeployment> where TContractDeployment: ContractDeploymentMessage
    {
        protected DeployContractTransactionBuilder DeployContractTransactionBuilder { get; set; }
        public string DefaultAddressFrom { get; set; }

        public DeploymentMessageEncodingService(string defaultAddressFrom = null)
        {
            DeployContractTransactionBuilder = new DeployContractTransactionBuilder();
            DefaultAddressFrom = defaultAddressFrom;
        }

        public TransactionInput CreateTransactionInput(TContractDeployment contractMessage)
        {
            var transactionInput = DeployContractTransactionBuilder.BuildTransaction<TContractDeployment>(
                contractMessage.ByteCode,
                contractMessage.SetDefaultFromAddressIfNotSet(DefaultAddressFrom),             
                contractMessage.GetHexMaximumGas(),
                contractMessage.GetHexGasPrice(),
                contractMessage.GetHexValue(),
                contractMessage.GetHexNonce(),
                contractMessage);
            return transactionInput;
        }


        public CallInput CreateCallInput(TContractDeployment contractMessage)
        {
            var transactionInput = DeployContractTransactionBuilder.BuildTransaction<TContractDeployment>(
                contractMessage.ByteCode,
                contractMessage.SetDefaultFromAddressIfNotSet(DefaultAddressFrom),
                contractMessage.GetHexMaximumGas(),
                contractMessage.GetHexGasPrice(),
                contractMessage.GetHexValue(),
                contractMessage);
            return transactionInput;
        }
    }
}