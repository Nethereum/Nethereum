using System;
using System.Collections.Generic;
using Nethereum.EVM.BlockchainState;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    public class AddressNativeBoundariesTests
    {
        private static readonly EvmAddress Account = EvmAddress.FromHex("0x00000000000000000000000000000000000000aa");

        private sealed class StringRefusingRecorder : IAddressAccessRecorder
        {
            public readonly List<EvmAddress> AccountReads = new List<EvmAddress>();
            public readonly List<(EvmAddress Address, EvmUInt256 Key)> StorageReads = new List<(EvmAddress, EvmUInt256)>();

            public void RecordAccountRead(string address) => throw new InvalidOperationException("string address recorded");
            public void RecordStorageRead(string address, EvmUInt256 key) => throw new InvalidOperationException("string address recorded");
            public void RecordAccountRead(EvmAddress address) => AccountReads.Add(address);
            public void RecordStorageRead(EvmAddress address, EvmUInt256 key) => StorageReads.Add((address, key));
        }

        private sealed class StringOnlyRecorder : IStateAccessRecorder
        {
            public readonly List<string> AccountReads = new List<string>();
            public readonly List<string> StorageReads = new List<string>();

            public void RecordAccountRead(string address) => AccountReads.Add(address);
            public void RecordStorageRead(string address, EvmUInt256 key) => StorageReads.Add(address);
        }

        private sealed class StringRefusingStorageReader : IStateReader, IAccountStorageAddressReader
        {
            private readonly InMemoryStateReader _inner = new InMemoryStateReader(new Dictionary<string, AccountState>());
            public int AddressCalls;

            public bool AccountHasStorage(string address) => throw new InvalidOperationException("string address read");
            public bool AccountHasStorage(EvmAddress address) { AddressCalls++; return true; }

            public EvmUInt256 GetBalance(byte[] address) => _inner.GetBalance(address);
            public EvmUInt256 GetBalance(string address) => _inner.GetBalance(address);
            public byte[] GetCode(byte[] address) => _inner.GetCode(address);
            public byte[] GetCode(string address) => _inner.GetCode(address);
            public byte[] GetStorageAt(byte[] address, EvmUInt256 position) => _inner.GetStorageAt(address, position);
            public byte[] GetStorageAt(string address, EvmUInt256 position) => _inner.GetStorageAt(address, position);
            public EvmUInt256 GetTransactionCount(byte[] address) => _inner.GetTransactionCount(address);
            public EvmUInt256 GetTransactionCount(string address) => _inner.GetTransactionCount(address);
            public bool AccountExists(string address) => _inner.AccountExists(address);
            public byte[] GetBlockHash(long blockNumber) => _inner.GetBlockHash(blockNumber);
        }

        private static ExecutionStateService NewState()
            => new ExecutionStateService(new InMemoryStateReader(new Dictionary<string, AccountState>()));

        [Fact]
        public void Given_a_recorder_that_accepts_addresses_When_execution_reads_an_account_and_a_slot_Then_it_receives_the_EvmAddress_and_no_string_is_recorded()
        {
            var state = NewState();
            var recorder = new StringRefusingRecorder();
            state.AccessRecorder = recorder;

            state.GetCode(Account);
            state.GetFromStorage(Account, new EvmUInt256(7));

            Assert.Contains(Account, recorder.AccountReads);
            Assert.Equal((Account, new EvmUInt256(7)), Assert.Single(recorder.StorageReads));
        }

        [Fact]
        public void Given_a_recorder_that_only_knows_strings_When_execution_reads_an_account_and_a_slot_Then_it_still_receives_the_lowercase_hex_address()
        {
            var state = NewState();
            var recorder = new StringOnlyRecorder();
            state.AccessRecorder = recorder;

            state.GetCode(Account);
            state.GetFromStorage(Account, new EvmUInt256(7));

            Assert.Contains(Account.ToHexLower(), recorder.AccountReads);
            Assert.Equal(Account.ToHexLower(), Assert.Single(recorder.StorageReads));
        }

        [Fact]
        public void Given_a_state_reader_that_answers_by_address_When_CREATE_asks_whether_the_account_has_storage_Then_the_address_overload_answers_and_no_string_is_built()
        {
            var reader = new StringRefusingStorageReader();
            var state = new ExecutionStateService(reader);

            Assert.True(state.AccountHasStorage(Account));
            Assert.Equal(1, reader.AddressCalls);
        }

        [Fact]
        public void Given_a_state_reader_that_only_knows_strings_When_CREATE_asks_whether_the_account_has_storage_Then_the_string_reader_still_answers()
        {
            var accounts = new Dictionary<string, AccountState>
            {
                [Account.ToHexLower()] = new AccountState
                {
                    Balance = EvmUInt256.Zero,
                    Nonce = EvmUInt256.Zero,
                    Storage = new Dictionary<EvmUInt256, byte[]> { [new EvmUInt256(1)] = new EvmUInt256(5).ToBigEndian() }
                }
            };
            var state = new ExecutionStateService(new InMemoryStateReader(accounts));

            Assert.True(state.AccountHasStorage(Account));
        }
    }
}
