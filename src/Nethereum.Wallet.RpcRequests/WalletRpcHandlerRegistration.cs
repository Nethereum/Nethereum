using Nethereum.Wallet.UI;
using Nethereum.Wallet.Hosting;

namespace Nethereum.Wallet.RpcRequests
{
    public static class WalletRpcHandlerRegistration
    {
        public static void RegisterAll(RpcHandlerRegistry registry)
        {
            registry.Register(new WalletAddEthereumChainHandler());
            registry.Register(new WalletSwitchEthereumChainHandler());
            registry.Register(new WalletGetPermissionsHandler());
            registry.Register(new WalletRequestPermissionsHandler());
            registry.Register(new WalletRevokePermissionsHandler());
            registry.Register(new WalletRegisterOnboardingHandler());
            registry.Register(new WalletWatchAssetHandler());
            registry.Register(new PersonalSignHandler());
            registry.Register(new EthSignTypedDataV4Handler());
            registry.Register(new EthRequestAccountsHandler());
            registry.Register(new EthAccountsHandler());
            registry.Register(new EthDecryptHandler());
            registry.Register(new EthGetEncryptionPublicKeyHandler());
            registry.Register(new Web3ClientVersionHandler());
            registry.Register(new EthSubscribeHandler());
            registry.Register(new EthSendTransactionHandler());
        }
    }

}
