using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.RLP;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Model.UnitTests
{
    public class Phase3DecodeRejectionTests
    {
        private static byte[] E(params byte[] bytes) => RLP.RLP.EncodeElement(bytes);
        private static byte[] Filled(byte value, int length) => Enumerable.Repeat(value, length).ToArray();
        private static readonly byte[] EmptyList = RLP.RLP.EncodeList();

        private static byte[][] Fields1559(bool signed)
        {
            var fields = new[]
            {
                E(1), E(), E(1), E(2), E(0x52, 0x08), E(Filled(0x11, 20)), E(), E(0x12, 0x34), EmptyList
            };
            return signed ? fields.Concat(new[] { E(1), E(Filled(0x22, 32)), E(Filled(0x33, 32)) }).ToArray() : fields;
        }

        private static byte[] Typed(byte type, byte[][] fields) => new[] { type }.Concat(RLP.RLP.EncodeList(fields)).ToArray();

        private static byte[] BlobTransactionFields()
        {
            return RLP.RLP.EncodeList(
                E(1), E(), E(1), E(2), E(0x52, 0x08), E(Filled(0x11, 20)), E(), E(0x12, 0x34), EmptyList,
                E(1), RLP.RLP.EncodeList(E(Filled(0x44, 32))), E(1), E(Filled(0x22, 32)), E(Filled(0x33, 32)));
        }

        private static byte[] BlobNetworkForm(int blobBytes, int commitmentBytes, int proofBytes, int? version)
        {
            var parts = new List<byte[]> { BlobTransactionFields() };
            if (version.HasValue) parts.Add(E((byte)version.Value));
            parts.Add(RLP.RLP.EncodeList(E(new byte[blobBytes])));
            parts.Add(RLP.RLP.EncodeList(E(new byte[commitmentBytes])));
            parts.Add(RLP.RLP.EncodeList(E(new byte[proofBytes])));
            return new byte[] { 3 }.Concat(RLP.RLP.EncodeList(parts.ToArray())).ToArray();
        }

        private static byte[] WireItem(byte[] typed) => RLP.RLP.EncodeElement(typed);

        private static ISignedTransaction ReadFromWire(byte[] typed)
        {
            var reader = new RlpReader(WireItem(typed));
            var transaction = TransactionFactory.ReadTransaction(reader);
            reader.EnsureEnd();
            return transaction;
        }

        [Theory]
        [InlineData(131072, 48, 48, null, true)]
        [InlineData(131072, 48, 48, 1, true)]
        [InlineData(131071, 48, 48, null, false)]
        [InlineData(131073, 48, 48, 1, false)]
        [InlineData(131072, 47, 48, null, false)]
        [InlineData(131072, 49, 48, 1, false)]
        [InlineData(131072, 48, 47, null, false)]
        [InlineData(131072, 48, 96, 1, false)]
        public void Given_ABlobTransactionInTheNetworkForm_When_ItsSidecarElementSizesAreChecked_Then_OnlyBlobCommitmentAndProofSizesAreAccepted(
            int blobBytes, int commitmentBytes, int proofBytes, int? version, bool accepted)
        {
            var rlp = BlobNetworkForm(blobBytes, commitmentBytes, proofBytes, version);

            if (accepted)
            {
                var transaction = (Transaction4844)TransactionFactory.CreateTransaction(rlp, true);
                Assert.Single(transaction.Sidecar.Blobs);
            }
            else
            {
                Assert.ThrowsAny<Exception>(() => TransactionFactory.CreateTransaction(rlp, true));
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(2)]
        [InlineData(255)]
        public void Given_ABlobSidecarCarryingAnExplicitVersionOtherThanOne_When_Decoded_Then_ItIsRefused(int version)
        {
            var rlp = BlobNetworkForm(131072, 48, 48, version);

            Assert.ThrowsAny<Exception>(() => TransactionFactory.CreateTransaction(rlp, true));
        }

        [Fact]
        public void Given_ABlobSidecarWithMoreBlobsThanCommitments_When_Decoded_Then_TheCountsAreLeftToValidation()
        {
            var parts = new[]
            {
                BlobTransactionFields(),
                RLP.RLP.EncodeList(E(new byte[131072]), E(new byte[131072])),
                RLP.RLP.EncodeList(E(new byte[48])),
                RLP.RLP.EncodeList(E(new byte[48]))
            };
            var rlp = new byte[] { 3 }.Concat(RLP.RLP.EncodeList(parts)).ToArray();

            var transaction = (Transaction4844)TransactionFactory.CreateTransaction(rlp, true);

            Assert.Equal(2, transaction.Sidecar.Blobs.Count);
            Assert.Single(transaction.Sidecar.Commitments);
        }

        [Fact]
        public void Given_AnUnsignedTypedTransaction_When_ItArrivesOnThePeerWire_Then_ItIsRefused()
        {
            var unsigned = Typed(2, Fields1559(signed: false));

            Assert.ThrowsAny<Exception>(() => ReadFromWire(unsigned));
            Assert.ThrowsAny<Exception>(() => TransactionsMessageEncoder.Decode(RLP.RLP.EncodeList(WireItem(unsigned))));
            Assert.ThrowsAny<Exception>(() => PooledTransactionsMessageEncoder.Decode(
                RLP.RLP.EncodeList(E(1), RLP.RLP.EncodeList(WireItem(unsigned)))));
        }

        [Fact]
        public void Given_AnUnsignedTypedTransaction_When_ItIsDecodedThroughThePublicFactory_Then_ItIsStillAccepted()
        {
            var unsigned = Typed(2, Fields1559(signed: false));

            var transaction = TransactionFactory.CreateTransaction(unsigned);

            Assert.Null(transaction.Signature);
            Assert.Equal(unsigned, transaction.GetRLPEncoded());
        }

        [Fact]
        public void Given_ASignedTypedTransaction_When_ItArrivesOnThePeerWire_Then_ItIsAccepted()
        {
            var signed = Typed(2, Fields1559(signed: true));

            Assert.NotNull(ReadFromWire(signed).Signature);
        }

        private static byte[] ReceiptList(byte[] status, byte[] bloom, params byte[][] logs)
        {
            return RLP.RLP.EncodeList(E(status), E(0x52, 0x08), E(bloom), RLP.RLP.EncodeList(logs));
        }

        [Fact]
        public void Given_ATypedReceiptWhoseTypeByteIsZero_When_Decoded_Then_ItIsRefused()
        {
            var body = ReceiptList(new byte[] { 1 }, new byte[256]);

            Assert.ThrowsAny<Exception>(() => ReceiptEncoder.Current.Decode(new byte[] { 0 }.Concat(body).ToArray()));
            Assert.Equal(2, ReceiptEncoder.Current.Decode(new byte[] { 2 }.Concat(body).ToArray()).TransactionType);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(255)]
        [InlineData(257)]
        public void Given_AReceiptWhoseBloomIsNot256Bytes_When_Decoded_Then_ItIsRefused(int bloomBytes)
        {
            var receipt = ReceiptList(new byte[] { 1 }, new byte[bloomBytes]);

            Assert.ThrowsAny<Exception>(() => ReceiptEncoder.Current.Decode(receipt));
        }

        [Theory]
        [InlineData(2, false)]
        [InlineData(31, false)]
        [InlineData(33, false)]
        [InlineData(32, true)]
        [InlineData(1, true)]
        [InlineData(0, true)]
        public void Given_AnEth69ReceiptWithAPostStateOrStatusOfAGivenLength_When_Decoded_Then_OnlyEmptyOneByteAndThirtyTwoBytesAreAccepted(int length, bool accepted)
        {
            var receipt = RLP.RLP.EncodeList(E(), E(Filled(1, length)), E(0x52, 0x08), EmptyList);
            var reader = new RlpReader(receipt);

            if (accepted) Assert.Equal(length, ReceiptsMessageEth69Encoder.ReadSingleReceipt(reader).PostStateOrStatus.Length);
            else Assert.ThrowsAny<Exception>(() => ReceiptsMessageEth69Encoder.ReadSingleReceipt(reader));
        }

        [Fact]
        public void Given_AnEth69ReceiptWhoseTypeIsAboveSevenF_When_Decoded_Then_ItIsRefused()
        {
            var receipt = RLP.RLP.EncodeList(E(0x80), E(1), E(0x52, 0x08), EmptyList);

            Assert.ThrowsAny<Exception>(() => ReceiptsMessageEth69Encoder.ReadSingleReceipt(new RlpReader(receipt)));
        }

        [Fact]
        public void Given_AnEth70ReceiptsMessageWhoseIncompleteFlagIsNotABoolean_When_Decoded_Then_ItIsRefused()
        {
            var message = RLP.RLP.EncodeList(E(1), E(2), EmptyList);

            Assert.ThrowsAny<Exception>(() => ReceiptsMessageEth70Encoder.Decode(message));
            Assert.True(ReceiptsMessageEth70Encoder.Decode(RLP.RLP.EncodeList(E(1), E(1), EmptyList)).LastBlockIncomplete);
        }

        [Fact]
        public void Given_ALogWithAShortAddressOrAShortTopic_When_Decoded_Then_ItIsRefused()
        {
            var shortAddress = RLP.RLP.EncodeList(E(Filled(1, 19)), EmptyList, E());
            var shortTopic = RLP.RLP.EncodeList(E(Filled(1, 20)), RLP.RLP.EncodeList(E(Filled(1, 31))), E());

            Assert.ThrowsAny<Exception>(() => LogEncoder.Current.Decode(shortAddress));
            Assert.ThrowsAny<Exception>(() => LogEncoder.Current.Decode(shortTopic));
        }

        [Fact]
        public void Given_AWithdrawalWithAShortAddressOrNineByteIndex_When_Decoded_Then_ItIsRefused()
        {
            var shortAddress = RLP.RLP.EncodeList(E(1), E(2), E(Filled(1, 19)), E(3));
            var wideIndex = RLP.RLP.EncodeList(E(Filled(1, 9)), E(2), E(Filled(1, 20)), E(3));

            Assert.ThrowsAny<Exception>(() => WithdrawalEncoder.Current.Decode(shortAddress));
            Assert.ThrowsAny<Exception>(() => WithdrawalEncoder.Current.Decode(wideIndex));
        }

        [Fact]
        public void Given_AWithdrawalWhoseFieldsAreTheLargestUnsigned64BitValue_When_Decoded_Then_ItIsAccepted()
        {
            var max = Filled(0xff, 8);
            var withdrawal = WithdrawalEncoder.Current.Decode(RLP.RLP.EncodeList(E(max), E(max), E(Filled(1, 20)), E(max)));

            Assert.Equal(ulong.MaxValue, withdrawal.Index);
            Assert.Equal(ulong.MaxValue, withdrawal.ValidatorIndex);
            Assert.Equal(ulong.MaxValue, withdrawal.AmountInGwei);
        }

        [Fact]
        public void Given_ABlockBodyWithAFourthField_When_Decoded_Then_ItIsRefusedAndTwoOrThreeFieldsAreAccepted()
        {
            var two = RLP.RLP.EncodeList(EmptyList, EmptyList);
            var three = RLP.RLP.EncodeList(EmptyList, EmptyList, EmptyList);
            var four = RLP.RLP.EncodeList(EmptyList, EmptyList, EmptyList, EmptyList);

            Assert.Null(BlockBodiesMessageEncoder.DecodeBody(two).Withdrawals);
            Assert.Empty(BlockBodiesMessageEncoder.DecodeBody(three).Withdrawals);
            Assert.ThrowsAny<Exception>(() => BlockBodiesMessageEncoder.DecodeBody(four));
            Assert.ThrowsAny<Exception>(() => BlockBodiesMessageEncoder.DecodeBody(RLP.RLP.EncodeList(EmptyList)));
        }

        [Fact]
        public void Given_ABlockBodyWithATransactionListAnUncleListAndWithdrawals_When_ReadFromAReader_Then_TheReaderStopsAtTheEndOfTheBody()
        {
            var body = RLP.RLP.EncodeList(EmptyList, EmptyList, EmptyList);
            var reader = new RlpReader(body.Concat(new byte[] { 0x05 }).ToArray());

            BlockBodiesMessageEncoder.ReadBody(reader);

            Assert.Equal(body.Length, reader.Position);
        }

        [Fact]
        public void Given_AnAccountRangeWhoseHashIsNot32Bytes_When_Decoded_Then_ItIsRefused()
        {
            var slim = RLP.RLP.EncodeList(E(1), E(2), E(Filled(3, 32)), E(Filled(4, 32)));
            Func<int, byte[]> message = hashBytes => RLP.RLP.EncodeList(
                E(1), RLP.RLP.EncodeList(RLP.RLP.EncodeList(E(Filled(9, hashBytes)), slim)), EmptyList);

            Assert.Single(AccountRangeMessageEncoder.Decode(message(32)).Accounts);
            Assert.ThrowsAny<Exception>(() => AccountRangeMessageEncoder.Decode(message(31)));
            Assert.ThrowsAny<Exception>(() => AccountRangeMessageEncoder.Decode(message(33)));
        }

        [Fact]
        public void Given_AStorageRangesSlotWhoseHashIsNot32Bytes_When_Decoded_Then_ItIsRefused()
        {
            Func<int, byte[]> message = hashBytes => RLP.RLP.EncodeList(
                E(1),
                RLP.RLP.EncodeList(RLP.RLP.EncodeList(RLP.RLP.EncodeList(E(Filled(9, hashBytes)), E(Filled(7, 4))))),
                EmptyList);

            Assert.Single(StorageRangesMessageEncoder.Decode(message(32)).Slots);
            Assert.ThrowsAny<Exception>(() => StorageRangesMessageEncoder.Decode(message(31)));
        }

        [Theory]
        [InlineData(128, true)]
        [InlineData(129, false)]
        public void Given_AnAccountRangeOrStorageRangesProofOfAGivenLength_When_Decoded_Then_MoreThan128NodesAreRefused(int nodes, bool accepted)
        {
            var proof = RLP.RLP.EncodeList(Enumerable.Range(0, nodes).Select(i => E(1, 2, 3)).ToArray());
            var accountRange = RLP.RLP.EncodeList(E(1), EmptyList, proof);
            var storageRanges = RLP.RLP.EncodeList(E(1), EmptyList, proof);

            if (accepted)
            {
                Assert.Equal(nodes, AccountRangeMessageEncoder.Decode(accountRange).Proof.Count);
                Assert.Equal(nodes, StorageRangesMessageEncoder.Decode(storageRanges).Proof.Count);
            }
            else
            {
                Assert.ThrowsAny<Exception>(() => AccountRangeMessageEncoder.Decode(accountRange));
                Assert.ThrowsAny<Exception>(() => StorageRangesMessageEncoder.Decode(storageRanges));
            }
        }

        [Fact]
        public void Given_ANestedListWhereAProofNodeIsExpected_When_ASnapResponseIsDecoded_Then_ItIsRefused()
        {
            Assert.ThrowsAny<Exception>(() => TrieNodesMessageEncoder.Decode(RLP.RLP.EncodeList(E(1), RLP.RLP.EncodeList(EmptyList))));
            Assert.ThrowsAny<Exception>(() => ByteCodesMessageEncoder.Decode(RLP.RLP.EncodeList(E(1), RLP.RLP.EncodeList(EmptyList))));
            Assert.ThrowsAny<Exception>(() => AccountRangeMessageEncoder.Decode(RLP.RLP.EncodeList(E(1), EmptyList, RLP.RLP.EncodeList(EmptyList))));
        }

        [Fact]
        public void Given_AGetTrieNodesRequestOfMoreThanTheServedLookups_When_Decoded_Then_PathsetsBeyondTheCapAreNotMaterialisedButZeroItemPathsetsStillRefuseTheRequest()
        {
            var pathsets = Enumerable.Range(0, SnapServingLimits.MaxTrieNodeLookups + 10).Select(i => RLP.RLP.EncodeList(E(1))).ToArray();
            var request = RLP.RLP.EncodeList(E(1), E(Filled(1, 32)), RLP.RLP.EncodeList(pathsets), E());

            var decoded = GetTrieNodesMessageEncoder.Decode(request);

            Assert.Equal(SnapServingLimits.MaxTrieNodeLookups, decoded.Paths.Count);

            var withZeroItemPathsetPastTheCap = RLP.RLP.EncodeList(E(1), E(Filled(1, 32)),
                RLP.RLP.EncodeList(pathsets.Concat(new[] { EmptyList }).ToArray()), E());
            Assert.ThrowsAny<Exception>(() => GetTrieNodesMessageEncoder.Decode(withZeroItemPathsetPastTheCap));
        }

        [Fact]
        public void Given_ANewBlockHashesEntryWithAShortHash_When_Decoded_Then_ItIsRefused()
        {
            var entry = RLP.RLP.EncodeList(E(Filled(1, 31)), E(1));

            Assert.ThrowsAny<Exception>(() => NewBlockHashesMessageEncoder.Decode(RLP.RLP.EncodeList(entry)));
        }

        [Fact]
        public void Given_ABlockAccessListEntryThatIsAnEmptyStringAndAList_When_ABlockAccessListsMessageIsDecoded_Then_BothAreKeptAtTheirPositions()
        {
            var list = RLP.RLP.EncodeList(EmptyList);
            var message = RLP.RLP.EncodeList(E(1), RLP.RLP.EncodeList(E(), list));

            var decoded = Nethereum.Model.P2P.BlockAccessListsMessageEncoder.Decode(message);

            Assert.Equal(2, decoded.BlockAccessListsByBlock.Count);
            Assert.Empty(decoded.BlockAccessListsByBlock[0]);
            Assert.Equal(list, decoded.BlockAccessListsByBlock[1]);
        }
    }
}
