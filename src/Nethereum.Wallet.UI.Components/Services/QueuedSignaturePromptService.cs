using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Wallet.UI;
using Nethereum.ABI.EIP712;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Newtonsoft.Json.Linq;

namespace Nethereum.Wallet.UI.Components.Services
{
    public class QueuedSignaturePromptService : Nethereum.Wallet.UI.ISignaturePromptService
    {
        private readonly IPromptQueueService _queueService;
        private readonly IPromptOverlayService _overlayService;
        private readonly IComponentLocalizer<PromptInfrastructureLocalizer> _messages;
        private readonly TimeSpan _defaultTimeout = TimeSpan.FromMinutes(5);

        public QueuedSignaturePromptService(
            IPromptQueueService queueService,
            IPromptOverlayService overlayService,
            IComponentLocalizer<PromptInfrastructureLocalizer> messages)
        {
            _queueService = queueService;
            _overlayService = overlayService;
            _messages = messages;
        }

        public async Task<string?> PromptSignatureAsync(SignaturePromptContext context)
        {
            var promptInfo = CreateSignaturePromptInfo(context);
            return await ProcessSignaturePromptAsync(
                promptInfo,
                _queueService.EnqueueSignaturePromptAsync).ConfigureAwait(false);
        }

        public async Task<string?> PromptTypedDataSignAsync(TypedDataSignPromptContext context)
        {
            var formatted = FormatDeclaredTypedData(context.TypedDataJson);

            var promptInfo = new SignaturePromptInfo
            {
                Method = "eth_signTypedData_v4",
                RawMessage = context.TypedDataJson ?? string.Empty,
                DecodedMessage = formatted,
                IsMessageHex = false,
                Address = context.Address,
                Origin = context.Origin,
                DAppIcon = context.DappIcon,
                DAppName = context.DappName,
                DomainName = context.DomainName,
                DomainVersion = context.DomainVersion,
                VerifyingContract = context.VerifyingContract,
                PrimaryType = context.PrimaryType,
                ChainId = context.ChainId
            };

            return await ProcessSignaturePromptAsync(
                promptInfo,
                _queueService.EnqueueTypedDataPromptAsync).ConfigureAwait(false);
        }

        private async Task<string?> ProcessSignaturePromptAsync(
            SignaturePromptInfo promptInfo,
            Func<SignaturePromptInfo, Task<string>> enqueuePromptAsync)
        {
            var promptId = await enqueuePromptAsync(promptInfo).ConfigureAwait(false);
            
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
                    var timeoutTask = Task.Delay(_defaultTimeout, cts.Token);
                    var completedTask = await Task.WhenAny(prompt.CompletionSource.Task, timeoutTask).ConfigureAwait(false);

                    if (completedTask == prompt.CompletionSource.Task)
                    {
                        cts.Cancel();
                        var result = await prompt.CompletionSource.Task.ConfigureAwait(false);
                        return result as string;
                    }

                    prompt.Status = PromptStatus.TimedOut;
                    var timeoutMessage = _messages.GetString(PromptInfrastructureLocalizer.Keys.GenericRequestTimedOut);
                    await _queueService.RejectPromptAsync(
                        promptId,
                        timeoutMessage,
                        new TimeoutException(timeoutMessage)).ConfigureAwait(false);
                    return null;
                }
                catch (OperationCanceledException)
                {
                    throw;
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

        private static SignaturePromptInfo CreateSignaturePromptInfo(SignaturePromptContext context)
        {
            var info = new SignaturePromptInfo
            {
                Method = string.IsNullOrEmpty(context.Method) ? "personal_sign" : context.Method,
                RawMessage = context.Message ?? string.Empty,
                Address = context.Address ?? string.Empty,
                Origin = context.Origin,
                DAppName = context.DappName,
                DAppIcon = context.DappIcon,
                IsMessageHex = context.IsMessageHex,
                DecodedMessage = context.Message
            };

            if (!info.IsMessageHex && !string.IsNullOrEmpty(context.Message) && context.Message.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                info.IsMessageHex = true;
            }

            if (info.IsMessageHex)
            {
                info.IsMessageHex = true;
                info.DecodedMessage = context.DecodedMessage ?? context.Message;
            }
            else if (!string.IsNullOrEmpty(context.DecodedMessage))
            {
                info.DecodedMessage = context.DecodedMessage;
            }

            return info;
        }

        private static string? FormatDeclaredTypedData(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                var declared = JObject.Parse(TypedDataRawJsonConversion.DeserialiseJsonToRawTypedData(json).ToJson());
                declared.Remove("types");
                return declared.ToString();
            }
            catch
            {
                return null;
            }
        }
    }
}
