using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Wallet.UI;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.Services
{
    public class QueuedDappPermissionPromptService : IDappPermissionPromptService
    {
        private readonly IPromptQueueService _queueService;
        private readonly IPromptOverlayService _overlayService;
        private readonly IComponentLocalizer<PromptInfrastructureLocalizer> _messages;
        private readonly TimeSpan _defaultTimeout = TimeSpan.FromMinutes(2);

        public QueuedDappPermissionPromptService(
            IPromptQueueService queueService,
            IPromptOverlayService overlayService,
            IComponentLocalizer<PromptInfrastructureLocalizer> messages)
        {
            _queueService = queueService;
            _overlayService = overlayService;
            _messages = messages;
        }

        public async Task<bool> RequestPermissionAsync(DappPermissionPromptRequest request)
        {
            var promptInfo = new DappPermissionPromptInfo
            {
                Origin = request.Origin,
                DAppName = request.DappName,
                DAppIcon = request.DappIcon,
                AccountAddress = request.AccountAddress
            };

            var promptId = await _queueService.EnqueuePermissionPromptAsync(promptInfo);

            if (!_overlayService.IsOverlayVisible)
            {
                await _overlayService.ShowNextPromptAsync();
            }

            var prompt = _queueService.GetPromptById(promptId);
            if (prompt == null)
            {
                return false;
            }

            try
            {
                using var cts = new CancellationTokenSource(_defaultTimeout);
                var tcs = new TaskCompletionSource<bool>();
                using (cts.Token.Register(() => tcs.TrySetCanceled()))
                {
                    var completedTask = await Task.WhenAny(prompt.CompletionSource.Task, tcs.Task).ConfigureAwait(false);
                    if (completedTask == prompt.CompletionSource.Task)
                    {
                        var result = await prompt.CompletionSource.Task.ConfigureAwait(false);
                        return result is bool b && b;
                    }
                    else
                    {
                        var timeoutMessage = _messages.GetString(PromptInfrastructureLocalizer.Keys.PermissionRequestTimedOut);
                        await _queueService.RejectPromptAsync(
                            promptId,
                            timeoutMessage,
                            new TimeoutException(timeoutMessage)).ConfigureAwait(false);
                        return false;
                    }
                }
            }
            catch (TaskCanceledException)
            {
                await _queueService.RejectPromptAsync(
                    promptId,
                    _messages.GetString(PromptInfrastructureLocalizer.Keys.PermissionRequestCanceled)).ConfigureAwait(false);
                return false;
            }
            catch (Exception ex)
            {
                await _queueService.RejectPromptAsync(
                    promptId,
                    _messages.GetString(PromptInfrastructureLocalizer.Keys.PermissionRequestFailed),
                    ex).ConfigureAwait(false);
                return false;
            }
        }
    }
}
