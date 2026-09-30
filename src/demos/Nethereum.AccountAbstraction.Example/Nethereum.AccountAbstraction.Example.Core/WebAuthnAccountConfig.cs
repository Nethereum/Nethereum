using System;

namespace Nethereum.AccountAbstraction.Example.Core
{
    public class WebAuthnAccountConfig
    {
        public string ValidatorAddress { get; }
        public string RpId { get; }

        public WebAuthnAccountConfig(string validatorAddress, string rpId)
        {
            ValidatorAddress = validatorAddress ?? throw new ArgumentNullException(nameof(validatorAddress));
            RpId = rpId ?? throw new ArgumentNullException(nameof(rpId));
        }
    }
}
