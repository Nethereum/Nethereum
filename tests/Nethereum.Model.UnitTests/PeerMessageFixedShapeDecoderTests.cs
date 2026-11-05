using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.RLP;
using Xunit;

namespace Nethereum.Model.UnitTests
{
    public class PeerMessageFixedShapeDecoderTests
    {
        private static readonly Random Rng = new Random(20261027);

        private static byte[] Hash()
        {
            var hash = new byte[32];
            Rng.NextBytes(hash);
            return hash;
        }

        private static ulong Number()
        {
            switch (Rng.Next(5))
            {
                case 0: return 0;
                case 1: return (ulong)Rng.Next(1, 128);
                case 2: return (ulong)Rng.Next(128, 70000);
                case 3: return (ulong)long.MaxValue - (ulong)Rng.Next(1000);
                default: return (ulong)(Rng.NextDouble() * 1e15) + 1;
            }
        }

        private static List<byte[]> Hashes()
        {
            var list = new List<byte[]>();
            for (var i = Rng.Next(0, 40); i > 0; i--) list.Add(Hash());
            return list;
        }

        private static string Describe(object value)
        {
            if (value == null) return "0x";
            if (value is byte[] bytes) return "0x" + bytes.ToHex();
            if (value is string || value.GetType().IsPrimitive || value is BigInteger) return value.ToString();
            if (value is IEnumerable enumerable)
                return "[" + string.Join(",", enumerable.Cast<object>().Select(Describe)) + "]";
            var builder = new StringBuilder("{");
            foreach (var property in value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                builder.Append(property.Name).Append('=').Append(Describe(property.GetValue(value))).Append(';');
            return builder.Append('}').ToString();
        }

        private static ulong U(IRLPElement e) => (ulong)e.RLPData.ToLongFromRLPDecoded();
        private static RLPCollection Tree(byte[] d) => (RLPCollection)RLP.RLP.Decode(d, RLP.RLP.UntrustedElementBudget(d.Length));

        private static (uint, ulong) ForkIdOf(RLPCollection c)
        {
            ForkIdEncoder.DecodeItems(c, out var hash, out var next);
            return (hash, next);
        }

        [Fact]
        public void Given_GeneratedEth68Statuses_When_Decoded_Then_TheyEqualTheTreeDecoder()
        {
            for (var i = 0; i < 200; i++)
            {
                var message = new Eth68StatusMessage
                {
                    ProtocolVersion = 68, NetworkId = Number(), TotalDifficulty = new BigInteger(Number()) * Number(),
                    BestHash = Hash(), GenesisHash = Hash(), ForkHash = (uint)Rng.Next(), ForkNext = Number()
                };
                var data = Eth68StatusMessageEncoder.Encode(message);
                var t = Tree(data);
                var (fh, fn) = ForkIdOf((RLPCollection)t[5]);
                var reference = new Eth68StatusMessage
                {
                    ProtocolVersion = t[0].RLPData.ToIntFromRLPDecoded(), NetworkId = U(t[1]),
                    TotalDifficulty = t[2].RLPData.ToBigIntegerFromRLPDecoded(), BestHash = t[3].RLPData,
                    GenesisHash = t[4].RLPData, ForkHash = fh, ForkNext = fn
                };

                Assert.Equal(Describe(reference), Describe(Eth68StatusMessageEncoder.Decode(data)));
            }
        }

        [Fact]
        public void Given_GeneratedEth69Statuses_When_Decoded_Then_TheyEqualTheTreeDecoder()
        {
            for (var i = 0; i < 200; i++)
            {
                var message = new Eth69StatusMessage
                {
                    ProtocolVersion = 69, NetworkId = Number(), GenesisHash = Hash(), ForkHash = (uint)Rng.Next(),
                    ForkNext = Number(), EarliestBlock = Number(), LatestBlock = Number(), LatestBlockHash = Hash()
                };
                var data = Eth69StatusMessageEncoder.Encode(message);
                var t = Tree(data);
                var (fh, fn) = ForkIdOf((RLPCollection)t[3]);
                var reference = new Eth69StatusMessage
                {
                    ProtocolVersion = t[0].RLPData.ToIntFromRLPDecoded(), NetworkId = U(t[1]), GenesisHash = t[2].RLPData,
                    ForkHash = fh, ForkNext = fn, EarliestBlock = U(t[4]), LatestBlock = U(t[5]), LatestBlockHash = t[6].RLPData
                };

                Assert.Equal(Describe(reference), Describe(Eth69StatusMessageEncoder.Decode(data)));
            }
        }

        [Fact]
        public void Given_GeneratedBlockRangeUpdates_When_Decoded_Then_TheyEqualTheTreeDecoder()
        {
            for (var i = 0; i < 200; i++)
            {
                var data = BlockRangeUpdateMessageEncoder.Encode(new BlockRangeUpdateMessage
                {
                    EarliestBlock = Number(), LatestBlock = Number(), LatestBlockHash = Hash()
                });
                var t = Tree(data);
                var reference = new BlockRangeUpdateMessage { EarliestBlock = U(t[0]), LatestBlock = U(t[1]), LatestBlockHash = t[2].RLPData };

                Assert.Equal(Describe(reference), Describe(BlockRangeUpdateMessageEncoder.Decode(data)));
            }
        }

        [Fact]
        public void Given_GeneratedBlockHeaderRequests_When_Decoded_Then_TheyEqualTheTreeDecoder()
        {
            for (var i = 0; i < 200; i++)
            {
                var message = new GetBlockHeadersMessage
                {
                    RequestId = Number(), Limit = Number(), Skip = Number(), Reverse = Rng.Next(2) == 0
                };
                if (Rng.Next(2) == 0) message.StartBlockHash = Hash(); else message.StartBlock = Number();
                var data = GetBlockHeadersMessageEncoder.Encode(message);

                var outer = (RLPCollection)RLP.RLP.Decode(data, 16);
                var inner = (RLPCollection)outer[1];
                var reference = new GetBlockHeadersMessage { RequestId = U(outer[0]) };
                var start = inner[0].RLPData ?? new byte[0];
                if (start.Length == 32) reference.StartBlockHash = start; else reference.StartBlock = (ulong)start.ToLongFromRLPDecoded();
                reference.Limit = U(inner[1]);
                reference.Skip = U(inner[2]);
                var reverse = inner[3].RLPData ?? new byte[0];
                reference.Reverse = reverse.Length > 0 && reverse[0] != 0;

                Assert.Equal(Describe(reference), Describe(GetBlockHeadersMessageEncoder.Decode(data)));
            }
        }

        [Fact]
        public void Given_GeneratedHashListRequests_When_Decoded_Then_TheyEqualTheTreeDecoder()
        {
            for (var i = 0; i < 100; i++)
            {
                var hashes = Hashes();
                var id = Number();

                var bodies = GetBlockBodiesMessageEncoder.Encode(new GetBlockBodiesMessage { RequestId = id, BlockHashes = hashes.ToArray() });
                var receipts = GetReceiptsMessageEncoder.Encode(new GetReceiptsMessage { RequestId = id, BlockHashes = hashes.ToArray() });
                var pooled = GetPooledTransactionsMessageEncoder.Encode(new GetPooledTransactionsMessage { RequestId = id, Hashes = hashes });
                var receipts70 = GetReceiptsMessage70Encoder.Encode(new GetReceiptsMessage70 { RequestId = id, FirstBlockReceiptIndex = Number(), BlockHashes = hashes.ToArray() });
                var bals = Nethereum.Model.P2P.GetBlockAccessListsMessageEncoder.Encode(new Nethereum.Model.P2P.GetBlockAccessListsMessage { RequestId = id, BlockHashes = hashes.ToArray() });

                var t = Tree(bodies);
                Assert.Equal(id, GetBlockBodiesMessageEncoder.Decode(bodies).RequestId);
                Assert.Equal(Describe(((RLPCollection)t[1]).Select(h => h.RLPData).ToArray()), Describe(GetBlockBodiesMessageEncoder.Decode(bodies).BlockHashes));
                Assert.Equal(Describe(((RLPCollection)Tree(receipts)[1]).Select(h => h.RLPData).ToArray()), Describe(GetReceiptsMessageEncoder.Decode(receipts).BlockHashes));
                Assert.Equal(Describe(((RLPCollection)Tree(pooled)[1]).Select(h => h.RLPData).ToList()), Describe(GetPooledTransactionsMessageEncoder.Decode(pooled).Hashes));
                var t70 = Tree(receipts70);
                var decoded70 = GetReceiptsMessage70Encoder.Decode(receipts70);
                Assert.Equal(U(t70[1]), decoded70.FirstBlockReceiptIndex);
                Assert.Equal(Describe(((RLPCollection)t70[2]).Select(h => h.RLPData).ToArray()), Describe(decoded70.BlockHashes));
                Assert.Equal(Describe(((RLPCollection)Tree(bals)[1]).Select(h => h.RLPData).ToArray()),
                    Describe(Nethereum.Model.P2P.GetBlockAccessListsMessageEncoder.Decode(bals).BlockHashes));
            }
        }

        [Fact]
        public void Given_GeneratedPooledTransactionAnnouncements_When_Decoded_Then_TheyEqualTheTreeDecoder()
        {
            for (var i = 0; i < 100; i++)
            {
                var count = Rng.Next(0, 40);
                var message = new NewPooledTransactionHashesMessage { Types = new byte[count] };
                for (var j = 0; j < count; j++)
                {
                    message.Types[j] = (byte)Rng.Next(0, 5);
                    message.Sizes.Add(Rng.Next(100, 130000));
                    message.Hashes.Add(Hash());
                }
                var data = NewPooledTransactionHashesMessageEncoder.Encode(message);
                var t = Tree(data);
                var reference = new NewPooledTransactionHashesMessage { Types = t[0].RLPData ?? new byte[0] };
                foreach (var size in (RLPCollection)t[1]) reference.Sizes.Add(size.RLPData.ToLongFromRLPDecoded());
                foreach (var hash in (RLPCollection)t[2]) reference.Hashes.Add(hash.RLPData);

                Assert.Equal(Describe(reference), Describe(NewPooledTransactionHashesMessageEncoder.Decode(data)));
            }
        }

        [Fact]
        public void Given_GeneratedSnapRequests_When_Decoded_Then_TheyEqualTheTreeDecoder()
        {
            for (var i = 0; i < 100; i++)
            {
                var id = Number();
                var account = GetAccountRangeMessageEncoder.Encode(new GetAccountRangeMessage
                {
                    RequestId = id, RootHash = Hash(), StartingHash = Hash(), LimitHash = Hash(), ResponseBytes = Number()
                });
                var t = Tree(account);
                var decoded = GetAccountRangeMessageEncoder.Decode(account);
                Assert.Equal(Describe(new GetAccountRangeMessage
                {
                    RequestId = U(t[0]), RootHash = t[1].RLPData, StartingHash = t[2].RLPData, LimitHash = t[3].RLPData, ResponseBytes = U(t[4])
                }), Describe(decoded));

                var storage = GetStorageRangesMessageEncoder.Encode(new GetStorageRangesMessage
                {
                    RequestId = id, RootHash = Hash(), AccountHashes = Hashes(),
                    StartingHash = Rng.Next(2) == 0 ? new byte[0] : Hash(), LimitHash = Rng.Next(2) == 0 ? new byte[0] : Hash(), ResponseBytes = Number()
                });
                t = Tree(storage);
                var reference = new GetStorageRangesMessage
                {
                    RequestId = U(t[0]), RootHash = t[1].RLPData, StartingHash = t[3].RLPData ?? new byte[0],
                    LimitHash = t[4].RLPData ?? new byte[0], ResponseBytes = U(t[5])
                };
                foreach (var h in (RLPCollection)t[2]) reference.AccountHashes.Add(h.RLPData);
                Assert.Equal(Describe(reference), Describe(GetStorageRangesMessageEncoder.Decode(storage)));

                var codes = GetByteCodesMessageEncoder.Encode(new GetByteCodesMessage { RequestId = id, Hashes = Hashes(), ResponseBytes = Number() });
                t = Tree(codes);
                var codesReference = new GetByteCodesMessage { RequestId = U(t[0]), ResponseBytes = U(t[2]) };
                foreach (var h in (RLPCollection)t[1]) codesReference.Hashes.Add(h.RLPData);
                Assert.Equal(Describe(codesReference), Describe(GetByteCodesMessageEncoder.Decode(codes)));

                var paths = new List<List<byte[]>>();
                for (var g = Rng.Next(0, 10); g > 0; g--)
                {
                    var group = new List<byte[]>();
                    for (var p = Rng.Next(1, 6); p > 0; p--)
                    {
                        var path = new byte[Rng.Next(0, 33)];
                        Rng.NextBytes(path);
                        if (path.Length == 1 && path[0] == 0) path[0] = 1;
                        group.Add(path);
                    }
                    paths.Add(group);
                }
                var trie = GetTrieNodesMessageEncoder.Encode(new GetTrieNodesMessage { RequestId = id, RootHash = Hash(), Paths = paths, ResponseBytes = Number() });
                t = Tree(trie);
                var trieReference = new GetTrieNodesMessage { RequestId = U(t[0]), RootHash = t[1].RLPData, ResponseBytes = U(t[3]) };
                foreach (RLPCollection group in (RLPCollection)t[2])
                    trieReference.Paths.Add(group.Select(p => p.RLPData).ToList());
                Assert.Equal(Describe(trieReference), Describe(GetTrieNodesMessageEncoder.Decode(trie)));

                var snapBals = Nethereum.Model.P2P.Snap.GetBlockAccessListsMessageEncoder.Encode(new Nethereum.Model.P2P.Snap.GetBlockAccessListsMessage
                {
                    RequestId = id, Hashes = Hashes(), Bytes = Number()
                });
                t = Tree(snapBals);
                var balsReference = new Nethereum.Model.P2P.Snap.GetBlockAccessListsMessage { RequestId = U(t[0]), Bytes = U(t[2]) };
                foreach (var h in (RLPCollection)t[1]) balsReference.Hashes.Add(h.RLPData);
                Assert.Equal(Describe(balsReference), Describe(Nethereum.Model.P2P.Snap.GetBlockAccessListsMessageEncoder.Decode(snapBals)));
            }
        }

        private static byte[] Int(ulong value) => RLP.RLP.EncodeElement(value == 0 ? new byte[0] : ((long)value).ToBytesForRLPEncoding());

        [Fact]
        public void Given_AnExtraFieldInAFixedShapeMessage_When_Decoded_Then_ItIsRejected()
        {
            var extra = RLP.RLP.EncodeList(Int(1), RLP.RLP.EncodeElement(Hash()), RLP.RLP.EncodeElement(Hash()), Int(5), Int(9));

            Assert.ThrowsAny<Exception>(() => BlockRangeUpdateMessageEncoder.Decode(extra));
            Assert.ThrowsAny<Exception>(() => GetBlockBodiesMessageEncoder.Decode(RLP.RLP.EncodeList(Int(1), RLP.RLP.EncodeList(), Int(2))));
            Assert.ThrowsAny<Exception>(() => GetAccountRangeMessageEncoder.Decode(
                RLP.RLP.EncodeList(Int(1), RLP.RLP.EncodeElement(Hash()), RLP.RLP.EncodeElement(Hash()), RLP.RLP.EncodeElement(Hash()), Int(5), Int(6))));
        }

        [Fact]
        public void Given_AMissingFieldInAFixedShapeMessage_When_Decoded_Then_ItIsRejected()
        {
            Assert.ThrowsAny<Exception>(() => BlockRangeUpdateMessageEncoder.Decode(RLP.RLP.EncodeList(Int(1), Int(2))));
            Assert.ThrowsAny<Exception>(() => GetAccountRangeMessageEncoder.Decode(
                RLP.RLP.EncodeList(Int(1), RLP.RLP.EncodeElement(Hash()), RLP.RLP.EncodeElement(Hash()), RLP.RLP.EncodeElement(Hash()))));
            Assert.ThrowsAny<Exception>(() => GetBlockHeadersMessageEncoder.Decode(
                RLP.RLP.EncodeList(Int(1), RLP.RLP.EncodeList(Int(1), Int(2), Int(3)))));
        }

        [Fact]
        public void Given_AHashOfTheWrongLength_When_Decoded_Then_ItIsRejected()
        {
            var shortHash = RLP.RLP.EncodeElement(new byte[31]);
            var hashes = RLP.RLP.EncodeList(RLP.RLP.EncodeElement(Hash()), shortHash);

            Assert.ThrowsAny<Exception>(() => GetBlockBodiesMessageEncoder.Decode(RLP.RLP.EncodeList(Int(1), hashes)));
            Assert.ThrowsAny<Exception>(() => BlockRangeUpdateMessageEncoder.Decode(RLP.RLP.EncodeList(Int(1), Int(2), shortHash)));
        }

        [Fact]
        public void Given_ANonCanonicalOrOverflowingInteger_When_Decoded_Then_ItIsRejected()
        {
            var leadingZero = RLP.RLP.EncodeElement(new byte[] { 0x00, 0x05 });
            var nine = RLP.RLP.EncodeElement(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0 });

            Assert.ThrowsAny<Exception>(() => BlockRangeUpdateMessageEncoder.Decode(RLP.RLP.EncodeList(leadingZero, Int(2), RLP.RLP.EncodeElement(Hash()))));
            Assert.ThrowsAny<Exception>(() => BlockRangeUpdateMessageEncoder.Decode(RLP.RLP.EncodeList(nine, Int(2), RLP.RLP.EncodeElement(Hash()))));
        }

        [Fact]
        public void Given_BytesAfterTheMessage_When_Decoded_Then_ItIsRejected()
        {
            var data = BlockRangeUpdateMessageEncoder.Encode(new BlockRangeUpdateMessage { EarliestBlock = 1, LatestBlock = 2, LatestBlockHash = Hash() });
            var padded = data.Concat(new byte[] { 0x80 }).ToArray();

            Assert.ThrowsAny<Exception>(() => BlockRangeUpdateMessageEncoder.Decode(padded));
        }

        [Fact]
        public void Given_NewPooledTransactionHashesWithMismatchedFieldLengths_When_Decoded_Then_ItIsRejected()
        {
            var data = RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(new byte[] { 2, 2 }),
                RLP.RLP.EncodeList(Int(100)),
                RLP.RLP.EncodeList(RLP.RLP.EncodeElement(Hash()), RLP.RLP.EncodeElement(Hash())));

            Assert.ThrowsAny<Exception>(() => NewPooledTransactionHashesMessageEncoder.Decode(data));
        }

        [Fact]
        public void Given_AHeaderRequestWhoseOriginIsAList_When_Decoded_Then_ItIsRejected()
        {
            var data = RLP.RLP.EncodeList(Int(1), RLP.RLP.EncodeList(RLP.RLP.EncodeList(), Int(2), Int(0), RLP.RLP.EncodeElement(new byte[0])));

            Assert.ThrowsAny<Exception>(() => GetBlockHeadersMessageEncoder.Decode(data));
        }

        [Fact]
        public void Given_AZeroItemPathsetInGetTrieNodes_When_Decoded_Then_ItIsRejected()
        {
            var data = RLP.RLP.EncodeList(
                Int(1), RLP.RLP.EncodeElement(Hash()), RLP.RLP.EncodeList(RLP.RLP.EncodeList()), Int(1000));

            Assert.ThrowsAny<Exception>(() => GetTrieNodesMessageEncoder.Decode(data));
        }

        [Fact]
        public void Given_AnAccountRangeWhoseBodyIsAString_When_Decoded_Then_ItIsRejected()
        {
            var entry = RLP.RLP.EncodeList(RLP.RLP.EncodeElement(Hash()), RLP.RLP.EncodeElement(new byte[] { 0xc0 }));
            var message = RLP.RLP.EncodeList(Int(1), RLP.RLP.EncodeList(entry), RLP.RLP.EncodeList());

            Assert.ThrowsAny<Exception>(() => AccountRangeMessageEncoder.Decode(message));
        }

        [Fact]
        public void Given_AnAccountRangeBodyWithNestedListsOrTheWrongItemCount_When_Decoded_Then_ItIsRejected()
        {
            var nested = RLP.RLP.EncodeList(Int(1), RLP.RLP.EncodeList(), RLP.RLP.EncodeElement(new byte[0]), RLP.RLP.EncodeElement(new byte[0]));
            var short3 = RLP.RLP.EncodeList(Int(1), Int(2), RLP.RLP.EncodeElement(new byte[0]));

            foreach (var body in new[] { nested, short3 })
            {
                var entry = RLP.RLP.EncodeList(RLP.RLP.EncodeElement(Hash()), body);
                var message = RLP.RLP.EncodeList(Int(1), RLP.RLP.EncodeList(entry), RLP.RLP.EncodeList());
                Assert.ThrowsAny<Exception>(() => AccountRangeMessageEncoder.Decode(message));
            }
        }

        [Fact]
        public void Given_AValidSlimAccountRange_When_Decoded_Then_ItRoundTripsTheWireBody()
        {
            var body = RLP.RLP.EncodeList(Int(1), Int(1000), RLP.RLP.EncodeElement(new byte[0]), RLP.RLP.EncodeElement(new byte[0]));
            var hash = Hash();
            var entry = RLP.RLP.EncodeList(RLP.RLP.EncodeElement(hash), body);
            var proof = RLP.RLP.EncodeElement(new byte[] { 1, 2, 3 });
            var message = RLP.RLP.EncodeList(Int(7), RLP.RLP.EncodeList(entry), RLP.RLP.EncodeList(proof));

            var decoded = AccountRangeMessageEncoder.Decode(message);

            Assert.Equal((ulong)7, decoded.RequestId);
            Assert.Equal(hash, decoded.Accounts[0].Hash);
            Assert.Equal(body, decoded.Accounts[0].Body);
            Assert.Equal(new byte[] { 1, 2, 3 }, decoded.Proof[0]);
        }

        [Fact]
        public void Given_FixedShapeMessagesCarryingAMillionElements_When_Decoded_Then_AllocationIsBoundedByThePayload()
        {
            var flood = new byte[4 * 1024 * 1024];
            for (var i = 0; i < flood.Length; i++) flood[i] = 0xc0;
            var message = RLP.RLP.EncodeList(Int(1), RLP.RLP.EncodeList(flood));
            var decoders = new Action<byte[]>[]
            {
                d => Eth68StatusMessageEncoder.Decode(d),
                d => Eth69StatusMessageEncoder.Decode(d),
                d => BlockRangeUpdateMessageEncoder.Decode(d),
                d => GetAccountRangeMessageEncoder.Decode(d),
                d => GetBlockHeadersMessageEncoder.Decode(d),
                d => GetBlockBodiesMessageEncoder.Decode(d),
                d => GetStorageRangesMessageEncoder.Decode(d),
                d => GetTrieNodesMessageEncoder.Decode(d),
                d => NewPooledTransactionHashesMessageEncoder.Decode(d)
            };

            foreach (var decode in decoders)
            {
                var before = GC.GetAllocatedBytesForCurrentThread();
                try { decode(message); } catch (Exception) { }
                var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.True(allocated < message.Length, "allocated " + allocated + " bytes for a " + message.Length + " byte message");
            }
        }
    }
}
