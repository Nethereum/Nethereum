using System;
using System.Collections.Generic;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    public class BlockAccessListCollectorTests
    {
        private const string A = "0x000000000000000000000000000000000000000a";
        private static readonly byte[] Code1 = new byte[] { 0x60, 0x00 };

        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_APreExistingAccountAbsentFromTheCollectorsOverlay_When_ATransactionOnlyReadsIt_Then_NoBalanceNonceOrCodeChangeIsRecorded()
        {
            var preBlockReader = new InMemoryStateReader(new Dictionary<string, AccountState>
            {
                [A] = new AccountState { Balance = new EvmUInt256(100), Nonce = new EvmUInt256(5), Code = Code1 }
            });
            var committedReader = new InMemoryStateReader(new Dictionary<string, AccountState>
            {
                [A] = new AccountState { Balance = new EvmUInt256(100), Nonce = new EvmUInt256(5), Code = Code1 }
            });

            var builder = new BlockAccessListBuilder();
            var collector = new BlockAccessListCollector(builder, preBlockReader);

            var executionState = new ExecutionStateService(preBlockReader);
            collector.BeginUnit(1);
            collector.RecordAccountRead(A);
            executionState.CreateOrGetAccountExecutionState(A);

            collector.Record(1, executionState, committedReader);

            var account = Assert.Single(builder.Build());
            Assert.Equal(A, account.Address);
            Assert.Empty(account.BalanceChanges);
            Assert.Empty(account.NonceChanges);
            Assert.Empty(account.CodeChanges);
        }

        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_ASlotWithANonZeroPreBlockValueNotInTheOverlay_When_ATransactionWritesTheSameValue_Then_NoStorageChangeIsRecorded()
        {
            var slot = new EvmUInt256(1);
            var value = new EvmUInt256(42);

            var preBlockReader = new InMemoryStateReader(new Dictionary<string, AccountState>
            {
                [A] = new AccountState
                {
                    Balance = EvmUInt256.Zero,
                    Nonce = EvmUInt256.Zero,
                    Code = null,
                    Storage = new Dictionary<EvmUInt256, byte[]> { [slot] = value.ToBigEndian() }
                }
            });
            var committedReader = new InMemoryStateReader(new Dictionary<string, AccountState>
            {
                [A] = new AccountState
                {
                    Balance = EvmUInt256.Zero,
                    Nonce = EvmUInt256.Zero,
                    Code = null,
                    Storage = new Dictionary<EvmUInt256, byte[]> { [slot] = value.ToBigEndian() }
                }
            });

            var builder = new BlockAccessListBuilder();
            var collector = new BlockAccessListCollector(builder, preBlockReader);

            var executionState = new ExecutionStateService(preBlockReader);
            var acct = executionState.CreateOrGetAccountExecutionState(A);
            acct.Storage[slot] = value.ToBigEndian();

            collector.Record(1, executionState, committedReader);

            Assert.Empty(builder.Build());
        }

        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_AnAccountTheBlockCreates_When_Built_Then_ItsBalanceNonceAndCodeChangesAreAllRecorded()
        {
            var preBlockReader = new InMemoryStateReader(new Dictionary<string, AccountState>());
            var committedReader = new InMemoryStateReader(new Dictionary<string, AccountState>
            {
                [A] = new AccountState { Balance = new EvmUInt256(7), Nonce = new EvmUInt256(1), Code = Code1 }
            });

            var builder = new BlockAccessListBuilder();
            var collector = new BlockAccessListCollector(builder, preBlockReader);

            var executionState = new ExecutionStateService(preBlockReader);
            executionState.CreateOrGetAccountExecutionState(A);

            collector.Record(1, executionState, committedReader);

            var account = Assert.Single(builder.Build());
            Assert.Equal(A, account.Address);
            Assert.Equal(new EvmUInt256(7), Assert.Single(account.BalanceChanges).PostBalance);
            Assert.Equal(1UL, Assert.Single(account.NonceChanges).NewNonce);
            Assert.Equal(Code1, Assert.Single(account.CodeChanges).NewCode);
        }

        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_ANullPreBlockReader_When_TheCollectorIsConstructed_Then_ArgumentNullExceptionIsThrown()
        {
            var builder = new BlockAccessListBuilder();
            Assert.Throws<ArgumentNullException>(() => new BlockAccessListCollector(builder, null));
        }

        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_ASlotWrittenAtTwoConsecutiveIndexes_When_TheSecondWriteReturnsToThePreBlockValue_Then_BothChangesAreRecorded()
        {
            var slot = new EvmUInt256(1);
            var preBlockValue = new EvmUInt256(42);
            var firstValue = new EvmUInt256(43);
            var secondValue = preBlockValue;

            var preBlockReader = new InMemoryStateReader(new Dictionary<string, AccountState>
            {
                [A] = new AccountState
                {
                    Balance = EvmUInt256.Zero,
                    Nonce = EvmUInt256.Zero,
                    Code = null,
                    Storage = new Dictionary<EvmUInt256, byte[]> { [slot] = preBlockValue.ToBigEndian() }
                }
            });

            var builder = new BlockAccessListBuilder();
            var collector = new BlockAccessListCollector(builder, preBlockReader);

            var executionState1 = new ExecutionStateService(preBlockReader);
            var acct1 = executionState1.CreateOrGetAccountExecutionState(A);
            acct1.Storage[slot] = firstValue.ToBigEndian();
            var committedAfterUnit1 = new InMemoryStateReader(new Dictionary<string, AccountState>
            {
                [A] = new AccountState
                {
                    Balance = EvmUInt256.Zero,
                    Nonce = EvmUInt256.Zero,
                    Code = null,
                    Storage = new Dictionary<EvmUInt256, byte[]> { [slot] = firstValue.ToBigEndian() }
                }
            });
            collector.Record(1, executionState1, committedAfterUnit1);

            var executionState2 = new ExecutionStateService(committedAfterUnit1);
            var acct2 = executionState2.CreateOrGetAccountExecutionState(A);
            acct2.Storage[slot] = secondValue.ToBigEndian();
            var committedAfterUnit2 = new InMemoryStateReader(new Dictionary<string, AccountState>
            {
                [A] = new AccountState
                {
                    Balance = EvmUInt256.Zero,
                    Nonce = EvmUInt256.Zero,
                    Code = null,
                    Storage = new Dictionary<EvmUInt256, byte[]> { [slot] = secondValue.ToBigEndian() }
                }
            });
            collector.Record(2, executionState2, committedAfterUnit2);

            var account = Assert.Single(builder.Build());
            var slotChanges = Assert.Single(account.StorageChanges).Changes;
            Assert.Equal(2, slotChanges.Count);
            Assert.Equal(1UL, slotChanges[0].BlockAccessIndex);
            Assert.Equal(firstValue, slotChanges[0].PostValue);
            Assert.Equal(2UL, slotChanges[1].BlockAccessIndex);
            Assert.Equal(secondValue, slotChanges[1].PostValue);
        }
    }
}
