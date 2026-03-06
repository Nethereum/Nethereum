namespace Nethereum.BlockchainProcessing.ProgressRepositories
{
    public interface IChainStateRepositoryFactory
    {
        IChainStateRepository CreateChainStateRepository();
    }
}
