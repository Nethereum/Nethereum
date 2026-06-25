using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Validation;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Validation
{
    public class MainnetKnownCheckpointsTests
    {
        // Block 1 — first miner reward. Canonical values from go-ethereum
        // reference fixtures and the public mainnet record.
        private const string Block1StateRoot =
            "0xd67e4d450343046425ae4271474353857ab860dbc0a1dde64b41b5cd3a532bf3";
        private const string Block1BlockHash =
            "0x88e96d4537bea4d9c05d12549907b32561d3bf31f45aae734cdc119f13406cb6";

        // DAO fork block 1,920,000 — the famous post-fork canonical block hash.
        private const string DaoForkStateRoot =
            "0xc5e389416116e3696cce82ec4533cce33efccb24ce245ae9546a4b8f0d5e9a75";
        private const string DaoForkBlockHash =
            "0x4985f5ca3d2afbec36529aa96f74de3cc10a2a4a6c44f2157a57d2c6059a11bb";

        [Fact]
        public void Name_IsStableLabel()
        {
            var sut = new MainnetKnownCheckpoints();
            Assert.Equal("MainnetKnownCheckpoints", sut.Name);
        }

        [Fact]
        public void Count_IsNonZero()
        {
            var sut = new MainnetKnownCheckpoints();
            Assert.True(sut.Count > 0, "Expected at least one pinned checkpoint in the default table.");
        }

        [Fact]
        public async Task GetCanonicalAsync_ReturnsPinnedRoot_AtBlock1()
        {
            var sut = new MainnetKnownCheckpoints();
            var (root, hash) = await sut.GetCanonicalAsync(1, CancellationToken.None);
            Assert.NotNull(root);
            Assert.NotNull(hash);
            Assert.Equal(Block1StateRoot, "0x" + root.ToHex());
            Assert.Equal(Block1BlockHash, "0x" + hash.ToHex());
        }

        [Fact]
        public async Task GetCanonicalAsync_ReturnsPinnedRoot_AtDaoFork()
        {
            var sut = new MainnetKnownCheckpoints();
            var (root, hash) = await sut.GetCanonicalAsync(1_920_000, CancellationToken.None);
            Assert.NotNull(root);
            Assert.NotNull(hash);
            Assert.Equal(DaoForkStateRoot, "0x" + root.ToHex());
            Assert.Equal(DaoForkBlockHash, "0x" + hash.ToHex());
        }

        [Fact]
        public async Task GetCanonicalAsync_ReturnsNull_ForUnknownBlock()
        {
            var sut = new MainnetKnownCheckpoints();
            // Block 12 is not in the table — must report no answer so the
            // composite can fall through to the next source.
            var (root, hash) = await sut.GetCanonicalAsync(12, CancellationToken.None);
            Assert.Null(root);
            Assert.Null(hash);
        }

        [Fact]
        public async Task DiagnoseAsync_ReturnsSourceUnavailable_WhenBlockNotInTable()
        {
            // Block 12 isn't pinned — diagnostics extension reports
            // SourceUnavailable so the caller continues to the next source.
            var sut = new MainnetKnownCheckpoints();
            var verdict = await sut.DiagnoseAsync(
                blockNumber: 12,
                peerHeaderStateRoot: new byte[32],
                ourComputedStateRoot: new byte[32],
                ct: CancellationToken.None);
            Assert.Equal(DivergenceOutcome.SourceUnavailable, verdict.Outcome);
        }

        [Fact]
        public async Task DiagnoseAsync_ReturnsEvmBug_WhenOursDivergesAtPinnedBlock()
        {
            // At block 1 the peer header matches canonical but our compute
            // produced a wrong root → unambiguous EVM bug.
            var sut = new MainnetKnownCheckpoints();
            var canonicalRoot = Block1StateRoot.HexToByteArray();
            var ourRoot = new byte[32];
            for (int i = 0; i < ourRoot.Length; i++) ourRoot[i] = 0xab;

            var verdict = await sut.DiagnoseAsync(
                blockNumber: 1,
                peerHeaderStateRoot: canonicalRoot,
                ourComputedStateRoot: ourRoot,
                ct: CancellationToken.None);

            Assert.Equal(DivergenceOutcome.EvmBug, verdict.Outcome);
            Assert.Equal(canonicalRoot, verdict.CanonicalStateRoot);
            Assert.Equal("MainnetKnownCheckpoints", verdict.SourceName);
        }

        [Fact]
        public async Task DiagnoseAsync_ReturnsEvmBug_WhenOursDivergesAtDaoFork()
        {
            // Same shape at a fork-boundary block: peer is honest, our
            // execution is wrong, classifier flags EvmBug.
            var sut = new MainnetKnownCheckpoints();
            var canonicalRoot = DaoForkStateRoot.HexToByteArray();
            var ourRoot = new byte[32];

            var verdict = await sut.DiagnoseAsync(
                blockNumber: 1_920_000,
                peerHeaderStateRoot: canonicalRoot,
                ourComputedStateRoot: ourRoot,
                ct: CancellationToken.None);

            Assert.Equal(DivergenceOutcome.EvmBug, verdict.Outcome);
        }

        [Fact]
        public async Task DiagnoseAsync_ReturnsPeerLied_WhenPeerHeaderWrongAndOursMatchesCanonical()
        {
            // Peer fed a bad header but our compute matches canonical: peer
            // was on the wrong fork.
            var sut = new MainnetKnownCheckpoints();
            var canonicalRoot = Block1StateRoot.HexToByteArray();
            var badPeerRoot = new byte[32];

            var verdict = await sut.DiagnoseAsync(
                blockNumber: 1,
                peerHeaderStateRoot: badPeerRoot,
                ourComputedStateRoot: canonicalRoot,
                ct: CancellationToken.None);

            Assert.Equal(DivergenceOutcome.PeerLied, verdict.Outcome);
        }

        [Fact]
        public async Task CustomTable_RoundTripsAllEntries()
        {
            // Operator-supplied table — e.g. for a private chain that wants
            // its own milestones pinned. Lookup contract identical to
            // default-ctor behaviour.
            var custom = new Dictionary<ulong, (byte[] StateRoot, byte[] BlockHash)>
            {
                [42] = (new byte[32], new byte[32]),
            };
            for (int i = 0; i < 32; i++) custom[42].StateRoot[i] = (byte)i;

            var sut = new MainnetKnownCheckpoints(custom);
            Assert.Equal(1, sut.Count);

            var (root, hash) = await sut.GetCanonicalAsync(42, CancellationToken.None);
            Assert.NotNull(root);
            Assert.NotNull(hash);

            var (missingRoot, missingHash) = await sut.GetCanonicalAsync(43, CancellationToken.None);
            Assert.Null(missingRoot);
            Assert.Null(missingHash);
        }
    }
}
