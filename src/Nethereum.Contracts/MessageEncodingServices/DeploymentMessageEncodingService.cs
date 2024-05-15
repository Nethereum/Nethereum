using Nethereum.ABI.FunctionEncoding;
using Nethereum.Contracts.Extensions;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts.MessageEncodingServices
{
    public class DeploymentMessageEncodingService<TContractDeployment> : 
        IContractMessageTransactionInputCreator<TContractDeployment> where TContractDeployment: ContractDeploymentMessage, new()
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

            transactionInput.Type = contractMessage.GetHexTransactionType();
            transactionInput.MaxFeePerGas = contractMessage.GetHexMaxFeePerGas();
            transactionInput.MaxPriorityFeePerGas = contractMessage.GetMaxPriorityFeePerGas();
            transactionInput.AccessList = contractMessage.AccessList;
            transactionInput.Nonce = contractMessage.GetHexNonce();

            return transactionInput;
        }

        public string GetDeploymentData(TContractDeployment contractMessage, params ByteCodeLibrary[] byteCodeLibraries)
        {
            contractMessage.LinkLibraries(byteCodeLibraries);
            return DeployContractTransactionBuilder.GetData(contractMessage.ByteCode, contractMessage);
        }

        public string GetDeploymentData(TContractDeployment contractMessage)
        {
            return DeployContractTransactionBuilder.GetData(contractMessage.ByteCode, contractMessage);
        }

        public byte[] GetDeploymentDataHash(TContractDeployment contractMessage)
        {
            return Util.Sha3Keccack.Current.CalculateHash(GetDeploymentData(contractMessage).HexToByteArray());
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

            transactionInput.Type = contractMessage.GetHexTransactionType();
            transactionInput.MaxFeePerGas = contractMessage.GetHexMaxFeePerGas();
            transactionInput.MaxPriorityFeePerGas = contractMessage.GetMaxPriorityFeePerGas();
            transactionInput.AccessList = contractMessage.AccessList;
            transactionInput.Nonce = contractMessage.GetHexNonce();

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

        public TContractDeployment DecodeTransaction(TContractDeployment contractMessageOutput, Transaction transactionInput)
        {
            contractMessageOutput = DecodeInput(contractMessageOutput, transactionInput.Input);
            contractMessageOutput.Nonce = transactionInput.Nonce?.Value;
            contractMessageOutput.GasPrice = transactionInput.GasPrice?.Value;
            contractMessageOutput.AmountToSend = transactionInput.Value == null ? 0 : transactionInput.Value.Value;
            contractMessageOutput.Gas = transactionInput.Gas?.Value;
            contractMessageOutput.FromAddress = transactionInput.From;
            contractMessageOutput = DecodeInput(contractMessageOutput, transactionInput.Input);

            contractMessageOutput.FromAddress = transactionInput.From;
            contractMessageOutput.MaxFeePerGas = transactionInput.MaxFeePerGas?.Value;
            contractMessageOutput.MaxPriorityFeePerGas = transactionInput.MaxPriorityFeePerGas?.Value;

            if (transactionInput.Type == null)
            {
                contractMessageOutput.TransactionType = null;
            }
            else
            {
                contractMessageOutput.TransactionType = (byte)(transactionInput.Type.Value);
            }

            contractMessageOutput.AccessList = transactionInput.AccessList;

            return contractMessageOutput;
        }

        public string GetSwarmAddressFromByteCode(TContractDeployment contractMessageOutput)
        {
            return ByteCodeSwarmExtractor.GetSwarmAddress(contractMessageOutput.ByteCode);
        }

        public bool HasASwarmAddressTheByteCode(TContractDeployment contractMessageOutput)
        {
            return ByteCodeSwarmExtractor.HasSwarmAddress(contractMessageOutput.ByteCode);
        }
    }
}