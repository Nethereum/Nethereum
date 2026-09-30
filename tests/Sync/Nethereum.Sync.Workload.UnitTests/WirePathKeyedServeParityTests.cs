using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia.ProofVerification;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Chain.TestData.Vectors;
using Xunit;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;

namespace Nethereum.Chain.TestData.UnitTests
{
    public class WirePathKeyedServeParityTests : IClassFixture<WirePathKeyedServeParityTests.Fixture>
    {
        private readonly Fixture _fx;
        public WirePathKeyedServeParityTests(Fixture fx) => _fx = fx;

        [Theory]
        [InlineData(55)]
        [InlineData(60)]
        [InlineData(70)]
        public async Task GetProof_AsOfInWindowPastBlock_VerifiesAgainstThatBlockStateRoot_AndNotItsParent(int nInt)
        {
            ulong n = (ulong)nInt;
            Assert.True(n > _fx.Server.Floor && n < _fx.Server.Head,
                $"block {n} must be in-window: floor {_fx.Server.Floor} < n < head {_fx.Server.Head}");

            var rootN = _fx.Server.Roots[n];
            var resp = await _fx.Server.GetProofAsync(_fx.Account, new string[0], "0x" + n.ToString("x"));
            Assert.Null(resp.Error);

            var proof = ToAccountProof(resp.Result);
            Assert.True(VerifyAccount(_fx.Account, proof, rootN), $"as-of-{n} account proof must verify vs block {n} root");
            Assert.False(VerifyAccount(_fx.Account, proof, _fx.Server.Roots[n - 1]),
                $"as-of-{n} account proof must NOT verify vs block {n - 1} root (the account changed at {n})");
        }

        [Fact]
        public async Task GetProof_Latest_StillServesTheHeadAccount()
        {
            var headRoot = _fx.Server.Roots[_fx.Server.Head];
            var resp = await _fx.Server.GetProofAsync(_fx.Account, new string[0], "latest");
            Assert.Null(resp.Error);
            var proof = ToAccountProof(resp.Result);
            Assert.True(VerifyAccount(_fx.Account, proof, headRoot), "LATEST account proof must verify vs the head root");
        }

        [Fact]
        public async Task GetProof_BelowRetainedFloor_Returns32000()
        {
            ulong belowFloor = 1;
            Assert.True(belowFloor < _fx.Server.Floor, $"block {belowFloor} must be below the floor {_fx.Server.Floor}");

            var resp = await _fx.Server.GetProofAsync(_fx.Account, new string[0], "0x" + belowFloor.ToString("x"));
            Assert.NotNull(resp.Error);
            Assert.Equal(-32000, resp.Error.Code);
        }

        [Fact]
        public async Task HistoryIndexOff_Server_DeclinesHistorical_With32000()
        {
            using var indexOff = await PathKeyedWorkloadServer.CreateAsync(
                _fx.Sequencer, pathKeyed: true, trieNodeHistoryBlocks: Fixture.Window, historyIndex: false);

            Assert.Null(indexOff.Bundle.NodeServing);
            Assert.False(((Nethereum.CoreChain.Services.IHistoricalProofCapable)indexOff.Node)
                .CanServeProofAsOf(5, indexOff.Head));

            var resp = await indexOff.GetProofAsync(_fx.Account, new string[0], "0x5");
            Assert.NotNull(resp.Error);
            Assert.Equal(-32000, resp.Error.Code);
        }

        [Theory]
        [InlineData(60)]
        [InlineData(70)]
        public async Task Snap_GetAccountRange_AsOfInWindowBlock_ProofVerifies_HeadServes_AndBelowFloorDeclines(int nInt)
        {
            ulong n = (ulong)nInt;
            Assert.True(n > _fx.Server.Floor && n < _fx.Server.Head,
                $"block {n} must be in-window: floor {_fx.Server.Floor} < n < head {_fx.Server.Head}");

            var ns = _fx.Server.Bundle.NodeServing;
            var zero = new byte[32];
            var full = new byte[32];
            for (int i = 0; i < 32; i++) full[i] = 0xff;

            var asOfN = new PatriciaSnapRequestHandler(
                new AsOfBlockNodeStore(ns.Journal, ns.Latest, n), new StateStoreBytecodeStore(_fx.Server.Bundle.State));
            var rootN = _fx.Server.Roots[n];
            var respN = await asOfN.GetAccountRangeAsync(new GetAccountRangeMessage
            {
                RequestId = 1, RootHash = rootN, StartingHash = zero, LimitHash = full, ResponseBytes = 4UL * 1024 * 1024,
            });
            Assert.NotEmpty(respN.Accounts);
            Assert.True(VerifyAccountRange(rootN, zero, respN), $"as-of-{n} account range proof must verify vs block {n} root");

            var handler = _fx.Server.SnapHandler;
            var headRoot = _fx.Server.Roots[_fx.Server.Head];
            var respHead = await handler.GetAccountRangeAsync(new GetAccountRangeMessage
            {
                RequestId = 2, RootHash = headRoot, StartingHash = zero, LimitHash = full, ResponseBytes = 4UL * 1024 * 1024,
            });
            Assert.NotEmpty(respHead.Accounts);
            Assert.True(VerifyAccountRange(headRoot, zero, respHead), "head account range proof must verify vs head root");

            var belowFloorRoot = _fx.Server.Roots[1];
            Assert.True(1 < _fx.Server.Floor, $"block 1 must be below floor {_fx.Server.Floor}");
            var respLow = await handler.GetAccountRangeAsync(new GetAccountRangeMessage
            {
                RequestId = 3, RootHash = belowFloorRoot, StartingHash = zero, LimitHash = full, ResponseBytes = 4UL * 1024 * 1024,
            });
            Assert.Empty(respLow.Accounts);
        }

        [Fact]
        public void Selector_ResolvesInWindowNonLatestRoot_ToItsOwnBlock()
        {
            var journal = _fx.Server.Bundle.NodeServing.Journal;
            for (ulong n = _fx.Server.Floor + 1; n < _fx.Server.Head; n++)
            {
                var mapped = journal.FindBlockByStateRoot(_fx.Server.Roots[n]);
                Assert.Equal(n, mapped);
            }
        }

        [Fact]
        public async Task PathKeyedAndHashKeyed_Servers_ServeByteIdenticalData_AtTip()
        {
            using var hash = await PathKeyedWorkloadServer.CreateAsync(_fx.Sequencer, pathKeyed: false);
            Assert.False(hash.PathKeyed);
            Assert.True(_fx.Server.PathKeyed);
            Assert.Equal(_fx.Server.Head, hash.Head);
            Assert.Equal(_fx.Server.Roots[_fx.Server.Head].ToHex(), hash.Roots[hash.Head].ToHex());

            var pathResp = await _fx.Server.GetProofAsync(_fx.Account, new string[0], "latest");
            var hashResp = await hash.GetProofAsync(_fx.Account, new string[0], "latest");
            Assert.Null(pathResp.Error);
            Assert.Null(hashResp.Error);
            var p = ToAccountProof(pathResp.Result);
            var h = ToAccountProof(hashResp.Result);

            Assert.Equal(h.Balance.Value, p.Balance.Value);
            Assert.Equal(h.Nonce.Value, p.Nonce.Value);
            Assert.Equal(h.CodeHash, p.CodeHash);
            Assert.Equal(h.StorageHash, p.StorageHash);
            Assert.Equal(
                h.AccountProofs.Select(x => x.ToLowerInvariant()).ToList(),
                p.AccountProofs.Select(x => x.ToLowerInvariant()).ToList());

            var headRoot = _fx.Server.Roots[_fx.Server.Head];
            Assert.True(VerifyAccount(_fx.Account, p, headRoot), "path-keyed head proof must verify vs head root");
            Assert.True(VerifyAccount(_fx.Account, h, headRoot), "hash-keyed head proof must verify vs head root");

            var zero = new byte[32];
            var full = new byte[32];
            for (int i = 0; i < 32; i++) full[i] = 0xff;
            var req = new Func<GetAccountRangeMessage>(() => new GetAccountRangeMessage
            {
                RequestId = 1, RootHash = headRoot, StartingHash = zero, LimitHash = full, ResponseBytes = 4UL * 1024 * 1024,
            });
            var pathRange = await _fx.Server.SnapHandler.GetAccountRangeAsync(req());
            var hashRange = await hash.SnapHandler.GetAccountRangeAsync(req());

            Assert.NotEmpty(pathRange.Accounts);
            Assert.Equal(hashRange.Accounts.Count, pathRange.Accounts.Count);
            for (int i = 0; i < pathRange.Accounts.Count; i++)
            {
                Assert.Equal(hashRange.Accounts[i].Hash.ToHex(), pathRange.Accounts[i].Hash.ToHex());
                Assert.Equal(hashRange.Accounts[i].Body.ToHex(), pathRange.Accounts[i].Body.ToHex());
            }
        }

        private static bool VerifyAccount(string address, AccountProof proof, byte[] stateRoot)
        {
            var proofBytes = proof.AccountProofs.Select(p => p.HexToByteArray()).ToList();
            var account = new Account
            {
                Balance = proof.Balance.Value,
                Nonce = proof.Nonce.Value,
                CodeHash = proof.CodeHash.HexToByteArray(),
                StateRoot = proof.StorageHash.HexToByteArray()
            };
            return ProofVerification.Current.Account.Verify(stateRoot, proofBytes, address, account);
        }

        private static bool VerifyAccountRange(byte[] stateRoot, byte[] origin, AccountRangeMessage resp)
        {
            var keys = new List<byte[]>(resp.Accounts.Count);
            var values = new List<byte[]>(resp.Accounts.Count);
            foreach (var entry in resp.Accounts)
            {
                keys.Add(entry.Hash);
                values.Add(SlimAccountEncoder.FromSlim(entry.Body));
            }
            var proof = (IList<byte[]>)(resp.Proof ?? new List<byte[]>());
            return ProofVerification.Current.Range.Verify(stateRoot, origin, keys, values, proof).Valid;
        }

        private static AccountProof ToAccountProof(object result)
        {
            if (result is AccountProof ap) return ap;
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(result);
            return Newtonsoft.Json.JsonConvert.DeserializeObject<AccountProof>(json);
        }

        public sealed class Fixture : IDisposable
        {
            public const int Window = 32;
            public const int TailBlocks = 24;

            public InProcessSequencerDriver Sequencer { get; }
            public PathKeyedWorkloadServer Server { get; }
            public string Account { get; }

            private static BigInteger Eth(long n) => new BigInteger(n) * BigInteger.Pow(10, 18);

            public Fixture()
            {
                Sequencer = InProcessSequencerDriver.CreateAsync(generatedAccounts: 50).GetAwaiter().GetResult();
                new WorkloadV1().BuildAsync(Sequencer).GetAwaiter().GetResult();
                for (var i = 0; i < TailBlocks; i++)
                {
                    Sequencer.QueueTransfer(Sequencer.Accounts.Alice, Sequencer.Accounts.Bob.Address, Eth(1));
                    Sequencer.ProduceBlockAsync().GetAwaiter().GetResult();
                }
                Account = Sequencer.Accounts.Bob.Address;
                Server = PathKeyedWorkloadServer.CreateAsync(
                    Sequencer, pathKeyed: true, trieNodeHistoryBlocks: Window, historyIndex: true)
                    .GetAwaiter().GetResult();
            }

            public void Dispose() => Server.Dispose();
        }
    }
}
