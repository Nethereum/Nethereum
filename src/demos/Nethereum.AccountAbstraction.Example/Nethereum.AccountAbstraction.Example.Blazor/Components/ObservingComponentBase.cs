using System.ComponentModel;
using Microsoft.AspNetCore.Components;

namespace Nethereum.AccountAbstraction.Example.Blazor.Components
{
    public abstract class ObservingComponentBase : ComponentBase, IDisposable
    {
        protected abstract INotifyPropertyChanged Observed { get; }

        protected override void OnInitialized()
        {
            Observed.PropertyChanged += OnObservedChanged;
        }

        private void OnObservedChanged(object? sender, PropertyChangedEventArgs e)
            => InvokeAsync(StateHasChanged);

        public virtual void Dispose()
        {
            Observed.PropertyChanged -= OnObservedChanged;
        }
    }
}
