using Nethereum.RPC.Eth.Blocks;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.RPC.IntegrationTests.Testers.Service
{
    public class LastConfirmedBlockNumberServiceTester : ServiceTestBase
    {
        [Fact]
        public async Task LastConfirmedBlockIsBeforeTheLatestBlock()
        {
            SetupBlockNumberMock(new BigInteger(120));
            const uint MIN_CONFIRMATIONS = 12;
            var fromBlockNumber = new BigInteger(100);
            var lastConfirmedBlockNumberService = new LastConfirmedBlockNumberService(_blockNumberMock.Object, MIN_CONFIRMATIONS);
            var lastConfirmedBlockNumber = await lastConfirmedBlockNumberService.GetLastConfirmedBlockNumberAsync(fromBlockNumber, new CancellationTokenSource().Token);
            //assert
            Assert.Equal(lastConfirmedBlockNumber, new BigInteger(108));
        }
    }
}
