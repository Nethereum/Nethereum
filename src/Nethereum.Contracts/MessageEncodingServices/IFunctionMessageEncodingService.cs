using Nethereum.Contracts.CQS;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts.MessageEncodingServices
{
    public interface IFunctionMessageEncodingService<TContractFunction>: IDefaultAddressService
        where TContractFunction : ContractMessageBase
    {
        string ContractAddress { get; }
        CallInput CreateCallInput(TContractFunction contractMessage);
        TransactionInput CreateTransactionInput(TContractFunction contractMessage);
        TReturn DecodeDTOTypeOutput<TReturn>(string output) where TReturn : new();
        TContractFunction DecodeInput(TContractFunction function, string data);
        TReturn DecodeSimpleTypeOutput<TReturn>(string output);
        byte[] GetCallData(TContractFunction contractMessage);
        void SetContractAddress(string address);
    }
}