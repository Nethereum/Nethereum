using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace Nethereum.ChainStateVerification.NodeData
{
    public class VerifiedNodeDataService : IStateReader
    {
        private readonly IVerifiedStateService _verifiedState;

        public VerifiedNodeDataService(IVerifiedStateService verifiedState)
        {
            _verifiedState = verifiedState ?? throw new ArgumentNullException(nameof(verifiedState));
        }

        public async Task<EvmUInt256> GetBalanceAsync(byte[] address)
            => await GetBalanceAsync(address.ToHex(true)).ConfigureAwait(false);

        public async Task<EvmUInt256> GetBalanceAsync(string address)
        {
            var balance = await _verifiedState.GetBalanceAsync(address).ConfigureAwait(false);
            return EvmUInt256BigIntegerExtensions.FromBigInteger(balance);
        }

        public async Task<byte[]> GetCodeAsync(byte[] address)
            => await GetCodeAsync(address.ToHex(true)).ConfigureAwait(false);

        public Task<byte[]> GetCodeAsync(string address)
            => _verifiedState.GetCodeAsync(address);

        public Task<byte[]> GetBlockHashAsync(long blockNumber)
        {
            if (blockNumber < 0)
                return Task.FromResult(new byte[32]);

            var blockNum = (ulong)blockNumber;
            var header = _verifiedState.GetCurrentHeader();

            if (blockNum > header.BlockNumber || (header.BlockNumber - blockNum) > 256)
                return Task.FromResult(new byte[32]);

            var hash = _verifiedState.GetBlockHash(blockNum);
            return Task.FromResult(hash ?? new byte[32]);
        }

        public async Task<byte[]> GetStorageAtAsync(byte[] address, EvmUInt256 position)
            => await GetStorageAtAsync(address.ToHex(true), position).ConfigureAwait(false);

        public Task<byte[]> GetStorageAtAsync(string address, EvmUInt256 position)
            => _verifiedState.GetStorageAtAsync(address, position.ToBigInteger());

        public async Task<EvmUInt256> GetTransactionCountAsync(byte[] address)
            => await GetTransactionCountAsync(address.ToHex(true)).ConfigureAwait(false);

        public async Task<EvmUInt256> GetTransactionCountAsync(string address)
        {
            var nonce = await _verifiedState.GetNonceAsync(address).ConfigureAwait(false);
            return EvmUInt256BigIntegerExtensions.FromBigInteger(nonce);
        }
    }
}
