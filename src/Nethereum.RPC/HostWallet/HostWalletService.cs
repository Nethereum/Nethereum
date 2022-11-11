using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC.HostWallet
{
    public class HostWalletService : RpcClientWrapper, IHostWalletService
    {
        public HostWalletService(IClient client) : base(client)
        {
            RequestAccounts = new EthRequestAccounts(client);
            GetPermissions = new WalletGetPermissions(client);
            RequestPermissions = new WalletRequestPermissions(client);
            WatchAsset = new WalletWatchAsset(client);
            AddEthereumChain = new WalletAddEthereumChain(client);
        }

        /// <summary>
        /// EIP-1102 https://eips.ethereum.org/EIPS/eip-1102
        /// Requests that the user provides an Ethereum address to be identified by.
        /// </summary>
        public IEthRequestAccounts RequestAccounts { get; private set; }
        /// <summary>
        /// Gets the caller's current permissions. 
        /// </summary>
        public IWalletGetPermissions GetPermissions { get; private set; }

        public IWalletRequestPermissions RequestPermissions { get; private set; }

        public IWalletWatchAsset WatchAsset { get; private set; }

        public IWalletAddEthereumChain AddEthereumChain { get; private set; }
    }
}
