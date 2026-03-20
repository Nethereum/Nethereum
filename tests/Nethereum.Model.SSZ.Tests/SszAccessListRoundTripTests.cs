using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.SSZ;
using Xunit;

namespace Nethereum.Model.SSZ.Tests
{
    public class SszAccessListRoundTripTests
    {
        private static readonly byte[] SlotZero = new byte[32];
        private static readonly byte[] SlotOne;

        static SszAccessListRoundTripTests()
        {
            SlotOne = new byte[32];
            SlotOne[31] = 0x01;
        }

        [Fact]
        public void AccessTuple_NoKeys_RoundTrip()
        {
            var original = new AccessListItem("0xdead000000000000000000000000000000000001",
                new List<byte[]>());

            var encoded = SszAccessListEncoder.Current.EncodeAccessTuple(original);
            var decoded = SszAccessListEncoder.Current.DecodeAccessTuple(encoded);

            Assert.Equal(original.Address.ToLower(), decoded.Address.ToLower());
            Assert.Empty(decoded.StorageKeys);
        }

        [Fact]
        public void AccessTuple_SingleKey_RoundTrip()
        {
            var original = new AccessListItem("0xdead000000000000000000000000000000000001",
                new List<byte[]> { SlotZero });

            var encoded = SszAccessListEncoder.Current.EncodeAccessTuple(original);
            var decoded = SszAccessListEncoder.Current.DecodeAccessTuple(encoded);

            Assert.Equal(original.Address.ToLower(), decoded.Address.ToLower());
            Assert.Single(decoded.StorageKeys);
            Assert.Equal(SlotZero, decoded.StorageKeys[0]);
        }

        [Fact]
        public void AccessTuple_MultipleKeys_RoundTrip()
        {
            var slot2 = "0x0000000000000000000000000000000000000000000000000000000000000002".HexToByteArray();
            var original = new AccessListItem("0xbeef000000000000000000000000000000000002",
                new List<byte[]> { SlotZero, SlotOne, slot2 });

            var encoded = SszAccessListEncoder.Current.EncodeAccessTuple(original);
            var decoded = SszAccessListEncoder.Current.DecodeAccessTuple(encoded);

            Assert.Equal(3, decoded.StorageKeys.Count);
            Assert.Equal(SlotZero, decoded.StorageKeys[0]);
            Assert.Equal(SlotOne, decoded.StorageKeys[1]);
            Assert.Equal(slot2, decoded.StorageKeys[2]);
        }

        [Fact]
        public void AccessTuple_HashTreeRoot_StableAcrossEncodeDecode()
        {
            var original = new AccessListItem("0xdead000000000000000000000000000000000001",
                new List<byte[]> { SlotZero, SlotOne });

            var rootBefore = SszAccessListEncoder.Current.HashTreeRootAccessTuple(original);
            var encoded = SszAccessListEncoder.Current.EncodeAccessTuple(original);
            var decoded = SszAccessListEncoder.Current.DecodeAccessTuple(encoded);
            var rootAfter = SszAccessListEncoder.Current.HashTreeRootAccessTuple(decoded);

            Assert.Equal(rootBefore, rootAfter);
        }

        [Fact]
        public void AccessList_Empty_RoundTrip()
        {
            var encoded = SszAccessListEncoder.Current.EncodeAccessList(new List<AccessListItem>());
            var decoded = SszAccessListEncoder.Current.DecodeAccessList(encoded);
            Assert.Empty(decoded);
        }

        [Fact]
        public void AccessList_SingleItem_RoundTrip()
        {
            var original = new List<AccessListItem>
            {
                new AccessListItem("0xdead000000000000000000000000000000000001",
                    new List<byte[]> { SlotZero })
            };

            var encoded = SszAccessListEncoder.Current.EncodeAccessList(original);
            var decoded = SszAccessListEncoder.Current.DecodeAccessList(encoded);

            Assert.Single(decoded);
            Assert.Equal(original[0].Address.ToLower(), decoded[0].Address.ToLower());
            Assert.Single(decoded[0].StorageKeys);
        }

        [Fact]
        public void AccessList_MultipleItems_RoundTrip()
        {
            var original = new List<AccessListItem>
            {
                new AccessListItem("0xdead000000000000000000000000000000000001",
                    new List<byte[]> { SlotZero, SlotOne }),
                new AccessListItem("0xbeef000000000000000000000000000000000002",
                    new List<byte[]>()),
                new AccessListItem("0xcafe000000000000000000000000000000000003",
                    new List<byte[]> { SlotOne })
            };

            var encoded = SszAccessListEncoder.Current.EncodeAccessList(original);
            var decoded = SszAccessListEncoder.Current.DecodeAccessList(encoded);

            Assert.Equal(3, decoded.Count);
            Assert.Equal(2, decoded[0].StorageKeys.Count);
            Assert.Empty(decoded[1].StorageKeys);
            Assert.Single(decoded[2].StorageKeys);
            Assert.Equal(SlotOne, decoded[2].StorageKeys[0]);
        }

        [Fact]
        public void AccessList_HashTreeRoot_StableAcrossEncodeDecode()
        {
            var original = new List<AccessListItem>
            {
                new AccessListItem("0xdead000000000000000000000000000000000001",
                    new List<byte[]> { SlotZero }),
                new AccessListItem("0xbeef000000000000000000000000000000000002",
                    new List<byte[]> { SlotOne })
            };

            var rootBefore = SszAccessListEncoder.Current.HashTreeRootAccessList(original);
            var encoded = SszAccessListEncoder.Current.EncodeAccessList(original);
            var decoded = SszAccessListEncoder.Current.DecodeAccessList(encoded);
            var rootAfter = SszAccessListEncoder.Current.HashTreeRootAccessList(decoded);

            Assert.Equal(rootBefore, rootAfter);
        }

        [Fact]
        public void AccessList_OrderMatters()
        {
            var item1 = new AccessListItem("0xdead000000000000000000000000000000000001",
                new List<byte[]> { SlotZero });
            var item2 = new AccessListItem("0xbeef000000000000000000000000000000000002",
                new List<byte[]> { SlotOne });

            var root12 = SszAccessListEncoder.Current.HashTreeRootAccessList(
                new List<AccessListItem> { item1, item2 });
            var root21 = SszAccessListEncoder.Current.HashTreeRootAccessList(
                new List<AccessListItem> { item2, item1 });

            Assert.NotEqual(root12, root21);
        }
    }
}
