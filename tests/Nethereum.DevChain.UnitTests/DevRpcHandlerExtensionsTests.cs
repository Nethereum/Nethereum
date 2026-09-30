using System.Linq;
using Nethereum.CoreChain.Rpc;
using Nethereum.DevChain.Rpc;
using Nethereum.DevChain.Rpc.Handlers;
using Nethereum.DevChain.Rpc.Handlers.Dev;
using Xunit;

namespace Nethereum.DevChain.UnitTests
{
    public class DevRpcHandlerExtensionsTests
    {
        [Fact]
        public void CreateDevChainRegistry_RegistersThePriorAddDevChainServerSurfacePlusDebugSetHead()
        {
            var expected = new RpcHandlerRegistry();
            expected.AddStandardHandlers();

            expected.Register(new EvmMineHandler());
            expected.Register(new EvmSnapshotHandler());
            expected.Register(new EvmRevertHandler());
            expected.Register(new HardhatSetBalanceHandler());
            expected.Register(new HardhatSetCodeHandler());
            expected.Register(new HardhatSetStorageAtHandler());
            expected.Register(new HardhatSetNonceHandler());
            expected.Register(new EvmIncreaseTimeHandler());
            expected.Register(new EvmSetNextBlockTimestampHandler());

            expected.AddAnvilAliases();

            expected.Override(new EthAccountsHandler());
            expected.Register(new HardhatImpersonateAccountHandler());
            expected.Register(new HardhatStopImpersonatingAccountHandler());

            var expectedMethodNames = expected.GetAllMethodNames()
                .Select(n => n.ToLowerInvariant())
                .Concat(new[] { "debug_sethead" })
                .OrderBy(n => n)
                .ToArray();

            var actual = DevRpcHandlerExtensions.CreateDevChainRegistry();
            var actualMethodNames = actual.GetAllMethodNames()
                .Select(n => n.ToLowerInvariant())
                .OrderBy(n => n)
                .ToArray();

            Assert.Equal(expectedMethodNames, actualMethodNames);
        }
    }
}
