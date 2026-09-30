using System;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.WebAuthn;

namespace Nethereum.AccountAbstraction.WebAuthn.ERC7579.Modules
{
    public class WebAuthnValidatorConfig : ModuleConfigBase
    {
        public override BigInteger ModuleTypeId => ERC7579ModuleTypes.TYPE_VALIDATOR;

        public BigInteger Threshold { get; set; } = 1;

        public List<WebAuthnCredential> Credentials { get; set; } = new List<WebAuthnCredential>();

        public WebAuthnValidatorConfig() { }

        public WebAuthnValidatorConfig(string moduleAddress, BigInteger threshold, params WebAuthnCredential[] credentials)
        {
            ModuleAddress = moduleAddress;
            Threshold = threshold;
            Credentials = new List<WebAuthnCredential>(credentials);
        }

        public override byte[] GetInitData()
        {
            if (Credentials == null || Credentials.Count == 0)
                throw new InvalidOperationException("At least one WebAuthn credential is required");

            if (Threshold <= 0 || Threshold > Credentials.Count)
                throw new InvalidOperationException("Threshold must be between 1 and the number of credentials");

            return WebAuthnValidatorFormat.EncodeInstallData(Threshold, Credentials);
        }
    }
}
