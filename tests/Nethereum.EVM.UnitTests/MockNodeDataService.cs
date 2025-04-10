using Nethereum.EVM.BlockchainState;
using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.EVM.UnitTests
{
    public class MockNodeDataService : INodeDataService
    {
        private static readonly byte[] GenericHash = Enumerable.Repeat((byte)0x42, 32).ToArray(); // example fixed 32-byte hash

        public Task<BigInteger> GetBalanceAsync(byte[] address) => Task.FromResult(BigInteger.Zero);
        public Task<BigInteger> GetBalanceAsync(string address) => Task.FromResult(BigInteger.Zero);
        public Task<byte[]> GetCodeAsync(byte[] address) => Task.FromResult(Array.Empty<byte>());
        public Task<byte[]> GetCodeAsync(string address) => Task.FromResult(Array.Empty<byte>());
        public Task<byte[]> GetBlockHashAsync(BigInteger blockNumber) => Task.FromResult(GenericHash);
        public Task<byte[]> GetStorageAtAsync(byte[] address, BigInteger position) => Task.FromResult(Array.Empty<byte>());
        public Task<byte[]> GetStorageAtAsync(string address, BigInteger position) => Task.FromResult(Array.Empty<byte>());
        public Task<BigInteger> GetTransactionCount(byte[] address) => Task.FromResult(BigInteger.Zero);
        public Task<BigInteger> GetTransactionCount(string address) => Task.FromResult(BigInteger.Zero);
    }
}