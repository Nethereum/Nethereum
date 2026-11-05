using Nethereum.EEST.Conformance.Diagnosis;
using Nethereum.Testing;

namespace Nethereum.EVM.UnitTests.GeneralStateTests
{
    public static class GethTools
    {
        public static GethEvmRunner EvmRunner() => new GethEvmRunner(TestEnvironment.Tool("Evm"));

        public static GethT8nRunner T8nRunner() => new GethT8nRunner(TestEnvironment.Tool("Evm"));
    }
}
