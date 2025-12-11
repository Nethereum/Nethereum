using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.ChainStateVerification
{
    public interface IVerifiedStateBackend
    {
        Task<Account> GetAccountAsync(string address);
    }
}
