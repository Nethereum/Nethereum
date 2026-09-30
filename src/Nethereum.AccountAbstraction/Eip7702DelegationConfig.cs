namespace Nethereum.AccountAbstraction
{
    public class Eip7702DelegationConfig
    {
        public string DelegateAddress { get; set; }
        public byte[] FactoryData { get; set; }

        public Eip7702DelegationConfig() { }

        public Eip7702DelegationConfig(string delegateAddress, byte[] factoryData = null)
        {
            DelegateAddress = delegateAddress;
            FactoryData = factoryData;
        }
    }
}
