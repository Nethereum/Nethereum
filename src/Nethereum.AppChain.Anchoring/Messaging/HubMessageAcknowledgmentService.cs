using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.AppChain.Anchoring.Hub.Contracts.AppChainHub.AppChainHub;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3.Accounts;

namespace Nethereum.AppChain.Anchoring.Messaging
{
    public class HubMessageAcknowledgmentService : IMessageAcknowledgmentService
    {
        private readonly ulong _appChainId;
        private readonly AppChainHubService? _hubService;
        private readonly ILogger<HubMessageAcknowledgmentService>? _logger;

        public HubMessageAcknowledgmentService(
            ulong appChainId,
            string hubRpcUrl,
            string hubContractAddress,
            string sequencerPrivateKey,
            ulong hubChainId = 1,
            ILogger<HubMessageAcknowledgmentService>? logger = null)
        {
            _appChainId = appChainId;
            _logger = logger;

            if (!string.IsNullOrEmpty(hubRpcUrl) &&
                !string.IsNullOrEmpty(hubContractAddress) &&
                !string.IsNullOrEmpty(sequencerPrivateKey))
            {
                var account = new Account(sequencerPrivateKey, hubChainId);
                var web3 = new Web3.Web3(account, hubRpcUrl);
                _hubService = new AppChainHubService(web3, hubContractAddress);
            }
        }

        public HubMessageAcknowledgmentService(
            ulong appChainId,
            AppChainHubService hubService,
            ILogger<HubMessageAcknowledgmentService>? logger = null)
        {
            _appChainId = appChainId;
            _hubService = hubService ?? throw new ArgumentNullException(nameof(hubService));
            _logger = logger;
        }

        public async Task<bool> AcknowledgeMessagesAsync(
            ulong sourceChainId,
            ulong processedUpToMessageId,
            byte[] merkleRoot)
        {
            if (_hubService == null)
            {
                _logger?.LogWarning("Hub acknowledgment service not configured, skipping");
                return false;
            }

            try
            {
                _logger?.LogInformation(
                    "Acknowledging messages for chain {AppChainId} from source {SourceChainId}: processedUpTo={MessageId}",
                    _appChainId, sourceChainId, processedUpToMessageId);

                var receipt = await _hubService.AcknowledgeMessagesRequestAndWaitForReceiptAsync(
                    _appChainId,
                    processedUpToMessageId,
                    merkleRoot);

                if (receipt.Succeeded())
                {
                    _logger?.LogInformation(
                        "Messages acknowledged for chain {AppChainId} from source {SourceChainId}: processedUpTo={MessageId} tx={TxHash}",
                        _appChainId, sourceChainId, processedUpToMessageId, receipt.TransactionHash);
                    return true;
                }

                _logger?.LogError(
                    "Acknowledgment transaction failed for chain {AppChainId} from source {SourceChainId}",
                    _appChainId, sourceChainId);
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex,
                    "Failed to acknowledge messages for chain {AppChainId} from source {SourceChainId}",
                    _appChainId, sourceChainId);
                return false;
            }
        }
    }
}
