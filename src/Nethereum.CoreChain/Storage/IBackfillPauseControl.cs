namespace Nethereum.CoreChain.Storage
{
    public interface IBackfillPauseControl
    {
        bool ShouldPauseBackfill();

        string DescribeBackfillPause();

        void SetYieldToExecution(bool armed) { }

        bool ShouldYieldToExecution() => false;

        string DescribeYieldToExecution() => null;
    }
}
