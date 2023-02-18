namespace Nethereum.RPC.HostWallet
{
    public interface IHostWalletService
    {
        IWalletAddEthereumChain AddEthereumChain { get; }
        IWalletGetPermissions GetPermissions { get; }
        IEthRequestAccounts RequestAccounts { get; }
        IWalletRequestPermissions RequestPermissions { get; }
        IWalletSwitchEthereumChain SwitchEthereumChain { get; }
        IWalletWatchAsset WatchAsset { get; }
    }
}