using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.AppChain.P2P.BlockHandling;
using Nethereum.CoreChain.P2P;

namespace Nethereum.AppChain.P2P
{
    public class P2PMessageDispatcher : IDisposable
    {
        private readonly IP2PTransport _transport;
        private readonly IP2PBlockHandler? _blockHandler;
        private readonly ILogger<P2PMessageDispatcher>? _logger;
        private bool _disposed;

        public P2PMessageDispatcher(
            IP2PTransport transport,
            IP2PBlockHandler? blockHandler = null,
            ILogger<P2PMessageDispatcher>? logger = null)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _blockHandler = blockHandler;
            _logger = logger;

            _transport.MessageReceived += OnMessageReceived;
        }

        private async void OnMessageReceived(object? sender, P2PMessageEventArgs e)
        {
            try
            {
                await HandleMessageAsync(e.PeerId, e.Message);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[P2P Dispatcher] Error handling message type {MessageType} from {PeerId}",
                    e.Message.Type, e.PeerId);
            }
        }

        private async Task HandleMessageAsync(string peerId, P2PMessage message)
        {
            switch (message.Type)
            {
                case P2PMessageType.NewBlock:
                    await HandleNewBlockAsync(peerId, message);
                    break;

                case P2PMessageType.NewBlockHashes:
                    _logger?.LogDebug("[P2P Dispatcher] Received NewBlockHashes from {PeerId} (not yet implemented)", peerId);
                    break;

                case P2PMessageType.GetBlocks:
                    _logger?.LogDebug("[P2P Dispatcher] Received GetBlocks from {PeerId} (not yet implemented)", peerId);
                    break;

                case P2PMessageType.NewTransaction:
                case P2PMessageType.NewTransactionHashes:
                    _logger?.LogDebug("[P2P Dispatcher] Received TX message type {Type} from {PeerId}", message.Type, peerId);
                    break;

                default:
                    _logger?.LogTrace("[P2P Dispatcher] Received message type {Type} from {PeerId}", message.Type, peerId);
                    break;
            }
        }

        private async Task HandleNewBlockAsync(string peerId, P2PMessage message)
        {
            if (_blockHandler == null)
            {
                _logger?.LogWarning("[P2P Dispatcher] No block handler configured, ignoring NewBlock from {PeerId}", peerId);
                return;
            }

            if (message.Payload == null || message.Payload.Length == 0)
            {
                _logger?.LogWarning("[P2P Dispatcher] Empty NewBlock payload from {PeerId}", peerId);
                return;
            }

            _logger?.LogDebug("[P2P Dispatcher] Processing NewBlock ({PayloadSize} bytes) from {PeerId}",
                message.Payload.Length, peerId);

            var result = await _blockHandler.HandleNewBlockMessageAsync(message.Payload, peerId);

            if (result.Success)
            {
                if (result.Reason == BlockImportReason.Imported)
                {
                    _logger?.LogInformation("[P2P Dispatcher] Successfully imported block {BlockNumber} from {PeerId}",
                        result.Header?.BlockNumber, peerId);
                }
            }
            else
            {
                _logger?.LogWarning("[P2P Dispatcher] Failed to import block from {PeerId}: {Error}",
                    peerId, result.Error);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _transport.MessageReceived -= OnMessageReceived;
        }
    }
}
