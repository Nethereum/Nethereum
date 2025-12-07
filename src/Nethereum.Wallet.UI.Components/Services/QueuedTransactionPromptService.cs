using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Wallet.UI;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.Services
{
    public class QueuedTransactionPromptService : Nethereum.Wallet.UI.ITransactionPromptService
    {
        private readonly IPromptQueueService _queueService;
        private readonly IPromptOverlayService _overlayService;
        private readonly IComponentLocalizer<PromptInfrastructureLocalizer> _messages;
        private readonly TimeSpan _defaultTimeout = TimeSpan.FromMinutes(5);

        public QueuedTransactionPromptService(
            IPromptQueueService queueService,
            IPromptOverlayService overlayService,
            IComponentLocalizer<PromptInfrastructureLocalizer> messages)
        {
            _queueService = queueService;
            _overlayService = overlayService;
            _messages = messages;
        }
        
        public async Task<string?> PromptTransactionAsync(TransactionInput transactionInput)
        {
            var promptInfo = new TransactionPromptInfo
            {
                TransactionInput = transactionInput
            };
            
            return await PromptTransactionWithInfoAsync(promptInfo);
        }
        
        public async Task<string?> PromptTransactionWithInfoAsync(TransactionPromptInfo promptInfo)
        {
            var promptId = await _queueService.EnqueueTransactionPromptAsync(promptInfo);
            
            if (!_overlayService.IsOverlayVisible)
            {
                await _overlayService.ShowNextPromptAsync();
            }
            
            var prompt = _queueService.GetPromptById(promptId);
            if (prompt != null)
            {
                try
                {
                    using var cts = new CancellationTokenSource(_defaultTimeout);
                    
                    var tcs = new TaskCompletionSource<bool>();
                    using (cts.Token.Register(() => tcs.TrySetCanceled()))
                    {
                        var completedTask = await Task.WhenAny(
                            prompt.CompletionSource.Task,
                            tcs.Task
                        );
                        
                        if (completedTask == prompt.CompletionSource.Task)
                        {
                            var result = await prompt.CompletionSource.Task;
                            return result as string;
                        }
                        else
                        {
                            prompt.Status = PromptStatus.TimedOut;
                            var message = _messages.GetString(PromptInfrastructureLocalizer.Keys.GenericRequestTimedOut);
                            await _queueService.RejectPromptAsync(
                                promptId,
                                message,
                                new TimeoutException(message));
                            throw new TimeoutException(
                                _messages.GetString(PromptInfrastructureLocalizer.Keys.TransactionPromptTimedOut));
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    return null;
                }
                catch (TimeoutException)
                {
                    return null;
                }
                catch (Exception)
                {
                    return null;
                }
            }
            
            return null;
        }
    }
}
