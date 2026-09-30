namespace Nethereum.EEST.ConformanceRunner
{
    public interface IConformanceDriver
    {
        string SuiteId { get; }

        string SuiteName { get; }
    }
}
