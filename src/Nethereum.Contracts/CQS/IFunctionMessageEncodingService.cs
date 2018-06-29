using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts.CQS
{
    public interface IFunctionMessageEncodingService<TContractFunction> where TContractFunction : ContractMessage
    {
        string ContractAddress { get; }
        string DefaultAddressFrom { get; set; }

        CallInput CreateCallInput(TContractFunction contractMessage);
        TransactionInput CreateTransactionInput(TContractFunction contractMessage);
        TReturn DecodeDTOTypeOutput<TReturn>(string output) where TReturn : new();
        TContractFunction DecodeInput(TContractFunction function, string data);
        TReturn DecodeSimpleTypeOutput<TReturn>(string output);
        byte[] GetCallData(TContractFunction contractMessage);
        void SetContractAddress(string address);
    }
}