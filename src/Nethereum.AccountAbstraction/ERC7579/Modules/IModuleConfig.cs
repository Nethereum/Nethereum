using System.Numerics;

namespace Nethereum.AccountAbstraction.ERC7579.Modules
{
    public interface IModuleConfig
    {
        BigInteger ModuleTypeId { get; }
        string ModuleAddress { get; set; }
        byte[] GetInitData();
        byte[] GetDeInitData();
    }

    public abstract class ModuleConfigBase : IModuleConfig
    {
        public abstract BigInteger ModuleTypeId { get; }
        public string ModuleAddress { get; set; }

        public abstract byte[] GetInitData();

        public virtual byte[] GetDeInitData()
        {
            return System.Array.Empty<byte>();
        }
    }
}
