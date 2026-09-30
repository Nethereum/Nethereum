using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests
{
    public class Eip8246SelfDestructNoBurnTests
    {
        private const string SenderAddress = "0x1111111111111111111111111111111111111111";
        private const string BeneficiaryAddress = "0x4444444444444444444444444444444444444444";
        private const string PreExistingContractAddress = "0x5555555555555555555555555555555555555555";

        private static byte[] SelfDestructToSelfInitCode() => new byte[]
        {
            (byte)Instruction.ADDRESS,
            (byte)Instruction.SELFDESTRUCT
        };

        private static byte[] SstoreThenSelfDestructToSelfInitCode() => new byte[]
        {
            (byte)Instruction.PUSH1, 0x2A,
            (byte)Instruction.PUSH1, 0x00,
            (byte)Instruction.SSTORE,
            (byte)Instruction.ADDRESS,
            (byte)Instruction.SELFDESTRUCT
        };

        private static byte[] SelfDestructToBeneficiaryCode(string beneficiaryAddress)
        {
            var code = new List<byte> { (byte)Instruction.PUSH20 };
            code.AddRange(beneficiaryAddress.HexToByteArray());
            code.Add((byte)Instruction.SELFDESTRUCT);
            return code.ToArray();
        }

        private static string NormalizeAddress(string address) =>
            AddressUtil.Current.ConvertToValid20ByteAddress(address).ToLower();

        private static async Task<(TransactionExecutionContext ctx, TransactionExecutionResult result)> RunAsync(
            HardforkConfig config, TransactionExecutionContext ctx)
        {
            var executor = new TransactionExecutor(config);
            var result = await executor.ExecuteAsync(ctx);
            return (ctx, result);
        }

        private static async Task<EIP7702TestNodeDataService> FundedNodeAsync(string address, BigInteger balance)
        {
            var node = new EIP7702TestNodeDataService();
            await node.SetBalanceAsync(address, balance);
            return node;
        }

        private static TransactionExecutionContext CreationCtx(
            ExecutionStateService executionState, byte[] initCode, long value, long gasLimit = 1_000_000) => new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = "",
                Data = initCode,
                IsContractCreation = true,
                GasLimit = gasLimit,
                Value = value,
                GasPrice = 1,
                Nonce = 0,
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 0,
                ChainId = 1,
                Coinbase = SenderAddress,
                ExecutionState = executionState
            };

        private static TransactionExecutionContext CallCtx(
            ExecutionStateService executionState, string to, long gasLimit = 1_000_000) => new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = to,
                Data = null,
                IsContractCreation = false,
                GasLimit = gasLimit,
                Value = 0,
                GasPrice = 1,
                Nonce = 0,
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 0,
                ChainId = 1,
                Coinbase = SenderAddress,
                ExecutionState = executionState
            };

        [Fact]
        public async Task Given_SameTxCreatedContractWithBalance_When_SelfDestructsToItself_Then_BalancePreservedNotBurned()
        {
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            var executionState = new ExecutionStateService(node);
            var ctx = CreationCtx(executionState, SelfDestructToSelfInitCode(), value: 1000);

            var (resultCtx, result) = await RunAsync(HardforkConfig.Amsterdam, ctx);

            Assert.True(result.Success, result.Error);
            var balance = await executionState.GetTotalBalanceAsync(resultCtx.ContractAddress);
            Assert.Equal(new EvmUInt256(1000), balance);
        }

        [Fact]
        public async Task Given_SameTxCreatedContractWithBalance_When_SelfDestructsToItself_Then_AccountSurvivesWithZeroNonceAndEmptyCode()
        {
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            var executionState = new ExecutionStateService(node);
            var ctx = CreationCtx(executionState, SelfDestructToSelfInitCode(), value: 1000);

            var (resultCtx, result) = await RunAsync(HardforkConfig.Amsterdam, ctx);

            Assert.True(result.Success, result.Error);
            var key = Nethereum.Util.EvmAddress.FromHex(NormalizeAddress(resultCtx.ContractAddress));
            Assert.True(executionState.AccountsState.ContainsKey(key));
            var account = executionState.AccountsState[key];
            Assert.Equal(EvmUInt256.Zero, account.Nonce);
            Assert.True(account.Code == null || account.Code.Length == 0);
        }

        [Fact]
        public async Task Given_SameTxCreatedContractWithStorage_When_SelfDestructsToItself_Then_AllStorageCleared_ButBalanceKept()
        {
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            var executionState = new ExecutionStateService(node);
            var ctx = CreationCtx(executionState, SstoreThenSelfDestructToSelfInitCode(), value: 1000);

            var (resultCtx, result) = await RunAsync(HardforkConfig.Amsterdam, ctx);

            Assert.True(result.Success, result.Error);
            var key = Nethereum.Util.EvmAddress.FromHex(NormalizeAddress(resultCtx.ContractAddress));
            var account = executionState.AccountsState[key];
            Assert.Empty(account.Storage);
            var balance = await executionState.GetTotalBalanceAsync(resultCtx.ContractAddress);
            Assert.Equal(new EvmUInt256(1000), balance);
        }

        [Fact]
        public async Task Given_SameTxCreatedContract_When_SelfDestructsToDifferentBeneficiary_Then_BalanceMovedAndAccountCleared()
        {
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            var executionState = new ExecutionStateService(node);
            var ctx = CreationCtx(executionState, SelfDestructToBeneficiaryCode(BeneficiaryAddress), value: 1000, gasLimit: 2_000_000);

            var (resultCtx, result) = await RunAsync(HardforkConfig.Amsterdam, ctx);

            Assert.True(result.Success, result.Error);
            var beneficiaryBalance = await executionState.GetTotalBalanceAsync(BeneficiaryAddress);
            Assert.Equal(new EvmUInt256(1000), beneficiaryBalance);

            Assert.Contains(result.ProgramResult.ClearedContractAccounts,
                a => a.IsTheSameAddress(resultCtx.ContractAddress));
            Assert.DoesNotContain(result.ProgramResult.DeletedContractAccounts,
                a => a.IsTheSameAddress(resultCtx.ContractAddress));

            // storage), leaving it empty -> the EIP-161 sweep removes it,
            // matching "deletion is an emptiness consequence". The overlay
            var key = Nethereum.Util.EvmAddress.FromHex(NormalizeAddress(resultCtx.ContractAddress));
            Assert.True(executionState.AccountsState[key].IsRemoved);
            Assert.Contains(result.DeletedAccounts, a => a.IsTheSameAddress(resultCtx.ContractAddress));
        }

        [Fact]
        public async Task Given_PreExistingContract_When_SelfDestructs_Then_NotMarkedForSweep_AndBalanceMoved()
        {
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(PreExistingContractAddress, SelfDestructToBeneficiaryCode(BeneficiaryAddress));
            await node.SetNonceAsync(PreExistingContractAddress, 1);
            await node.SetBalanceAsync(PreExistingContractAddress, BigInteger.Parse("1000"));
            var executionState = new ExecutionStateService(node);
            var ctx = CallCtx(executionState, PreExistingContractAddress);

            var (_, result) = await RunAsync(HardforkConfig.Amsterdam, ctx);

            Assert.True(result.Success, result.Error);
            var beneficiaryBalance = await executionState.GetTotalBalanceAsync(BeneficiaryAddress);
            Assert.Equal(new EvmUInt256(1000), beneficiaryBalance);

            var contractKey = Nethereum.Util.EvmAddress.FromHex(NormalizeAddress(PreExistingContractAddress));
            Assert.DoesNotContain(result.ProgramResult.ClearedContractAccounts,
                a => a.IsTheSameAddress(PreExistingContractAddress));
            Assert.DoesNotContain(result.ProgramResult.DeletedContractAccounts,
                a => a.IsTheSameAddress(PreExistingContractAddress));

            Assert.True(executionState.AccountsState.ContainsKey(contractKey));
            Assert.False(executionState.AccountsState[contractKey].IsRemoved);
            var code = await executionState.GetCodeAsync(PreExistingContractAddress);
            Assert.True(code.Length > 0);
        }

        [Fact]
        public async Task Given_OsakaFork_When_SelfDestructToSelf_Then_BalanceStillBurned_ProvingNoCrossForkLeak()
        {
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            var executionState = new ExecutionStateService(node);
            var ctx = CreationCtx(executionState, SelfDestructToSelfInitCode(), value: 1000, gasLimit: 500_000);

            var (resultCtx, result) = await RunAsync(HardforkConfig.Osaka, ctx);

            Assert.True(result.Success, result.Error);
            var balance = await executionState.GetTotalBalanceAsync(resultCtx.ContractAddress);
            Assert.Equal(EvmUInt256.Zero, balance);
        }
    }
}
