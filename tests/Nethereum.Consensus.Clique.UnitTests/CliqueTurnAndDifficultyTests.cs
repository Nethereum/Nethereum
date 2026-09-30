using System.Numerics;
using Nethereum.Consensus.Clique;
using Xunit;

namespace Nethereum.Consensus.Clique.UnitTests
{
    public class CliqueTurnAndDifficultyTests
    {
        private const string SignerA = "0xf39fd6e51aad88f6f4ce6ab8827279cfffb92266";
        private const string SignerB = "0x70997970c51812dc3a010c7d01b50e0d17dc79c8";
        private const string SignerC = "0x3c44cdddb6a900fa2b585dd299e03d12fa4293bc";

        private static CliqueEngine ThreeSigners() => new CliqueEngine(new CliqueConfig
        {
            InitialSigners = new System.Collections.Generic.List<string> { SignerA, SignerB, SignerC },
            LocalSignerAddress = SignerA,
            BlockPeriodSeconds = 1,
            EpochLength = 30000
        });

        [Fact]
        public void Given_ThreeSigners_When_TheTurnIsAsked_Then_ItRotatesOneSignerPerBlock()
        {
            var engine = ThreeSigners();
            // EIP-225, Specification: "SIGNER_INDEX: Zero-based index of the block signer in the
            // sorted list of current authorized signers." Ascending byte order: SignerC < SignerB < SignerA.
            var order = new[] { SignerC, SignerB, SignerA };

            for (long block = 1; block <= 9; block++)
            {
                var expected = order[(int)(block % 3)];
                foreach (var signer in order)
                    Assert.Equal(signer == expected, engine.IsInTurn(block, signer));
            }
        }

        [Fact]
        public void Given_ASignerOutOfTurn_When_TheDifficultyIsAsked_Then_ItIsLowerThanInTurn()
        {
            var engine = ThreeSigners();

            for (long block = 1; block <= 9; block++)
            {
                var inTurn = engine.IsInTurn(block, SignerA);
                var difficulty = engine.GetDifficulty(block, SignerA);
                Assert.Equal(inTurn ? new BigInteger(CliqueEngine.DIFF_IN_TURN)
                                    : new BigInteger(CliqueEngine.DIFF_OUT_OF_TURN), difficulty);
            }
        }

        [Fact]
        public void Given_AnyBlock_When_EveryoneIsAsked_Then_ExactlyOneSignerIsInTurn()
        {
            var engine = ThreeSigners();

            for (long block = 0; block <= 12; block++)
            {
                var inTurnCount = 0;
                foreach (var signer in new[] { SignerA, SignerB, SignerC })
                    if (engine.IsInTurn(block, signer)) inTurnCount++;

                Assert.Equal(1, inTurnCount);
            }
        }

        [Fact]
        public void Given_AnAddressThatIsNotASigner_When_TheTurnIsAsked_Then_ItIsNeverInTurn()
        {
            var engine = ThreeSigners();
            const string stranger = "0x000000000000000000000000000000000000dead";

            for (long block = 0; block <= 12; block++)
            {
                Assert.False(engine.IsInTurn(block, stranger));
                Assert.Equal(new BigInteger(CliqueEngine.DIFF_OUT_OF_TURN),
                    engine.GetDifficulty(block, stranger));
            }

            Assert.False(engine.IsAuthorizedSigner(stranger));
            Assert.True(engine.IsAuthorizedSigner(SignerA));
        }

        [Fact]
        public void Given_ASignerAddressInAnyCasing_When_TheTurnIsAsked_Then_TheAnswerIsTheSame()
        {
            var engine = ThreeSigners();

            for (long block = 0; block <= 6; block++)
                Assert.Equal(engine.IsInTurn(block, SignerB), engine.IsInTurn(block, SignerB.ToUpperInvariant()));
        }
    }
}
