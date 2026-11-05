using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Merkle.Binary.Hashing;
using Nethereum.Model;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class BinaryStateRootDirtyAccountsByAddressTests
    {
        public interface IRawStateStore : IStateStore, IRawStorageEnumerator
        {
        }

        public class CallRecordingProxy : DispatchProxy
        {
            public InMemoryStateStore Inner { get; set; } = null!;
            public List<string> Calls { get; } = new List<string>();

            protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            {
                if (targetMethod == null) return null;
                var parameter = args is { Length: > 0 } ? args[0]?.GetType().Name : "";
                Calls.Add(targetMethod.Name + "(" + parameter + ")");
                try { return targetMethod.Invoke(Inner, args); }
                catch (TargetInvocationException ex) when (ex.InnerException != null)
                {
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                    throw;
                }
            }
        }

        private static async Task SeedAsync(IStateStore store, string address, ulong balance)
        {
            await store.SaveAccountAsync(address, new Account
            {
                Balance = new EvmUInt256(balance),
                Nonce = 1,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
                StateRoot = DefaultValues.EMPTY_TRIE_HASH
            });
            await store.SaveStorageAsync(address, BigInteger.One, new byte[] { 0x42 });
        }

        [Fact]
        public async Task Given_dirty_accounts_after_a_full_build_When_the_binary_calculator_updates_incrementally_Then_it_reads_them_by_EvmAddress_and_the_root_equals_a_full_rebuild()
        {
            const string address = "0x1000000000000000000000000000000000000000";
            var inner = new InMemoryStateStore();
            await SeedAsync(inner, address, 1000);
            var proxied = DispatchProxy.Create<IRawStateStore, CallRecordingProxy>();
            var proxy = (CallRecordingProxy)(object)proxied;
            proxy.Inner = inner;

            var calculator = new BinaryIncrementalStateRootCalculator(proxied, new Blake3HashProvider());
            await calculator.ComputeStateRootAsync();
            await SeedAsync(inner, address, 2000);
            proxy.Calls.Clear();
            var incrementalRoot = await calculator.ComputeStateRootAsync();

            var fullRoot = await new BinaryIncrementalStateRootCalculator(inner, new Blake3HashProvider()).ComputeFullStateRootAsync();
            Assert.Equal(fullRoot, incrementalRoot);
            Assert.Contains(proxy.Calls, c => c.StartsWith("GetDirtyAccountsAsync"));
            Assert.DoesNotContain(proxy.Calls, c => c.StartsWith("GetDirtyAccountAddressesAsync"));
            Assert.DoesNotContain(proxy.Calls, c => c == "GetDirtyStorageSlotsAsync(String)");
        }
    }
}
