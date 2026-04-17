using System.Collections.Generic;
using Nethereum.CoreChain;
using Nethereum.EVM.BlockchainState;
using Nethereum.Merkle.Binary.Hashing;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EVM.Core.Tests
{
    public class BinaryStateRootCalculatorTests
    {
        private readonly ITestOutputHelper _output;

        public BinaryStateRootCalculatorTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void EmptyState_ReturnsZeroRoot()
        {
            var calc = new BinaryStateRootCalculator();
            var state = new ExecutionStateService(new InMemoryStateReader(new Dictionary<string, Nethereum.EVM.BlockchainState.AccountState>()));
            var root = calc.ComputeStateRoot(state);
            Assert.Equal(32, root.Length);
            Assert.Equal(new byte[32], root);
        }

        [Fact]
        public void SingleAccount_ReturnsNonZeroRoot()
        {
            var calc = new BinaryStateRootCalculator();
            var state = CreateStateWithOneAccount();
            var root = calc.ComputeStateRoot(state);
            Assert.Equal(32, root.Length);
            Assert.NotEqual(new byte[32], root);
            _output.WriteLine($"Root: {Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.ToHex(root, true)}");
        }

        [Fact]
        public void SameState_ProducesDeterministicRoot()
        {
            var calc = new BinaryStateRootCalculator();
            var state1 = CreateStateWithOneAccount();
            var state2 = CreateStateWithOneAccount();
            var root1 = calc.ComputeStateRoot(state1);
            var root2 = calc.ComputeStateRoot(state2);
            Assert.Equal(root1, root2);
        }

        [Fact]
        public void DifferentBalance_ProducesDifferentRoot()
        {
            var calc = new BinaryStateRootCalculator();
            var state1 = CreateStateWithOneAccount();
            var state2 = CreateStateWithOneAccount(balance: new EvmUInt256(999));
            var root1 = calc.ComputeStateRoot(state1);
            var root2 = calc.ComputeStateRoot(state2);
            Assert.NotEqual(root1, root2);
        }

        [Fact]
        public void AccountWithCode_IncludesCodeChunks()
        {
            var calc = new BinaryStateRootCalculator();
            var stateNoCode = CreateStateWithOneAccount();
            var stateWithCode = CreateStateWithOneAccount(code: new byte[] { 0x60, 0x42, 0x60, 0x00, 0x55, 0x00 });
            var root1 = calc.ComputeStateRoot(stateNoCode);
            var root2 = calc.ComputeStateRoot(stateWithCode);
            Assert.NotEqual(root1, root2);
        }

        [Fact]
        public void AccountWithStorage_IncludesStorageSlots()
        {
            var calc = new BinaryStateRootCalculator();
            var stateNoStorage = CreateStateWithOneAccount();
            var stateWithStorage = CreateStateWithOneAccount(
                storageSlots: new Dictionary<EvmUInt256, byte[]>
                {
                    { EvmUInt256.Zero, new byte[] { 0x42 } }
                });
            var root1 = calc.ComputeStateRoot(stateNoStorage);
            var root2 = calc.ComputeStateRoot(stateWithStorage);
            Assert.NotEqual(root1, root2);
        }

        [Fact]
        public void BinaryAndPatricia_ProduceDifferentRoots_ForSameState()
        {
            var binaryCalc = new BinaryStateRootCalculator();
            var patriciaCalc = new PatriciaStateRootCalculator(
                new Nethereum.Model.RlpBlockEncodingProvider());

            var state = CreateStateWithOneAccount(
                code: new byte[] { 0x60, 0x42, 0x60, 0x00, 0x55, 0x00 },
                storageSlots: new Dictionary<EvmUInt256, byte[]>
                {
                    { EvmUInt256.Zero, new byte[] { 0x42 } }
                });

            var binaryRoot = binaryCalc.ComputeStateRoot(state);
            var patriciaRoot = patriciaCalc.ComputeStateRoot(state);

            Assert.NotEqual(new byte[32], binaryRoot);
            Assert.NotEqual(binaryRoot, patriciaRoot);

            _output.WriteLine($"Binary root:   {Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.ToHex(binaryRoot, true)}");
            _output.WriteLine($"Patricia root: {Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.ToHex(patriciaRoot, true)}");
        }

        [Fact]
        public void PluggableHashProvider_Sha256_ProducesDifferentRoot()
        {
            var blake3Calc = new BinaryStateRootCalculator(new Blake3HashProvider());
            var sha256Calc = new BinaryStateRootCalculator(new Sha256HashProvider());

            var state = CreateStateWithOneAccount();
            var blake3Root = blake3Calc.ComputeStateRoot(state);
            var sha256Root = sha256Calc.ComputeStateRoot(state);

            Assert.NotEqual(blake3Root, sha256Root);
        }

        [Fact]
        public void PluggableHashProvider_Poseidon_ProducesDifferentRoot()
        {
            var blake3Calc = new BinaryStateRootCalculator(new Blake3HashProvider());
            var poseidonCalc = new BinaryStateRootCalculator(new PoseidonPairHashProvider());

            var state = CreateStateWithOneAccount(
                code: new byte[] { 0x60, 0x42, 0x60, 0x00, 0x55, 0x00 },
                storageSlots: new Dictionary<EvmUInt256, byte[]>
                {
                    { EvmUInt256.Zero, new byte[] { 0x42 } },
                    { (EvmUInt256)1, new byte[] { 0x01, 0x02, 0x03 } }
                });

            var blake3Root = blake3Calc.ComputeStateRoot(state);
            var poseidonRoot = poseidonCalc.ComputeStateRoot(state);

            Assert.Equal(32, poseidonRoot.Length);
            Assert.NotEqual(new byte[32], poseidonRoot);
            Assert.NotEqual(blake3Root, poseidonRoot);

            _output.WriteLine($"BLAKE3 root:   {Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.ToHex(blake3Root, true)}");
            _output.WriteLine($"Poseidon root: {Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.ToHex(poseidonRoot, true)}");
        }

        [Fact]
        public void Poseidon_SameState_Deterministic()
        {
            var calc = new BinaryStateRootCalculator(new PoseidonPairHashProvider());
            var state1 = CreateStateWithOneAccount();
            var state2 = CreateStateWithOneAccount();
            var root1 = calc.ComputeStateRoot(state1);
            var root2 = calc.ComputeStateRoot(state2);
            Assert.Equal(root1, root2);
        }

        private static ExecutionStateService CreateStateWithOneAccount(
            EvmUInt256? balance = null,
            byte[] code = null,
            Dictionary<EvmUInt256, byte[]> storageSlots = null)
        {
            var stateReader = new InMemoryStateReader(new Dictionary<string, Nethereum.EVM.BlockchainState.AccountState>());
            var state = new ExecutionStateService(stateReader);

            var address = "0x1000000000000000000000000000000000000000";
            var account = new AccountExecutionState { Address = address };
            account.Balance.SetInitialChainBalance(balance ?? new EvmUInt256(1000000000000000000UL));
            account.Nonce = (EvmUInt256)1;
            account.Code = code ?? new byte[0];

            if (storageSlots != null)
            {
                foreach (var kvp in storageSlots)
                    account.Storage[kvp.Key] = kvp.Value;
            }

            state.AccountsState[address] = account;
            return state;
        }
    }
}
