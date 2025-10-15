using System;
using System.Threading.Tasks;
using Nethereum.Wallet.UI;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.Services
{
    public class QueuedChainSwitchPromptService : IChainSwitchPromptService
    {
        private readonly IPromptQueueService _queueService;
        private readonly IPromptOverlayService _overlayService;
        private readonly IComponentLocalizer<PromptInfrastructureLocalizer> _messages;
        private readonly TimeSpan _defaultTimeout = TimeSpan.FromMinutes(5);

        public QueuedChainSwitchPromptService(
            IPromptQueueService queueService,
            IPromptOverlayService overlayService,
            IComponentLocalizer<PromptInfrastructureLocalizer> messages)
        {
            _queueService = queueService;
            _overlayService = overlayService;
            _messages = messages;
        }

        public async Task<bool> RequestSwitchAsync(ChainSwitchPromptRequest request)
        {
            if (request == null)
            {
                return false;
            }

            var promptInfo = new ChainSwitchPromptInfo
            {
                ChainId = request.ChainId,
                TargetChain = request.Chain,
                IsKnown = request.IsKnown,
                AllowAdd = request.AllowAdd,
                CurrentChainId = request.CurrentChainId,
                CurrentChain = request.CurrentChain,
                Origin = request.Origin,
                DAppName = request.DappName,
                DAppIcon = request.DappIcon
            };

            var promptId = await _queueService.EnqueueNetworkSwitchPromptAsync(promptInfo).ConfigureAwait(false);

            if (!_overlayService.IsOverlayVisible)
            {
                await _overlayService.ShowNextPromptAsync().ConfigureAwait(false);
            }

            var prompt = _queueService.GetPromptById(promptId);
            if (prompt == null)
            {
                return false;
            }

            try
            {
                var timeoutTask = Task.Delay(_defaultTimeout);

                var completedTask = await Task.WhenAny(prompt.CompletionSource.Task, timeoutTask).ConfigureAwait(false);
                if (completedTask == prompt.CompletionSource.Task)
                {
                    var result = await prompt.CompletionSource.Task.ConfigureAwait(false);
                    return result is bool approved && approved;
                }

                await _queueService.RejectPromptAsync(
                    promptId,
                    _messages.GetString(PromptInfrastructureLocalizer.Keys.NetworkSwitchPromptTimedOut)).ConfigureAwait(false);
                return false;
            }
            catch (Exception ex)
            {
                await _queueService.RejectPromptAsync(promptId, ex.Message).ConfigureAwait(false);
                return false;
            }
        }
    }
}
