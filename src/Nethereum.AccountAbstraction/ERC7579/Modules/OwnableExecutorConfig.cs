using System;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.AccountAbstraction.ERC7579.Modules
{
    public class OwnableExecutorConfig : ModuleConfigBase
    {
        public override BigInteger ModuleTypeId => ERC7579ModuleTypes.TYPE_EXECUTOR;

        public string Owner { get; set; }

        public OwnableExecutorConfig() { }

        public OwnableExecutorConfig(string moduleAddress, string owner)
        {
            ModuleAddress = moduleAddress;
            Owner = owner;
        }

        public static OwnableExecutorConfig Create(string moduleAddress, string owner)
        {
            return new OwnableExecutorConfig(moduleAddress, owner);
        }

        public override byte[] GetInitData()
        {
            if (string.IsNullOrEmpty(Owner))
                throw new InvalidOperationException("Owner address is required");

            return Owner.HexToByteArray();
        }
    }
}
