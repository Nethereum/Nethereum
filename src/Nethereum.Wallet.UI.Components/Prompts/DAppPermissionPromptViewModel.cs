using CommunityToolkit.Mvvm.ComponentModel;
using Nethereum.Wallet.UI.Components.Services;

namespace Nethereum.Wallet.UI.Components.Prompts
{
    public partial class DAppPermissionPromptViewModel : ObservableObject
    {
        [ObservableProperty]
        private DappPermissionPromptInfo? promptInfo;

        public void Initialize(DappPermissionPromptInfo info)
        {
            PromptInfo = info;
        }
    }
}
