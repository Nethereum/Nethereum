using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Eth71
{
    public class Eth71MessageRoundTripTests
    {
        private static byte[] Make32(byte fill)
        {
            var bytes = new byte[32];
            for (int i = 0; i < 32; i++) bytes[i] = (byte)(fill ^ i);
            return bytes;
        }

        private static string Address(byte fill)
        {
            var bytes = new byte[20];
            for (int i = 0; i < 20; i++) bytes[i] = (byte)(fill ^ i);
            return bytes.ToHex(true);
        }

        private static List<AccountChanges> BuildFullyPopulatedBal()
        {
            var account1 = new AccountChanges(Address(0x11));
            account1.StorageChanges.Add(new SlotChanges(new EvmUInt256(7))
            {
                Changes =
                {
                    new StorageChange(0, new EvmUInt256(42)),
                    new StorageChange(1, new EvmUInt256(43))
                }
            });
            account1.StorageReads.Add(new EvmUInt256(99));
            account1.StorageReads.Add(new EvmUInt256(100));
            account1.BalanceChanges.Add(new BalanceChange(2, new EvmUInt256(1000)));
            account1.NonceChanges.Add(new NonceChange(3, 5));
            account1.CodeChanges.Add(new CodeChange(4, new byte[] { 0x60, 0x00, 0x54 }));

            var account2 = new AccountChanges(Address(0x22));
            account2.StorageChanges.Add(new SlotChanges(new EvmUInt256(1))
            {
                Changes = { new StorageChange(5, new EvmUInt256(200)) }
            });
            account2.StorageReads.Add(new EvmUInt256(1));
            account2.BalanceChanges.Add(new BalanceChange(6, new EvmUInt256(2000)));
            account2.NonceChanges.Add(new NonceChange(7, 9));
            account2.CodeChanges.Add(new CodeChange(8, new byte[] { 0x00 }));

            var account3 = new AccountChanges(Address(0x33));
            account3.StorageChanges.Add(new SlotChanges(new EvmUInt256(2))
            {
                Changes = { new StorageChange(9, EvmUInt256.Zero) }
            });
            account3.StorageReads.Add(new EvmUInt256(5));
            account3.BalanceChanges.Add(new BalanceChange(10, EvmUInt256.Zero));

            return new List<AccountChanges> { account1, account2, account3 };
        }

        [Fact]
        public void GetBlockAccessListsMessage_RoundTrips_ReqIdAndHashes()
        {
            var msg = new GetBlockAccessListsMessage
            {
                RequestId = 0xC0FFEEul,
                BlockHashes = new[] { Make32(0x11), Make32(0x22) }
            };

            var bytes = GetBlockAccessListsMessageEncoder.Encode(msg);
            var decoded = GetBlockAccessListsMessageEncoder.Decode(bytes);

            Assert.Equal(msg.RequestId, decoded.RequestId);
            Assert.Equal(msg.BlockHashes.Length, decoded.BlockHashes.Length);
            for (int i = 0; i < msg.BlockHashes.Length; i++)
                Assert.Equal(msg.BlockHashes[i].ToHex(), decoded.BlockHashes[i].ToHex());
        }

        [Fact]
        public void GetBlockAccessListsMessage_RoundTrips_ReqIdAndHashes_Twin_MutatingAHashChangesTheDecode()
        {
            var original = new GetBlockAccessListsMessage
            {
                RequestId = 0xC0FFEEul,
                BlockHashes = new[] { Make32(0x11), Make32(0x22) }
            };
            var mutated = new GetBlockAccessListsMessage
            {
                RequestId = 0xC0FFEEul,
                BlockHashes = new[] { Make32(0x11), Make32(0x33) }
            };

            var decodedOriginal = GetBlockAccessListsMessageEncoder.Decode(GetBlockAccessListsMessageEncoder.Encode(original));
            var decodedMutated = GetBlockAccessListsMessageEncoder.Decode(GetBlockAccessListsMessageEncoder.Encode(mutated));

            Assert.NotEqual(decodedOriginal.BlockHashes[1].ToHex(), decodedMutated.BlockHashes[1].ToHex());

            var mutatedReqId = new GetBlockAccessListsMessage { RequestId = 0xC0FFEFul, BlockHashes = original.BlockHashes };
            var decodedMutatedReqId = GetBlockAccessListsMessageEncoder.Decode(GetBlockAccessListsMessageEncoder.Encode(mutatedReqId));
            Assert.NotEqual(decodedOriginal.RequestId, decodedMutatedReqId.RequestId);
        }

        /// <summary>
        /// EIP-8159 §BlockAccessLists (0x13): <i>"The RLP empty string (<c>0x80</c>) is
        /// returned for blocks where the BAL is unavailable."</i>
        ///
        /// <para>The response is positional against the request's block hashes, so an
        /// unavailable list has to occupy its slot. Dropping it instead shortens the
        /// response and moves every later entry onto the wrong hash — the requester then
        /// checks one block's access list against another block's
        /// <c>BlockAccessListHash</c> and rejects a list that was served correctly.</para>
        ///
        /// <para>The gap is in the MIDDLE deliberately. A stop-at-gap serve path — what
        /// geth does, and what this encoder's own doc comment used to prescribe — is
        /// indistinguishable from the EIP's rule when the gap is last.</para>
        /// </summary>
        [Fact]
        public void Given_MixedBalAndNoBalBlocksRequested_Then_MissingSlotsAreEmptyStringNotOmitted()
        {
            var firstBal = BlockAccessListRLPEncoder.Current.Encode(BuildFullyPopulatedBal());
            var thirdBal = BlockAccessListRLPEncoder.Current.Encode(BuildFullyPopulatedBal());

            var msg = new BlockAccessListsMessage
            {
                RequestId = 7ul,
                BlockAccessListsByBlock = new List<byte[]> { firstBal, new byte[0], thirdBal }
            };

            var wire = BlockAccessListsMessageEncoder.Encode(msg);
            var decoded = BlockAccessListsMessageEncoder.Decode(wire);

            Assert.Equal(3, decoded.BlockAccessListsByBlock.Count);
            Assert.Equal(firstBal, decoded.BlockAccessListsByBlock[0]);
            Assert.Empty(decoded.BlockAccessListsByBlock[1]);
            Assert.Equal(thirdBal, decoded.BlockAccessListsByBlock[2]);

            Assert.Contains<byte>(0x80, wire);
        }

        [Fact]
        public void Given_EveryRequestedBlockHasABal_Then_NoEntryIsAnEmptyString()
        {
            var bal = BlockAccessListRLPEncoder.Current.Encode(BuildFullyPopulatedBal());

            var decoded = BlockAccessListsMessageEncoder.Decode(
                BlockAccessListsMessageEncoder.Encode(new BlockAccessListsMessage
                {
                    RequestId = 7ul,
                    BlockAccessListsByBlock = new List<byte[]> { bal, bal }
                }));

            Assert.Equal(2, decoded.BlockAccessListsByBlock.Count);
            Assert.All(decoded.BlockAccessListsByBlock, entry => Assert.Equal(bal, entry));
        }

        [Fact]
        public void Given_TheSameEntries_When_EncodedForEth71AndSnap2_Then_TheWireBytesAreIdentical()
        {
            var bal = BlockAccessListRLPEncoder.Current.Encode(BuildFullyPopulatedBal());
            var entries = new List<byte[]> { bal, new byte[0], bal };

            var eth71 = BlockAccessListsMessageEncoder.Encode(
                new BlockAccessListsMessage { RequestId = 7ul, BlockAccessListsByBlock = entries });

            var snap2 = Nethereum.Model.P2P.Snap.BlockAccessListsMessageEncoder.Encode(
                new Nethereum.Model.P2P.Snap.BlockAccessListsMessage
                {
                    RequestId = 7ul,
                    BlockAccessListsByBlock = new List<byte[]>(entries)
                });

            Assert.Equal(eth71.ToHex(), snap2.ToHex());
        }

        [Fact]
        public void BlockAccessListsMessage_RoundTrips_NestedBalStructure()
        {
            var originalBal = BuildFullyPopulatedBal();
            var originalBalBytes = BlockAccessListRLPEncoder.Current.Encode(originalBal);

            var msg = new BlockAccessListsMessage
            {
                RequestId = 42ul,
                BlockAccessListsByBlock = new List<byte[]> { originalBalBytes }
            };

            var bytes = BlockAccessListsMessageEncoder.Encode(msg);
            var decoded = BlockAccessListsMessageEncoder.Decode(bytes);

            Assert.Equal(msg.RequestId, decoded.RequestId);
            Assert.Equal(1, decoded.BlockAccessListsByBlock.Count);

            var recoveredBalBytes = decoded.BlockAccessListsByBlock[0];
            Assert.Equal(originalBalBytes, recoveredBalBytes);

            var expected = originalBal;
            var actual = BlockAccessListRLPEncoder.Current.Decode(recoveredBalBytes);
            Assert.Equal(expected.Count, actual.Count);

            for (int i = 0; i < expected.Count; i++)
            {
                Assert.Equal(expected[i].Address, actual[i].Address, ignoreCase: true);

                Assert.Equal(expected[i].StorageChanges.Count, actual[i].StorageChanges.Count);
                for (int s = 0; s < expected[i].StorageChanges.Count; s++)
                {
                    Assert.Equal(expected[i].StorageChanges[s].Slot, actual[i].StorageChanges[s].Slot);
                    Assert.Equal(expected[i].StorageChanges[s].Changes.Count, actual[i].StorageChanges[s].Changes.Count);
                    for (int c = 0; c < expected[i].StorageChanges[s].Changes.Count; c++)
                    {
                        Assert.Equal(expected[i].StorageChanges[s].Changes[c].BlockAccessIndex, actual[i].StorageChanges[s].Changes[c].BlockAccessIndex);
                        Assert.Equal(expected[i].StorageChanges[s].Changes[c].PostValue, actual[i].StorageChanges[s].Changes[c].PostValue);
                    }
                }

                Assert.Equal(expected[i].StorageReads, actual[i].StorageReads);
                Assert.NotEmpty(actual[i].StorageReads);

                Assert.Equal(expected[i].BalanceChanges.Count, actual[i].BalanceChanges.Count);
                for (int c = 0; c < expected[i].BalanceChanges.Count; c++)
                {
                    Assert.Equal(expected[i].BalanceChanges[c].BlockAccessIndex, actual[i].BalanceChanges[c].BlockAccessIndex);
                    Assert.Equal(expected[i].BalanceChanges[c].PostBalance, actual[i].BalanceChanges[c].PostBalance);
                }

                Assert.Equal(expected[i].NonceChanges.Count, actual[i].NonceChanges.Count);
                for (int c = 0; c < expected[i].NonceChanges.Count; c++)
                {
                    Assert.Equal(expected[i].NonceChanges[c].BlockAccessIndex, actual[i].NonceChanges[c].BlockAccessIndex);
                    Assert.Equal(expected[i].NonceChanges[c].NewNonce, actual[i].NonceChanges[c].NewNonce);
                }

                Assert.Equal(expected[i].CodeChanges.Count, actual[i].CodeChanges.Count);
                for (int c = 0; c < expected[i].CodeChanges.Count; c++)
                {
                    Assert.Equal(expected[i].CodeChanges[c].BlockAccessIndex, actual[i].CodeChanges[c].BlockAccessIndex);
                    Assert.Equal(expected[i].CodeChanges[c].NewCode, actual[i].CodeChanges[c].NewCode);
                }
            }
        }

        private static List<AccountChanges> RoundTripThroughMessage(List<AccountChanges> bal)
        {
            var blob = BlockAccessListRLPEncoder.Current.Encode(bal);
            var msg = new BlockAccessListsMessage
            {
                RequestId = 1ul,
                BlockAccessListsByBlock = new List<byte[]> { blob }
            };
            var decoded = BlockAccessListsMessageEncoder.Decode(BlockAccessListsMessageEncoder.Encode(msg));
            return BlockAccessListRLPEncoder.Current.Decode(decoded.BlockAccessListsByBlock[0]);
        }

        [Fact]
        public void BlockAccessListsMessage_Decode_SurfacesBlobCorruption_ByteIdentityIsLoadBearing()
        {
            var originalBalBytes = BlockAccessListRLPEncoder.Current.Encode(BuildFullyPopulatedBal());
            var msg = new BlockAccessListsMessage
            {
                RequestId = 42ul,
                BlockAccessListsByBlock = new List<byte[]> { originalBalBytes }
            };
            var encoded = BlockAccessListsMessageEncoder.Encode(msg);
            encoded[encoded.Length - 1] ^= 0xFF;

            var decoded = BlockAccessListsMessageEncoder.Decode(encoded);

            Assert.Single(decoded.BlockAccessListsByBlock);
            Assert.NotEqual(originalBalBytes, decoded.BlockAccessListsByBlock[0]);
        }

        [Fact]
        public void BlockAccessListsMessage_RoundTrip_CarriesStorageReads_FieldIsLoadBearing()
        {
            var withReads = new List<AccountChanges> { new AccountChanges(Address(0x11)) };
            withReads[0].StorageReads.Add(new EvmUInt256(99));
            withReads[0].StorageReads.Add(new EvmUInt256(100));

            var withoutReads = new List<AccountChanges> { new AccountChanges(Address(0x11)) };

            var recoveredWith = RoundTripThroughMessage(withReads);
            var recoveredWithout = RoundTripThroughMessage(withoutReads);

            Assert.Equal(2, recoveredWith[0].StorageReads.Count);
            Assert.Empty(recoveredWithout[0].StorageReads);
            Assert.Equal(new EvmUInt256(99), recoveredWith[0].StorageReads[0]);
            Assert.Equal(new EvmUInt256(100), recoveredWith[0].StorageReads[1]);
        }

        [Fact]
        public void BlockAccessListsMessage_BalBody_DelegatesToValidatedEncoder_NoForkedImplementation()
        {
            var bal = BuildFullyPopulatedBal();
            var directBalBytes = BlockAccessListRLPEncoder.Current.Encode(bal);
            var msg = new BlockAccessListsMessage
            {
                RequestId = 7ul,
                BlockAccessListsByBlock = new List<byte[]> { directBalBytes }
            };

            var messageBytes = BlockAccessListsMessageEncoder.Encode(msg);

            Assert.Contains(directBalBytes.ToHex(), messageBytes.ToHex());
        }

        [Fact]
        public void BlockAccessListsMessage_Empty_RoundTrips()
        {
            var msg = new BlockAccessListsMessage { RequestId = 1ul };
            var bytes = BlockAccessListsMessageEncoder.Encode(msg);
            var decoded = BlockAccessListsMessageEncoder.Decode(bytes);
            Assert.Empty(decoded.BlockAccessListsByBlock);
        }
    }
}
