using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Gas;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests
{
    public class AccountRemovalReachesCommittedStateTests
    {
        private const string SenderAddress = "0x1111111111111111111111111111111111111111";
        private const string BeneficiaryAddress = "0x000000000000000000000000000000000000dead";
        private const string CoinbaseAddress = "0x2222222222222222222222222222222222222222";
        private const string ContractAddress = "0x5555555555555555555555555555555555555555";
        private const string RevertingCallerAddress = "0x6666666666666666666666666666666666666666";
        private const string OuterCallerAddress = "0x7777777777777777777777777777777777777777";
        private const string EmptyPreStateAddress = "0x8888888888888888888888888888888888888888";
        private const string RipemdAddress = "0x0000000000000000000000000000000000000003";

        private static readonly BigInteger SenderFunds = BigInteger.Parse("1000000000000000000000");

        private static string Push1(long value) => "60" + value.ToString("x2");
        private static string Push20(string address) => "73" + address.Substring(2).ToLowerInvariant();

        private static byte[] SelfDestructTo(string beneficiary) =>
            (Push20(beneficiary) + "ff").HexToByteArray();

        private static readonly byte[] SelfDestructToSelf = "30ff".HexToByteArray();

        private static byte[] CallThenStop(string target) =>
            (Push1(0) + Push1(0) + Push1(0) + Push1(0) + Push1(0) + Push20(target) + "620f4240" + "f1" + "00")
                .HexToByteArray();

        private static byte[] CallThenRevert(string target) =>
            (Push1(0) + Push1(0) + Push1(0) + Push1(0) + Push1(0) + Push20(target) + "620f4240" + "f1"
             + Push1(0) + Push1(0) + "fd").HexToByteArray();

        private static byte[] CallWithDataThenRevert(string target) =>
            (Push1(1) + Push1(0) + "52"
             + Push1(0) + Push1(0) + Push1(0x20) + Push1(0) + Push1(0) + Push20(target) + "620f4240" + "f1"
             + "50" + Push1(0) + Push1(0) + "fd").HexToByteArray();

        private static string Normalize(string address) =>
            AddressUtil.Current.ConvertToValid20ByteAddress(address).ToLower();

        private sealed class CommittedState
        {
            public Dictionary<string, AccountState> Accounts;
            public InMemoryStateReader Reader;

            public bool Holds(string address) => Accounts.ContainsKey(Normalize(address));

            public EvmUInt256 BalanceOf(string address) =>
                Accounts.TryGetValue(Normalize(address), out var account) ? account.Balance : EvmUInt256.Zero;

            public BigInteger TotalSupply() =>
                Accounts.Values.Aggregate(BigInteger.Zero, (sum, a) => sum + a.Balance.ToBigInteger());
        }

        private static CommittedState PreState(params (string Address, BigInteger Balance, byte[] Code)[] accounts)
        {
            var committed = new Dictionary<string, AccountState>();
            committed[Normalize(SenderAddress)] = new AccountState { Balance = new EvmUInt256(SenderFunds) };
            committed[Normalize(CoinbaseAddress)] = new AccountState { Balance = new EvmUInt256(1) };
            foreach (var (address, balance, code) in accounts)
            {
                committed[Normalize(address)] = new AccountState
                {
                    Balance = new EvmUInt256(balance),
                    Code = code ?? new byte[0]
                };
            }
            return new CommittedState { Accounts = committed, Reader = new InMemoryStateReader(committed) };
        }

        private static TransactionExecutionContext Creation(
            ExecutionStateService executionState, byte[] initCode, long value) =>
            new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = "",
                Data = initCode,
                IsContractCreation = true,
                GasLimit = 2_000_000,
                Value = value,
                GasPrice = 0,
                Nonce = 0,
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 0,
                ChainId = 1,
                Coinbase = CoinbaseAddress,
                ExecutionState = executionState
            };

        private static TransactionExecutionContext CallTo(
            ExecutionStateService executionState, string to, long value = 0) =>
            new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = to,
                Data = null,
                IsContractCreation = false,
                GasLimit = 2_000_000,
                Value = value,
                GasPrice = 0,
                Nonce = 0,
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 0,
                ChainId = 1,
                Coinbase = CoinbaseAddress,
                ExecutionState = executionState
            };

        private static async Task<TransactionExecutionResult> ExecuteAndCommitAsync(
            HardforkConfig config, CommittedState state, TransactionExecutionContext ctx)
        {
            var result = await new TransactionExecutor(config).ExecuteAsync(ctx);
            Assert.True(result.Success, result.Error);
            state.Reader.CommitChanges(ctx.ExecutionState);
            return result;
        }

        private static string CreatedContractAddress() =>
            ContractUtils.CalculateContractAddress(SenderAddress, 0);

        [Fact]
        public async Task Given_AContractThatSelfDestructsToAFreshBeneficiary_When_TheBlockIsCommitted_Then_TheDestroyedAccountIsGone()
        {
            var created = CreatedContractAddress();
            var state = PreState((created, 1, null));
            var executionState = new ExecutionStateService(state.Reader);

            await ExecuteAndCommitAsync(HardforkConfig.Amsterdam, state,
                Creation(executionState, SelfDestructTo(BeneficiaryAddress), value: 0));

            Assert.False(state.Holds(created));
        }

        [Fact]
        public async Task Given_AContractThatSelfDestructsToAFreshBeneficiary_When_TheBlockIsCommitted_Then_TotalSupplyIsUnchanged()
        {
            var created = CreatedContractAddress();
            var state = PreState((created, 1, null));
            var supplyBefore = state.TotalSupply();
            var executionState = new ExecutionStateService(state.Reader);

            await ExecuteAndCommitAsync(HardforkConfig.Amsterdam, state,
                Creation(executionState, SelfDestructTo(BeneficiaryAddress), value: 0));

            Assert.Equal(new EvmUInt256(1), state.BalanceOf(BeneficiaryAddress));
            Assert.Equal(supplyBefore, state.TotalSupply());
        }

        [Fact]
        public async Task Given_AContractThatSelfDestructsToItself_When_Committed_Then_ItSurvivesWithItsBalance()
        {
            var created = CreatedContractAddress();
            var state = PreState((created, 0, null));
            var executionState = new ExecutionStateService(state.Reader);

            await ExecuteAndCommitAsync(HardforkConfig.Amsterdam, state,
                Creation(executionState, SelfDestructToSelf, value: 1000));

            Assert.True(state.Holds(created));
            Assert.Equal(new EvmUInt256(1000), state.BalanceOf(created));
        }

        [Fact]
        public async Task Given_ABlockWithNoSelfDestruct_When_Committed_Then_NoAccountIsRemoved()
        {
            var state = PreState((ContractAddress, 7, null));
            var addressesBefore = state.Accounts.Keys.OrderBy(k => k).ToList();
            var executionState = new ExecutionStateService(state.Reader);

            await ExecuteAndCommitAsync(HardforkConfig.Amsterdam, state,
                CallTo(executionState, ContractAddress, value: 3));

            Assert.All(addressesBefore, address => Assert.True(state.Accounts.ContainsKey(address)));
            Assert.All(executionState.AccountsState.Values, account => Assert.False(account.IsRemoved));
            Assert.Equal(new EvmUInt256(10), state.BalanceOf(ContractAddress));
        }

        [Fact]
        public async Task Given_APreExistingEmptyAccount_When_ItIsTouchedAtSpuriousDragon_Then_ItIsDeletedFromCommittedState()
        {
            var state = PreState((EmptyPreStateAddress, 0, null));
            var executionState = new ExecutionStateService(state.Reader) { TouchPersistsOnRevert = true };

            await ExecuteAndCommitAsync(HardforkConfig.SpuriousDragon, state,
                CallTo(executionState, EmptyPreStateAddress));

            Assert.False(state.Holds(EmptyPreStateAddress));
        }

        [Fact]
        public async Task Given_APreExistingEmptyAccount_When_ItIsTouchedAtTangerineWhistle_Then_ItSurvives()
        {
            var state = PreState((EmptyPreStateAddress, 0, null));
            var executionState = new ExecutionStateService(state.Reader);

            await ExecuteAndCommitAsync(HardforkConfig.TangerineWhistle, state,
                CallTo(executionState, EmptyPreStateAddress));

            Assert.True(state.Holds(EmptyPreStateAddress));
        }

        [Fact]
        public async Task Given_APreExistingNonEmptyAccount_When_ItIsTouched_Then_ItIsNotDeleted()
        {
            var state = PreState((EmptyPreStateAddress, 5, null));
            var executionState = new ExecutionStateService(state.Reader) { TouchPersistsOnRevert = true };

            await ExecuteAndCommitAsync(HardforkConfig.SpuriousDragon, state,
                CallTo(executionState, EmptyPreStateAddress));

            Assert.True(state.Holds(EmptyPreStateAddress));
            Assert.Equal(new EvmUInt256(5), state.BalanceOf(EmptyPreStateAddress));
        }

        [Fact]
        public async Task Given_AFundedAccountWithNoCode_When_ACallOpcodeTouchesItWithZeroValue_Then_ItKeepsItsBalance()
        {
            var state = PreState(
                (ContractAddress, 0, CallThenStop(EmptyPreStateAddress)),
                (EmptyPreStateAddress, 5, null));
            var supplyBefore = state.TotalSupply();
            var executionState = new ExecutionStateService(state.Reader);

            await ExecuteAndCommitAsync(HardforkConfig.Amsterdam, state,
                CallTo(executionState, ContractAddress));

            Assert.True(state.Holds(EmptyPreStateAddress));
            Assert.Equal(new EvmUInt256(5), state.BalanceOf(EmptyPreStateAddress));
            Assert.Equal(supplyBefore, state.TotalSupply());
        }

        [Fact]
        public async Task Given_AnEmptyBalanceAccountWithANonce_When_ACallOpcodeTouchesItWithZeroValue_Then_ItSurvives()
        {
            var state = PreState(
                (ContractAddress, 0, CallThenStop(EmptyPreStateAddress)),
                (EmptyPreStateAddress, 0, null));
            state.Accounts[Normalize(EmptyPreStateAddress)].Nonce = 3;
            var executionState = new ExecutionStateService(state.Reader);

            await ExecuteAndCommitAsync(HardforkConfig.Amsterdam, state,
                CallTo(executionState, ContractAddress));

            Assert.True(state.Holds(EmptyPreStateAddress));
            Assert.Equal(new EvmUInt256(3), state.Accounts[Normalize(EmptyPreStateAddress)].Nonce);
        }

        [Fact]
        public async Task Given_AnEip6780SameTransactionCreatedSelfDestructAtCancun_When_TheGuestCommits_Then_TheAccountIsGoneAndSupplyIsUnchanged()
        {
            var created = CreatedContractAddress();
            var state = PreState((created, 1, null));
            var supplyBefore = state.TotalSupply();
            var executionState = new ExecutionStateService(state.Reader);

            await ExecuteAndCommitAsync(HardforkConfig.Cancun, state,
                Creation(executionState, SelfDestructTo(BeneficiaryAddress), value: 0));

            Assert.False(state.Holds(created));
            Assert.Equal(new EvmUInt256(1), state.BalanceOf(BeneficiaryAddress));
            Assert.Equal(supplyBefore, state.TotalSupply());
        }

        [Fact]
        public async Task Given_APreCancunSelfDestruct_When_TheGuestCommits_Then_TheAccountIsGone()
        {
            var state = PreState((ContractAddress, 1, SelfDestructTo(BeneficiaryAddress)));
            var supplyBefore = state.TotalSupply();
            var executionState = new ExecutionStateService(state.Reader);

            await ExecuteAndCommitAsync(HardforkConfig.Shanghai, state,
                CallTo(executionState, ContractAddress));

            Assert.False(state.Holds(ContractAddress));
            Assert.Equal(new EvmUInt256(1), state.BalanceOf(BeneficiaryAddress));
            Assert.Equal(supplyBefore, state.TotalSupply());
        }

        [Fact]
        public async Task Given_ASelfDestructInsideARevertedCallFrame_When_TheTransactionCommits_Then_TheAccountSurvives()
        {
            var state = PreState(
                (ContractAddress, 1, SelfDestructTo(BeneficiaryAddress)),
                (RevertingCallerAddress, 0, CallThenRevert(ContractAddress)),
                (OuterCallerAddress, 0, CallThenStop(RevertingCallerAddress)));
            var supplyBefore = state.TotalSupply();
            var executionState = new ExecutionStateService(state.Reader);

            await ExecuteAndCommitAsync(HardforkConfig.Shanghai, state,
                CallTo(executionState, OuterCallerAddress));

            Assert.True(state.Holds(ContractAddress));
            Assert.Equal(new EvmUInt256(1), state.BalanceOf(ContractAddress));
            Assert.False(state.Holds(BeneficiaryAddress));
            Assert.Equal(supplyBefore, state.TotalSupply());
        }

        [Fact]
        public async Task Given_ARipemdTouchInAlreadyAccessListedStateInsideARevertedFrame_When_TheTransactionCommits_Then_ItsBalanceSurvives()
        {
            var state = PreState(
                (RipemdAddress, 5, null),
                (ContractAddress, 0, CallWithDataThenRevert(RipemdAddress)),
                (OuterCallerAddress, 0, CallThenStop(ContractAddress)));
            var supplyBefore = state.TotalSupply();
            var executionState = new ExecutionStateService(state.Reader);

            var ctx = CallTo(executionState, OuterCallerAddress);
            ctx.AccessList = new List<AccessListEntry> { new AccessListEntry { Address = RipemdAddress } };

            await ExecuteAndCommitAsync(
                HardforkConfig.Amsterdam.WithPrecompiles(Precompiles.DefaultPrecompileRegistries.OsakaBase()),
                state, ctx);

            Assert.Contains(Nethereum.Util.EvmAddress.FromHex(Normalize(RipemdAddress)), executionState.TxGloballyTouchedAddresses);
            Assert.True(state.Holds(RipemdAddress));
            Assert.Equal(new EvmUInt256(5), state.BalanceOf(RipemdAddress));
            Assert.Equal(supplyBefore, state.TotalSupply());
        }

        [Fact]
        public async Task Given_AnAccountMarkedRemoved_When_TheFrameThatMarkedItReverts_Then_TheMarkIsRolledBack()
        {
            var state = PreState((ContractAddress, 1, null));
            var executionState = new ExecutionStateService(state.Reader);
            await executionState.LoadBalanceNonceAndCodeFromStorageAsync(ContractAddress);

            var snapshotId = executionState.TakeSnapshot();
            executionState.DeleteAccount(ContractAddress);
            executionState.RevertToSnapshot(snapshotId);

            var account = executionState.AccountsState[Nethereum.Util.EvmAddress.FromHex(Normalize(ContractAddress))];
            Assert.False(account.IsRemoved);
            Assert.Equal(new EvmUInt256(1), account.Balance.GetTotalBalance());
        }

        [Fact]
        public async Task Given_AnAccountAlreadyMarkedRemoved_When_ALaterFrameReverts_Then_TheMarkSurvives()
        {
            var state = PreState((ContractAddress, 1, null));
            var executionState = new ExecutionStateService(state.Reader);
            await executionState.LoadBalanceNonceAndCodeFromStorageAsync(ContractAddress);
            executionState.DeleteAccount(ContractAddress);

            var snapshotId = executionState.TakeSnapshot();
            executionState.CreditBalance(ContractAddress, new EvmUInt256(5));
            executionState.RevertToSnapshot(snapshotId);

            var account = executionState.AccountsState[Nethereum.Util.EvmAddress.FromHex(Normalize(ContractAddress))];
            Assert.True(account.IsRemoved);
            Assert.True(account.Balance.GetTotalBalance().IsZero);
        }
    }
}
