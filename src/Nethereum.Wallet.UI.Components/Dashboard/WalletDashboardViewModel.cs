using Nethereum.Wallet.Hosting;
using Nethereum.Wallet.WalletAccounts;
using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using Nethereum.Wallet.Services.Network;
using Nethereum.RPC.Chain;
using Nethereum.Wallet.UI.Components.Networks;
using Nethereum.Wallet.UI.Components.Utils;

namespace Nethereum.Wallet.UI.Components.Dashboard
{
    public partial class WalletDashboardViewModel : ObservableObject
    {
        private readonly NethereumWalletHostProvider _walletHostProvider;
        private readonly IChainManagementService _chainManagementService;
        private readonly INetworkIconProvider _networkIconProvider;
        
        [ObservableProperty]
        private IWalletAccount? _selectedAccount;
        
        [ObservableProperty]
        private int _selectedAccountIndex;
        
        [ObservableProperty]
        private string _selectedNetworkName = "Unknown Network";
        
        [ObservableProperty]
        private string _selectedNetworkLogoPath = "";
        
        [ObservableProperty]
        private string _selectedChainId = "";

        public WalletDashboardViewModel(NethereumWalletHostProvider walletHostProvider, IChainManagementService chainManagementService, INetworkIconProvider networkIconProvider)
        {
            _walletHostProvider = walletHostProvider ?? throw new ArgumentNullException(nameof(walletHostProvider));
            _chainManagementService = chainManagementService ?? throw new ArgumentNullException(nameof(chainManagementService));
            _networkIconProvider = networkIconProvider ?? throw new ArgumentNullException(nameof(networkIconProvider));
        }

        partial void OnSelectedAccountChanged(IWalletAccount? value)
        {
            OnPropertyChanged(nameof(SelectedAccountName));
            OnPropertyChanged(nameof(SelectedAccountAddress));
            OnPropertyChanged(nameof(HasSelectedAccount));
        }

        partial void OnSelectedAccountIndexChanged(int value)
        {
            OnPropertyChanged(nameof(SelectedAccountName));
        }
        public string SelectedAccountName
        {
            get
            {
                if (SelectedAccount == null) return "";
                return SelectedAccount.Name ?? $"Account {SelectedAccountIndex + 1}";
            }
        }
        public string SelectedAccountAddress => SelectedAccount?.Address ?? "";
        public bool HasSelectedAccount => SelectedAccount != null;
        public bool HasSelectedNetworkLogo
        {
            get
            {
                try
                {
                    var chainId = _walletHostProvider.SelectedNetworkChainId;
                    return _networkIconProvider.HasNetworkIcon(chainId);
                }
                catch
                {
                    return false;
                }
            }
        }

        public async Task InitializeAsync()
        {
            await RefreshSelectedAccount();
            await RefreshNetworkInfo();
            
            _walletHostProvider.SelectedAccountChanged += OnHostProviderAccountChanged;
            
            _walletHostProvider.NetworkChanged += OnHostProviderNetworkChanged;
        }

        public void Dispose()
        {
            _walletHostProvider.SelectedAccountChanged -= OnHostProviderAccountChanged;
            _walletHostProvider.NetworkChanged -= OnHostProviderNetworkChanged;
        }
        public async Task RefreshSelectedAccount()
        {
            var currentAccount = _walletHostProvider.GetSelectedAccount();
            var accounts = _walletHostProvider.GetAccounts().ToList();
            
            SelectedAccount = currentAccount;
            SelectedAccountIndex = currentAccount != null ? accounts.IndexOf(currentAccount) : -1;
        }
        public async Task RefreshNetworkInfo()
        {
            try
            {
                var chainId = _walletHostProvider.SelectedNetworkChainId;
                SelectedChainId = chainId.ToString();
                
                // Get network icon immediately (synchronous)
                SelectedNetworkLogoPath = _networkIconProvider.GetNetworkIcon(chainId) ?? "";
                
                var chain = await _chainManagementService.GetChainAsync(new System.Numerics.BigInteger(chainId));
                SelectedNetworkName = chain?.ChainName ?? $"Chain {chainId}";
                
                OnPropertyChanged(nameof(HasSelectedNetworkLogo));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing network info: {ex.Message}");
                SelectedNetworkName = $"Chain {_walletHostProvider.SelectedNetworkChainId}";
                SelectedNetworkLogoPath = "";
                SelectedChainId = _walletHostProvider.SelectedNetworkChainId.ToString();
            }
        }

        private async Task OnHostProviderAccountChanged(string accountAddress)
        {
            await RefreshSelectedAccount();
        }

        private async Task OnHostProviderNetworkChanged(long chainId)
        {
            await RefreshNetworkInfo();
        }
        public string FormatAddress(string address)
        {
            if (string.IsNullOrEmpty(address) || address.Length <= 16)
                return address;
            
            return $"{address[..8]}...{address[^6..]}";
        }
        public string GetFormattedSelectedAccountAddress(int maxDisplayWidth = 0, bool showFullAddress = true)
        {
            if (SelectedAccount == null) return "";
            
            var address = SelectedAccount.Address;
            
            if (showFullAddress && maxDisplayWidth > 0)
            {
                // Rough calculation: each character is ~8px in monospace
                var estimatedAddressWidth = address.Length * 8;
                if (estimatedAddressWidth <= maxDisplayWidth)
                    return address;
            }
            else if (showFullAddress && address.Length <= 42)
            {
                return address;
            }
                
            return FormatAddress(address);
        }
    }
}