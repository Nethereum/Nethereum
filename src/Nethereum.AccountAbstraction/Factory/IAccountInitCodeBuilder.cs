namespace Nethereum.AccountAbstraction.Factory
{
    public interface IAccountInitCodeBuilder
    {
        string FactoryAddress { get; }

        byte[] BuildFactoryData();
    }
}
