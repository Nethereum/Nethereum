using System;

namespace Nethereum.AccountAbstraction.Example.Core
{
    public class Eip7702AccountConfig
    {
        public string AccountImplementationAddress { get; }
        public string EcdsaValidatorAddress { get; }

        public Eip7702AccountConfig(string accountImplementationAddress, string ecdsaValidatorAddress)
        {
            AccountImplementationAddress = accountImplementationAddress ?? throw new ArgumentNullException(nameof(accountImplementationAddress));
            EcdsaValidatorAddress = ecdsaValidatorAddress ?? throw new ArgumentNullException(nameof(ecdsaValidatorAddress));
        }
    }
}
