using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Consensus.Clique;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Consensus.Clique.UnitTests
{
    /// <summary>
    /// EIP-225 puts the seal in extraData so that a receiver "would allow anyone obtaining a block to
    /// verify it against a list of authorized signers". Until this gate existed, an AppChain follower
    /// imported whatever a peer sent it: no configuration in the tree performed that check.
    /// </summary>
    public class CliqueConsensusBlockGateTests
    {
        private static readonly EthECKey Authorised =
            new EthECKey("0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80");
        private static readonly EthECKey Stranger =
            new EthECKey("0xdbda1821b80551c9d65939329250298aa3472ba22feea921c0cf5d620ea67b97");

        private static CliqueEngine EngineFor(EthECKey signer) => new CliqueEngine(new CliqueConfig
        {
            InitialSigners = new List<string> { Authorised.GetPublicAddress() },
            LocalSignerAddress = signer.GetPublicAddress(),
            LocalSignerPrivateKey = signer.GetPrivateKey(),
            BlockPeriodSeconds = 1,
            EpochLength = 30000
        });

        private static BlockHeader SealedBy(EthECKey signer, long number, BigInteger difficulty)
        {
            var header = new BlockHeader
            {
                BlockNumber = number,
                ParentHash = new byte[32],
                Difficulty = (EvmUInt256)difficulty,
                MixHash = new byte[32],
                Nonce = new byte[8],
                ExtraData = new byte[CliqueEngine.EXTRA_VANITY + CliqueEngine.EXTRA_SEAL],
                Timestamp = 1
            };
            var engine = EngineFor(signer);
            engine.InsertSignature(header.ExtraData, engine.SignBlock(header));
            return header;
        }

        private static CliqueConsensusBlockGate GateFor(CliqueEngine engine) =>
            new CliqueConsensusBlockGate(engine, new InMemoryBlockStore());

        [Fact]
        public async Task Given_ABlockSealedByAnAuthorisedSigner_When_TheGateIsAsked_Then_ItIsAccepted()
        {
            var engine = EngineFor(Authorised);
            var header = SealedBy(Authorised, 1, CliqueEngine.DIFF_IN_TURN);

            var verdict = await GateFor(engine).IsBlockCanonicalAsync(header, new byte[32], CancellationToken.None);

            Assert.True(verdict.Accepted, verdict.Reason);
        }

        [Fact]
        public async Task Given_ABlockSealedByAnUnauthorisedSigner_When_TheGateIsAsked_Then_ItIsRejected()
        {
            var engine = EngineFor(Authorised);
            var header = SealedBy(Stranger, 1, CliqueEngine.DIFF_IN_TURN);

            var verdict = await GateFor(engine).IsBlockCanonicalAsync(header, new byte[32], CancellationToken.None);

            Assert.False(verdict.Accepted);
            Assert.Contains("nauthorized", verdict.Reason ?? "");
        }

        [Fact]
        public async Task Given_ABlockWhoseDifficultyIsNotTheSignersTurn_When_TheGateIsAsked_Then_ItIsRejected()
        {
            var engine = EngineFor(Authorised);
            var header = SealedBy(Authorised, 1, CliqueEngine.DIFF_OUT_OF_TURN);

            var verdict = await GateFor(engine).IsBlockCanonicalAsync(header, new byte[32], CancellationToken.None);

            Assert.False(verdict.Accepted);
        }

        [Fact]
        public async Task Given_ABlockWithNoSealAtAll_When_TheGateIsAsked_Then_ItIsRejected()
        {
            var engine = EngineFor(Authorised);
            var header = new BlockHeader
            {
                BlockNumber = 1,
                ParentHash = new byte[32],
                Difficulty = (EvmUInt256)(BigInteger)CliqueEngine.DIFF_IN_TURN,
                MixHash = new byte[32],
                Nonce = new byte[8],
                ExtraData = new byte[4],
                Timestamp = 1
            };

            var verdict = await GateFor(engine).IsBlockCanonicalAsync(header, new byte[32], CancellationToken.None);

            Assert.False(verdict.Accepted);
        }

        [Fact]
        public async Task Given_ABlockTheGateAccepted_When_ItHasNotBeenImportedYet_Then_TheCliqueSnapshotHasNotAdvanced()
        {
            var engine = EngineFor(Authorised);
            var header = SealedBy(Authorised, 1, CliqueEngine.DIFF_IN_TURN);
            var before = engine.CurrentSnapshot.BlockNumber;

            var verdict = await GateFor(engine).IsBlockCanonicalAsync(header, new byte[32], CancellationToken.None);

            Assert.True(verdict.Accepted, verdict.Reason);
            Assert.Equal(before, engine.CurrentSnapshot.BlockNumber);
        }

        [Fact]
        public async Task Given_ABlockThatImported_When_TheGateIsTold_Then_TheCliqueSnapshotAdvancesToIt()
        {
            var engine = EngineFor(Authorised);
            var gate = GateFor(engine);
            var header = SealedBy(Authorised, 1, CliqueEngine.DIFF_IN_TURN);

            await gate.IsBlockCanonicalAsync(header, new byte[32], CancellationToken.None);
            await gate.OnBlockImportedAsync(header, new byte[32], CancellationToken.None);

            Assert.Equal(1, engine.CurrentSnapshot.BlockNumber);
        }

        [Fact]
        public async Task Given_ABlockWithAnUnauthorisedSeal_When_TheGateIsToldItImported_Then_TheSnapshotStillDoesNotAdvance()
        {
            var engine = EngineFor(Authorised);
            var header = SealedBy(Stranger, 1, CliqueEngine.DIFF_IN_TURN);

            await GateFor(engine).OnBlockImportedAsync(header, new byte[32], CancellationToken.None);

            Assert.Equal(0, engine.CurrentSnapshot.BlockNumber);
        }

        [Fact]
        public void Given_ACliqueNodeHoldingNoSigningKeyAtAll_When_TheEngineIsBuilt_Then_ItComposesInsteadOfThrowing()
        {
            var engine = new CliqueEngine(new CliqueConfig
            {
                InitialSigners = new List<string> { Authorised.GetPublicAddress() },
                LocalSignerAddress = null,
                LocalSignerPrivateKey = null,
                BlockPeriodSeconds = 1,
                EpochLength = 30000
            });

            Assert.False(engine.CanProduceBlock(1));
        }
    }
}
