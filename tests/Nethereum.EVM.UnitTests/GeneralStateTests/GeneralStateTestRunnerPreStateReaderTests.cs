using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.EVM.UnitTests.GeneralStateTests
{
    public class GeneralStateTestRunnerPreStateReaderTests
    {
        private const string PreAccountAddress = "0x1000000000000000000000000000000000000001";

        private static GeneralStateTest BuildTestWithOneUntouchedPreAccount(string nonce = "0x7")
        {
            return new GeneralStateTest
            {
                Pre = new Dictionary<string, TestAccount>
                {
                    [PreAccountAddress] = new TestAccount
                    {
                        Code = "0x6001600155",
                        Balance = "0x2a",
                        Nonce = nonce,
                        Storage = new Dictionary<string, string>
                        {
                            ["0x1"] = "0x2a"
                        }
                    }
                }
            };
        }

        private static async Task<ExecutionStateService> InvokeSetupPreStateAsync(GeneralStateTest test)
        {
            var runner = new GeneralStateTestRunner();
            return await Task.FromResult(runner.SetupPreState(test));
        }

        [Fact]
        public async Task Given_AFixtureWhosePreStateCarriesCode_When_TheRunnerSeedsIt_Then_TheStateReaderReturnsThatCode()
        {
            var test = BuildTestWithOneUntouchedPreAccount();
            var executionState = await InvokeSetupPreStateAsync(test);

            var code = await executionState.StateReader.GetCodeAsync(PreAccountAddress);

            Assert.Equal("0x6001600155".HexToByteArray(), code);
        }

        [Fact]
        public async Task Given_AFixtureWhosePreStateCarriesBalance_When_TheRunnerSeedsIt_Then_TheStateReaderReturnsThatBalance()
        {
            var test = BuildTestWithOneUntouchedPreAccount();
            var executionState = await InvokeSetupPreStateAsync(test);

            var balance = await executionState.StateReader.GetBalanceAsync(PreAccountAddress);

            Assert.Equal(EvmUInt256BigIntegerExtensions.FromBigInteger(42), balance);
        }

        [Fact]
        public async Task Given_AFixtureWhosePreStateCarriesNonce_When_TheRunnerSeedsIt_Then_TheStateReaderReturnsThatNonce()
        {
            var test = BuildTestWithOneUntouchedPreAccount();
            var executionState = await InvokeSetupPreStateAsync(test);

            var nonce = await executionState.StateReader.GetTransactionCountAsync(PreAccountAddress);

            Assert.Equal(EvmUInt256BigIntegerExtensions.FromBigInteger(7), nonce);
        }

        [Fact]
        public async Task Given_AFixtureWhosePreStateCarriesTheMaximumNonce_When_TheRunnerSeedsIt_Then_TheStateReaderReturnsThatNonce()
        {
            var test = BuildTestWithOneUntouchedPreAccount("0xffffffffffffffff");
            var executionState = await InvokeSetupPreStateAsync(test);

            var nonce = await executionState.StateReader.GetTransactionCountAsync(PreAccountAddress);

            Assert.Equal(new EvmUInt256(ulong.MaxValue), nonce);
        }

        [Fact]
        public async Task Given_AFixtureWhosePreStateCarriesStorage_When_TheRunnerSeedsIt_Then_TheStateReaderReturnsThatStorage()
        {
            var test = BuildTestWithOneUntouchedPreAccount();
            var executionState = await InvokeSetupPreStateAsync(test);

            var slot = await executionState.StateReader.GetStorageAtAsync(
                PreAccountAddress, EvmUInt256BigIntegerExtensions.FromBigInteger(1));

            Assert.Equal(EvmUInt256BigIntegerExtensions.FromBigInteger(42).ToBigEndian(), slot);
        }

        [Fact]
        public async Task Given_AnAddressAbsentFromPreState_When_TheRunnerSeedsIt_Then_TheStateReaderStillAnswersEmpty()
        {
            var test = BuildTestWithOneUntouchedPreAccount();
            var executionState = await InvokeSetupPreStateAsync(test);

            const string absentAddress = "0x9999999999999999999999999999999999999999";
            var code = await executionState.StateReader.GetCodeAsync(absentAddress);
            var balance = await executionState.StateReader.GetBalanceAsync(absentAddress);
            var nonce = await executionState.StateReader.GetTransactionCountAsync(absentAddress);

            Assert.Empty(code);
            Assert.Equal(EvmUInt256.Zero, balance);
            Assert.Equal(EvmUInt256.Zero, nonce);
        }
    }
}
