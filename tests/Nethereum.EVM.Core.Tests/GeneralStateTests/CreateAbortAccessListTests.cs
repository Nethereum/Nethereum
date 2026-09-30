using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.EVM;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Precompiles;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests.GeneralStateTests
{
    /// <summary>
    /// AMS-7928-22 of <c>docs/internal/glamsterdam-traceability-matrix.md</c>:
    /// a CREATE that aborts on the balance check — before anything reads the
    /// destination — leaves the would-be address ABSENT from the block
    /// access list.
    ///
    /// <para>
    /// EIP-7928 §Specification qualifies the rule the whole file turns on:
    /// an address is listed <i>"only if the target account is accessed"</i>.
    /// Computing an address is arithmetic over the creator and its nonce; it
    /// touches no account, so a create that never gets as far as loading the
    /// destination has not accessed it.
    /// </para>
    ///
    /// <para>
    /// The pair below differs in one thing only — whether the creator can
    /// fund the endowment — so the absence is attributable to the abort and
    /// to nothing else. Absence on its own proves very little: an engine
    /// that records no creates at all satisfies it, which is what the
    /// funded twin exists to rule out.
    /// </para>
    /// </summary>
    public class CreateAbortAccessListTests
    {
        private const string FactoryAddress = "0x00000000000000000000000000000000000000f0";
        private const ulong FactoryNonce = 1;

        private static readonly byte[] CreateEndowedWithOneWei =
        {
            0x60, 0x00,
            0x60, 0x00,
            0x60, 0x01,
            0xF0,
            0x50,
            0x00
        };

        private static BlockExecutionResult ExecuteFactoryBlock(EvmUInt256 factoryBalance, ulong factoryNonce = FactoryNonce)
        {
            var sender = TestTransactionHelper.GetDefaultSenderAddress();

            var tx = TestTransactionHelper.CreateSignedContractCall(
                FactoryAddress,
                data: new byte[0],
                value: EvmUInt256.Zero,
                nonce: EvmUInt256.Zero,
                gasPrice: new EvmUInt256(1UL),
                gasLimit: new EvmUInt256(1_000_000UL));

            var block = new BlockWitnessData
            {
                BlockNumber = 1,
                Timestamp = 1000,
                BaseFee = 1,
                BlockGasLimit = 30000000,
                ChainId = 1,
                Coinbase = "0x2adc25665018aa1fe0e6bc666dac8fc2697ff9ba",
                Difficulty = new byte[32],
                ParentHash = new byte[32],
                ExtraData = new byte[0],
                MixHash = new byte[32],
                Nonce = new byte[8],
                Features = new BlockFeatureConfig { Fork = HardforkName.Amsterdam },
                Transactions = new List<BlockWitnessTransaction> { tx },
                Accounts = new List<WitnessAccount>
                {
                    new WitnessAccount
                    {
                        Address = sender,
                        Balance = new EvmUInt256(1_000_000_000_000_000_000UL),
                        Nonce = 0,
                        Code = new byte[0],
                        Storage = new List<WitnessStorageSlot>()
                    },
                    new WitnessAccount
                    {
                        Address = FactoryAddress,
                        Balance = factoryBalance,
                        Nonce = factoryNonce,
                        Code = CreateEndowedWithOneWei,
                        Storage = new List<WitnessStorageSlot>()
                    }
                }
            };

            return BlockExecutor.Execute(
                block.AddRequestPredeploys(),
                RlpBlockEncodingProvider.Instance,
                DefaultMainnetHardforkRegistry.Instance);
        }

        private static string WouldBeContractAddress() =>
            ContractUtils.CalculateContractAddress(FactoryAddress, FactoryNonce);

        private static AccountChanges Entry(BlockExecutionResult result, string address) =>
            result.BlockAccessList.SingleOrDefault(
                a => string.Equals(a.Address, address, StringComparison.OrdinalIgnoreCase));

        private static EvmUInt256 NonceOf(BlockExecutionResult result, string address) =>
            result.StateReader.Accounts
                .Single(a => string.Equals(a.Key, address, StringComparison.OrdinalIgnoreCase)).Value.Nonce;

        private static IEnumerable<string> AddressesListed(BlockExecutionResult result) =>
            result.BlockAccessList.Select(a => a.Address.ToLowerInvariant()).OrderBy(a => a);

        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_ACreateThatCannotFundItsEndowment_When_TheBlockAccessListIsBuilt_Then_TheWouldBeAddressIsAbsent()
        {
            var result = ExecuteFactoryBlock(factoryBalance: EvmUInt256.Zero);

            Assert.True(result.TxResults.Single().Success, result.TxResults.Single().Error);
            Assert.NotNull(result.BlockAccessList);
            Assert.True(Entry(result, FactoryAddress) != null, "the creator must be listed");
            Assert.True(Entry(result, WouldBeContractAddress()) == null,
                "the destination of an aborted create was listed — computing an address is not accessing it");
        }

        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_ACreateThatCanFundItsEndowment_When_TheBlockAccessListIsBuilt_Then_TheNewAddressIsPresent()
        {
            var result = ExecuteFactoryBlock(factoryBalance: new EvmUInt256(1UL));

            Assert.True(result.TxResults.Single().Success, result.TxResults.Single().Error);
            Assert.True(Entry(result, WouldBeContractAddress()) != null,
                "a create that proceeds reads its destination and must list it");
        }

        /// <summary>
        /// EIP-2681: <i>"Limit account nonce to be between 0 and 2^64-1."</i>
        ///
        /// <para>A creator already at that limit cannot produce a child, and the
        /// abort happens before the destination is read — so the create leaves no
        /// address behind it and the creator's own nonce does not move. The nonce
        /// asserted below is the one the witness carried: a carrier that narrows it
        /// reports a different number here, and one that drops the abort reports
        /// 2^64. The addresses it touches are those of a create aborted by the
        /// balance check above — the pair the rest of this file establishes has no
        /// destination in it.</para>
        /// </summary>
        [Fact]
        [Trait("Category", "EIP2681")]
        [Trait("Category", "EIP7928")]
        public void Given_AnAccountWithTheMaximumNonce_When_ACreateIsAttempted_Then_ItAbortsWithoutWarmingTheTarget()
        {
            var abortedOnNonce = ExecuteFactoryBlock(
                factoryBalance: new EvmUInt256(1UL), factoryNonce: ulong.MaxValue);

            Assert.True(abortedOnNonce.TxResults.Single().Success, abortedOnNonce.TxResults.Single().Error);
            Assert.Equal(new EvmUInt256(ulong.MaxValue), NonceOf(abortedOnNonce, FactoryAddress));
            Assert.Equal(
                AddressesListed(ExecuteFactoryBlock(factoryBalance: EvmUInt256.Zero)),
                AddressesListed(abortedOnNonce));
        }
    }
}
