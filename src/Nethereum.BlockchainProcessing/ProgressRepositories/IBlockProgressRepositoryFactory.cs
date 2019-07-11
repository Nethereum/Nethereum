namespace Nethereum.BlockchainProcessing.ProgressRepositories
{
    public interface IBlockProgressRepositoryFactory
    {
        IBlockProgressRepository CreateBlockProgressRepository();
    }
}
