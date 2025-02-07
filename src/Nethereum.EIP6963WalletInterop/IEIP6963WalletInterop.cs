using System.Threading.Tasks;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.EIP6963WalletInterop
{
#if NETCOREAPP3_1_OR_GREATER
    public interface IEIP6963WalletInterop
    {
        ValueTask<string> EnableEthereumAsync();
        ValueTask<bool> CheckAvailabilityAsync();
        ValueTask<string> GetSelectedAddress();
        ValueTask<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage);
        ValueTask<RpcResponseMessage> SendTransactionAsync(EIP6963RpcRequestMessage rpcRequestMessage);
        ValueTask<string> SignAsync(string utf8Hex);
        ValueTask<EIP6963WalletInfo[]> GetAvailableWalletsAsync();
        ValueTask SelectWalletAsync(string walletId);
        ValueTask<string> GetWalletIconAsync(string walletId);
    }
#else
    public interface IEIP6963WalletInterop
    {
        Task<string> EnableEthereumAsync();
        Task<bool> CheckAvailabilityAsync();
        Task<string> GetSelectedAddress();
        Task<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage);
        Task<RpcResponseMessage> SendTransactionAsync(EIP6963RpcRequestMessage rpcRequestMessage);
        Task<string> SignAsync(string utf8Hex);

        // Extra methods for extended functionality
        Task<EIP6963WalletInfo[]> GetAvailableWalletsAsync();
        Task SelectWalletAsync(string walletId);
        Task<string> GetWalletIconAsync(string walletId);
    }
#endif


}