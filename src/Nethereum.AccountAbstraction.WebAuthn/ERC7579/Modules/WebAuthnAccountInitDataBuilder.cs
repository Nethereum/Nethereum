using System.Numerics;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.WebAuthn;
using CoreAccountInitDataBuilder = Nethereum.AccountAbstraction.ERC7579.Modules.AccountInitDataBuilder;

namespace Nethereum.AccountAbstraction.WebAuthn.ERC7579.Modules
{
    public static class WebAuthnAccountInitDataBuilder
    {
        public static byte[] BuildWebAuthn(string validatorAddress, BigInteger pubKeyX, BigInteger pubKeyY, bool requireUV, BigInteger? threshold = null)
        {
            return CoreAccountInitDataBuilder.Build(
                new WebAuthnValidatorConfig(validatorAddress, threshold ?? 1, new WebAuthnCredential(pubKeyX, pubKeyY, requireUV)));
        }
    }
}
