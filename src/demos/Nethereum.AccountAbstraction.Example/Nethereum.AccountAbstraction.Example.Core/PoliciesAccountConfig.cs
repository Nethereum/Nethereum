using System;

namespace Nethereum.AccountAbstraction.Example.Core
{
    public class PoliciesAccountConfig
    {
        public string SmartSessionAddress { get; }
        public string SudoPolicyAddress { get; }
        public string UniActionPolicyAddress { get; }
        public string SessionValidatorAddress { get; }

        public PoliciesAccountConfig(string smartSessionAddress, string sudoPolicyAddress, string uniActionPolicyAddress, string sessionValidatorAddress)
        {
            SmartSessionAddress = smartSessionAddress ?? throw new ArgumentNullException(nameof(smartSessionAddress));
            SudoPolicyAddress = sudoPolicyAddress ?? throw new ArgumentNullException(nameof(sudoPolicyAddress));
            UniActionPolicyAddress = uniActionPolicyAddress ?? throw new ArgumentNullException(nameof(uniActionPolicyAddress));
            SessionValidatorAddress = sessionValidatorAddress ?? throw new ArgumentNullException(nameof(sessionValidatorAddress));
        }
    }
}
