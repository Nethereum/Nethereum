using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Nethereum.ABI;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.AccountAbstraction.ERC7579.Modules
{
    public class OwnableValidatorConfig : ModuleConfigBase
    {
        public override BigInteger ModuleTypeId => ERC7579ModuleTypes.TYPE_VALIDATOR;

        public BigInteger Threshold { get; set; } = 1;

        public List<string> Owners { get; set; } = new List<string>();

        public OwnableValidatorConfig() { }

        public OwnableValidatorConfig(string moduleAddress, BigInteger threshold, params string[] owners)
        {
            ModuleAddress = moduleAddress;
            Threshold = threshold;
            Owners = owners.ToList();
        }

        public static OwnableValidatorConfig Create(string moduleAddress, BigInteger threshold, params string[] owners)
        {
            return new OwnableValidatorConfig(moduleAddress, threshold, owners);
        }

        public OwnableValidatorConfig WithOwner(string owner)
        {
            Owners.Add(owner);
            return this;
        }

        public OwnableValidatorConfig WithThreshold(BigInteger threshold)
        {
            Threshold = threshold;
            return this;
        }

        public override byte[] GetInitData()
        {
            if (Owners == null || Owners.Count == 0)
                throw new InvalidOperationException("At least one owner is required");

            if (Threshold <= 0 || Threshold > Owners.Count)
                throw new InvalidOperationException("Threshold must be between 1 and the number of owners");

            var abiEncode = new ABIEncode();
            return abiEncode.GetABIParamsEncoded(new OwnableValidatorInitData
            {
                Threshold = Threshold,
                Owners = Owners
            });
        }

        [FunctionOutput]
        private class OwnableValidatorInitData
        {
            [Parameter("uint256", "threshold", 1)]
            public BigInteger Threshold { get; set; }

            [Parameter("address[]", "owners", 2)]
            public List<string> Owners { get; set; }
        }
    }
}
