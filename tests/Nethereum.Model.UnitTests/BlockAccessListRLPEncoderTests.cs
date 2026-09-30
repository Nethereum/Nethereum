using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Model.UnitTests
{
    /// <summary>
    /// EIP-7928. The block access list is not carried in the block body, so the
    /// only thing that can be checked against the outside world is its hash —
    /// which the header carries, and which every Amsterdam block therefore
    /// depends on.
    ///
    /// <para>
    /// The vector below is a real block access list lifted from the execution
    /// spec fixtures, with the block access list hash that block's header
    /// declares. It exercises all five populated change kinds, so a wrong
    /// field order or a non-minimal integer encoding fails here rather than
    /// surviving to be blamed on execution later.
    ///
    /// <para>
    /// It does NOT prove nesting: every account in it has one slot and every
    /// slot one change, so an encoder that flattened a slot's changes would
    /// still reproduce this hash. The corpus sweep in Nethereum.EVM.Core.Tests
    /// covers that, along with every case a single chosen block cannot.
    /// </para>
    /// </para>
    /// </summary>
    public class BlockAccessListRLPEncoderTests
    {
        private const string ExpectedHash =
            "0x02213d0c13e57cc6de56073a64e1c1b120edc4135635da8599a73a6eb03dba3e";

        private const string BlockAccessListJson =
            @"[{""address"":""0x00000961ef480eb55e80d19ad83579a64c007002"",""nonceChanges"":[],""balanceChanges"":[],""codeC" +
            @"hanges"":[],""storageChanges"":[],""storageReads"":[""0x00"",""0x01"",""0x02"",""0x03""]},{""address"":""0x000" +
            @"064d678505ad48f8ccb093bc65613800e8282"",""nonceChanges"":[],""balanceChanges"":[],""codeChanges"":[],""storage" +
            @"Changes"":[],""storageReads"":[""0x00"",""0x01"",""0x02"",""0x03""]},{""address"":""0x0000bbddc7ce488642fb579f" +
            @"8b00f3a590007251"",""nonceChanges"":[],""balanceChanges"":[],""codeChanges"":[],""storageChanges"":[],""storag" +
            @"eReads"":[""0x00"",""0x01"",""0x02"",""0x03""]},{""address"":""0x0000bff46984e3725691fa540a8c7589300d8282"",""" +
            @"nonceChanges"":[],""balanceChanges"":[],""codeChanges"":[],""storageChanges"":[],""storageReads"":[""0x00"",""" +
            @"0x01"",""0x02"",""0x03""]},{""address"":""0x0000f90827f1c53a10cb7a02335b175320002935"",""nonceChanges"":[],""b" +
            @"alanceChanges"":[],""codeChanges"":[],""storageChanges"":[{""slot"":""0x00"",""slotChanges"":[{""blockAccessIn" +
            @"dex"":""0x00"",""postValue"":""0xfa542c9718333cd6aee04e765aab8af0fb27d163b6a44ffa86380eeb2ada0a3e""}]}],""stor" +
            @"ageReads"":[]},{""address"":""0x000f3df6d732807ef1319fb7b8bb8522d0beac02"",""nonceChanges"":[],""balanceChange" +
            @"s"":[],""codeChanges"":[],""storageChanges"":[{""slot"":""0x0c"",""slotChanges"":[{""blockAccessIndex"":""0x00" +
            @""",""postValue"":""0x0c""}]}],""storageReads"":[""0x200b""]},{""address"":""0x17ad248d3f234b5fbec5fe581a7454de" +
            @"7b61828c"",""nonceChanges"":[],""balanceChanges"":[],""codeChanges"":[{""blockAccessIndex"":""0x01"",""newCode" +
            @""":""0x600054""}],""storageChanges"":[],""storageReads"":[""0x00""]},{""address"":""0x2adc25665018aa1fe0e6bc66" +
            @"6dac8fc2697ff9ba"",""nonceChanges"":[],""balanceChanges"":[{""blockAccessIndex"":""0x01"",""postBalance"":""0x" +
            @"c86d""}],""codeChanges"":[],""storageChanges"":[],""storageReads"":[]},{""address"":""0xf6c3a9edc1afa0ad5b720e" +
            @"4d42e1437c43d3b3ff"",""nonceChanges"":[{""blockAccessIndex"":""0x01"",""postNonce"":""0x01""}],""balanceChange" +
            @"s"":[{""blockAccessIndex"":""0x01"",""postBalance"":""0x033b2e3c9fd0803ce7fd63ea""}],""codeChanges"":[],""stor" +
            @"ageChanges"":[],""storageReads"":[]}]";

        private static EvmUInt256 Num(string hex)
            => EvmUInt256.FromBigEndian(Pad32(hex.HexToByteArray()));

        private static byte[] Pad32(byte[] value)
        {
            if (value.Length == 32) return value;
            var padded = new byte[32];
            Array.Copy(value, 0, padded, 32 - value.Length, value.Length);
            return padded;
        }

        private static ulong Index(string hex)
        {
            var bytes = hex.HexToByteArray();
            ulong v = 0;
            foreach (var b in bytes) v = (v << 8) | b;
            return v;
        }

        private static List<AccountChanges> Parse(string json)
        {
            var result = new List<AccountChanges>();
            using var doc = JsonDocument.Parse(json);

            foreach (var a in doc.RootElement.EnumerateArray())
            {
                var account = new AccountChanges(a.GetProperty("address").GetString());

                foreach (var s in a.GetProperty("storageChanges").EnumerateArray())
                {
                    var slot = new SlotChanges(Num(s.GetProperty("slot").GetString()));
                    foreach (var c in s.GetProperty("slotChanges").EnumerateArray())
                        slot.Changes.Add(new StorageChange(
                            Index(c.GetProperty("blockAccessIndex").GetString()),
                            Num(c.GetProperty("postValue").GetString())));
                    account.StorageChanges.Add(slot);
                }

                foreach (var r in a.GetProperty("storageReads").EnumerateArray())
                    account.StorageReads.Add(Num(r.GetString()));

                foreach (var c in a.GetProperty("balanceChanges").EnumerateArray())
                    account.BalanceChanges.Add(new BalanceChange(
                        Index(c.GetProperty("blockAccessIndex").GetString()),
                        Num(c.GetProperty("postBalance").GetString())));

                foreach (var c in a.GetProperty("nonceChanges").EnumerateArray())
                    account.NonceChanges.Add(new NonceChange(
                        Index(c.GetProperty("blockAccessIndex").GetString()),
                        Index(c.GetProperty("postNonce").GetString())));

                foreach (var c in a.GetProperty("codeChanges").EnumerateArray())
                    account.CodeChanges.Add(new CodeChange(
                        Index(c.GetProperty("blockAccessIndex").GetString()),
                        c.GetProperty("newCode").GetString().HexToByteArray()));

                result.Add(account);
            }

            return result;
        }

        [Fact]
        public void Given_ARealBlockAccessList_When_Hashed_Then_ItMatchesTheHashTheHeaderDeclares()
        {
            var accessList = Parse(BlockAccessListJson);

            var hash = BlockAccessListRLPEncoder.Current.Hash(accessList);

            Assert.Equal(ExpectedHash, "0x" + hash.ToHex());
        }

        [Fact]
        public void Given_TheVector_When_Parsed_Then_ItExercisesEveryChangeKind()
        {
            var accessList = Parse(BlockAccessListJson);

            Assert.Contains(accessList, a => a.StorageChanges.Any());
            Assert.Contains(accessList, a => a.StorageReads.Any());
            Assert.Contains(accessList, a => a.BalanceChanges.Any());
            Assert.Contains(accessList, a => a.NonceChanges.Any());
            Assert.Contains(accessList, a => a.CodeChanges.Any());
        }

        [Fact]
        public void Given_AnEmptyBlockAccessList_When_Hashed_Then_ItIsTheHashOfAnEmptyRlpList()
        {
            var encoded = BlockAccessListRLPEncoder.Current.Encode(new List<AccountChanges>());

            Assert.Equal(new byte[] { 0xc0 }, encoded);
        }

        [Fact]
        public void Given_ARealBlockAccessList_When_DecodedAfterEncoding_Then_ItRoundTripsExactly()
        {
            // eth/71 (EIP-8159) is the caller Decode exists for: a peer sends
            // back the exact bytes Encode produced, and the receiving side
            // must reconstruct the identical AccountChanges list — including
            // the StorageReads list and the nested SlotChanges->StorageChange
            // structure a flattening bug would silently drop or misplace.
            var original = Parse(BlockAccessListJson);

            var decoded = BlockAccessListRLPEncoder.Current.Decode(
                BlockAccessListRLPEncoder.Current.Encode(original));

            AssertBlockAccessListsEqual(original, decoded);
        }

        [Fact]
        public void Given_ADecodedBlockAccessList_When_ASourceValueIsMutated_Then_ItNoLongerRoundTrips()
        {
            var original = Parse(BlockAccessListJson);
            var decoded = BlockAccessListRLPEncoder.Current.Decode(
                BlockAccessListRLPEncoder.Current.Encode(original));

            var mutated = Parse(BlockAccessListJson);
            var accountWithReads = mutated.First(a => a.StorageReads.Count > 0);
            accountWithReads.StorageReads[0] = accountWithReads.StorageReads[0] + EvmUInt256.One;

            Assert.ThrowsAny<Exception>(() => AssertBlockAccessListsEqual(mutated, decoded));
        }

        private static void AssertBlockAccessListsEqual(List<AccountChanges> expected, List<AccountChanges> actual)
        {
            Assert.Equal(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                var e = expected[i];
                var a = actual[i];
                Assert.Equal(e.Address, a.Address, ignoreCase: true);

                Assert.Equal(e.StorageChanges.Count, a.StorageChanges.Count);
                for (int s = 0; s < e.StorageChanges.Count; s++)
                {
                    Assert.Equal(e.StorageChanges[s].Slot, a.StorageChanges[s].Slot);
                    Assert.Equal(e.StorageChanges[s].Changes.Count, a.StorageChanges[s].Changes.Count);
                    for (int c = 0; c < e.StorageChanges[s].Changes.Count; c++)
                    {
                        Assert.Equal(e.StorageChanges[s].Changes[c].BlockAccessIndex, a.StorageChanges[s].Changes[c].BlockAccessIndex);
                        Assert.Equal(e.StorageChanges[s].Changes[c].PostValue, a.StorageChanges[s].Changes[c].PostValue);
                    }
                }

                Assert.Equal(e.StorageReads, a.StorageReads);

                Assert.Equal(e.BalanceChanges.Count, a.BalanceChanges.Count);
                for (int c = 0; c < e.BalanceChanges.Count; c++)
                {
                    Assert.Equal(e.BalanceChanges[c].BlockAccessIndex, a.BalanceChanges[c].BlockAccessIndex);
                    Assert.Equal(e.BalanceChanges[c].PostBalance, a.BalanceChanges[c].PostBalance);
                }

                Assert.Equal(e.NonceChanges.Count, a.NonceChanges.Count);
                for (int c = 0; c < e.NonceChanges.Count; c++)
                {
                    Assert.Equal(e.NonceChanges[c].BlockAccessIndex, a.NonceChanges[c].BlockAccessIndex);
                    Assert.Equal(e.NonceChanges[c].NewNonce, a.NonceChanges[c].NewNonce);
                }

                Assert.Equal(e.CodeChanges.Count, a.CodeChanges.Count);
                for (int c = 0; c < e.CodeChanges.Count; c++)
                {
                    Assert.Equal(e.CodeChanges[c].BlockAccessIndex, a.CodeChanges[c].BlockAccessIndex);
                    Assert.Equal(e.CodeChanges[c].NewCode, a.CodeChanges[c].NewCode);
                }
            }
        }

        [Fact]
        public void Given_ValuesTheFixtureCorpusCannotReach_When_Encoded_Then_TheyUseMinimalIntegers()
        {
            var account = new AccountChanges("0x1111111111111111111111111111111111111111");
            account.NonceChanges.Add(new NonceChange(256, 1));
            account.CodeChanges.Add(new CodeChange(0, new byte[0]));

            var encoded = BlockAccessListRLPEncoder.Current.Encode(
                new List<AccountChanges> { account });

            var hex = encoded.ToHex();

            Assert.Contains("820100", hex);
            Assert.Contains("8080", hex);
            Assert.DoesNotContain("8100", hex);
        }
    }
}
