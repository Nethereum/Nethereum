using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Publish;
using Nethereum.Model;
using Nethereum.Model.P2P;

namespace Nethereum.AppChain.Server.Hosting
{
    public class DevP2PProducedBlockPublisher
    {
        private readonly Eth68PeerPool _peers;
        private readonly ITransactionStore _transactions;
        private readonly ILogger<DevP2PProducedBlockPublisher> _logger;

        public DevP2PProducedBlockPublisher(
            Eth68PeerPool peers,
            ITransactionStore transactions,
            ILogger<DevP2PProducedBlockPublisher> logger = null)
        {
            _peers = peers ?? throw new ArgumentNullException(nameof(peers));
            _transactions = transactions ?? throw new ArgumentNullException(nameof(transactions));
            _logger = logger;
        }

        public void OnBlockProduced(object sender, BlockProductionResult result)
        {
            _ = PublishAsync(result);
        }

        public async Task PublishAsync(BlockProductionResult result)
        {
            if (result?.Header == null || result.BlockHash == null) return;
            if (_peers.Count == 0) return;

            try
            {
                var payload = NewBlockMessageEncoder.Encode(await BuildMessageAsync(result));
                await _peers.BroadcastAsync(Eth68MessageIds.NewBlock, payload).ConfigureAwait(false);
                _logger?.LogInformation(
                    "Published block {Number} to {Peers} DevP2P peer(s)",
                    result.Header.BlockNumber, _peers.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Publishing block {Number} failed", result.Header.BlockNumber);
            }
        }

        private async Task<NewBlockMessage> BuildMessageAsync(BlockProductionResult result)
        {
            var transactions = await _transactions.GetByBlockHashAsync(result.BlockHash).ConfigureAwait(false)
                               ?? new List<ISignedTransaction>();

            return new NewBlockMessage
            {
                Header = result.Header,
                Transactions = transactions,
                Uncles = new List<BlockHeader>(),
                Withdrawals = new List<Withdrawal>(),
                TotalDifficulty = result.Header.Difficulty
            };
        }
    }
}
