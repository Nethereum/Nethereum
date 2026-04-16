using System.Collections.Generic;
using Nethereum.EVM.BlockchainState;
using Nethereum.Util;
#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Witness
{
    /// <summary>
    /// Wraps any <see cref="IStateReader"/> and records every state access.
    /// After execution, call <see cref="GetWitnessAccounts"/> to get the
    /// minimal pre-state needed to re-execute the block/transaction.
    ///
    /// Works for both our own chain (direct state) and external chains (RPC).
    /// The recorded witness feeds into <see cref="BinaryBlockWitness.Serialize"/>
    /// for Zisk proving or stateless validation.
    /// </summary>
    public class WitnessRecordingStateReader : IStateReader
    {
        private readonly IStateReader _inner;

        private readonly Dictionary<string, WitnessAccountBuilder> _accounts =
            new Dictionary<string, WitnessAccountBuilder>(System.StringComparer.OrdinalIgnoreCase);

        public WitnessRecordingStateReader(IStateReader inner)
        {
            _inner = inner;
        }

        private WitnessAccountBuilder GetOrCreate(string address)
        {
            address = AddressUtil.Current.ConvertToValid20ByteAddress(address).ToLower();
            if (!_accounts.TryGetValue(address, out var builder))
            {
                builder = new WitnessAccountBuilder { Address = address };
                _accounts[address] = builder;
            }
            return builder;
        }

        public List<WitnessAccount> GetWitnessAccounts()
        {
            var result = new List<WitnessAccount>(_accounts.Count);
            foreach (var kvp in _accounts)
            {
                var b = kvp.Value;
                result.Add(new WitnessAccount
                {
                    Address = b.Address,
                    Balance = b.Balance ?? EvmUInt256.Zero,
                    Nonce = b.Nonce ?? 0,
                    Code = b.Code ?? new byte[0],
                    Storage = new List<WitnessStorageSlot>(b.Storage.Values)
                });
            }
            return result;
        }

#if EVM_SYNC
        public EvmUInt256 GetBalance(byte[] address) => GetBalance(address.ConvertToEthereumChecksumAddress());
        public EvmUInt256 GetBalance(string address)
        {
            var val = _inner.GetBalance(address);
            GetOrCreate(address).Balance = val;
            return val;
        }

        public byte[] GetCode(byte[] address) => GetCode(address.ConvertToEthereumChecksumAddress());
        public byte[] GetCode(string address)
        {
            var val = _inner.GetCode(address);
            GetOrCreate(address).Code = val;
            return val;
        }

        public byte[] GetStorageAt(byte[] address, EvmUInt256 position) => GetStorageAt(address.ConvertToEthereumChecksumAddress(), position);
        public byte[] GetStorageAt(string address, EvmUInt256 position)
        {
            var val = _inner.GetStorageAt(address, position);
            var builder = GetOrCreate(address);
            builder.Storage[position] = new WitnessStorageSlot { Key = position, Value = EvmUInt256.FromBigEndian(val ?? new byte[32]) };
            return val;
        }

        public EvmUInt256 GetTransactionCount(byte[] address) => GetTransactionCount(address.ConvertToEthereumChecksumAddress());
        public EvmUInt256 GetTransactionCount(string address)
        {
            var val = _inner.GetTransactionCount(address);
            GetOrCreate(address).Nonce = (long)(ulong)val;
            return val;
        }
#else
        public async Task<EvmUInt256> GetBalanceAsync(byte[] address) => await GetBalanceAsync(address.ConvertToEthereumChecksumAddress());
        public async Task<EvmUInt256> GetBalanceAsync(string address)
        {
            var val = await _inner.GetBalanceAsync(address);
            GetOrCreate(address).Balance = val;
            return val;
        }

        public async Task<byte[]> GetCodeAsync(byte[] address) => await GetCodeAsync(address.ConvertToEthereumChecksumAddress());
        public async Task<byte[]> GetCodeAsync(string address)
        {
            var val = await _inner.GetCodeAsync(address);
            GetOrCreate(address).Code = val;
            return val;
        }

        public async Task<byte[]> GetStorageAtAsync(byte[] address, EvmUInt256 position) => await GetStorageAtAsync(address.ConvertToEthereumChecksumAddress(), position);
        public async Task<byte[]> GetStorageAtAsync(string address, EvmUInt256 position)
        {
            var val = await _inner.GetStorageAtAsync(address, position);
            var builder = GetOrCreate(address);
            builder.Storage[position] = new WitnessStorageSlot { Key = position, Value = EvmUInt256.FromBigEndian(val ?? new byte[32]) };
            return val;
        }

        public async Task<EvmUInt256> GetTransactionCountAsync(byte[] address) => await GetTransactionCountAsync(address.ConvertToEthereumChecksumAddress());
        public async Task<EvmUInt256> GetTransactionCountAsync(string address)
        {
            var val = await _inner.GetTransactionCountAsync(address);
            GetOrCreate(address).Nonce = (long)(ulong)val;
            return val;
        }
#endif

        private class WitnessAccountBuilder
        {
            public string Address;
            public EvmUInt256? Balance;
            public long? Nonce;
            public byte[] Code;
            public Dictionary<EvmUInt256, WitnessStorageSlot> Storage = new Dictionary<EvmUInt256, WitnessStorageSlot>();
        }
    }
}
