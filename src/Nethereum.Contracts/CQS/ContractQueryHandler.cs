using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts.CQS
{
#if !DOTNET35
    public class ContractQueryHandler<TContractMessage> : ContractHandlerBase<TContractMessage>
        where TContractMessage : ContractMessage
    {
        public async Task<TFunctionOutput> QueryDeserializingToObjectAsync<TFunctionOutput>(
            TContractMessage contractFunctionMessage, string contractAddress,
            BlockParameter block = null) where TFunctionOutput : new()

        {
            var contract = Eth.GetContract<TContractMessage>(contractAddress);
            var function = contract.GetFunction<TContractMessage>();
            ValidateContractMessage(contractFunctionMessage);
            return await function.CallDeserializingToObjectAsync<TFunctionOutput>(contractFunctionMessage,
                contractFunctionMessage.FromAddress,
                GetMaximumGas(contractFunctionMessage), GetValue(contractFunctionMessage), block).ConfigureAwait(false);
        }

        public async Task<TFunctionOutput> QueryAsync<TFunctionOutput>(TContractMessage contractFunctionMessage,
            string contractAddress,
            BlockParameter block = null)

        {
            var contract = Eth.GetContract<TContractMessage>(contractAddress);
            var function = contract.GetFunction<TContractMessage>();
            ValidateContractMessage(contractFunctionMessage);
            return await function.CallAsync<TFunctionOutput>(contractFunctionMessage,
                contractFunctionMessage.FromAddress,
                GetMaximumGas(contractFunctionMessage), GetValue(contractFunctionMessage), block).ConfigureAwait(false);
        }
    }


#endif
}