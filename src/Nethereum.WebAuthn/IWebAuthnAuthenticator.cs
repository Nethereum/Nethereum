using System.Threading.Tasks;

namespace Nethereum.WebAuthn
{
    public interface IWebAuthnAuthenticator
    {
        Task<WebAuthnAssertion> GetAssertionAsync(byte[] challenge, byte[] credentialId, string rpId);
    }
}
