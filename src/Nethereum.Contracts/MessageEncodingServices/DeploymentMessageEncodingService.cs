using Nethereum.ABI.FunctionEncoding;
using Nethereum.Contracts.Extensions;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts.MessageEncodingServices
{
    public class DeploymentMessageEncodingService<TContractDeployment> : 
        IContractMessageTransactionInputCreator<TContractDeployment> where TContractDeployment: ContractDeploymentMessage
    {
        protected DeployContractTransactionBuilder DeployContractTransactionBuilder { get; set; }
        protected ConstructorCallDecoder ConstructorCallDecoder { get; set; }
        protected ByteCodeSwarmExtractor ByteCodeSwarmExtractor { get; set; }
        public string DefaultAddressFrom { get; set; }

        public DeploymentMessageEncodingService(string defaultAddressFrom = null)
        {
            DeployContractTransactionBuilder = new DeployContractTransactionBuilder();
            ConstructorCallDecoder = new ConstructorCallDecoder();
            ByteCodeSwarmExtractor = new ByteCodeSwarmExtractor();
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

        public TContractDeployment DecodeInput(TContractDeployment contractMessage, string data)
        {
            if (ByteCodeSwarmExtractor.HasSwarmAddress(data))
            {
                return ConstructorCallDecoder.DecodeConstructorParameters<TContractDeployment>(contractMessage, data);
            }
            else // fallback to "our" bytecode.. 
            {
                return ConstructorCallDecoder.DecodeConstructorParameters<TContractDeployment>(contractMessage,
                    contractMessage.ByteCode, data);
            }
        }

        public TContractDeployment DecodeTransaction(TContractDeployment contractMessageOuput, Transaction transactionInput)
        {
            contractMessageOuput = DecodeInput(contractMessageOuput, transactionInput.Input);
            contractMessageOuput.Nonce = transactionInput.Nonce?.Value;
            contractMessageOuput.GasPrice = transactionInput.GasPrice?.Value;
            contractMessageOuput.AmountToSend = transactionInput.Value == null ? 0 : transactionInput.Value.Value;
            contractMessageOuput.Gas = transactionInput.Gas?.Value;
            contractMessageOuput.FromAddress = transactionInput.From;
            contractMessageOuput = DecodeInput(contractMessageOuput, transactionInput.Input);
            return contractMessageOuput;
        }

        public string GetSwarmAddressFromByteCode(TContractDeployment contractMessageOuput)
        {
            return ByteCodeSwarmExtractor.GetSwarmAddress(contractMessageOuput.ByteCode);
        }

        public bool HasASwarmAddressTheByteCode(TContractDeployment contractMessageOuput)
        {
            return ByteCodeSwarmExtractor.HasSwarmAddress(contractMessageOuput.ByteCode);
        }
    }
}