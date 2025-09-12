using System;
using System.Threading.Tasks;

namespace Nethereum.Wallet.UI.Components.Abstractions
{
    public interface IWalletDashboardModule
    {
        string ModuleId { get; }
        string DisplayName { get; }
        string Icon { get; }
        int Order { get; }
        bool IsEnabled { get; }
        bool CanBeDefault { get; }
        string ComponentTypeName { get; }
        Task InitializeAsync();
        Task DisposeAsync();
        Task<bool> ShouldDisplayAsync();
    }
    public abstract class WalletDashboardModuleBase : IWalletDashboardModule
    {
        public abstract string ModuleId { get; }
        public abstract string DisplayName { get; }
        public abstract string Icon { get; }
        public virtual int Order { get; } = 100;
        public virtual bool IsEnabled { get; } = true;
        public virtual bool CanBeDefault { get; } = false;
        public abstract string ComponentTypeName { get; }

        public virtual Task InitializeAsync() => Task.CompletedTask;
        public virtual Task DisposeAsync() => Task.CompletedTask;
        public virtual Task<bool> ShouldDisplayAsync() => Task.FromResult(IsEnabled);
    }
}