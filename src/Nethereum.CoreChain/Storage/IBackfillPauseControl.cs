namespace Nethereum.CoreChain.Storage
{
    public interface IBackfillPauseControl
    {
        bool ShouldPauseBackfill();

        string DescribeBackfillPause();
    }
}
