namespace Nethereum.CoreChain.Storage
{
    public interface IStateWriteBackpressure
    {
        bool ShouldPauseStateWrites();

        string DescribeStateBackpressure();
    }
}
