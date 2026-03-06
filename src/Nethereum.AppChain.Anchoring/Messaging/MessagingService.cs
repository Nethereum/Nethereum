using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Nethereum.AppChain.Anchoring.Messaging
{
    public class MessagingService : IMessagingService
    {
        private readonly ulong _targetChainId;
        private readonly ConcurrentDictionary<ulong, ulong> _lastProcessedIds = new();
        private readonly MessagingConfig _config;
        private readonly IMessageIndexStore _store;
        private readonly IMessageQueue? _messageQueue;
        private readonly ILogger<MessagingService>? _logger;

        public event Func<MessageInfo, Task>? OnMessageReceived;

        public MessagingService(
            ulong targetChainId,
            MessagingConfig config,
            IMessageIndexStore store,
            IMessageQueue? messageQueue = null,
            ILogger<MessagingService>? logger = null,
            IMessageMerkleAccumulator? accumulator = null)
        {
            _targetChainId = targetChainId;
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _messageQueue = messageQueue;
            _logger = logger;

            foreach (var source in config.SourceChains)
            {
                var initialCursor = accumulator?.GetLastProcessedMessageId(source.ChainId) ?? 0;
                _lastProcessedIds[source.ChainId] = initialCursor;
                if (initialCursor > 0)
                {
                    logger?.LogInformation("Resuming message polling for source chain {ChainId} from message {MessageId}",
                        source.ChainId, initialCursor);
                }
            }
        }

        public ulong GetLastProcessedMessageId(ulong sourceChainId)
        {
            return _lastProcessedIds.TryGetValue(sourceChainId, out var id) ? id : 0;
        }

        public async Task<List<MessageInfo>> PollAllSourcesAsync()
        {
            var allMessages = new List<MessageInfo>();

            foreach (var source in _config.SourceChains)
            {
                try
                {
                    _lastProcessedIds.TryGetValue(source.ChainId, out var lastProcessed);
                    var messages = await _store.GetPendingAsync(source.ChainId, lastProcessed, _config.MaxMessagesPerPoll);

                    foreach (var message in messages)
                    {
                        _messageQueue?.Enqueue(message);
                        allMessages.Add(message);

                        if (OnMessageReceived != null)
                        {
                            await OnMessageReceived(message);
                        }

                        if (message.MessageId > lastProcessed)
                        {
                            _lastProcessedIds[source.ChainId] = message.MessageId;
                            lastProcessed = message.MessageId;
                        }
                    }

                    if (messages.Count > 0)
                    {
                        _logger?.LogInformation("Read {Count} messages from store for source chain {SourceChainId} (lastProcessed={LastProcessed})",
                            messages.Count, source.ChainId, _lastProcessedIds[source.ChainId]);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error reading messages from store for source chain {SourceChainId}", source.ChainId);
                }
            }

            return allMessages;
        }
    }
}
