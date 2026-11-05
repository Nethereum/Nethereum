using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.UnitTests.Oracle;
using Nethereum.RLP;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Model.UnitTests
{
    public class GeneratedTransactionDifferentialTests
    {
        private static readonly Random Rng = new Random(20261027);

        private static byte[] Bytes(int length)
        {
            var bytes = new byte[length];
            Rng.NextBytes(bytes);
            return bytes;
        }

        private static string NonZeroLeadingHex(int length)
        {
            var bytes = Bytes(length);
            if (bytes[0] == 0) bytes[0] = 1;
            return "0x" + bytes.ToHex();
        }

        private static EvmUInt256 Number()
        {
            switch (Rng.Next(5))
            {
                case 0: return EvmUInt256.Zero;
                case 1: return new EvmUInt256((ulong)Rng.Next(1, 200));
                case 2: return new EvmUInt256((ulong)Rng.Next(200, 70000));
                case 3: return new EvmUInt256(ulong.MaxValue - (ulong)Rng.Next(1000));
                default: return new EvmUInt256((ulong)(Rng.NextDouble() * 1e15) + 1);
            }
        }

        private static string RequiredTo() => "0x" + Bytes(20).ToHex();

        private static string To() => Rng.Next(4) == 0 ? "0x" : "0x" + Bytes(20).ToHex();

        private static string Data() => Rng.Next(4) == 0 ? "0x" : "0x" + Bytes(Rng.Next(1, 400)).ToHex();

        private static List<AccessListItem> AccessList()
        {
            if (Rng.Next(3) == 0) return null;
            var list = new List<AccessListItem>();
            for (var i = Rng.Next(1, 4); i > 0; i--)
            {
                var keys = new List<byte[]>();
                for (var k = Rng.Next(0, 4); k > 0; k--) keys.Add(Bytes(32));
                list.Add(new AccessListItem("0x" + Bytes(20).ToHex(), keys));
            }
            return list;
        }

        private static List<Authorisation7702Signed> Authorisations()
        {
            var list = new List<Authorisation7702Signed>();
            for (var i = Rng.Next(1, 4); i > 0; i--)
            {
                list.Add(new Authorisation7702Signed(Number(), "0x" + Bytes(20).ToHex(), new EvmUInt256((ulong)Rng.Next(0, 1000)),
                    NonZero(32), NonZero(32), new byte[] { (byte)Rng.Next(0, 2) }.Where(b => b != 0).ToArray()));
            }
            return list;
        }

        private static byte[] NonZero(int length)
        {
            var bytes = Bytes(length);
            if (bytes[0] == 0) bytes[0] = 1;
            return bytes;
        }

        private static string V() => Rng.Next(2) == 0 ? "0x" : "0x01";

        private static ISignedTransaction Generate(int kind)
        {
            switch (kind)
            {
                case 0:
                    return TransactionFactory.CreateLegacyTransaction(To(), Number(), Number(), Number(), Data(), Number(),
                        NonZeroLeadingHex(32), NonZeroLeadingHex(32), Rng.Next(2) == 0 ? "0x1b" : "0x1c");
                case 1:
                    return TransactionFactory.CreateLegacyTransaction(To(), Number(), Number(), Number(), Data(), Number(),
                        NonZeroLeadingHex(32), NonZeroLeadingHex(32), Rng.Next(2) == 0 ? "0x25" : "0x26");
                case 2:
                    return TransactionFactory.Create2930Transaction(Number(), Number(), Number(), Number(), To(), Number(),
                        Data(), AccessList(), NonZeroLeadingHex(32), NonZeroLeadingHex(32), V());
                case 3:
                    return TransactionFactory.Create1559Transaction(Number(), Number(), Number(), Number(), Number(), To(),
                        Number(), Data(), AccessList(), NonZeroLeadingHex(32), NonZeroLeadingHex(32), V());
                case 4:
                    return TransactionFactory.Create7702Transaction(Number(), Number(), Number(), Number(), Number(), RequiredTo(),
                        Number(), Data(), AccessList(), Authorisations(), NonZeroLeadingHex(32), NonZeroLeadingHex(32), V());
                default:
                    var hashes = new List<byte[]>();
                    for (var i = Rng.Next(0, 4); i > 0; i--) hashes.Add(Bytes(32));
                    return TransactionFactory.Create4844Transaction(Number(), Number(), Number(), Number(), Number(), RequiredTo(),
                        Number(), Data(), AccessList(), Number(), hashes, NonZeroLeadingHex(32), NonZeroLeadingHex(32), V());
            }
        }

        private static BlobSidecar Sidecar(byte wrapperVersion)
        {
            var blobs = new List<byte[]>();
            var commitments = new List<byte[]>();
            var proofs = new List<byte[]>();
            for (var i = Rng.Next(1, 4); i > 0; i--)
            {
                blobs.Add(Bytes(BlobEncoder.BLOB_SIZE));
                commitments.Add(Bytes(48));
                proofs.Add(Bytes(48));
            }
            return new BlobSidecar(blobs, commitments, proofs) { WrapperVersion = wrapperVersion };
        }

        private static void AssertSame(ISignedTransaction expected, ISignedTransaction actual, byte[] wire)
        {
            Assert.Null(ObjectComparer.FirstDifference(expected, actual));
            Assert.Equal(expected.TransactionType, actual.TransactionType);
            Assert.Equal(expected.Hash.ToHex(), actual.Hash.ToHex());
            Assert.Equal(expected.RawHash.ToHex(), actual.RawHash.ToHex());
            if (!(actual is Transaction4844))
                Assert.Equal(wire.ToHex(), actual.GetRLPEncoded().ToHex());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        public void Given_GeneratedSignedTransactionsOfEachType_When_Decoded_Then_TheStreamingDecoderEqualsTheTreeOracle(int kind)
        {
            for (var i = 0; i < 400; i++)
            {
                var wire = Generate(kind).GetRLPEncoded();
                foreach (var allowBlob in new[] { false, true })
                {
                    var expected = TreeDecodeOracle.DecodeTransaction(wire, allowBlob);
                    var actual = TransactionFactory.CreateTransaction(wire, allowBlob);
                    AssertSame(expected, actual, wire);
                }
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void Given_GeneratedBlobTransactionsInTheNetworkForm_When_Decoded_Then_TheSidecarEqualsTheTreeOracle(int wrapperVersion)
        {
            for (var i = 0; i < 200; i++)
            {
                var transaction = (Transaction4844)Generate(5);
                transaction.Sidecar = Sidecar((byte)wrapperVersion);
                var wire = transaction.GetRLPEncodedWithSidecar();

                var expected = (Transaction4844)TreeDecodeOracle.DecodeTransaction(wire, true);
                var actual = (Transaction4844)TransactionFactory.CreateTransaction(wire, true);

                Assert.Null(ObjectComparer.FirstDifference(expected, actual));
                Assert.Equal(wrapperVersion, actual.Sidecar.WrapperVersion);
                Assert.Equal(expected.Sidecar.Blobs.Count, actual.Sidecar.Blobs.Count);
                Assert.Equal(expected.Hash.ToHex(), actual.Hash.ToHex());
                Assert.Equal(wire.ToHex(), actual.GetRLPEncoded().ToHex());
                Assert.Throws<BlobSidecarNotAllowedInBlockBodyException>(() => TransactionFactory.CreateTransaction(wire, false));
                Assert.ThrowsAny<Exception>(() => TreeDecodeOracle.DecodeTransaction(wire, false));
            }
        }

        [Fact]
        public void Given_TheTypedEncoderDecodeEntryPoints_When_GivenAnEncodedTransaction_Then_TheyShareTheStreamingDecoder()
        {
            var tx1559 = TransactionFactory.Create1559Transaction(1, 2, 3, 4, 21000, "0x" + Bytes(20).ToHex(), 5, "0x1234",
                AccessList(), NonZeroLeadingHex(32), NonZeroLeadingHex(32), "0x01");
            var decoded = Transaction1559Encoder.Current.Decode(tx1559.GetRLPEncoded());
            Assert.Equal(tx1559.Hash.ToHex(), decoded.Hash.ToHex());

            var withoutTypeByte = Transaction1559Encoder.Current.Decode(tx1559.GetRLPEncoded().Skip(1).ToArray());
            Assert.Equal(tx1559.Hash.ToHex(), withoutTypeByte.Hash.ToHex());
        }

        [Fact]
        public void Given_AnAccessListAndAnAuthorisationList_When_DecodedThroughThePublicDecoders_Then_TheyMatchWhatWasEncoded()
        {
            var accessList = AccessList() ?? new List<AccessListItem>();
            if (accessList.Count == 0) accessList.Add(new AccessListItem("0x" + Bytes(20).ToHex(), new List<byte[]> { Bytes(32) }));
            var decodedAccessList = AccessListRLPEncoderDecoder.DecodeAccessList(AccessListRLPEncoderDecoder.EncodeAccessList(accessList));
            Assert.Equal(accessList.Count, decodedAccessList.Count);
            Assert.Null(AccessListRLPEncoderDecoder.DecodeAccessList(AccessListRLPEncoderDecoder.EncodeAccessList(null)));

            var authorisations = Authorisations();
            var decodedAuthorisations = AuthorisationListRLPEncoderDecoder.Decode(AuthorisationListRLPEncoderDecoder.Encode(authorisations));
            Assert.Null(ObjectComparer.FirstDifference(authorisations.Select(a => a.Address.EnsureHexPrefix().ToLowerInvariant()).ToList(),
                decodedAuthorisations.Select(a => a.Address.EnsureHexPrefix().ToLowerInvariant()).ToList()));
        }
    }
}
