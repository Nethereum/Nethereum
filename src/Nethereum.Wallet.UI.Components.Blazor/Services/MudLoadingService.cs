using Microsoft.AspNetCore.Components;
using Nethereum.Wallet.UI.Components.Abstractions;
using System;
using System.Threading.Tasks;

namespace Nethereum.Wallet.UI.Components.Blazor.Services
{
    public class MudLoadingService : IWalletLoadingService
    {
        public event Action<bool, string?>? LoadingStateChanged;
        public event Action<double, string?>? ProgressChanged;

        public bool IsLoading { get; private set; }
        public string? LoadingMessage { get; private set; }
        public double Progress { get; private set; }

        public void SetLoading(bool isLoading, string? message = null)
        {
            IsLoading = isLoading;
            LoadingMessage = message;
            
            if (!isLoading)
            {
                Progress = 0;
            }

            LoadingStateChanged?.Invoke(isLoading, message);
        }

        public void ShowProgress(double percentage, string? message = null)
        {
            Progress = Math.Max(0, Math.Min(100, percentage));
            LoadingMessage = message;
            
            if (!IsLoading)
            {
                IsLoading = true;
            }

            ProgressChanged?.Invoke(Progress, message);
            
            if (Progress >= 100)
            {
                _ = Task.Delay(500).ContinueWith(_ => SetLoading(false));
            }
        }
        public async Task<T> WithLoadingAsync<T>(Func<Task<T>> operation, string? message = null)
        {
            SetLoading(true, message);
            try
            {
                return await operation();
            }
            finally
            {
                SetLoading(false);
            }
        }
        public async Task WithLoadingAsync(Func<Task> operation, string? message = null)
        {
            SetLoading(true, message);
            try
            {
                await operation();
            }
            finally
            {
                SetLoading(false);
            }
        }
    }
}