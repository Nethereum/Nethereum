namespace Nethereum.BlockchainProcessing.Storage.Entities
{
    public interface IContractView
    {
        string ABI { get; }
        string Address { get; }
        string Code { get; }
        string Creator { get; }
        string Name { get; }
        string TransactionHash { get; }
    }
}