using System.Collections.Generic;
using System.Linq;
using Nethereum.CoreChain;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests.GeneralStateTests
{
    /// <summary>
    /// EIP-6110 §Block validity: <i>"Beginning with the <c>FORK_BLOCK</c>, each deposit accumulated
    /// in the block MUST appear in the EIP-7685 requests list in the order they appear in the
    /// logs."</i>
    ///
    /// <para>The guest reaches those logs through the receipts it built, not through a logs list it
    /// was handed. Every other guest requests test runs a block with no transactions, so no deposit
    /// log is ever produced and the deposit half of the commitment is unexercised - a guest that
    /// dropped it would still agree with all of them.</para>
    /// </summary>
    public class GuestDepositRequestsTests
    {
        private const byte PayloadOffset = 0x35;
        private const int FirstPubkeyByte = 192;

        private static byte[] Word(int value)
        {
            var word = new byte[32];
            word[28] = (byte)(value >> 24);
            word[29] = (byte)(value >> 16);
            word[30] = (byte)(value >> 8);
            word[31] = (byte)value;
            return word;
        }

        private static byte[] DepositEventData(byte pubkeyByte)
        {
            var data = new List<byte>();
            data.AddRange(Word(160)); data.AddRange(Word(256)); data.AddRange(Word(320));
            data.AddRange(Word(384)); data.AddRange(Word(512));
            data.AddRange(Word(48)); data.AddRange(Enumerable.Repeat((byte)0xa1, 48)); data.AddRange(new byte[16]);
            data.AddRange(Word(32)); data.AddRange(Enumerable.Repeat((byte)0xb2, 32));
            data.AddRange(Word(8)); data.AddRange(Enumerable.Repeat((byte)0xd4, 8)); data.AddRange(new byte[24]);
            data.AddRange(Word(96)); data.AddRange(Enumerable.Repeat((byte)0xc3, 96));
            data.AddRange(Word(8)); data.AddRange(Enumerable.Repeat((byte)0xe5, 8)); data.AddRange(new byte[24]);

            var payload = data.ToArray();
            payload[FirstPubkeyByte] = pubkeyByte;
            return payload;
        }

        /// <summary>
        /// Copies its own trailing payload into memory and emits it under the deposit topic, so the
        /// log the block records is byte-for-byte a real deposit event. EIP-6110 counts a log only
        /// when its address is <c>DEPOSIT_CONTRACT_ADDRESS</c>, so this is deployed there - which
        /// means every deposit in the block comes from this one contract. Stamping the calldata
        /// size over the first pubkey byte is what makes two of them tell apart.
        /// </summary>
        private static byte[] EmitsADepositLogStampedWithItsCalldataSize()
        {
            var prologue = new List<byte>();
            prologue.AddRange(new byte[] { 0x61, 0x02, 0x40 });
            prologue.AddRange(new byte[] { 0x61, 0x00, PayloadOffset });
            prologue.AddRange(new byte[] { 0x60, 0x00 });
            prologue.Add(0x39);
            prologue.Add(0x36);
            prologue.AddRange(new byte[] { 0x60, (byte)FirstPubkeyByte });
            prologue.Add(0x53);
            prologue.Add(0x7f);
            prologue.AddRange(DepositRequests.DepositEventSignatureHash);
            prologue.AddRange(new byte[] { 0x61, 0x02, 0x40 });
            prologue.AddRange(new byte[] { 0x60, 0x00 });
            prologue.Add(0xa1);
            prologue.Add(0x00);

            Assert.Equal(PayloadOffset, prologue.Count);
            return prologue.Concat(DepositEventData(0xa1)).ToArray();
        }

        private static BlockWitnessData BlockWhereEachTransactionEmitsADeposit(params byte[] pubkeyBytes)
        {
            var block = SystemCallExpectations.BlockAt(HardforkName.Amsterdam);
            block.ProduceBlockCommitments = true;
            block.ComputePostStateRoot = false;

            for (var i = 0; i < pubkeyBytes.Length; i++)
                block.Transactions.Add(TestTransactionHelper.CreateSignedContractCall(
                    DepositRequests.DepositContractAddress, new byte[pubkeyBytes[i]],
                    EvmUInt256.Zero, new EvmUInt256((ulong)i), new EvmUInt256(10),
                    new EvmUInt256(500_000)));

            block.Accounts.Add(Account(TestTransactionHelper.GetDefaultSenderAddress(),
                new EvmUInt256(1_000_000_000_000_000), new byte[0]));
            block.Accounts.Add(Account(DepositRequests.DepositContractAddress,
                EvmUInt256.Zero, EmitsADepositLogStampedWithItsCalldataSize()));

            return block;
        }

        private static WitnessAccount Account(string address, EvmUInt256 balance, byte[] code) =>
            new WitnessAccount
            {
                Address = address,
                Balance = balance,
                Nonce = 0,
                Code = code,
                Storage = new List<WitnessStorageSlot>()
            };

        private static BlockExecutionResult Execute(BlockWitnessData block) =>
            BlockExecutor.Execute(
                block.AddRequestPredeploys(),
                RlpBlockEncodingProvider.Instance,
                Nethereum.EVM.Precompiles.DefaultMainnetHardforkRegistry.Instance,
                null,
                new PatriciaBlockRootCalculator());

        private static string CommitmentForDeposits(params byte[] pubkeyBytes) =>
            ExecutionRequests.ComputeRequestsHash(new[]
            {
                ExecutionRequests.Compose(ExecutionRequests.DepositRequestType,
                    pubkeyBytes
                        .SelectMany(p => DepositRequests.ExtractDepositData(DepositEventData(p)))
                        .ToArray())
            }).ToHex();

        [Fact]
        [Trait("Category", "EIP6110")]
        public void Given_ABlockWhoseTransactionEmitsADepositLog_When_TheGuestExecutesIt_Then_TheProducedHeaderCommitsToTheDeposit()
        {
            var result = Execute(BlockWhereEachTransactionEmitsADeposit(0x01));

            Assert.All(result.TxResults, tx => Assert.True(tx.Success, tx.Error));
            Assert.NotNull(result.ProducedHeader.RequestsHash);
            Assert.Equal(CommitmentForDeposits(0x01), result.ProducedHeader.RequestsHash.ToHex());
        }

        /// <summary>
        /// The twin that separates "committed to the deposit" from "committed to whatever an empty
        /// block commits to": with no deposit the type <c>0x00</c> entry carries only its type byte
        /// and EIP-7685 excludes it, so the two commitments must differ.
        /// </summary>
        [Fact]
        [Trait("Category", "EIP6110")]
        public void Given_TheSameBlockWithoutTheDepositTransaction_When_TheGuestExecutesIt_Then_ItCommitsToTheEmptyList()
        {
            var withDeposit = Execute(BlockWhereEachTransactionEmitsADeposit(0x01));
            var withNone = Execute(BlockWhereEachTransactionEmitsADeposit());

            Assert.Equal(ExecutionRequests.ComputeRequestsHash(new byte[0][]).ToHex(),
                withNone.ProducedHeader.RequestsHash.ToHex());
            Assert.NotEqual(withNone.ProducedHeader.RequestsHash.ToHex(),
                withDeposit.ProducedHeader.RequestsHash.ToHex());
        }

        [Fact]
        [Trait("Category", "EIP6110")]
        public void Given_TwoTransactionsEachEmittingADepositLog_When_TheGuestExecutesThem_Then_BothAreCommittedInReceiptOrder()
        {
            var result = Execute(BlockWhereEachTransactionEmitsADeposit(0x01, 0x02));

            Assert.Equal(CommitmentForDeposits(0x01, 0x02), result.ProducedHeader.RequestsHash.ToHex());
            Assert.NotEqual(CommitmentForDeposits(0x02, 0x01), result.ProducedHeader.RequestsHash.ToHex());
        }
    }
}
