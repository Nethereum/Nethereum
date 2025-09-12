using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Nethereum.Wallet.UI.Components.Services
{
    public partial class PromptQueueService : ObservableObject, IPromptQueueService
    {
        private readonly ConcurrentDictionary<string, PromptRequest> _prompts = new();
        
        public event EventHandler<PromptQueueChangedEventArgs>? QueueChanged;
        
        public IReadOnlyList<PromptRequest> PendingPrompts => 
            _prompts.Values
                .Where(p => p.Status == PromptStatus.Pending || p.Status == PromptStatus.InProgress)
                .OrderBy(p => p.CreatedAt)
                .ToList();
        
        public int PendingCount => PendingPrompts.Count;
        public bool HasPendingPrompts => PendingCount > 0;
        
        public async Task<string> EnqueueTransactionPromptAsync(TransactionPromptInfo promptInfo)
        {
            var prompt = new PromptRequest
            {
                Type = PromptType.Transaction,
                Data = promptInfo,
                Origin = promptInfo.Origin,
                DAppName = promptInfo.DAppName,
                DAppIcon = promptInfo.DAppIcon
            };
            
            _prompts[prompt.Id] = prompt;
            OnPropertyChanged(nameof(PendingPrompts));
            OnPropertyChanged(nameof(PendingCount));
            OnPropertyChanged(nameof(HasPendingPrompts));
            
            QueueChanged?.Invoke(this, new PromptQueueChangedEventArgs
            {
                ChangeType = PromptQueueChangeType.Added,
                Prompt = prompt,
                NewCount = PendingCount
            });
            
            return prompt.Id;
        }
        
        public async Task<string> EnqueueSignaturePromptAsync(SignaturePromptInfo promptInfo)
        {
            var prompt = new PromptRequest
            {
                Type = PromptType.Signature,
                Data = promptInfo,
                Origin = promptInfo.Origin,
                DAppName = promptInfo.DAppName,
                DAppIcon = promptInfo.DAppIcon
            };
            
            _prompts[prompt.Id] = prompt;
            OnPropertyChanged(nameof(PendingPrompts));
            OnPropertyChanged(nameof(PendingCount));
            OnPropertyChanged(nameof(HasPendingPrompts));
            
            QueueChanged?.Invoke(this, new PromptQueueChangedEventArgs
            {
                ChangeType = PromptQueueChangeType.Added,
                Prompt = prompt,
                NewCount = PendingCount
            });
            
            return prompt.Id;
        }
        
        public PromptRequest? GetNextPrompt()
        {
            return PendingPrompts.FirstOrDefault();
        }
        
        public PromptRequest? GetPromptById(string id)
        {
            return _prompts.TryGetValue(id, out var prompt) ? prompt : null;
        }
        
        public async Task CompletePromptAsync(string promptId, object? result)
        {
            if (_prompts.TryGetValue(promptId, out var prompt))
            {
                prompt.Status = PromptStatus.Completed;
                prompt.CompletionSource.SetResult(result);
                
                _prompts.TryRemove(promptId, out _);
                OnPropertyChanged(nameof(PendingPrompts));
                OnPropertyChanged(nameof(PendingCount));
                OnPropertyChanged(nameof(HasPendingPrompts));
                
                QueueChanged?.Invoke(this, new PromptQueueChangedEventArgs
                {
                    ChangeType = PromptQueueChangeType.Completed,
                    Prompt = prompt,
                    NewCount = PendingCount
                });
            }
        }
        
        public async Task RejectPromptAsync(string promptId, string? reason = null)
        {
            if (_prompts.TryGetValue(promptId, out var prompt))
            {
                prompt.Status = PromptStatus.Rejected;
                prompt.CompletionSource.SetException(
                    new Exception(reason ?? "User rejected the request"));
                
                _prompts.TryRemove(promptId, out _);
                OnPropertyChanged(nameof(PendingPrompts));
                OnPropertyChanged(nameof(PendingCount));
                OnPropertyChanged(nameof(HasPendingPrompts));
                
                QueueChanged?.Invoke(this, new PromptQueueChangedEventArgs
                {
                    ChangeType = PromptQueueChangeType.Rejected,
                    Prompt = prompt,
                    NewCount = PendingCount
                });
            }
        }
        
        public async Task RejectAllAsync()
        {
            var pendingPrompts = PendingPrompts.ToList();
            foreach (var prompt in pendingPrompts)
            {
                await RejectPromptAsync(prompt.Id, "Bulk rejection");
            }
        }
        
        public void ClearQueue()
        {
            var pendingPrompts = PendingPrompts.ToList();
            foreach (var prompt in pendingPrompts)
            {
                prompt.Status = PromptStatus.Rejected;
                prompt.CompletionSource.SetException(new Exception("Queue cleared"));
            }
            
            _prompts.Clear();
            OnPropertyChanged(nameof(PendingPrompts));
            OnPropertyChanged(nameof(PendingCount));
            OnPropertyChanged(nameof(HasPendingPrompts));
            
            QueueChanged?.Invoke(this, new PromptQueueChangedEventArgs
            {
                ChangeType = PromptQueueChangeType.Rejected,
                Prompt = null,
                NewCount = 0
            });
        }
    }
}