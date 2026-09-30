using System;

namespace Nethereum.AccountAbstraction.Example.Core
{
    public class SocialRecoveryAccountConfig
    {
        public string SocialRecoveryAddress { get; }
        public string EcdsaValidatorAddress { get; }

        public SocialRecoveryAccountConfig(string socialRecoveryAddress, string ecdsaValidatorAddress)
        {
            SocialRecoveryAddress = socialRecoveryAddress ?? throw new ArgumentNullException(nameof(socialRecoveryAddress));
            EcdsaValidatorAddress = ecdsaValidatorAddress ?? throw new ArgumentNullException(nameof(ecdsaValidatorAddress));
        }
    }
}
