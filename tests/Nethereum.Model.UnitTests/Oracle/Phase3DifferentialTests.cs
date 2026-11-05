using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.RLP;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.Model.UnitTests.Oracle
{
    [Trait("Requires", "Externals")]
    public class Phase3DifferentialTests
    {
        private static readonly Lazy<DifferentialCorpus> Corpus = new Lazy<DifferentialCorpus>(DifferentialCorpus.Load);
        private const int MutantsPerSample = 6;
        private readonly ITestOutputHelper _output;
        private readonly DifferentialHarness _harness;
        private readonly Random _rng = new Random(20261028);

        public Phase3DifferentialTests(ITestOutputHelper output)
        {
            _output = output;
            _harness = new DifferentialHarness(output);
        }

        private byte[] Bytes(int length)
        {
            var bytes = new byte[length];
            _rng.NextBytes(bytes);
            return bytes;
        }

        private byte[] CanonicalScalar(int maxLength)
        {
            var bytes = Bytes(_rng.Next(1, maxLength + 1));
            bytes[0] |= 1;
            return bytes;
        }

        private ulong Number()
        {
            switch (_rng.Next(5))
            {
                case 0: return 0;
                case 1: return (ulong)_rng.Next(1, 200);
                case 2: return (ulong)_rng.Next(200, 70000);
                case 3: return ulong.MaxValue - (ulong)_rng.Next(1000);
                default: return (ulong)(_rng.NextDouble() * 1e15) + 1;
            }
        }

        private Log MakeLog()
        {
            var log = Log.Create(Bytes(_rng.Next(0, 70)), "0x" + Bytes(20).ToHex());
            for (var i = _rng.Next(0, 5); i > 0; i--) log.Topics.Add(Bytes(32));
            return log;
        }

        private Receipt MakeReceipt(byte type)
        {
            var logs = new List<Log>();
            for (var i = _rng.Next(0, 4); i > 0; i--) logs.Add(MakeLog());
            var bloom = new LogBloomFilter();
            foreach (var log in logs) bloom.AddLog(log);
            byte[] status;
            switch (_rng.Next(3))
            {
                case 0: status = new byte[] { 1 }; break;
                case 1: status = new byte[0]; break;
                default: status = Bytes(32); break;
            }
            return new Receipt
            {
                PostStateOrStatus = status,
                CumulativeGasUsed = new EvmUInt256(Number()),
                Bloom = bloom.Data,
                Logs = logs,
                TransactionType = type
            };
        }

        private byte TypeFor(int index)
        {
            var types = new byte[] { 0, 0, 1, 2, 3, 4 };
            return types[index % types.Length];
        }

        private static byte[] EncodeConsensus(Receipt receipt)
        {
            return receipt.TransactionType > 0
                ? ReceiptEncoder.Current.EncodeTyped(receipt, receipt.TransactionType)
                : ReceiptEncoder.Current.Encode(receipt);
        }

        private IEnumerable<byte[]> Mutants(byte[] valid, int count)
        {
            for (var i = 0; i < count; i++)
            {
                var copy = (byte[])valid.Clone();
                switch (_rng.Next(6))
                {
                    case 0:
                        if (copy.Length == 0) continue;
                        copy[_rng.Next(copy.Length)] ^= (byte)(1 << _rng.Next(8));
                        yield return copy;
                        break;
                    case 1:
                        if (copy.Length < 2) continue;
                        yield return copy.Take(_rng.Next(1, copy.Length)).ToArray();
                        break;
                    case 2:
                        yield return copy.Concat(new[] { (byte)_rng.Next(256) }).ToArray();
                        break;
                    case 3:
                        if (copy.Length < 2) continue;
                        var at = _rng.Next(copy.Length);
                        yield return copy.Where((b, index) => index != at).ToArray();
                        break;
                    case 4:
                        if (copy.Length == 0) continue;
                        copy[_rng.Next(copy.Length)] = new byte[] { 0x00, 0x80, 0xc0, 0xff, 0x81, 0xb8 }[_rng.Next(6)];
                        yield return copy;
                        break;
                    default:
                        if (copy.Length == 0) continue;
                        var position = _rng.Next(copy.Length);
                        yield return copy.Take(position).Concat(new[] { copy[position] }).Concat(copy.Skip(position)).ToArray();
                        break;
                }
            }
        }

        private List<CorpusSample> WithMutants(IEnumerable<byte[]> valid)
        {
            var samples = new List<CorpusSample>();
            foreach (var bytes in valid)
            {
                samples.Add(new CorpusSample { Bytes = bytes, MustBeValid = true, Source = "generated" });
                foreach (var mutant in Mutants(bytes, MutantsPerSample))
                    samples.Add(new CorpusSample { Bytes = mutant, MustBeValid = false, Source = "mutant" });
            }
            return samples;
        }

        private void Check<T>(string name, List<CorpusSample> samples, Func<byte[], T> oracle, Func<byte[], T> decoder,
            Func<T, T, byte[], string> compare = null, bool opaqueNestedEntries = false)
        {
            var outcome = new DifferentialHarness.Outcome();
            _harness.Run(samples, oracle, decoder, compare ?? ((expected, actual, input) => ObjectComparer.FirstDifference(expected, actual)), outcome);
            if (opaqueNestedEntries)
            {
                Assert.All(outcome.NewAccepts, line => Assert.Contains("old: Exception: Invalid RLP", line));
                outcome.NewAccepts.Clear();
            }
            DifferentialHarness.AssertEquivalent(outcome, _harness.Report(name, outcome));
            Assert.True(outcome.BothAcceptEqual > samples.Count / 10, name + ": too few accepted samples " + outcome.BothAcceptEqual);
        }

        [Fact]
        public void Given_GeneratedLogsAndWithdrawalsAndTheirMutants_When_Decoded_Then_TheStreamingDecoderEqualsTheTreeOracleOrIsStricter()
        {
            var logs = Enumerable.Range(0, 1500).Select(i => LogEncoder.Current.Encode(MakeLog()));
            Check("logs", WithMutants(logs), TreeDecodePhase3Oracle.DecodeLog, bytes => LogEncoder.Current.Decode(bytes));

            var withdrawals = Enumerable.Range(0, 1500).Select(i => WithdrawalEncoder.Current.Encode(new Withdrawal
            {
                Index = Number(),
                ValidatorIndex = Number(),
                Address = Bytes(20),
                AmountInGwei = Number()
            }));
            Check("withdrawals", WithMutants(withdrawals), TreeDecodePhase3Oracle.DecodeWithdrawal, bytes => WithdrawalEncoder.Current.Decode(bytes));
        }

        [Fact]
        public void Given_GeneratedReceiptsOfEveryTypeAndTheirMutants_When_DecodedInTheConsensusAndEth69Forms_Then_TheStreamingDecoderEqualsTheTreeOracleOrIsStricter()
        {
            var receipts = Enumerable.Range(0, 3000).Select(i => MakeReceipt(TypeFor(i))).ToList();

            Check("receipts consensus", WithMutants(receipts.Select(EncodeConsensus)),
                TreeDecodePhase3Oracle.DecodeReceipt, bytes => ReceiptEncoder.Current.Decode(bytes),
                (expected, actual, input) =>
                {
                    var difference = ObjectComparer.FirstDifference(expected, actual);
                    if (difference != null) return difference;
                    return EncodeConsensus(actual).SequenceEqual(input) ? null : DifferentialHarness.NonCanonical;
                });

            Check("receipts eth/69 single", WithMutants(receipts.Select(ReceiptsMessageEth69Encoder.EncodeSingleReceipt)),
                bytes => TreeDecodePhase3Oracle.DecodeEth69Receipt((RLPCollection)RLP.RLP.Decode(bytes)),
                bytes =>
                {
                    var reader = new RlpReader(bytes);
                    var receipt = ReceiptsMessageEth69Encoder.ReadSingleReceipt(reader);
                    reader.EnsureEnd();
                    return receipt;
                });
        }

        private List<List<Receipt>> MakeBlocksOfReceipts()
        {
            var blocks = new List<List<Receipt>>();
            for (var b = _rng.Next(0, 5); b >= 0; b--)
            {
                var receipts = new List<Receipt>();
                for (var r = _rng.Next(0, 5); r > 0; r--) receipts.Add(MakeReceipt(TypeFor(_rng.Next(6))));
                blocks.Add(receipts);
            }
            return blocks;
        }

        [Fact]
        public void Given_GeneratedReceiptsMessagesAndTheirMutants_When_Decoded_Then_TheStreamingDecoderEqualsTheTreeOracleOrIsStricter()
        {
            var messages = Enumerable.Range(0, 600).Select(i => new ReceiptsMessage { RequestId = Number(), ReceiptsByBlock = MakeBlocksOfReceipts() }).ToList();

            Check("receipts message eth/68", WithMutants(messages.Select(ReceiptsMessageEncoder.Encode)),
                TreeDecodePhase3Oracle.DecodeReceiptsMessage, ReceiptsMessageEncoder.Decode);

            Check("receipts message eth/69", WithMutants(messages.Select(ReceiptsMessageEth69Encoder.Encode)),
                TreeDecodePhase3Oracle.DecodeReceiptsMessageEth69, ReceiptsMessageEth69Encoder.Decode);

            Check("receipts message eth/70", WithMutants(messages.Select(m => ReceiptsMessageEth70Encoder.Encode(
                    new ReceiptsMessage70 { RequestId = m.RequestId, LastBlockIncomplete = m.RequestId % 2 == 0, ReceiptsByBlock = m.ReceiptsByBlock }))),
                TreeDecodePhase3Oracle.DecodeReceiptsMessageEth70, ReceiptsMessageEth70Encoder.Decode);
        }

        private static byte[] BodyFromBlock(RLPCollection block)
        {
            var parts = new List<byte[]>();
            for (var i = 1; i < block.Count; i++) parts.Add(block[i].RLPData);
            return RLP.RLP.EncodeList(parts.ToArray());
        }

        private static IEnumerable<CorpusSample> BlockSamples(DifferentialCorpus corpus)
        {
            return corpus.BlockRlps.Where(sample => RLP.RLP.Decode(sample.Bytes) is RLPCollection block && block.Count >= 3);
        }

        [Fact]
        public void Given_EveryBlockInTheLocalCorpora_When_DecodedAsABodyAndANewBlockAndABlockBodiesMessage_Then_TheStreamingDecoderEqualsTheTreeOracleOrIsRefused()
        {
            var corpus = Corpus.Value;
            Assert.True(corpus.BlockRlps.Count > 1000, "corpus too small: " + corpus.BlockRlps.Count);
            _output.WriteLine("uniqueBlocks=" + corpus.BlockRlps.Count);
            var blocks = BlockSamples(corpus).ToList();

            var bodies = blocks.Select(sample => new CorpusSample
            {
                Bytes = BodyFromBlock((RLPCollection)RLP.RLP.Decode(sample.Bytes)),
                MustBeValid = sample.MustBeValid,
                Source = sample.Source
            }).ToList();
            foreach (var allowBlob in new[] { false, true })
                Check("bodies allowBlobWrapper=" + allowBlob, bodies,
                    bytes => TreeDecodePhase3Oracle.DecodeBody(bytes, allowBlob),
                    bytes => BlockBodiesMessageEncoder.DecodeBody(bytes, allowBlob));

            Check("blocks (importer form)", blocks,
                bytes => TreeDecodePhase3Oracle.DecodeNewBlockMessage(RLP.RLP.EncodeList(bytes, new byte[] { 0x80 })),
                bytes => NewBlockMessageEncoder.DecodeBlock(bytes, false));

            var newBlocks = blocks.Select((sample, i) => new CorpusSample
            {
                Bytes = RLP.RLP.EncodeList(sample.Bytes, RLP.RLP.EncodeElement(new EvmUInt256(Number() + 1).ToBytesForRLPEncoding())),
                MustBeValid = sample.MustBeValid,
                Source = sample.Source
            }).ToList();
            Check("new block messages", newBlocks, TreeDecodePhase3Oracle.DecodeNewBlockMessage, NewBlockMessageEncoder.Decode,
                (expected, actual, input) => ObjectComparer.FirstDifference(expected, actual));

            var batches = new List<CorpusSample>();
            for (var i = 0; i < bodies.Count; i += 6)
            {
                var batch = bodies.Skip(i).Take(6).ToList();
                batches.Add(new CorpusSample
                {
                    Bytes = RLP.RLP.EncodeList(RLP.RLP.EncodeElement(new EvmUInt256(Number() + 1).ToBytesForRLPEncoding()),
                        RLP.RLP.EncodeList(batch.Select(b => b.Bytes).ToArray())),
                    MustBeValid = batch.All(b => b.MustBeValid),
                    Source = batch[0].Source
                });
            }
            Check("block bodies messages", batches, TreeDecodePhase3Oracle.DecodeBlockBodiesMessage, BlockBodiesMessageEncoder.Decode);
        }

        [Fact]
        public void Given_GeneratedNewBlockHashesAndBlockAccessListsMessagesAndTheirMutants_When_Decoded_Then_TheStreamingDecoderEqualsTheTreeOracleOrIsStricter()
        {
            var hashes = Enumerable.Range(0, 800).Select(i =>
            {
                var message = new NewBlockHashesMessage();
                for (var n = _rng.Next(0, 6); n > 0; n--)
                    message.Entries.Add(new NewBlockHashesMessage.BlockHashEntry { Hash = Bytes(32), Number = Number() });
                return NewBlockHashesMessageEncoder.Encode(message);
            });
            Check("new block hashes", WithMutants(hashes), TreeDecodePhase3Oracle.DecodeNewBlockHashesMessage, NewBlockHashesMessageEncoder.Decode);

            var ethLists = Enumerable.Range(0, 800).Select(i =>
            {
                var message = new Nethereum.Model.P2P.BlockAccessListsMessage { RequestId = Number() };
                for (var n = _rng.Next(0, 5); n > 0; n--)
                    message.BlockAccessListsByBlock.Add(_rng.Next(3) == 0 ? new byte[0] : BlockAccessListRLPEncoder.Current.Encode(MakeBlockAccessList()));
                return Nethereum.Model.P2P.BlockAccessListsMessageEncoder.Encode(message);
            }).ToList();
            Check("eth block access lists", WithMutants(ethLists),
                TreeDecodePhase3Oracle.DecodeEthBlockAccessListsMessage, Nethereum.Model.P2P.BlockAccessListsMessageEncoder.Decode, opaqueNestedEntries: true);
            Check("snap block access lists", WithMutants(ethLists),
                TreeDecodePhase3Oracle.DecodeSnapBlockAccessListsMessage, Nethereum.Model.P2P.Snap.BlockAccessListsMessageEncoder.Decode, opaqueNestedEntries: true);
        }

        private List<AccountChanges> MakeBlockAccessList()
        {
            var accounts = new List<AccountChanges>();
            for (var a = _rng.Next(0, 4); a > 0; a--)
            {
                var account = new AccountChanges("0x" + Bytes(20).ToHex());
                for (var s = _rng.Next(0, 3); s > 0; s--)
                {
                    var slot = new SlotChanges(new EvmUInt256(Number()));
                    for (var c = _rng.Next(0, 3); c > 0; c--) slot.Changes.Add(new StorageChange((ulong)_rng.Next(0, 1000), new EvmUInt256(Number())));
                    account.StorageChanges.Add(slot);
                }
                for (var s = _rng.Next(0, 3); s > 0; s--) account.StorageReads.Add(new EvmUInt256(Number()));
                for (var c = _rng.Next(0, 3); c > 0; c--) account.BalanceChanges.Add(new BalanceChange((ulong)_rng.Next(0, 1000), new EvmUInt256(Number())));
                for (var c = _rng.Next(0, 3); c > 0; c--) account.NonceChanges.Add(new NonceChange((ulong)_rng.Next(0, 1000), Number()));
                for (var c = _rng.Next(0, 3); c > 0; c--) account.CodeChanges.Add(new CodeChange((ulong)_rng.Next(0, 1000), Bytes(_rng.Next(0, 40))));
                accounts.Add(account);
            }
            return accounts;
        }

        [Fact]
        public void Given_GeneratedBlockAccessListsAndTheirMutants_When_Decoded_Then_TheStreamingDecoderEqualsTheTreeOracleOrIsStricter()
        {
            var lists = Enumerable.Range(0, 1500).Select(i => BlockAccessListRLPEncoder.Current.Encode(MakeBlockAccessList()));
            Check("block access lists", WithMutants(lists), TreeDecodePhase3Oracle.DecodeBlockAccessList,
                bytes => BlockAccessListRLPEncoder.Current.Decode(bytes));
        }

        [Fact]
        public void Given_GeneratedSnapResponsesAndTheirMutants_When_Decoded_Then_TheStreamingDecoderEqualsTheTreeOracleOrIsStricter()
        {
            Func<int, List<byte[]>> byteStrings = max => Enumerable.Range(0, _rng.Next(0, max)).Select(i => Bytes(_rng.Next(0, 60))).ToList();

            var accountRanges = Enumerable.Range(0, 800).Select(i =>
            {
                var message = new AccountRangeMessage { RequestId = Number(), Proof = byteStrings(5) };
                for (var n = _rng.Next(0, 5); n > 0; n--)
                    message.Accounts.Add(new AccountRangeMessage.AccountEntry
                    {
                        Hash = Bytes(32),
                        Body = RLP.RLP.EncodeList(
                            RLP.RLP.EncodeElement(CanonicalScalar(8)),
                            RLP.RLP.EncodeElement(CanonicalScalar(32)),
                            RLP.RLP.EncodeElement(Bytes(_rng.Next(1, 33))),
                            RLP.RLP.EncodeElement(Bytes(_rng.Next(1, 33))))
                    });
                return AccountRangeMessageEncoder.Encode(message);
            });
            Check("account range", WithMutants(accountRanges), TreeDecodePhase3Oracle.DecodeAccountRangeMessage, AccountRangeMessageEncoder.Decode);

            var storageRanges = Enumerable.Range(0, 800).Select(i =>
            {
                var message = new StorageRangesMessage { RequestId = Number(), Proof = byteStrings(5) };
                for (var a = _rng.Next(0, 4); a > 0; a--)
                {
                    var slots = new List<StorageRangesMessage.SlotEntry>();
                    for (var s = _rng.Next(0, 4); s > 0; s--)
                        slots.Add(new StorageRangesMessage.SlotEntry { Hash = Bytes(32), Data = Bytes(_rng.Next(0, 40)) });
                    message.Slots.Add(slots);
                }
                return StorageRangesMessageEncoder.Encode(message);
            });
            Check("storage ranges", WithMutants(storageRanges), TreeDecodePhase3Oracle.DecodeStorageRangesMessage, StorageRangesMessageEncoder.Decode);

            var byteCodes = Enumerable.Range(0, 800).Select(i => ByteCodesMessageEncoder.Encode(
                new ByteCodesMessage { RequestId = Number(), Codes = byteStrings(6) }));
            Check("byte codes", WithMutants(byteCodes), TreeDecodePhase3Oracle.DecodeByteCodesMessage, ByteCodesMessageEncoder.Decode);

            var trieNodes = Enumerable.Range(0, 800).Select(i => TrieNodesMessageEncoder.Encode(
                new TrieNodesMessage { RequestId = Number(), Nodes = byteStrings(6) }));
            Check("trie nodes", WithMutants(trieNodes), TreeDecodePhase3Oracle.DecodeTrieNodesMessage, TrieNodesMessageEncoder.Decode);
        }
    }
}
