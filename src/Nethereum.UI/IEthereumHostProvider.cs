using System;
using System.Threading.Tasks;

namespace Nethereum.UI
{
    public interface IEthereumHostProvider
    {
        string Name { get; }

        bool Available { get; }
        string SelectedAccount { get;}
        long SelectedNetworkChainId { get; }
        bool Enabled { get; }
        
        event Func<string, Task> SelectedAccountChanged;
        event Func<long, Task> NetworkChanged;
        event Func<bool, Task> AvailabilityChanged;
        event Func<bool, Task> EnabledChanged;

        Task<bool> CheckProviderAvailabilityAsync();
        Task<Web3.IWeb3> GetWeb3Async();
        Task<string> EnableProviderAsync();
        Task<string> GetProviderSelectedAccountAsync();
        Task<string> SignMessageAsync(string message);
    }
}