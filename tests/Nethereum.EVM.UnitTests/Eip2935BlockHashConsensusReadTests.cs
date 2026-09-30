using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.State;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution.Opcodes.Executors;
using Nethereum.EVM.Execution.Opcodes.Executors.Rules;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests
{
    public class Eip2935BlockHashConsensusReadTests
    {
        private const string CallerAddress = "0xABCDEF0123456789ABCDEF0123456789ABCDEF01";
        private const string ContractAddress = "0x1234567890123456789012345678901234567890";

        [Fact]
        public async Task Blockhash_HistoryContractSlot_LeadingZeroHash_ReturnsFull32ByteValue()
        {
            var flatStore = new InMemoryStateStore();

            var parentBlockNumber = (BigInteger)1_000_000;
            var parentHash = new byte[32];
            parentHash[1] = 0xAB;
            parentHash[31] = 0x7E;

            await Eip2935Helpers.ApplyAsync(flatStore, parentBlockNumber, parentHash);

            var slot = Eip2935Helpers.ComputeSlot(parentBlockNumber, Eip2935Constants.HistoryServeWindow);
            var durableBytes = await flatStore.GetStorageAsync(Eip2935Constants.HistoryStorageAddress, slot);
            Assert.Equal(31, durableBytes.Length);

            var stateReader = new StateStoreNodeDataService(flatStore);
            var executionStateService = new ExecutionStateService(stateReader);

            var callInput = new CallInput
            {
                To = ContractAddress,
                From = CallerAddress,
                Gas = new Nethereum.Hex.HexTypes.HexBigInteger(100000),
                Data = "0x"
            };

            var currentBlockNumber = parentBlockNumber + 1;
            var context = new ProgramContext(callInput, executionStateService,
                blockNumber: EvmUInt256BigIntegerExtensions.FromBigInteger(currentBlockNumber))
            {
                BlockHashRule = Eip2935BlockHashRule.Instance
            };

            var program = new Program(new byte[] { 0x40 }, context);
            program.GasRemaining = 100000;
            program.StackPush((long)parentBlockNumber);

            var executor = new BlockHashExecutor();
            var handled = await executor.ExecuteAsync(Instruction.BLOCKHASH, program);

            Assert.True(handled);
            var result = program.StackPop();

            Assert.Equal(32, result.Length);
            Assert.Equal(parentHash, result);
        }
    }
}
