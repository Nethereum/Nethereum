using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.Wallet.UI.Components.Dashboard
{
    public interface INavigatablePlugin
    {
        Task NavigateWithParametersAsync(Dictionary<string, object> parameters);
    }
}