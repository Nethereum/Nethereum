using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Nethereum.ABI;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.AccountAbstraction.ERC7579.Modules
{
    public class SocialRecoveryConfig : ModuleConfigBase
    {
        public override BigInteger ModuleTypeId => ERC7579ModuleTypes.TYPE_VALIDATOR;

        public BigInteger Threshold { get; set; } = 1;

        public List<string> Guardians { get; set; } = new List<string>();

        public SocialRecoveryConfig() { }

        public SocialRecoveryConfig(string moduleAddress, BigInteger threshold, params string[] guardians)
        {
            ModuleAddress = moduleAddress;
            Threshold = threshold;
            Guardians = guardians.ToList();
        }

        public static SocialRecoveryConfig Create(string moduleAddress, BigInteger threshold, params string[] guardians)
        {
            return new SocialRecoveryConfig(moduleAddress, threshold, guardians);
        }

        public SocialRecoveryConfig WithGuardian(string guardian)
        {
            Guardians.Add(guardian);
            return this;
        }

        public SocialRecoveryConfig WithThreshold(BigInteger threshold)
        {
            Threshold = threshold;
            return this;
        }

        public override byte[] GetInitData()
        {
            if (Guardians == null || Guardians.Count == 0)
                throw new InvalidOperationException("At least one guardian is required");

            if (Threshold <= 0 || Threshold > Guardians.Count)
                throw new InvalidOperationException("Threshold must be between 1 and the number of guardians");

            var abiEncode = new ABIEncode();
            return abiEncode.GetABIParamsEncoded(new SocialRecoveryInitData
            {
                Threshold = Threshold,
                Guardians = Guardians
            });
        }

        [FunctionOutput]
        private class SocialRecoveryInitData
        {
            [Parameter("uint256", "threshold", 1)]
            public BigInteger Threshold { get; set; }

            [Parameter("address[]", "guardians", 2)]
            public List<string> Guardians { get; set; }
        }
    }
}
