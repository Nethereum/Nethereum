using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.UI
{
    public class SelectedEthereumHostProviderService
    {
        private IEthereumHostProvider _selectedHostProvider = null;
        public event Func<IEthereumHostProvider, Task> SelectedHostProviderChanged;
        public IEthereumHostProvider SelectedHost => _selectedHostProvider;

        public Task SetSelectedEthereumHostProvider(IEthereumHostProvider ethereumHostProvider)
        {
            if (ethereumHostProvider != _selectedHostProvider)
            {
                _selectedHostProvider = ethereumHostProvider;
                if (SelectedHostProviderChanged != null)
                {
                    return SelectedHostProviderChanged(_selectedHostProvider);
                }
            }
            return Task.CompletedTask;
        }
    }
}
