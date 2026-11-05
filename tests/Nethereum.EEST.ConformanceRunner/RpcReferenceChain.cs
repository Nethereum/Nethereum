using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Node.HarnessServer;
using Newtonsoft.Json.Linq;

namespace Nethereum.EEST.ConformanceRunner
{
    public sealed class RpcReferenceChain : IAsyncDisposable
    {
        public RpcDispatcher Dispatcher { get; }
        public int ImportedBlocks { get; }
        public int ExpectedBlocks { get; }
        private readonly HarnessNode _harness;

        public Nethereum.CoreChain.Storage.IStateStore State => _harness.Node.State;

        private RpcReferenceChain(HarnessNode harness, int imported, int expected)
        {
            _harness = harness;
            Dispatcher = harness.Dispatcher;
            ImportedBlocks = imported;
            ExpectedBlocks = expected;
        }

        public static async Task<RpcReferenceChain> CreateAsync()
        {
            var testsRoot = FixtureProvisioning.ExecutionApisTestsRoot;
            var environment = JObject.Parse(File.ReadAllText(Path.Combine(testsRoot, "forkenv.json")))
                .Properties().ToDictionary(p => p.Name, p => (string)p.Value);
            environment.TryAdd(HiveGenesisMapping.TargetGasLimitVariable, HiveGenesisMapping.DefaultTargetGasLimit.ToString());

            var document = HarnessNodeHost.LoadGenesis(Path.Combine(testsRoot, "genesis.json"), environment);

            var harness = await HarnessNodeHost.ComposeInProcessAsync(
                document, targetGasLimit: HiveGenesisMapping.TargetGasLimit(environment)).ConfigureAwait(false);

            var summary = await new RlpBlockFileImporter(harness.Importer)
                .ImportAsync(Path.Combine(testsRoot, "chain.rlp"), blocksDirectory: string.Empty).ConfigureAwait(false);

            var headForkchoice = JsonSerializer.Deserialize(
                File.ReadAllText(Path.Combine(testsRoot, "headfcu.json")), CoreChainJsonContext.Default.JsonRpcRequest);
            await harness.EngineDispatcher.DispatchAsync(headForkchoice.ToRpcRequestMessage()).ConfigureAwait(false);

            return new RpcReferenceChain(harness, summary.Imported, summary.Imported + summary.Rejected);
        }

        public ValueTask DisposeAsync() => _harness.DisposeAsync();
    }
}
