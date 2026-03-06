using System.Numerics;
using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.AppChain.Anchoring.Hub
{
    public interface IHubRegistrationService
    {
        Task<TransactionReceipt> RegisterAppChainAsync(ulong chainId, string sequencer, byte[] sequencerSignature, BigInteger fee);
        Task<TransactionReceipt> UpdateMetadataAsync(ulong chainId, string name, string description, string url);
        Task<TransactionReceipt> SetSequencerAsync(ulong chainId, string newSequencer);
        Task<TransactionReceipt> SetAuthorizedSenderAsync(ulong chainId, string sender, bool authorized);
        Task<TransactionReceipt> SetVerifierAsync(ulong chainId, string verifierAddress);
        Task<TransactionReceipt> TransferOwnershipAsync(ulong chainId, string newOwner);
        Task<HubInfo?> GetAppChainInfoAsync(ulong chainId);
        Task<bool> IsAuthorizedSenderAsync(ulong chainId, string sender);
        Task<TransactionReceipt> WithdrawFeesAsync(ulong chainId);
    }
}
