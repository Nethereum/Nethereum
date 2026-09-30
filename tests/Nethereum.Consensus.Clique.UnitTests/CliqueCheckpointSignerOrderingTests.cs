using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Nethereum.Consensus.Clique;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Consensus.Clique.UnitTests
{
    /// <summary>
    /// EIP-225, Specification: "SIGNER_INDEX: Zero-based index of the block signer in the sorted list
    /// of current authorized signers." and "The list of signers in checkpoint block extra-data sections
    /// must be sorted in ascending byte order." These exercise <see cref="CliqueEngine"/> itself, never
    /// a copy of the sort.
    /// </summary>
    public class CliqueCheckpointSignerOrderingTests
    {
        private static readonly EthECKey KeyA = new EthECKey("0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80");
        private static readonly EthECKey KeyB = new EthECKey("0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690");
        private static readonly EthECKey KeyC = new EthECKey("0x5de4111afa1a4b94908f83103eb1f1706367c2e68ca870fc3fb9a804cdab365");

        private static string SortedByAscendingByteOrderSignerAt(int index)
        {
            var order = new[] { KeyC.GetPublicAddress(), KeyB.GetPublicAddress(), KeyA.GetPublicAddress() };
            return order[index];
        }

        private static byte[] EncodeCheckpointExtraData(IEnumerable<EthECKey> signersInOrder)
        {
            var addresses = signersInOrder.Select(k => k.GetPublicAddress()).ToList();
            var extra = new byte[CliqueEngine.EXTRA_VANITY + (addresses.Count * 20) + CliqueEngine.EXTRA_SEAL];
            for (int i = 0; i < addresses.Count; i++)
            {
                var bytes = addresses[i].HexToByteArray();
                Array.Copy(bytes, 0, extra, CliqueEngine.EXTRA_VANITY + (i * 20), 20);
            }
            return extra;
        }

        private static BlockHeader BuildCheckpointHeader(EthECKey sealer, long number, BigInteger difficulty, byte[] extraData)
        {
            var header = new BlockHeader
            {
                BlockNumber = number,
                ParentHash = new byte[32],
                Difficulty = (EvmUInt256)difficulty,
                MixHash = new byte[32],
                Nonce = new byte[8],
                ExtraData = extraData,
                Timestamp = number
            };

            var sealHash = BlockHeaderEncoder.Current.EncodeCliqueSigHeaderAndHash(header);
            var signature = sealer.SignAndCalculateV(sealHash).CreateStringSignature().HexToByteArray();
            Array.Copy(signature, 0, header.ExtraData, header.ExtraData.Length - CliqueEngine.EXTRA_SEAL, CliqueEngine.EXTRA_SEAL);
            return header;
        }

        [Fact]
        public void Given_InitialSignersSuppliedOutOfOrder_When_TheEngineTakesThem_Then_TheyAreSortedBeforeAnyTurnIsComputed()
        {
            var engine = new CliqueEngine(new CliqueConfig
            {
                InitialSigners = new List<string> { KeyA.GetPublicAddress(), KeyB.GetPublicAddress(), KeyC.GetPublicAddress() },
                LocalSignerAddress = KeyA.GetPublicAddress(),
                BlockPeriodSeconds = 1,
                EpochLength = 30000
            });

            for (long block = 0; block <= 5; block++)
            {
                var expected = SortedByAscendingByteOrderSignerAt((int)(block % 3));
                Assert.True(engine.IsInTurn(block, expected));
                foreach (var other in new[] { KeyA.GetPublicAddress(), KeyB.GetPublicAddress(), KeyC.GetPublicAddress() })
                    if (!other.Equals(expected, StringComparison.OrdinalIgnoreCase))
                        Assert.False(engine.IsInTurn(block, other));
            }
        }

        [Fact]
        public void Given_GenesisSignersAppliedOutOfOrder_When_ApplyGenesisSignersIsCalled_Then_TheStoredSignersAreSorted()
        {
            var engine = new CliqueEngine(new CliqueConfig
            {
                InitialSigners = new List<string> { KeyA.GetPublicAddress() },
                LocalSignerAddress = KeyA.GetPublicAddress(),
                BlockPeriodSeconds = 1,
                EpochLength = 30000
            });

            engine.ApplyGenesisSigners(new[] { KeyA.GetPublicAddress(), KeyB.GetPublicAddress(), KeyC.GetPublicAddress() });

            var expected = new List<string>
            {
                SortedByAscendingByteOrderSignerAt(0).ToLowerInvariant(),
                SortedByAscendingByteOrderSignerAt(1).ToLowerInvariant(),
                SortedByAscendingByteOrderSignerAt(2).ToLowerInvariant()
            };
            Assert.Equal(expected, engine.CurrentSnapshot.Signers);
        }

        [Fact]
        public void Given_AnEpochTransitionBlockAppliedWithSignersOutOfOrder_When_TheBlockIsApplied_Then_TheStoredSignersAreSorted()
        {
            var engine = new CliqueEngine(new CliqueConfig
            {
                InitialSigners = new List<string> { KeyA.GetPublicAddress() },
                LocalSignerAddress = KeyA.GetPublicAddress(),
                BlockPeriodSeconds = 1,
                EpochLength = 3
            });

            var outOfOrderExtraData = EncodeCheckpointExtraData(new[] { KeyA, KeyB, KeyC });
            var header = BuildCheckpointHeader(KeyA, 3, BigInteger.One, outOfOrderExtraData);

            engine.ApplyBlock(header, KeyA.GetPublicAddress());

            var expected = new List<string>
            {
                SortedByAscendingByteOrderSignerAt(0).ToLowerInvariant(),
                SortedByAscendingByteOrderSignerAt(1).ToLowerInvariant(),
                SortedByAscendingByteOrderSignerAt(2).ToLowerInvariant()
            };
            Assert.Equal(expected, engine.CurrentSnapshot.Signers);
        }

        [Fact]
        public void Given_ACheckpointBlockWhoseSignerListIsUnsorted_When_ItIsValidated_Then_ItIsRefused()
        {
            var engine = new CliqueEngine(new CliqueConfig
            {
                InitialSigners = new List<string> { KeyA.GetPublicAddress(), KeyB.GetPublicAddress(), KeyC.GetPublicAddress() },
                LocalSignerAddress = KeyA.GetPublicAddress(),
                BlockPeriodSeconds = 1,
                EpochLength = 3
            });

            var unsortedExtraData = EncodeCheckpointExtraData(new[] { KeyA, KeyB, KeyC });
            var difficulty = engine.GetDifficulty(3, KeyA.GetPublicAddress());
            var header = BuildCheckpointHeader(KeyA, 3, difficulty, unsortedExtraData);

            var verdict = engine.ValidateBlockInternal(header, null);

            Assert.False(verdict.IsValid);
            Assert.Contains("sorted", verdict.Error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Given_ACheckpointBlockWhoseSignerListIsSorted_When_ItIsValidated_Then_ItIsAccepted()
        {
            var engine = new CliqueEngine(new CliqueConfig
            {
                InitialSigners = new List<string> { KeyA.GetPublicAddress(), KeyB.GetPublicAddress(), KeyC.GetPublicAddress() },
                LocalSignerAddress = KeyA.GetPublicAddress(),
                BlockPeriodSeconds = 1,
                EpochLength = 3
            });

            var sortedExtraData = EncodeCheckpointExtraData(new[] { KeyC, KeyB, KeyA });
            var difficulty = engine.GetDifficulty(3, KeyA.GetPublicAddress());
            var header = BuildCheckpointHeader(KeyA, 3, difficulty, sortedExtraData);

            var verdict = engine.ValidateBlockInternal(header, null);

            Assert.True(verdict.IsValid, verdict.Error);
        }

        [Fact]
        public void Given_TheSnapshotSignersAreSorted_When_PrepareExtraDataIsCalledForACheckpoint_Then_TheExtraDataListIsInAscendingOrder()
        {
            var engine = new CliqueEngine(new CliqueConfig
            {
                InitialSigners = new List<string> { KeyA.GetPublicAddress(), KeyB.GetPublicAddress(), KeyC.GetPublicAddress() },
                LocalSignerAddress = KeyA.GetPublicAddress(),
                BlockPeriodSeconds = 1,
                EpochLength = 3
            });

            var extraData = engine.PrepareExtraData(3);

            var decoded = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                var chunk = new byte[20];
                Array.Copy(extraData, CliqueEngine.EXTRA_VANITY + (i * 20), chunk, 0, 20);
                decoded.Add("0x" + BitConverter.ToString(chunk).Replace("-", "").ToLowerInvariant());
            }

            for (int i = 1; i < decoded.Count; i++)
                Assert.True(string.CompareOrdinal(decoded[i - 1], decoded[i]) < 0);
        }

        /// <summary>
        /// EIP-225, Specification: "The list of signers in checkpoint block extra-data sections must be
        /// sorted in ascending byte order." An entry typed without the hex prefix sorts by the prefix
        /// character rather than by its bytes, so two operators entering the same signer set in different
        /// notations would derive different SIGNER_INDEX values and take different turns.
        /// </summary>
        [Fact]
        public void Given_InitialSignersWrittenInMixedNotation_When_TheEngineTakesThem_Then_TheyAreOrderedByAddressBytesNotByText()
        {
            var low = "aa" + new string('1', 38);
            var high = "0xbb" + new string('2', 38);

            var engine = new CliqueEngine(new CliqueConfig
            {
                InitialSigners = new List<string> { high, low },
                LocalSignerAddress = "0x" + low,
                BlockPeriodSeconds = 1,
                EpochLength = 30000
            });

            var signers = engine.CurrentSnapshot.Signers;

            Assert.Equal(2, signers.Count);
            Assert.StartsWith("0xaa", signers[0]);
            Assert.StartsWith("0xbb", signers[1]);
        }

        [Fact]
        public void Given_InitialSignersAlreadyInCanonicalNotation_When_TheEngineTakesThem_Then_TheSameOrderResults()
        {
            var low = "0xaa" + new string('1', 38);
            var high = "0xbb" + new string('2', 38);

            var engine = new CliqueEngine(new CliqueConfig
            {
                InitialSigners = new List<string> { high, low },
                LocalSignerAddress = low,
                BlockPeriodSeconds = 1,
                EpochLength = 30000
            });

            Assert.Equal(new[] { low, high }, engine.CurrentSnapshot.Signers);
        }
    }
}
