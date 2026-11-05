using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.State;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class ContractCodeAnalysisIsSharedAcrossTransactionsTests
    {
        private const string PrivateKey = "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string SenderAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private const string ContractAddress = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";
        private static readonly BigInteger ChainId = 1337;
        private static readonly byte[] ContractCode = "0x600150600260030150600560060250".HexToByteArray();

        private static ISignedTransaction CallContract(BigInteger nonce) =>
            TransactionFactory.CreateTransaction(new LegacyTransactionSigner().SignTransaction(
                PrivateKey.HexToByteArray(), ChainId, ContractAddress, 0, nonce, 2_000_000_000, 100_000, ""));

        private static BlockContext BlockContextForTheBlock() => new BlockContext
        {
            BlockNumber = 1,
            Timestamp = 1_700_000_000,
            Coinbase = SenderAddress,
            GasLimit = 30_000_000,
            BaseFee = 0,
            ChainId = ChainId,
            Difficulty = 0,
            PrevRandao = new byte[32],
            ExcessBlobGas = 0
        };

        private sealed class StateReaderThatReadsCodeAsFreshArrays : IStateReader
        {
            private readonly IStateReader _inner;

            public StateReaderThatReadsCodeAsFreshArrays(IStateReader inner) { _inner = inner; }

            public Task<EvmUInt256> GetBalanceAsync(byte[] address) => _inner.GetBalanceAsync(address);
            public Task<EvmUInt256> GetBalanceAsync(string address) => _inner.GetBalanceAsync(address);
            public async Task<byte[]> GetCodeAsync(byte[] address) => (byte[])(await _inner.GetCodeAsync(address))?.Clone();
            public async Task<byte[]> GetCodeAsync(string address) => (byte[])(await _inner.GetCodeAsync(address))?.Clone();
            public Task<byte[]> GetStorageAtAsync(byte[] address, EvmUInt256 position) => _inner.GetStorageAtAsync(address, position);
            public Task<byte[]> GetStorageAtAsync(string address, EvmUInt256 position) => _inner.GetStorageAtAsync(address, position);
            public Task<EvmUInt256> GetTransactionCountAsync(byte[] address) => _inner.GetTransactionCountAsync(address);
            public Task<EvmUInt256> GetTransactionCountAsync(string address) => _inner.GetTransactionCountAsync(address);
            public Task<bool> AccountExistsAsync(string address) => _inner.AccountExistsAsync(address);
            public Task<byte[]> GetBlockHashAsync(long blockNumber) => _inner.GetBlockHashAsync(blockNumber);
        }

        [Fact]
        public async Task Given_TheSameContractCalledInTwoTransactions_When_TheBlockExecutes_Then_EachTraceOwnsInstructionsDecodedFromTheSameCode()
        {
            var stateStore = new InMemoryStateStore();
            await stateStore.SaveAccountAsync(SenderAddress, new Account { Balance = 1_000_000_000_000_000, Nonce = 0 });
            var codeHash = new Sha3Keccack().CalculateHash(ContractCode);
            await stateStore.SaveCodeAsync(codeHash, ContractCode);
            await stateStore.SaveAccountAsync(ContractAddress, new Account { Balance = 0, Nonce = 1, CodeHash = codeHash });

            var config = new ChainConfig { ChainId = ChainId, BlockGasLimit = 30_000_000, BaseFee = 0, Hardfork = "Prague" };
            var processor = new TransactionProcessor(
                stateStore, new InMemoryBlockStore(), config,
                new TransactionVerificationAndRecoveryImp(), config.GetHardforkConfig());

            var reader = new StateReaderThatReadsCodeAsFreshArrays(new StateStoreNodeDataService(stateStore));

            var first = await processor.ExecuteTransactionAsync(
                CallContract(0), BlockContextForTheBlock(), txIndex: 0, cumulativeGasUsed: 0, stateReader: reader, traceEnabled: true);
            var second = await processor.ExecuteTransactionAsync(
                CallContract(1), BlockContextForTheBlock(), txIndex: 1, cumulativeGasUsed: 0, stateReader: reader, traceEnabled: true);

            Assert.NotEmpty(first.Traces);
            Assert.NotEmpty(second.Traces);
            var firstInstruction = first.Traces.First().Instruction;
            var secondInstruction = second.Traces.First().Instruction;
            Assert.NotSame(firstInstruction, secondInstruction);
            Assert.Equal(firstInstruction.Step, secondInstruction.Step);
            Assert.Equal(firstInstruction.Instruction, secondInstruction.Instruction);
            Assert.Equal(firstInstruction.Arguments, secondInstruction.Arguments);
        }
    }
}
