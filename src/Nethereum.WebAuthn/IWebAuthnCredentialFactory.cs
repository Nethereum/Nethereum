using System.Threading.Tasks;

namespace Nethereum.WebAuthn
{
    public interface IWebAuthnCredentialFactory
    {
        Task<WebAuthnCreatedCredential> CreateCredentialAsync(WebAuthnCredentialCreationOptions options);
    }
}
