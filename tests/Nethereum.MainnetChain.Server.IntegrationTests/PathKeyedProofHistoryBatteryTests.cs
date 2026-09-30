using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia.ProofVerification;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Xunit;
using Xunit.Abstractions;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.Documentation;

namespace Nethereum.MainnetChain.Server.IntegrationTests
{
    [Collection("PathKeyed2MDb")]
    public class PathKeyedProofHistoryBatteryTests
    {
        private readonly ITestOutputHelper _out;
        public PathKeyedProofHistoryBatteryTests(ITestOutputHelper o) => _out = o;

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "state-serving", "Verify eth_getProof account proofs at tip, in-window and floor against stored roots")]
        public async Task GetProof_Account_VerifiesAgainstStoredRoots_Tip_InWindow_Floor_AndDeclinesBelow()
        {
            var dir = Environment.GetEnvironmentVariable("NETHEREUM_PATHKEYED_2M_DB");
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

            using var bundle = RocksDbChainStoreBundle.Open(
                dir,
                journalOptions: HistoricalStateOptions.FullArchive,
                storageOptions: new RocksDbStorageOptions
                {
                    DatabasePath = dir, PathKeyedState = true, TrieNodeHistoryBlocks = 1024, TrieNodeHistoryIndex = true,
                });

            var head = bundle.Metadata.GetLastBlock();
            var floor = bundle.NodeServing.Floor.FloorFor(head);
            _out.WriteLine($"committed-state head={head} floor={floor} window={head - floor}");
            Assert.True(head >= 50_000, $"path-keyed DB has too little committed state to validate proofs; head={head}");

            var latestStore = ((ILatestProofServingBundle)bundle).LatestProofNodeStore;
            var selector = bundle.NodeServing as ISnapNodeStoreSelector;
            Assert.NotNull(selector);

            async Task<BlockHeader> HeaderAt(ulong n) => await bundle.Blocks.GetByNumberAsync(n);

            async Task<ITrieNodeStore> StoreFor(ulong n, byte[] root)
                => n == head ? latestStore : await selector.ResolveForRootAsync(root, default);

            foreach (var n in new[] { head, head - 1, head - 512, floor + 1, floor })
            {
                var header = await HeaderAt(n);
                var root = header.StateRoot;
                var miner = header.Coinbase;

                var store = await StoreFor(n, root);
                Assert.NotNull(store);
                Assert.True(store.ContainsKey(root), $"store must gate-accept the stored root at block {n}");

                var proof = await new ProofService(bundle.State, store)
                    .GenerateAccountProofAsync(miner, new List<BigInteger>(), root);

                Assert.True(VerifyAccount(miner, proof, root), $"account proof at block {n} must verify vs its stored root");

                if (n > floor && n != head)
                {
                    var neighbourRoot = (await HeaderAt(n - 1)).StateRoot;
                    Assert.False(VerifyAccount(miner, proof, neighbourRoot),
                        $"as-of block {n} proof must NOT verify vs block {n - 1} root");
                }
                _out.WriteLine($"getProof @ {n}: miner {miner} balance={proof.Balance.Value} verified vs stored root");
            }

            var belowRoot = (await HeaderAt(floor - 1)).StateRoot;
            var belowStore = await selector.ResolveForRootAsync(belowRoot, default);
            Assert.Null(belowStore);

            var a = floor + 1;
            var b = head;
            var hdrA = await HeaderAt(a);
            var proofA = await new ProofService(bundle.State, await StoreFor(a, hdrA.StateRoot))
                .GenerateAccountProofAsync(hdrA.Coinbase, new List<BigInteger>(), hdrA.StateRoot);
            var hdrB = await HeaderAt(b);
            var proofB = await new ProofService(bundle.State, await StoreFor(b, hdrB.StateRoot))
                .GenerateAccountProofAsync(hdrA.Coinbase, new List<BigInteger>(), hdrB.StateRoot);
            Assert.True(VerifyAccount(hdrA.Coinbase, proofA, hdrA.StateRoot), "historical proof @ floor+1 must verify");
            Assert.True(VerifyAccount(hdrA.Coinbase, proofB, hdrB.StateRoot), "latest proof @ head must verify");
            _out.WriteLine($"history: {hdrA.Coinbase} bal @ {a}={proofA.Balance.Value} vs @ {b}={proofB.Balance.Value} (both proven)");
        }

        private static bool VerifyAccount(string address, AccountProof proof, byte[] stateRoot)
        {
            var proofBytes = proof.AccountProofs.Select(p => p.HexToByteArray()).ToList();
            var account = new Account
            {
                Balance = proof.Balance.Value,
                Nonce = proof.Nonce.Value,
                CodeHash = proof.CodeHash.HexToByteArray(),
                StateRoot = proof.StorageHash.HexToByteArray(),
            };
            return ProofVerification.Current.Account.Verify(stateRoot, proofBytes, address, account);
        }
    }
}
