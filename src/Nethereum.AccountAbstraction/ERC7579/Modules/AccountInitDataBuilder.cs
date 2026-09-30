using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace Nethereum.AccountAbstraction.ERC7579.Modules
{
    public static class AccountInitDataBuilder
    {
        public static byte[] Build(IModuleConfig validator)
        {
            return ByteUtil.Merge(validator.ModuleAddress.HexToByteArray(), validator.GetInitData());
        }

        public static byte[] BuildEcdsa(string validatorAddress, string ownerAddress)
        {
            return Build(new ECDSAValidatorConfig(validatorAddress, ownerAddress));
        }
    }
}
