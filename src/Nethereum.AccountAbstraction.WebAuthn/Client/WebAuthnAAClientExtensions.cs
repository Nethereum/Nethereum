using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.WebAuthn.ERC7579.Modules;
using Nethereum.AccountAbstraction.WebAuthn.Signing;
using Nethereum.WebAuthn;

namespace Nethereum.AccountAbstraction.WebAuthn.Client
{
    public static class WebAuthnAAClientExtensions
    {
        public static Task<NethereumSmartAccount> CreateWebAuthnAccountAsync(
            this IAAClient client,
            WebAuthnCreatedCredential credential,
            IWebAuthnAuthenticator authenticator,
            string validatorAddress,
            string rpId,
            bool usePrecompile = false,
            byte[]? salt = null)
        {
            var initData = WebAuthnAccountInitDataBuilder.BuildWebAuthn(
                validatorAddress, credential.PubKeyX, credential.PubKeyY, credential.RequireUserVerification);
            var validator = new WebAuthnValidatorModule(validatorAddress, usePrecompile);
            var signingService = new WebAuthnAccountSigningService(
                authenticator, credential.OnChainCredentialId, rpId, usePrecompile, credential.PlatformCredentialId);

            return client.CreateAccountAsync(signingService, validator, initData, salt);
        }

        public static NethereumSmartAccount GetWebAuthnAccount(
            this IAAClient client,
            string address,
            WebAuthnCreatedCredential credential,
            IWebAuthnAuthenticator authenticator,
            string validatorAddress,
            string rpId,
            bool usePrecompile = false)
        {
            var validator = new WebAuthnValidatorModule(validatorAddress, usePrecompile);
            var signingService = new WebAuthnAccountSigningService(
                authenticator, credential.OnChainCredentialId, rpId, usePrecompile, credential.PlatformCredentialId);

            return client.GetAccount(address, signingService, validator);
        }
    }
}
