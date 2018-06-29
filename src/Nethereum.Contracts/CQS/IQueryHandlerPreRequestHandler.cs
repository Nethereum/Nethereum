using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts.CQS
{
    public interface IQueryHandlerPreRequestHandler<TContractMessage> where TContractMessage : ContractMessage
    {
        Task ExecuteAsync(TContractMessage contractMessage, string contractAddress, BlockParameter block);
    }
}