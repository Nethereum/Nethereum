using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Wallet.UI;

namespace Nethereum.Wallet.UI.Components.Services
{
    public class QueuedSignaturePromptService : Nethereum.Wallet.UI.ISignaturePromptService
    {
        private readonly IPromptQueueService _queueService;
        private readonly IPromptOverlayService _overlayService;
        private readonly TimeSpan _defaultTimeout = TimeSpan.FromMinutes(5);
        
        public QueuedSignaturePromptService(
            IPromptQueueService queueService,
            IPromptOverlayService overlayService)
        {
            _queueService = queueService;
            _overlayService = overlayService;
        }
        
        public async Task<string> PromptSignatureAsync(string message)
        {
            var result = await PromptPersonalSignAsync(message, null);
            return result ?? string.Empty;
        }
        
        public async Task<string?> PromptPersonalSignAsync(string message, string? description = null)
        {
            var promptInfo = new SignaturePromptInfo
            {
                Message = message,
                Origin = description
            };
            
            return await ProcessSignaturePromptAsync(promptInfo);
        }
        
        public async Task<string?> PromptTypedDataSignAsync(string typedData, string? description = null)
        {
            var promptInfo = new SignaturePromptInfo
            {
                Message = typedData,
                Origin = description
            };
            
            return await ProcessSignaturePromptAsync(promptInfo);
        }
        
        public async Task<string?> PromptTypedDataSignAsync(TypedDataSigningInfo signingInfo)
        {
            var promptInfo = new SignaturePromptInfo
            {
                Message = signingInfo.TypedData,
                Origin = signingInfo.Origin,
                DAppIcon = signingInfo.DAppIcon,
                DAppName = signingInfo.DAppName,
            };
            
            return await ProcessSignaturePromptAsync(promptInfo);
        }
        
        private async Task<string?> ProcessSignaturePromptAsync(SignaturePromptInfo promptInfo)
        {
            var promptId = await _queueService.EnqueueSignaturePromptAsync(promptInfo);
            
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
                            await _queueService.RejectPromptAsync(promptId, "Request timed out");
                            throw new TimeoutException("Signature prompt timed out");
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