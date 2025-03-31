using Nethereum.RPC.Eth.DTOs;
using System.Threading.Tasks;

namespace Nethereum.RPC.TransactionManagers
{
    public interface IEIP7022AuthorisationService
    {
        Task Add7022AuthorisationDelegationOnNextRequestAsync(string addressContract, bool useUniversalZeroChainId = false);
        Task<TransactionReceipt> AuthoriseRequestAndWaitForReceiptAsync(string addressContract, bool useUniversalZeroChainId = false);
        Task<string> AuthoriseRequestAsync(string addressContract, bool useUniversalZeroChainId = false);
        Task<string> GetDelegatedAccountAddressAsync(string address);
        Task<bool> IsDelegatedAccountAsync(string address);
        void Remove7022AuthorisationDelegationOnNextRequest();
        Task<TransactionReceipt> RemoveAuthorisationRequestAndWaitForReceiptAsync();
        Task<string> RemoveAuthorisationRequestAsync();
    }
}