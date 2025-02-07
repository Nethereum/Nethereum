using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Authorization;
using Nethereum.UI;
using System.Security.Claims;

namespace Nethereum.Blazor
{
    public class EthereumAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
    {
        protected IEthereumHostProvider EthereumHostProvider { get; set; }
        protected SelectedEthereumHostProviderService SelectedHostProviderService { get; }

        public EthereumAuthenticationStateProvider(SelectedEthereumHostProviderService selectedHostProviderService)
        {
            SelectedHostProviderService = selectedHostProviderService;
            SelectedHostProviderService.SelectedHostProviderChanged += SelectedHostProviderChanged;
            InitSelectedHostProvider();
        }

        public void NotifyStateHasChanged()
        {
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }

        private Task SelectedHostProviderChanged(IEthereumHostProvider newEthereumHostProvider)
        {
            if(EthereumHostProvider != newEthereumHostProvider)
            {
                if(EthereumHostProvider != null)
                {
                    EthereumHostProvider.SelectedAccountChanged -= SelectedAccountChanged;
                }
                InitSelectedHostProvider();
            }

            return Task.CompletedTask;
           
        }

        public void InitSelectedHostProvider()
        {
            EthereumHostProvider = SelectedHostProviderService.SelectedHost;
            if (SelectedHostProviderService.SelectedHost != null)
            {
                EthereumHostProvider.SelectedAccountChanged += SelectedAccountChanged;
            }
        }

        private async Task SelectedAccountChanged(string ethereumAddress)
        {
            if(string.IsNullOrEmpty(ethereumAddress))
            {
                await NotifyAuthenticationStateAsEthereumDisconnected();
            }
            else
            {
                await NotifyAuthenticationStateAsEthereumConnected(ethereumAddress);
            }
        }

        public async override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            try
            {
                if (EthereumHostProvider != null && EthereumHostProvider.Available)
                {

                    var currentAddress = await EthereumHostProvider.GetProviderSelectedAccountAsync();
                    if (currentAddress != null)
                    {
                        var claimsPrincipal = GetClaimsPrincipal(currentAddress);
                        return new AuthenticationState(claimsPrincipal);
                    }
                }

                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }
            catch
            {
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }

            
           
        }

        public async Task NotifyAuthenticationStateAsEthereumConnected()
        {
            var currentAddress = await EthereumHostProvider.GetProviderSelectedAccountAsync();
            await NotifyAuthenticationStateAsEthereumConnected(currentAddress);
        }

        public async Task NotifyAuthenticationStateAsEthereumConnected(string currentAddress)
        {
            var claimsPrincipal = GetClaimsPrincipal(currentAddress);
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(claimsPrincipal)));
        }

        public async Task NotifyAuthenticationStateAsEthereumDisconnected()
        {
           
            var identity = new ClaimsIdentity();
            var user = new ClaimsPrincipal(identity);
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
        }

        private ClaimsPrincipal GetClaimsPrincipal(string ethereumAddress)
        {
            var claimEthereumAddress = new Claim(ClaimTypes.NameIdentifier, ethereumAddress);
            var claimEthereumConnectedRole = new Claim(ClaimTypes.Role, "EthereumConnected");

            //create claimsIdentity
            var claimsIdentity = new ClaimsIdentity(new[] { claimEthereumAddress, claimEthereumConnectedRole }, "ethereumConnection");
            //create claimsPrincipal
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

            return claimsPrincipal;
        }

        public void Dispose()
        {
            if (EthereumHostProvider != null)
            {
                EthereumHostProvider.SelectedAccountChanged -= SelectedAccountChanged;
            }

            if (SelectedHostProviderService != null)
            {
                SelectedHostProviderService.SelectedHostProviderChanged -= SelectedHostProviderChanged;
            }
        }
    }
}