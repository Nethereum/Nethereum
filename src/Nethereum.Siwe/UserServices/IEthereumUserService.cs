using System.Threading.Tasks;

namespace Nethereum.Siwe.UserServices;

public interface IEthereumUserService
{
    /// <summary>
    /// Method to check if the Ethereum address is registered or part of the system (ie.. address is stored in a db, smart contract, et)
    /// </summary>
    /// <param name="address"></param>
    Task<bool> IsUserAddressRegistered(string address);
}