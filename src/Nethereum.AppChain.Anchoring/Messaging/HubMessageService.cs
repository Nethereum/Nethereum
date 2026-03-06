using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.AppChain.Anchoring.Hub.Contracts.AppChainHub.AppChainHub;
using Nethereum.AppChain.Anchoring.Hub.Contracts.AppChainHub.AppChainHub.ContractDefinition;

namespace Nethereum.AppChain.Anchoring.Messaging
{
    public class HubMessageService : IHubMessageService
    {
        private readonly AppChainHubService _hubService;
        private readonly ulong _targetChainId;
        private readonly ILogger<HubMessageService>? _logger;

        public HubMessageService(string rpcUrl, string hubContractAddress, ulong targetChainId, ILogger<HubMessageService>? logger = null)
        {
            if (string.IsNullOrEmpty(rpcUrl)) throw new ArgumentException("rpcUrl is required", nameof(rpcUrl));
            if (string.IsNullOrEmpty(hubContractAddress)) throw new ArgumentException("hubContractAddress is required", nameof(hubContractAddress));

            _targetChainId = targetChainId;
            _logger = logger;
            var web3 = new Web3.Web3(rpcUrl);
            _hubService = new AppChainHubService(web3, hubContractAddress);
        }

        public HubMessageService(Nethereum.Web3.IWeb3 web3, string hubContractAddress, ulong targetChainId, ILogger<HubMessageService>? logger = null)
        {
            if (web3 == null) throw new ArgumentNullException(nameof(web3));
            if (string.IsNullOrEmpty(hubContractAddress)) throw new ArgumentException("hubContractAddress is required", nameof(hubContractAddress));

            _targetChainId = targetChainId;
            _logger = logger;
            _hubService = new AppChainHubService(web3, hubContractAddress);
        }

        public async Task<MessageInfo?> GetMessageAsync(ulong messageId)
        {
            try
            {
                var result = await _hubService.GetMessageQueryAsync(_targetChainId, messageId);
                if (result.Timestamp == 0) return null;

                return new MessageInfo
                {
                    MessageId = messageId,
                    SourceChainId = result.SourceChainId,
                    Sender = result.Sender,
                    TargetChainId = _targetChainId,
                    Target = result.Target,
                    Data = result.Data ?? Array.Empty<byte>(),
                    Timestamp = (long)result.Timestamp
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get message {MessageId} for chain {ChainId}", messageId, _targetChainId);
                return null;
            }
        }

        public async Task<List<MessageInfo>> GetMessageRangeAsync(ulong fromId, ulong toId)
        {
            var messages = new List<MessageInfo>();
            try
            {
                var result = await _hubService.GetMessageRangeQueryAsync(_targetChainId, fromId, toId);
                if (result?.ReturnValue1 == null) return messages;

                ulong id = fromId;
                foreach (var m in result.ReturnValue1)
                {
                    if (m.Timestamp > 0)
                    {
                        messages.Add(new MessageInfo
                        {
                            MessageId = id,
                            SourceChainId = m.SourceChainId,
                            Sender = m.Sender,
                            TargetChainId = m.TargetChainId,
                            Target = m.Target,
                            Data = m.Data ?? Array.Empty<byte>(),
                            Timestamp = (long)m.Timestamp
                        });
                    }
                    id++;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get message range [{From},{To}) for chain {ChainId}", fromId, toId, _targetChainId);
            }

            return messages;
        }

        public async Task<ulong> GetPendingMessageCountAsync()
        {
            try
            {
                return await _hubService.PendingMessageCountQueryAsync(_targetChainId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get pending message count for chain {ChainId}", _targetChainId);
                return 0;
            }
        }

        public async Task<List<MessageInfo>> GetPendingMessagesAsync(ulong lastProcessedId, int maxMessages)
        {
            var messages = new List<MessageInfo>();
            try
            {
                var info = await _hubService.GetAppChainInfoQueryAsync(_targetChainId);
                if (!info.Registered) return messages;

                ulong fromId = lastProcessedId + 1;
                ulong nextId = info.NextMessageId;

                if (fromId >= nextId) return messages;

                ulong toId = Math.Min(fromId + (ulong)maxMessages, nextId);
                return await GetMessageRangeAsync(fromId, toId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get pending messages for chain {ChainId}", _targetChainId);
                return messages;
            }
        }
    }
}
