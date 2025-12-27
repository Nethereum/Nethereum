namespace Nethereum.TokenServices.MultiAccount.Models
{
    public class MultiAccountScanOptions
    {
        public int MaxParallelChains { get; set; } = 3;
        public int PageSize { get; set; } = 100;
        public int DelayBetweenPagesMs { get; set; } = 300;
        public bool IncludeZeroBalances { get; set; } = false;
        public bool IncludeNativeToken { get; set; } = true;
    }
}
