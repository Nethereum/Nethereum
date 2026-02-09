using System;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.AccountAbstraction.ERC7579.Modules
{
    public class ECDSAValidatorConfig : ModuleConfigBase
    {
        public override BigInteger ModuleTypeId => ERC7579ModuleTypes.TYPE_VALIDATOR;

        public string Owner { get; set; }

        public ECDSAValidatorConfig() { }

        public ECDSAValidatorConfig(string moduleAddress, string owner)
        {
            ModuleAddress = moduleAddress;
            Owner = owner;
        }

        public static ECDSAValidatorConfig Create(string moduleAddress, string owner)
        {
            return new ECDSAValidatorConfig(moduleAddress, owner);
        }

        public override byte[] GetInitData()
        {
            if (string.IsNullOrEmpty(Owner))
                throw new InvalidOperationException("Owner address is required");

            return Owner.HexToByteArray();
        }
    }
}
