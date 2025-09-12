using Nethereum.RPC.Eth.DTOs;
using System.Threading.Tasks;

namespace Nethereum.Wallet.UI
{
    public interface ITransactionPromptService
    {
        Task<string?> PromptTransactionAsync(TransactionInput input);
    }

}
