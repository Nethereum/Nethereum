using System.Threading.Tasks;

namespace Nethereum.Siwe.UserServices;

/// <summary>
/// Validates if a user is authorised by checking the balance of a ERC721 smart contract
/// </summary>
public class ERC721BalanceEthereumUserService : IEthereumUserService
{
    private readonly string _contractAddress;
    private readonly Web3.Web3 _web3;

    public ERC721BalanceEthereumUserService(string contractAddress, string rpcUrl)
    {
        _contractAddress = contractAddress;
        _web3 = new Web3.Web3(rpcUrl);
    }

    public ERC721BalanceEthereumUserService(string contractAddress, Web3.Web3 web3)
    {
        _contractAddress = contractAddress;
        _web3 = new Web3.Web3();
    }
    public async Task<bool> IsUserAddressRegistered(string address)
    {
        var balance = await _web3.Eth.ERC721.GetContractService(_contractAddress)
            .BalanceOfQueryAsync(address).ConfigureAwait(false);
        return balance > 0;
    }
}