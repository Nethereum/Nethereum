using System;
using System.Threading.Tasks;
using Nethereum.RPC.HostWallet;
using Nethereum.Wallet.UI;
using Nethereum.RPC.Chain;
using Nethereum.Wallet.UI.Components.Core.Localization;
namespace Nethereum.Wallet.UI.Components.Services
{
    public class QueuedChainAdditionPromptService : IChainAdditionPromptService
    {
        private readonly IPromptQueueService _queueService;
        private readonly IPromptOverlayService _overlayService;
        private readonly IComponentLocalizer<PromptInfrastructureLocalizer> _messages;
        private readonly TimeSpan _defaultTimeout = TimeSpan.FromMinutes(5);

        public QueuedChainAdditionPromptService(
            IPromptQueueService queueService,
            IPromptOverlayService overlayService,
            IComponentLocalizer<PromptInfrastructureLocalizer> messages)
        {
            _queueService = queueService;
            _overlayService = overlayService;
            _messages = messages;
        }

        public async Task<ChainAdditionPromptResult> RequestAddChainAsync(ChainAdditionPromptRequest request)
        {
            if (request?.Parameter == null)
            {
                return ChainAdditionPromptResult.Rejected(
                    _messages.GetString(PromptInfrastructureLocalizer.Keys.ChainAdditionInvalidRequest));
            }

            var promptInfo = new ChainAdditionPromptInfo
            {
                Parameter = request.Parameter,
                ChainFeature = request.Parameter.ToChainFeature(),
                SwitchAfterAdd = request.SwitchAfterAdd,
                Origin = request.Origin,
                DAppName = request.DappName,
                DAppIcon = request.DappIcon
            };

            var promptId = await _queueService.EnqueueChainAdditionPromptAsync(promptInfo).ConfigureAwait(false);

            if (!_overlayService.IsOverlayVisible)
            {
                await _overlayService.ShowNextPromptAsync().ConfigureAwait(false);
            }

            var prompt = _queueService.GetPromptById(promptId);
            if (prompt == null)
            {
                return ChainAdditionPromptResult.Rejected(
                    _messages.GetString(PromptInfrastructureLocalizer.Keys.PromptNotFound));
            }

            try
            {
                var timeoutTask = Task.Delay(_defaultTimeout);

                var completedTask = await Task.WhenAny(prompt.CompletionSource.Task, timeoutTask).ConfigureAwait(false);
                if (completedTask == prompt.CompletionSource.Task)
                {
                    var result = await prompt.CompletionSource.Task.ConfigureAwait(false);
                    if (result is ChainAdditionPromptResult chainResult)
                    {
                        return chainResult;
                    }

                    if (result is AddEthereumChainParameter param)
                    {
                        var chainId = param.ChainId.Value;
                        return ChainAdditionPromptResult.ApprovedResult(chainId, request.SwitchAfterAdd, request.SwitchAfterAdd);
                    }

                    return ChainAdditionPromptResult.ApprovedResult(null, request.SwitchAfterAdd, request.SwitchAfterAdd);
                }

                var timeoutMessage = _messages.GetString(PromptInfrastructureLocalizer.Keys.ChainAdditionTimedOut);
                await _queueService.RejectPromptAsync(
                    promptId,
                    timeoutMessage,
                    new TimeoutException(timeoutMessage)).ConfigureAwait(false);
                return ChainAdditionPromptResult.Rejected(
                    timeoutMessage);
            }
            catch (TaskCanceledException)
            {
                return ChainAdditionPromptResult.Rejected(
                    _messages.GetString(PromptInfrastructureLocalizer.Keys.ChainAdditionCanceled));
            }
            catch (Exception ex)
            {
                await _queueService.RejectPromptAsync(promptId, ex.Message, ex).ConfigureAwait(false);
                return ChainAdditionPromptResult.Rejected(ex.Message);
            }
        }
    }
}
