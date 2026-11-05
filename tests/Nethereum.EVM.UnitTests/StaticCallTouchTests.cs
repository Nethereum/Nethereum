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
    public class StaticCallTouchTests
    {
        private const string SenderAddress = "0x1111111111111111111111111111111111111111";
        private const string CoinbaseAddress = "0x2222222222222222222222222222222222222222";
        private const string CallerAddress = "0x5555555555555555555555555555555555555555";
        private const string EmptyAccountWithStorage = "0x1000000000000000000000000000000000000000";

        private static string Push1(long value) => "60" + value.ToString("x2");
        private static string Push20(string address) => "73" + address.Substring(2).ToLowerInvariant();

        private static string StaticCall(string target) =>
            Push1(0) + Push1(0) + Push1(0) + Push1(0) + Push20(target) + "620f4240" + "fa" + "50";

        private static byte[] StaticCallThenStop(string target) => (StaticCall(target) + "00").HexToByteArray();

        private static byte[] StaticCallThenRevert(string target) =>
            (StaticCall(target) + Push1(0) + Push1(0) + "fd").HexToByteArray();

        private static string Normalize(string address) =>
            AddressUtil.Current.ConvertToValid20ByteAddress(address).ToLower();

        private static (Dictionary<string, AccountState> Accounts, InMemoryStateReader Reader) PreState(byte[] callerCode)
        {
            var accounts = new Dictionary<string, AccountState>
            {
                [Normalize(SenderAddress)] = new AccountState { Balance = new EvmUInt256(BigInteger.Parse("1000000000000000000000")) },
                [Normalize(CoinbaseAddress)] = new AccountState { Balance = new EvmUInt256(1) },
                [Normalize(CallerAddress)] = new AccountState { Code = callerCode },
                [Normalize(EmptyAccountWithStorage)] = new AccountState
                {
                    Storage = new Dictionary<EvmUInt256, byte[]> { [EvmUInt256.Zero] = new byte[] { 1 } }
                }
            };
            return (accounts, new InMemoryStateReader(accounts));
        }

        private static TransactionExecutionContext CallTo(ExecutionStateService executionState, string to) =>
            new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = to,
                GasLimit = 2_000_000,
                GasPrice = 0,
                Nonce = 0,
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 0,
                ChainId = 1,
                Coinbase = CoinbaseAddress,
                ExecutionState = executionState
            };

        [Fact]
        public async Task Given_AnEip161EmptyAccountWithStorage_When_StaticCalled_Then_ItIsDeletedAtTransactionEnd()
        {
            var (accounts, reader) = PreState(StaticCallThenStop(EmptyAccountWithStorage));
            var executionState = new ExecutionStateService(reader);

            var result = await new TransactionExecutor(HardforkConfig.Constantinople).ExecuteAsync(CallTo(executionState, CallerAddress));
            reader.CommitChanges(executionState);

            Assert.True(result.Success, result.Error);
            Assert.False(accounts.ContainsKey(Normalize(EmptyAccountWithStorage)));
        }

        [Fact]
        public async Task Given_AStaticCallToAnEmptyAccount_When_TheCallerReverts_Then_TheAccountSurvives()
        {
            var (accounts, reader) = PreState(StaticCallThenRevert(EmptyAccountWithStorage));
            var executionState = new ExecutionStateService(reader);

            var result = await new TransactionExecutor(HardforkConfig.Constantinople).ExecuteAsync(CallTo(executionState, CallerAddress));
            reader.CommitChanges(executionState);

            Assert.False(result.Success);
            Assert.True(accounts.ContainsKey(Normalize(EmptyAccountWithStorage)));
        }
    }
}
