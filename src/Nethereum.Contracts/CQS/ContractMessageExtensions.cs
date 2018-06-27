using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts.CQS
{
    public static class ContractMessageExtensions
    {
        public static byte[] GetCallData<TContractMessage>(this TContractMessage contractMessage) where TContractMessage: ContractMessage
        {
            var contractBuilder = new ContractBuilder(typeof(TContractMessage), null);
            return contractBuilder.GetFunctionBuilder<TContractMessage>().GetDataAsBytes(contractMessage);
        }

        public static CallInput CreateCallInput<TContractMessage>(this TContractMessage contractMessage, string contractAddress)
            where TContractMessage : ContractMessage
        {
            var contractBuilder = new ContractBuilder(typeof(TContractMessage), contractAddress);
            return contractBuilder.GetFunctionBuilder<TContractMessage>().CreateCallInput(contractMessage,
                contractMessage.FromAddress, contractMessage.GetHexMaximumGas(), contractMessage.GetHexValue());
        }

        public static TransactionInput CreateTransactionInput<TContractMessage>(this TContractMessage contractMessage, string contractAddress)
            where TContractMessage : ContractMessage
        {
            var contractBuilder = new ContractBuilder(typeof(TContractMessage), contractAddress);
            var transactionInput = contractBuilder.GetFunctionBuilder<TContractMessage>().CreateTransactionInput(contractMessage,
                contractMessage.FromAddress, 
                contractMessage.GetHexMaximumGas(), 
                contractMessage.GetHexGasPrice(),
                contractMessage.GetHexValue());
            transactionInput.Nonce = contractMessage.GetHexNonce();
            return transactionInput;
        }

        public static TReturn DecodeSimpleTypeOutput<TContractMessage, TReturn>(this TContractMessage functionMessage, string output)
            where TContractMessage : ContractMessage
        {
            var contractBuilder = new ContractBuilder(typeof(TContractMessage), null);
            var functionBuilder = contractBuilder.GetFunctionBuilder<TContractMessage>();
            return functionBuilder.DecodeSimpleTypeOutput<TReturn>(output);
        }

        public static TReturn DecodeDTOTypeOutput<TContractMessage, TReturn>(this TContractMessage functionMessage, string output) where TReturn : new()
            where TContractMessage : ContractMessage
        {
            var contractBuilder = new ContractBuilder(typeof(TContractMessage), null);
            var functionBuilder = contractBuilder.GetFunctionBuilder<TContractMessage>();
            return functionBuilder.DecodeDTOTypeOutput<TReturn>(output);
        }
    }
}