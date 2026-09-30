using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage
{
    public interface ISnapFlatStateWriter
    {
        Task<Account> GetAccountByHashAsync(byte[] accountHash);

        Task SaveAccountByHashAsync(byte[] accountHash, Account account);

        Task DeleteAccountByHashAsync(byte[] accountHash);

        Task SaveStorageByHashAsync(byte[] accountHash, byte[] slotKeccak, byte[] value);
    }
}
