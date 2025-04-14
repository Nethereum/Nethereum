#if !LITE
using Nethereum.Accounts;
#endif

using Nethereum.BlockchainProcessing.Services;
using Nethereum.Contracts.Services;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC;
using Nethereum.RPC.DebugNode;
using Nethereum.RPC.TransactionManagers;
using Nethereum.RPC.TransactionReceipts;

namespace Nethereum.Web3
{
    public interface IWeb3
    {
        IClient Client { get; }
        IEthApiContractService Eth { get; }
        IBlockchainProcessingService Processing { get; }
        INetApiService Net { get; }
        IPersonalApiService Personal { get; }
        IShhApiService Shh { get; }
        ITransactionManager TransactionManager { get; set; }
        ITransactionReceiptService TransactionReceiptPolling { get; set; }
        IDebugApiService Debug { get; }
        FeeSuggestionService FeeSuggestion { get; }
#if !LITE
        EIP7022SponsorAuthorisationService GetEIP7022SponsorAuthorisation();
#endif 
    }
}