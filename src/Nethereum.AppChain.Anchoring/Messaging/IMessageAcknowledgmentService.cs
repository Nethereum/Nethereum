using System.Threading.Tasks;

namespace Nethereum.AppChain.Anchoring.Messaging
{
    public interface IMessageAcknowledgmentService
    {
        Task<bool> AcknowledgeMessagesAsync(
            ulong sourceChainId,
            ulong processedUpToMessageId,
            byte[] merkleRoot);
    }
}
