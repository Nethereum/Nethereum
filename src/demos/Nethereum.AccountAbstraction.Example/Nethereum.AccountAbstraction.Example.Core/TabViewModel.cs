using CommunityToolkit.Mvvm.ComponentModel;

namespace Nethereum.AccountAbstraction.Example.Core
{
    public abstract partial class TabViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        private string? _statusMessage;

        public event Action? Completed;

        protected async Task RunAsync(Func<Task> action)
        {
            if (IsBusy) return;

            IsBusy = true;
            ErrorMessage = null;
            try
            {
                await action().ConfigureAwait(false);
                Completed?.Invoke();
            }
            catch (Exception ex)
            {
                ErrorMessage = DescribeError(ex);
                StatusMessage = null;
            }
            finally
            {
                IsBusy = false;
            }
        }

        protected virtual string DescribeError(Exception ex)
        {
            var innermost = ex;
            while (innermost.InnerException != null) innermost = innermost.InnerException;
            return innermost.Message;
        }
    }
}
