using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.EEST.ConformanceRunner
{
    [Collection(RpcCompatCollection.Name)]
    public class SimulateReadOnlyTests
    {
        // System-call contracts warm-start dirties every block: EIP-4788 beacon roots, EIP-2935 history.
        private const string BeaconRoots4788 = "0x000F3df6D732807Ef1319fB7B8bB8522d0Beac02";
        private const string HistoryStorage2935 = "0x0000F90827F1C53a10cb7A02335B175320002935";

        private static string VectorPath(string name) =>
            Path.Combine(FixtureProvisioning.ExecutionApisTestsRoot, "eth_simulateV1", name);

        [Fact]
        public async Task Given_ASimulateWithStorageWrites_When_ItCompletes_Then_TheSharedNodesAccountsAndStateRootFieldsAreByteIdenticalToBefore()
        {
            await using var chain = await RpcReferenceChain.CreateAsync();

            var beacon4788Before = await StateRootHexAsync(chain.State, BeaconRoots4788);
            var history2935Before = await StateRootHexAsync(chain.State, HistoryStorage2935);
            var before = await SnapshotAsync(chain.State);

            var exchange = RpcCompatVectorLoader.Load(VectorPath("ethSimulate-simple.io")).Single();
            await RpcCompatDriver.Instance.RunAsync(chain, exchange);

            var beacon4788After = await StateRootHexAsync(chain.State, BeaconRoots4788);
            var history2935After = await StateRootHexAsync(chain.State, HistoryStorage2935);
            var after = await SnapshotAsync(chain.State);

            Assert.True(beacon4788Before == beacon4788After,
                $"simulate mutated the shared EIP-4788 beacon-roots account StateRoot ({BeaconRoots4788}): " +
                $"before {beacon4788Before} != after {beacon4788After}");
            Assert.True(history2935Before == history2935After,
                $"simulate mutated the shared EIP-2935 history-storage account StateRoot ({HistoryStorage2935}): " +
                $"before {history2935Before} != after {history2935After}");

            AssertSnapshotsEqual(before, after);
        }

        [Fact]
        public async Task Given_OtherSimulateVectorsHaveRunOnTheSameChain_When_TheEmptySimulateRunsAgain_Then_ItReturnsTheSameGethRoot()
        {
            await using var chain = await RpcReferenceChain.CreateAsync();
            var exchange = RpcCompatVectorLoader.Load(VectorPath("ethSimulate-empty.io")).Single();

            var first = await RpcCompatDriver.Instance.RunAsync(chain, exchange);
            Assert.True(first.Success, $"first empty simulate should match the geth root: [{first.Kind}] {first.Detail}");

            var second = await RpcCompatDriver.Instance.RunAsync(chain, exchange);
            Assert.True(second.Success,
                $"second empty simulate on the same shared chain must return the same geth root " +
                $"(contamination regression): [{second.Kind}] {second.Detail}");
        }

        private static async Task<string> StateRootHexAsync(IStateStore state, string address)
        {
            var account = await state.GetAccountAsync(address);
            return account?.StateRoot == null ? "<null>" : account.StateRoot.ToHex();
        }

        private static async Task<Dictionary<string, string>> SnapshotAsync(IStateStore state)
        {
            var snapshot = new Dictionary<string, string>();
            var accounts = await state.GetAllAccountsAsync();
            foreach (var kv in accounts)
            {
                var address = kv.Key;
                var account = kv.Value;
                var storage = await state.GetAllStorageAsync(address);
                var storageStr = string.Join(",", storage
                    .Select(s => (s.Key == null ? "" : s.Key.ToHex()) + "=" + (s.Value == null ? "" : s.Value.ToHex()))
                    .OrderBy(s => s, System.StringComparer.Ordinal));

                snapshot[address.ToLowerInvariant()] =
                    $"nonce={account.Nonce}|balance={account.Balance}|" +
                    $"codeHash={(account.CodeHash == null ? "" : account.CodeHash.ToHex())}|" +
                    $"stateRoot={(account.StateRoot == null ? "" : account.StateRoot.ToHex())}|" +
                    $"storage=[{storageStr}]";
            }
            return snapshot;
        }

        private static void AssertSnapshotsEqual(
            Dictionary<string, string> before, Dictionary<string, string> after)
        {
            Assert.Equal(before.Count, after.Count);
            foreach (var kv in before)
            {
                Assert.True(after.TryGetValue(kv.Key, out var afterValue),
                    $"account {kv.Key} present before simulate but missing after");
                Assert.True(kv.Value == afterValue,
                    $"simulate mutated shared account {kv.Key}:\n  before: {kv.Value}\n  after:  {afterValue}");
            }
        }
    }
}
