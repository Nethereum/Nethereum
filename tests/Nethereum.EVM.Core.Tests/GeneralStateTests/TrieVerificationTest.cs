using System.Collections.Generic;
using System.Numerics;
using Nethereum.CoreChain;
using Nethereum.CoreChain.IntegrationTests.BlockchainTests;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EVM.Core.Tests.GeneralStateTests
{
    public class TrieVerificationTest
    {
        private readonly ITestOutputHelper _output;
        public TrieVerificationTest(ITestOutputHelper output) { _output = output; }

        [Fact]
        public void PostState_GasLimitTooHigh2_MatchesExpectedRoot()
        {
            var tests = BlockchainTestLoader.LoadFromFile(
                FindTestFile("ValidBlocks/bcValidBlockTest/gasLimitTooHigh2.json"));

            foreach (var test in tests)
            {
                if (!test.Name.Contains("Cancun")) continue;

                var expectedRoot = test.Blocks[0].BlockHeader.StateRoot;
                _output.WriteLine($"Expected state root: 0x{expectedRoot.ToHex()}");

                // Build state from postState directly
                var encoding = RlpBlockEncodingProvider.Instance;
                var postAccounts = new List<WitnessAccount>();
                foreach (var kvp in test.PostState)
                {
                    var acc = new WitnessAccount
                    {
                        Address = kvp.Key,
                        Balance = EvmUInt256BigIntegerExtensions.FromBigInteger(kvp.Value.Balance),
                        Nonce = (long)kvp.Value.Nonce,
                        Code = kvp.Value.Code ?? new byte[0],
                        Storage = new List<WitnessStorageSlot>()
                    };
                    foreach (var s in kvp.Value.Storage)
                    {
                        acc.Storage.Add(new WitnessStorageSlot
                        {
                            Key = EvmUInt256BigIntegerExtensions.FromBigInteger(s.Key),
                            Value = EvmUInt256BigIntegerExtensions.FromBigInteger(s.Value)
                        });
                    }
                    postAccounts.Add(acc);
                }

                var accountState = WitnessStateBuilder.BuildAccountState(postAccounts);
                var stateReader = new InMemoryStateReader(accountState);
                var es = new ExecutionStateService(stateReader);
                WitnessStateBuilder.LoadAllAccountsAndStorage(es, postAccounts);

                var root = new PatriciaStateRootCalculator(encoding).ComputeStateRoot(es);
                _output.WriteLine($"Computed root:      0x{root.ToHex()}");

                Assert.True(root.AreTheSame(expectedRoot),
                    $"Post-state root mismatch: expected=0x{expectedRoot.ToHex()} actual=0x{root.ToHex()}");
                break;
            }
        }

        private static string FindTestFile(string relativePath)
        {
            var dir = new System.IO.DirectoryInfo(System.IO.Directory.GetCurrentDirectory());
            while (dir != null)
            {
                if (System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "Nethereum.sln")))
                    return System.IO.Path.Combine(dir.FullName, "external", "ethereum-tests", "BlockchainTests", relativePath);
                dir = dir.Parent;
            }
            return null;
        }
    }
}
