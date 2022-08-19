using Nethereum.RPC.AccountSigning;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Services;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.RPC.HostWallet;
using Nethereum.RPC.TransactionManagers;

namespace Nethereum.RPC
{
    public interface IEthApiService
    {
        IEthChainId ChainId { get; }
        IEthAccounts Accounts { get; }
        IEthApiBlockService Blocks { get; }
        IEthCoinBase CoinBase { get; }
        IEthApiCompilerService Compile { get; }
        BlockParameter DefaultBlock { get; set; }
        IEthApiFilterService Filters { get; }
        IEthGasPrice GasPrice { get; }
        IEthGetBalance GetBalance { get; }
        IEthGetCode GetCode { get; }
        IEthGetStorageAt GetStorageAt { get; }
        IEthApiMiningService Mining { get; }
        IEthProtocolVersion ProtocolVersion { get; }
        IEthSign Sign { get; }
        IEthSyncing Syncing { get; }
        ITransactionManager TransactionManager { get; set; }
        IEthApiTransactionsService Transactions { get; }
        IEthApiUncleService Uncles { get; }
        IEthFeeHistory FeeHistory { get; }
        IAccountSigningService AccountSigning { get; }
        IHostWalletService HostWallet { get; }
        IEthGetProof GetProof { get; }
        
#if !DOTNET35
            IEtherTransferService GetEtherTransferService();
#endif
    }
}