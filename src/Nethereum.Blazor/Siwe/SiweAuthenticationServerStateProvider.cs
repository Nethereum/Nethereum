using Microsoft.AspNetCore.Components.Authorization;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Nethereum.Siwe.Core;
using Nethereum.UI;
using Nethereum.Util;
using Nethereum.Blazor;
using Nethereum.Siwe;
using Nethereum.Siwe.Model;
using Nethereum.Siwe.Authentication;

namespace Nethereum.Blazor.Siwe
{

    public class SiweAuthenticationServerStateProvider<TUser, TSiweMessage> : EthereumAuthenticationStateProvider where TUser : User
        where TSiweMessage : SiweMessage, new()
    {
        private readonly NethereumSiweAuthenticatorService nethereumSiweAuthenticatorService;
        private readonly IAccessTokenService _accessTokenService;
        private readonly IUserService<TUser> _userService;
        private readonly SiweMessageService _siweMessageService;

        public SiweAuthenticationServerStateProvider(NethereumSiweAuthenticatorService nethereumSiweAuthenticatorService,
            IAccessTokenService accessTokenService, SelectedEthereumHostProviderService selectedHostProviderService, IUserService<TUser> userService, ISessionStorage sessionStorage) : base(selectedHostProviderService)
        {

            this.nethereumSiweAuthenticatorService = nethereumSiweAuthenticatorService;
            _accessTokenService = accessTokenService;
            _userService = userService;
            _siweMessageService = new SiweMessageService(sessionStorage);
        }

        public async override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var currentUser = await GetUserAsync();

            if (currentUser != null && currentUser.EthereumAddress != null)
            {
                var claimsPrincipal = GenerateSiweClaimsPrincipal(currentUser);
                return new AuthenticationState(claimsPrincipal);
            }
            await _accessTokenService.RemoveAccessTokenAsync();
            return await base.GetAuthenticationStateAsync();
        }

        public async Task AuthenticateAsync(string address = null)
        {

            if (EthereumHostProvider == null  || !EthereumHostProvider.Available)
            {
                throw new Exception("Cannot authenticate user, an Ethereum host is not available");
            }

            if (string.IsNullOrEmpty(address))
            {
                address = await EthereumHostProvider.GetProviderSelectedAccountAsync();
            }
            var siweMessage = new TSiweMessage();
            siweMessage.Address = address.ConvertToEthereumChecksumAddress();
            siweMessage.SetExpirationTime(DateTime.Now.AddMinutes(10)); 
            siweMessage.SetNotBefore(DateTime.Now);
            var authenticated = await nethereumSiweAuthenticatorService.AuthenticateWithSignatureAsync(siweMessage);
            await _accessTokenService.SetAccessTokenAsync(BuildToken(authenticated.Message, authenticated.Signature));
            await MarkUserAsAuthenticated();
        }


        public async Task MarkUserAsAuthenticated()
        {
            var user = await GetUserAsync();
            var claimsPrincipal = GenerateSiweClaimsPrincipal(user);

            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(claimsPrincipal)));
        }

        public async Task LogOutUserAsync()
        {
            var token = await _accessTokenService.GetAccessTokenAsync();
            await _accessTokenService.RemoveAccessTokenAsync();
            try
            {
                if (TryReadToken(token, out var siweMessage, out _))
                {
                    nethereumSiweAuthenticatorService.LogOut(siweMessage);
                }
            }
            catch
            {
            }
            var authenticationState = await GetAuthenticationStateAsync();
            NotifyAuthenticationStateChanged(Task.FromResult(authenticationState));
        }

        public async Task<TUser> GetUserAsync()
        {
            var token = await _accessTokenService.GetAccessTokenAsync();
            if (token == null) return null;
            SiweMessage siweMessage;
            bool current;
            try
            {
                if (!TryReadToken(token, out siweMessage, out var signature)) return null;
                current = _siweMessageService.IsMessageTheSameAsSessionStored(siweMessage) &&
                          _siweMessageService.HasMessageDateStartedAndNotExpired(siweMessage) &&
                          await _siweMessageService.IsMessageSignatureValid(siweMessage, signature);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is FormatException)
            {
                return null;
            }

            if (current && await _siweMessageService.IsUserAddressRegistered(siweMessage))
            {
                return await _userService.GetUserAsync(siweMessage.Address);
            }
            return null;
        }

        private static string BuildToken(SiweMessage siweMessage, string signature)
        {
            return signature + "\n" + SiweMessageStringBuilder.BuildMessage(siweMessage);
        }

        private static bool TryReadToken(string token, out SiweMessage siweMessage, out string signature)
        {
            siweMessage = null;
            signature = null;
            var separator = token.IndexOf('\n');
            if (separator < 0 || !token.StartsWith("0x", StringComparison.Ordinal)) return false;
            signature = token.Substring(0, separator);
            siweMessage = SiweMessageParser.Parse(token.Substring(separator + 1));
            return true;
        }

        private ClaimsPrincipal GenerateSiweClaimsPrincipal(User currentUser)
        {
            var claimName = new Claim(ClaimTypes.Name, currentUser.UserName);
            var claimEthereumAddress = new Claim(ClaimTypes.NameIdentifier, currentUser.EthereumAddress);
            var claimEthereumConnectedRole = new Claim(ClaimTypes.Role, "EthereumConnected");
            var claimSiweAuthenticatedRole = new Claim(ClaimTypes.Role, "SiweAuthenticated");

            var claimsIdentity = new ClaimsIdentity(new[] { claimEthereumAddress, claimName, claimEthereumConnectedRole, claimSiweAuthenticatedRole }, "siweAuth");
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

            return claimsPrincipal;
        }
    }
}
