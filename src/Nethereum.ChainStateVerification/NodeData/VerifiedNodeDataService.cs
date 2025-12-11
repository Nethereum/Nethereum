using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.ChainStateVerification.NodeData
{
    public class VerifiedNodeDataService : INodeDataService
    {
        private readonly IVerifiedStateService _verifiedState;

        public VerifiedNodeDataService(IVerifiedStateService verifiedState)
        {
            _verifiedState = verifiedState ?? throw new ArgumentNullException(nameof(verifiedState));
        }

        public async Task<BigInteger> GetBalanceAsync(byte[] address)
        {
            var addressHex = address.ToHex(true);
            return await GetBalanceAsync(addressHex).ConfigureAwait(false);
        }

        public async Task<BigInteger> GetBalanceAsync(string address)
        {
            return await _verifiedState.GetBalanceAsync(address).ConfigureAwait(false);
        }

        public async Task<byte[]> GetCodeAsync(byte[] address)
        {
            var addressHex = address.ToHex(true);
            return await GetCodeAsync(addressHex).ConfigureAwait(false);
        }

        public async Task<byte[]> GetCodeAsync(string address)
        {
            return await _verifiedState.GetCodeAsync(address).ConfigureAwait(false);
        }

        public Task<byte[]> GetBlockHashAsync(BigInteger blockNumber)
        {
            if (blockNumber < 0)
            {
                return Task.FromResult(new byte[32]);
            }

            var blockNum = (ulong)blockNumber;
            var header = _verifiedState.GetCurrentHeader();

            if (blockNum > header.BlockNumber || (header.BlockNumber - blockNum) > 256)
            {
                return Task.FromResult(new byte[32]);
            }

            var hash = _verifiedState.GetBlockHash(blockNum);
            return Task.FromResult(hash ?? new byte[32]);
        }

        public async Task<byte[]> GetStorageAtAsync(byte[] address, BigInteger position)
        {
            var addressHex = address.ToHex(true);
            return await GetStorageAtAsync(addressHex, position).ConfigureAwait(false);
        }

        public async Task<byte[]> GetStorageAtAsync(string address, BigInteger position)
        {
            return await _verifiedState.GetStorageAtAsync(address, position).ConfigureAwait(false);
        }

        public async Task<BigInteger> GetTransactionCount(byte[] address)
        {
            var addressHex = address.ToHex(true);
            return await GetTransactionCount(addressHex).ConfigureAwait(false);
        }

        public async Task<BigInteger> GetTransactionCount(string address)
        {
            return await _verifiedState.GetNonceAsync(address).ConfigureAwait(false);
        }
    }
}
