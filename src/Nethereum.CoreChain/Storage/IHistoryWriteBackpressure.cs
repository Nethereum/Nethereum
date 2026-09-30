namespace Nethereum.CoreChain.Storage
{
    public interface IHistoryWriteBackpressure
    {
        bool ShouldPauseHistoryWrites();

        string DescribeHistoryBackpressure();
    }
}
