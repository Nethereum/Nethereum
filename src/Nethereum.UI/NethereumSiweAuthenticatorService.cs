using Nethereum.Siwe;
using Nethereum.Siwe.Core;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Nethereum.UI
{
    public class NethereumSiweAuthenticatorService
    {
        private readonly SiweMessageService _siweMessageService;
        private readonly SelectedEthereumHostProviderService _selectedEthereumHostProviderService;

        public NethereumSiweAuthenticatorService(SelectedEthereumHostProviderService selectedEthereumHostProviderService, ISessionStorage sessionStorage)
        {
            _siweMessageService = new SiweMessageService(sessionStorage);
            _selectedEthereumHostProviderService = selectedEthereumHostProviderService;
        }

        public string GenerateNewSiweMessage(SiweMessage siweMessage)
        {
            return _siweMessageService.BuildMessageToSign(siweMessage);
        }

        public async Task<SiweMessage> AuthenticateAsync(SiweMessage siweMessage)
        {
            var host = _selectedEthereumHostProviderService.SelectedHost;
            if (host == null || !host.Available)
            {
                throw new Exception("Cannot authenticate user, an Ethereum host is not available");
            }

            var challenge = GenerateNewSiweMessage(siweMessage);
            var signedMessage = await host.SignMessageAsync(challenge).ConfigureAwait(false);
            if (await _siweMessageService.IsMessageSignatureValid(siweMessage, signedMessage).ConfigureAwait(false))
            {
                return siweMessage;
            }
            
            throw new Exception("SiweMessage signed with an invalid Address");
        }

        public void LogOut(SiweMessage siweMessage)
        {
            _siweMessageService.InvalidateSession(siweMessage);
        }

    }
}

