using System.Threading.Tasks;

namespace Nethereum.Siwe.Authentication
{
    public interface IAccessTokenService
    {
        Task<string> GetAccessTokenAsync();

        Task SetAccessTokenAsync(string tokenValue);

        Task RemoveAccessTokenAsync();
    }
}
