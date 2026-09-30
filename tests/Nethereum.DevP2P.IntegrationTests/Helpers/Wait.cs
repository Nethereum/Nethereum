using System;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Helpers
{
    public static class Wait
    {
        public static async Task UntilAsync(Func<bool> condition, TimeSpan timeout, string because = null)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (condition()) return;
                await Task.Delay(25);
            }
            Assert.True(condition(), because ?? "condition not met within timeout");
        }
    }
}
