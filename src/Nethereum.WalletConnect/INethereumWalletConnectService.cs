using Nethereum.RPC.Eth.DTOs;
using System.Threading.Tasks;
using WalletConnectSharp.Sign.Interfaces;
using Nethereum.RPC.HostWallet;

namespace Nethereum.WalletConnect
{
    public interface INethereumWalletConnectService
    {
        ISignClient WalletConnectClient { get; }

        Task<string> AddEthereumChainAsync(AddEthereumChainParameter addEthereumChainParameter);
        WalletConnectConnectedSession GetWalletConnectConnectedSession();
        Task<string> PersonalSignAsync(string hexUtf8);
        Task<string> SendTransactionAsync(TransactionInput transaction);
        Task<string> SignAsync(string hexUtf8);
        Task<string> SignTransactionAsync(TransactionInput transaction);
        Task<string> SignTypedDataAsync(string hexUtf8);
        Task<string> SignTypedDataV4Async(string hexUtf8);
        Task<string> SwitchEthereumChainAsync(SwitchEthereumChainParameter chainId);
    }
}