using System.Collections.Generic;
using System.Linq;
using Nethereum.EVM;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Gas;
using Nethereum.EVM.Precompiles;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests.GeneralStateTests
{
    public class Eip7702DelegationAccessListTests
    {
        private static string Addr(string suffix) => "0x" + suffix.PadLeft(40, '0');

        private static readonly string DriverAddress = Addr("da1");
        private static readonly string AuthorityAddress = Addr("aa1");
        private static readonly string Authority2Address = Addr("aa2");
        private static readonly string DelegateAddress = Addr("de1");
        private static readonly string Delegate2Address = Addr("de2");

        private const long BlockNumber = 300;

        private static readonly byte[] Stop = { 0x00 };
        private static readonly byte[] DelegateRuntimeCode = { 0x00 };

        private static byte[] BuildCallBytecode(string targetAddress)
        {
            var addressBytes = targetAddress.HexToByteArray();
            var code = new List<byte>
            {
                0x60, 0x00,
                0x60, 0x00,
                0x60, 0x00,
                0x60, 0x00,
                0x60, 0x00,
                0x73
            };
            code.AddRange(addressBytes);
            code.Add(0x5a);
            code.Add(0xf1);
            code.Add(0x50);
            return code.ToArray();
        }

        private static byte[] Concat(params byte[][] parts)
        {
            var result = new List<byte>();
            foreach (var part in parts) result.AddRange(part);
            return result.ToArray();
        }

        private static WitnessAccount SimpleAccount(string address, byte[] code, ulong nonce = 1)
        {
            return new WitnessAccount
            {
                Address = address,
                Balance = EvmUInt256.Zero,
                Nonce = nonce,
                Code = code,
                Storage = new List<WitnessStorageSlot>()
            };
        }

        private static BlockExecutionResult ExecuteDriverBlock(
            byte[] driverCode,
            IEnumerable<WitnessAccount> extraAccounts,
            long gasLimit = 300000,
            HardforkName fork = HardforkName.Amsterdam)
        {
            var sender = TestTransactionHelper.GetDefaultSenderAddress();

            var tx = TestTransactionHelper.CreateSignedContractCall(
                DriverAddress,
                data: new byte[0],
                value: EvmUInt256.Zero,
                nonce: EvmUInt256.Zero,
                gasPrice: new EvmUInt256(1UL),
                gasLimit: new EvmUInt256((ulong)gasLimit));

            var accounts = new List<WitnessAccount>
            {
                new WitnessAccount
                {
                    Address = sender,
                    Balance = new EvmUInt256(1_000_000_000_000_000_000UL),
                    Nonce = 0,
                    Code = new byte[0],
                    Storage = new List<WitnessStorageSlot>()
                },
                SimpleAccount(DriverAddress, driverCode)
            };
            accounts.AddRange(extraAccounts);

            var block = new BlockWitnessData
            {
                BlockNumber = BlockNumber,
                Timestamp = 1000,
                BaseFee = 1,
                BlockGasLimit = 30000000,
                ChainId = 1,
                Coinbase = "0x2adc25665018aa1fe0e6bc666dac8fc2697ff9ba",
                Difficulty = new byte[32],
                ParentHash = System.Linq.Enumerable.Repeat((byte)0x11, 32).ToArray(),
                ExtraData = new byte[0],
                MixHash = new byte[32],
                Nonce = new byte[8],
                Features = new BlockFeatureConfig { Fork = fork },
                Transactions = new List<BlockWitnessTransaction> { tx },
                Accounts = accounts
            };

            return BlockExecutor.Execute(
                block.AddRequestPredeploys(),
                RlpBlockEncodingProvider.Instance,
                DefaultMainnetHardforkRegistry.Instance);
        }

        private static long ProbeTightGasLimit(byte[] driverCode, IEnumerable<WitnessAccount> extraAccounts, HardforkName fork)
        {
            var accounts = extraAccounts as WitnessAccount[] ?? extraAccounts.ToArray();
            var probe = ExecuteDriverBlock(driverCode, accounts, fork: fork);
            Assert.True(probe.TxResults.Single().Success, "probe transaction failed");
            return probe.TxResults.Single().GasUsed - GasConstants.COLD_ACCOUNT_ACCESS_COST;
        }

        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_ADelegatedCallThatCannotAffordTheAccessCharge_When_Executed_Then_TheFrameRunsOutOfGas()
        {
            var delegationCode = Eip7702DelegationUtils.CreateDelegationCode(DelegateAddress);
            var driverCode = Concat(BuildCallBytecode(AuthorityAddress), Stop);
            var extraAccounts = new[]
            {
                SimpleAccount(AuthorityAddress, delegationCode),
                SimpleAccount(DelegateAddress, DelegateRuntimeCode)
            };

            var tightGasLimit = ProbeTightGasLimit(driverCode, extraAccounts, HardforkName.Amsterdam);
            var result = ExecuteDriverBlock(driverCode, extraAccounts, gasLimit: tightGasLimit);
            var txResult = result.TxResults.Single();

            Assert.False(txResult.Success, "expected the tightened gas limit to make the delegation charge unaffordable");
            Assert.Equal(tightGasLimit, txResult.GasUsed);

            Assert.NotNull(result.BlockAccessList);
            var delegateEntry = result.BlockAccessList.SingleOrDefault(a =>
                string.Equals(a.Address, DelegateAddress, System.StringComparison.OrdinalIgnoreCase));
            Assert.True(delegateEntry == null,
                "the delegate must not be listed — get_account (GetCode here) must never run when the delegation charge itself is unaffordable");
        }

        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_ADelegatedCallThatCanAffordTheAccessCharge_When_Executed_Then_ItProceedsAndTheDelegateIsListed()
        {
            var delegationCode = Eip7702DelegationUtils.CreateDelegationCode(DelegateAddress);
            var driverCode = Concat(BuildCallBytecode(AuthorityAddress), Stop);

            var result = ExecuteDriverBlock(driverCode, new[]
            {
                SimpleAccount(AuthorityAddress, delegationCode),
                SimpleAccount(DelegateAddress, DelegateRuntimeCode)
            });
            var txResult = result.TxResults.Single();

            Assert.True(txResult.Success, $"driver transaction failed: {txResult.RevertReason}");
            Assert.NotNull(result.BlockAccessList);

            var authorityEntry = result.BlockAccessList.SingleOrDefault(a =>
                string.Equals(a.Address, AuthorityAddress, System.StringComparison.OrdinalIgnoreCase));
            Assert.True(authorityEntry != null, "the genuinely-called authority is absent from the access list");

            var delegateEntry = result.BlockAccessList.SingleOrDefault(a =>
                string.Equals(a.Address, DelegateAddress, System.StringComparison.OrdinalIgnoreCase));
            Assert.True(delegateEntry != null,
                "the delegate must be listed when the delegation access charge is affordable — the reference's get_account call after a successful check_gas records it, same as any other call target");
        }

        /// <summary>
        /// Proves the halt is NOT Amsterdam-gated: EIP-7702 is Prague
        /// onward, and <see cref="Execution.CallFrame.Rules.Eip7702DelegationRule"/>
        /// is the SAME shared rule instance at Prague, Osaka and Amsterdam
        /// (<c>CallFrameInitRuleSets.cs:29-36</c>; <c>PragueSpec.cs</c> takes
        /// <c>CallFrameInit = CallFrameInitRuleSets.Prague</c> directly,
        /// with no Amsterdam-only branch in the rule itself). Same
        /// probe-and-undercut technique as the Amsterdam OOG test, at
        /// <see cref="HardforkName.Prague"/> instead.
        ///
        /// <para><b>Weaker red-capability than its Amsterdam sibling, stated
        /// plainly rather than implied.</b> Prague has no block access list
        /// (EIP-7928 is Amsterdam-only), so the one signal that actually
        /// distinguished "halted before get_account" from "halted one
        /// opcode later" at Amsterdam — delegate absent from the BAL — does
        /// not exist here. <c>InnerCalls</c> was tried as a fork-independent
        /// substitute and measured NOT to discriminate either: run against
        /// the true pre-AMS-EXEC-03 code, <c>InnerCalls</c> came back empty
        /// in BOTH the defective and the fixed run (the defect's actual
        /// failure point turned out to be somewhere between the delegate's
        /// — still-recording — code fetch and the child frame's merge, not
        /// at the very next opcode as first guessed; no completed child
        /// frame ever gets merged in the defective run either — so it is
        /// omitted here rather than kept as a vacuous assertion). Run
        /// against the true original code, THIS test (Success + GasUsed)
        /// stayed GREEN — it is not independently red-capable the way the
        /// Amsterdam sibling is. Its job is narrower and still real: with
        /// the fix in place, it confirms the SAME shared rule instance halts
        /// the same way at Prague, which the AMS-EXEC-03 row's core claim
        /// ("this is not an Amsterdam rule") depends on.</para>
        /// </summary>
        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_APragueDelegatedCall_When_TheAccessChargeIsUnaffordable_Then_ItAlsoRunsOutOfGas()
        {
            var delegationCode = Eip7702DelegationUtils.CreateDelegationCode(DelegateAddress);
            var driverCode = Concat(BuildCallBytecode(AuthorityAddress), Stop);
            var extraAccounts = new[]
            {
                SimpleAccount(AuthorityAddress, delegationCode),
                SimpleAccount(DelegateAddress, DelegateRuntimeCode)
            };

            var tightGasLimit = ProbeTightGasLimit(driverCode, extraAccounts, HardforkName.Prague);
            var result = ExecuteDriverBlock(driverCode, extraAccounts, gasLimit: tightGasLimit, fork: HardforkName.Prague);
            var txResult = result.TxResults.Single();

            Assert.False(txResult.Success, "expected the tightened gas limit to make the delegation charge unaffordable at Prague too");
            Assert.Equal(tightGasLimit, txResult.GasUsed);
        }

        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_ACallToADelegatedAccount_When_GasIsCharged_Then_TheDelegateIsStillWarmed()
        {
            var delegateAccount = SimpleAccount(DelegateAddress, DelegateRuntimeCode);
            var delegate2Account = SimpleAccount(Delegate2Address, DelegateRuntimeCode);
            var authority1Account = SimpleAccount(AuthorityAddress, Eip7702DelegationUtils.CreateDelegationCode(DelegateAddress));

            var driverCode = Concat(BuildCallBytecode(AuthorityAddress), BuildCallBytecode(Authority2Address), Stop);

            var resultWarmReuse = ExecuteDriverBlock(driverCode, new[]
            {
                authority1Account,
                SimpleAccount(Authority2Address, Eip7702DelegationUtils.CreateDelegationCode(DelegateAddress)),
                delegateAccount
            });

            var resultNoReuse = ExecuteDriverBlock(driverCode, new[]
            {
                authority1Account,
                SimpleAccount(Authority2Address, Eip7702DelegationUtils.CreateDelegationCode(Delegate2Address)),
                delegateAccount,
                delegate2Account
            });

            Assert.True(resultWarmReuse.TxResults.Single().Success, "warm-reuse transaction failed");
            Assert.True(resultNoReuse.TxResults.Single().Success, "no-reuse transaction failed");

            var gasWarmReuse = resultWarmReuse.TxResults.Single().GasUsed;
            var gasNoReuse = resultNoReuse.TxResults.Single().GasUsed;

            var expectedGap = GasConstants.EIP8038_COLD_ACCOUNT_ACCESS - GasConstants.WARM_STORAGE_READ_COST;
            Assert.Equal(expectedGap, gasNoReuse - gasWarmReuse);
        }

        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_ADelegateThatIsAlsoGenuinelyCalled_When_TheBlockAccessListIsBuilt_Then_ItIsStillRecorded()
        {
            var delegationCode = Eip7702DelegationUtils.CreateDelegationCode(DelegateAddress);
            var driverCode = Concat(BuildCallBytecode(AuthorityAddress), BuildCallBytecode(DelegateAddress), Stop);

            var result = ExecuteDriverBlock(driverCode, new[]
            {
                SimpleAccount(AuthorityAddress, delegationCode),
                SimpleAccount(DelegateAddress, DelegateRuntimeCode)
            });

            Assert.True(result.TxResults.Single().Success,
                $"driver transaction failed: {result.TxResults.Single().RevertReason}");
            Assert.NotNull(result.BlockAccessList);

            var delegateEntry = result.BlockAccessList.SingleOrDefault(a =>
                string.Equals(a.Address, DelegateAddress, System.StringComparison.OrdinalIgnoreCase));
            Assert.True(delegateEntry != null,
                "the delegate was genuinely CALLed directly and must still be recorded — this row only changes whether the DELEGATION-RESOLUTION's own read happens, not a real one");
        }
    }
}
