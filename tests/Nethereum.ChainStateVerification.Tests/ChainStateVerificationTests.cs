using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.ChainStateVerification;
using Nethereum.Consensus.LightClient;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.Model;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.ChainStateVerification.Tests
{
    public class ChainStateVerificationTests
    {
        [Fact]
        public async Task VerifiedStateBackend_ReturnsAccountFromProof()
        {
            var trustedHeader = CreateTrustedHeader();
            var headerProvider = new StubTrustedHeaderProvider(trustedHeader);
            var accountProof = new AccountProof
            {
                Address = "0xabc",
                AccountProofs = new List<string>(),
                StorageProof = new List<StorageProof>()
            };
            var expectedAccount = new Account();
            var trieVerifier = new StubTrieProofVerifier { AccountToReturn = expectedAccount };
            var ethGetProof = new StubEthGetProof(accountProof);
            var backend = new VerifiedStateBackend(headerProvider, ethGetProof, trieVerifier);

            var account = await backend.GetAccountAsync("0xabc");

            Assert.Same(expectedAccount, account);
            Assert.Equal("0xabc", ethGetProof.LastAddress);
            Assert.Equal(trustedHeader.BlockNumber, ethGetProof.LastBlockParameter.BlockNumber.Value);
        }

        [Fact]
        public async Task StorageProofVerifier_ReturnsStorageValue()
        {
            var trustedHeader = CreateTrustedHeader();
            var headerProvider = new StubTrustedHeaderProvider(trustedHeader);
            var storageProof = new StorageProof
            {
                Key = new HexBigInteger(1),
                Value = new HexBigInteger(2),
                Proof = new List<string>()
            };
            var accountProof = new AccountProof
            {
                Address = "0xabc",
                AccountProofs = new List<string>(),
                StorageProof = new List<StorageProof> { storageProof }
            };
            var expectedValue = new byte[] { 0x01, 0x02 };
            var trieVerifier = new StubTrieProofVerifier
            {
                AccountToReturn = new Account(),
                StorageValueToReturn = expectedValue
            };
            var ethGetProof = new StubEthGetProof(accountProof);
            var verifier = new StorageProofVerifier(headerProvider, ethGetProof, trieVerifier);

            var value = await verifier.GetStorageValueAsync("0xabc", "0x1");

            Assert.Equal(expectedValue, value);
            Assert.Equal("0xabc", ethGetProof.LastAddress);
            Assert.Equal("0x1", ethGetProof.LastStorageKeys[0]);
        }

        private static TrustedExecutionHeader CreateTrustedHeader()
        {
            return new TrustedExecutionHeader
            {
                BlockNumber = 123,
                StateRoot = new byte[32],
                ReceiptsRoot = new byte[32],
                BlockHash = new byte[32],
                Timestamp = DateTimeOffset.UtcNow
            };
        }

        private sealed class StubTrustedHeaderProvider : ITrustedHeaderProvider
        {
            private readonly TrustedExecutionHeader _header;

            public StubTrustedHeaderProvider(TrustedExecutionHeader header)
            {
                _header = header;
            }

            public TrustedExecutionHeader GetLatestFinalized() => _header;
            public TrustedExecutionHeader GetLatestOptimistic() => _header;
            public byte[] GetBlockHash(ulong blockNumber) => null;
        }

        private sealed class StubEthGetProof : IEthGetProof
        {
            private readonly AccountProof _response;

            public StubEthGetProof(AccountProof response)
            {
                _response = response;
            }

            public string LastAddress { get; private set; } = string.Empty;
            public string[] LastStorageKeys { get; private set; } = Array.Empty<string>();
            public BlockParameter LastBlockParameter { get; private set; } = BlockParameter.CreateLatest();

            public BlockParameter DefaultBlock { get; set; } = BlockParameter.CreateLatest();

            public RpcRequest BuildRequest(string address, string[] storageKeys, BlockParameter block, object id = null) =>
                throw new NotImplementedException();

            public Task<AccountProof> SendRequestAsync(string address, string[] storageKeys, object id = null) =>
                throw new NotImplementedException();

            public Task<AccountProof> SendRequestAsync(string address, string[] storageKeys, BlockParameter block, object id = null)
            {
                LastAddress = address;
                LastStorageKeys = storageKeys;
                LastBlockParameter = block;
                return Task.FromResult(_response);
            }
        }

        private sealed class StubTrieProofVerifier : ITrieProofVerifier
        {
            public Account AccountToReturn { get; set; } = new Account();
            public byte[] StorageValueToReturn { get; set; } = Array.Empty<byte>();

            public Account VerifyAccountProof(byte[] stateRoot, AccountProof accountProof) => AccountToReturn;

            public byte[] VerifyStorageProof(Account account, StorageProof storageProof) => StorageValueToReturn;
        }
    }
}
