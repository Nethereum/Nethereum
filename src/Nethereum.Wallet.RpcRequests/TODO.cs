using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Accounts;
using Nethereum.Wallet.UI;
using Nethereum.Web3;
using Nethereum.ABI.Util;
using System.Threading.Tasks;

namespace Nethereum.Wallet.RpcRequests
{

    public class WalletRegisterOnboardingHandler : RpcMethodHandlerBase
    {
        public override string MethodName => "wallet_registerOnboarding";
        public override Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, IWalletContext context) => Task.FromResult(MethodNotImplemented(request.Id));
    }
    
    public class WalletWatchAssetHandler : RpcMethodHandlerBase
    {
        public override string MethodName => "wallet_watchAsset";
        public override Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, IWalletContext context) => Task.FromResult(MethodNotImplemented(request.Id));
    }

    public class EthDecryptHandler : RpcMethodHandlerBase
    {
        public override string MethodName => "eth_decrypt";
        public override Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, IWalletContext context) => Task.FromResult(MethodNotImplemented(request.Id));
    }

    public class EthGetEncryptionPublicKeyHandler : RpcMethodHandlerBase
    {
        public override string MethodName => "eth_getEncryptionPublicKey";
        public override Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, IWalletContext context) => Task.FromResult(MethodNotImplemented(request.Id));
    }

    public class Web3ClientVersionHandler : RpcMethodHandlerBase
    {
        public override string MethodName => "web3_clientVersion";
        public override Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, IWalletContext context) => Task.FromResult(MethodNotImplemented(request.Id));
    }

    public class EthSubscribeHandler : RpcMethodHandlerBase
    {
        public override string MethodName => "eth_subscribe";
        public override Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, IWalletContext context) => Task.FromResult(MethodNotImplemented(request.Id));
    }

}
