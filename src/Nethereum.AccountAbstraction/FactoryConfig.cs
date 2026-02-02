using System.Numerics;

namespace Nethereum.AccountAbstraction
{
    public class FactoryConfig
    {
        public string FactoryAddress { get; set; }
        public string Owner { get; set; }
        public BigInteger Salt { get; set; } = 0;

        public FactoryConfig() { }

        public FactoryConfig(string factoryAddress, string owner, BigInteger? salt = null)
        {
            FactoryAddress = factoryAddress;
            Owner = owner;
            Salt = salt ?? 0;
        }
    }
}
