using System.Collections.Generic;
using System.Numerics;
using Nethereum.EVM.BlockchainState;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    public class InMemoryStateReaderTests
    {
        [Fact]
        public void GetBalance_ExistingAccount_ReturnsBalance()
        {
            var accounts = new Dictionary<string, AccountState>
            {
                ["0x1234567890abcdef1234567890abcdef12345678"] = new AccountState
                {
                    Balance = new EvmUInt256(1000000)
                }
            };
            var reader = new InMemoryStateReader(accounts);
            var balance = reader.GetBalance("0x1234567890abcdef1234567890abcdef12345678");
            Assert.Equal(new EvmUInt256(1000000), balance);
        }

        [Fact]
        public void GetBalance_NonExistentAccount_ReturnsZero()
        {
            var reader = new InMemoryStateReader(new Dictionary<string, AccountState>());
            var balance = reader.GetBalance("0xdeadbeefdeadbeefdeadbeefdeadbeefdeadbeef");
            Assert.Equal(EvmUInt256.Zero, balance);
        }

        [Fact]
        public void GetCode_ExistingAccount_ReturnsCode()
        {
            var code = new byte[] { 0x60, 0x01, 0x60, 0x00, 0x55 };
            var accounts = new Dictionary<string, AccountState>
            {
                ["0x1234567890abcdef1234567890abcdef12345678"] = new AccountState { Code = code }
            };
            var reader = new InMemoryStateReader(accounts);
            Assert.Equal(code, reader.GetCode("0x1234567890abcdef1234567890abcdef12345678"));
        }

        [Fact]
        public void GetCode_NonExistentAccount_ReturnsEmptyArray()
        {
            var reader = new InMemoryStateReader(new Dictionary<string, AccountState>());
            Assert.Empty(reader.GetCode("0xdeadbeefdeadbeefdeadbeefdeadbeefdeadbeef"));
        }

        [Fact]
        public void GetStorageAt_ExistingSlot_ReturnsValue()
        {
            var storageValue = new byte[32];
            storageValue[31] = 0x42;
            var accounts = new Dictionary<string, AccountState>
            {
                ["0x1234567890abcdef1234567890abcdef12345678"] = new AccountState
                {
                    Storage = new Dictionary<EvmUInt256, byte[]>
                    {
                        [EvmUInt256.Zero] = storageValue
                    }
                }
            };
            var reader = new InMemoryStateReader(accounts);
            Assert.Equal(storageValue, reader.GetStorageAt("0x1234567890abcdef1234567890abcdef12345678", EvmUInt256.Zero));
        }

        [Fact]
        public void GetStorageAt_NonExistentSlot_ReturnsZeroBytes()
        {
            var accounts = new Dictionary<string, AccountState>
            {
                ["0x1234567890abcdef1234567890abcdef12345678"] = new AccountState()
            };
            var reader = new InMemoryStateReader(accounts);
            Assert.Equal(new byte[32], reader.GetStorageAt("0x1234567890abcdef1234567890abcdef12345678", EvmUInt256.One));
        }

        [Fact]
        public void GetTransactionCount_ExistingAccount_ReturnsNonce()
        {
            var accounts = new Dictionary<string, AccountState>
            {
                ["0x1234567890abcdef1234567890abcdef12345678"] = new AccountState { Nonce = 42 }
            };
            var reader = new InMemoryStateReader(accounts);
            Assert.Equal(42UL, reader.GetTransactionCount("0x1234567890abcdef1234567890abcdef12345678"));
        }

        [Fact]
        public void GetTransactionCount_NonExistentAccount_ReturnsZero()
        {
            var reader = new InMemoryStateReader(new Dictionary<string, AccountState>());
            Assert.Equal(0UL, reader.GetTransactionCount("0xdeadbeefdeadbeefdeadbeefdeadbeefdeadbeef"));
        }

    }
}
