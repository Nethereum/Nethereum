using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts.CQS
{
    public class DeploymentMessageEncodingService<TContractDeployment> : IContractMessageTransactionInputCreator<TContractDeployment> where TContractDeployment: ContractDeploymentMessage
    {
        protected DeployContractTransactionBuilder DeployContractTransactionBuilder { get; set; }
        public string DefaultAddressFrom { get; set; }

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