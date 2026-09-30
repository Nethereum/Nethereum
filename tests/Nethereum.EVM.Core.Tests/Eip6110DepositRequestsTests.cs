using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.EVM.Execution;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    /// <summary>
    /// EIP-6110 §Block validity. A deposit is produced only by a log whose address is
    /// <c>DEPOSIT_CONTRACT_ADDRESS</c> and whose first topic is
    /// <c>DEPOSIT_EVENT_SIGNATURE_HASH</c>; the two twins below are what stop any other contract
    /// minting validators by emitting the same shape.
    /// </summary>
    public class Eip6110DepositRequestsTests
    {
        private const string AnotherContract = "0x1111111111111111111111111111111111111111";

        private static byte[] Word(int value)
        {
            var word = new byte[32];
            word[28] = (byte)(value >> 24);
            word[29] = (byte)(value >> 16);
            word[30] = (byte)(value >> 8);
            word[31] = (byte)value;
            return word;
        }

        private static byte[] Filled(int length, byte value) =>
            Enumerable.Repeat(value, length).ToArray();

        private static byte[] DepositEventData(
            byte pubkeyByte = 0xa1, byte credentialsByte = 0xb2, byte signatureByte = 0xc3)
        {
            var data = new List<byte>();
            data.AddRange(Word(160));
            data.AddRange(Word(256));
            data.AddRange(Word(320));
            data.AddRange(Word(384));
            data.AddRange(Word(512));

            data.AddRange(Word(48)); data.AddRange(Filled(48, pubkeyByte)); data.AddRange(new byte[16]);
            data.AddRange(Word(32)); data.AddRange(Filled(32, credentialsByte));
            data.AddRange(Word(8)); data.AddRange(Filled(8, 0xd4)); data.AddRange(new byte[24]);
            data.AddRange(Word(96)); data.AddRange(Filled(96, signatureByte));
            data.AddRange(Word(8)); data.AddRange(Filled(8, 0xe5)); data.AddRange(new byte[24]);

            return data.ToArray();
        }

        private static Log DepositLog(byte pubkeyByte = 0xa1) =>
            Log.Create(DepositEventData(pubkeyByte),
                DepositRequests.DepositContractAddress, DepositRequests.DepositEventSignatureHash);

        [Fact]
        public void Given_ADepositLog_When_ItsDataIsExtracted_Then_ItIsTheFiveFieldsConcatenated()
        {
            var deposit = DepositRequests.ExtractDepositData(DepositEventData());

            Assert.Equal(48 + 32 + 8 + 96 + 8, deposit.Length);
            Assert.Equal(Filled(48, 0xa1).ToHex(), deposit.Take(48).ToArray().ToHex());
            Assert.Equal(Filled(32, 0xb2).ToHex(), deposit.Skip(48).Take(32).ToArray().ToHex());
            Assert.Equal(Filled(8, 0xd4).ToHex(), deposit.Skip(80).Take(8).ToArray().ToHex());
            Assert.Equal(Filled(96, 0xc3).ToHex(), deposit.Skip(88).Take(96).ToArray().ToHex());
            Assert.Equal(Filled(8, 0xe5).ToHex(), deposit.Skip(184).Take(8).ToArray().ToHex());
        }

        [Fact]
        public void Given_DepositLogs_When_RequestDataIsCollected_Then_TheyAppearInLogOrder()
        {
            var collected = DepositRequests.CollectRequestData(
                new List<Log> { DepositLog(0x01), DepositLog(0x02) });

            Assert.Equal(192 * 2, collected.Length);
            Assert.Equal(Filled(48, 0x01).ToHex(), collected.Take(48).ToArray().ToHex());
            Assert.Equal(Filled(48, 0x02).ToHex(), collected.Skip(192).Take(48).ToArray().ToHex());
        }

        [Fact]
        public void Given_ALogFromAnotherContractCarryingTheDepositTopic_When_Collected_Then_ItIsIgnored()
        {
            var forged = Log.Create(DepositEventData(), AnotherContract,
                DepositRequests.DepositEventSignatureHash);

            Assert.False(DepositRequests.IsDepositLog(forged));
            Assert.Empty(DepositRequests.CollectRequestData(new List<Log> { forged }));
        }

        [Fact]
        public void Given_ADepositContractLogWithAnotherTopic_When_Collected_Then_ItIsIgnored()
        {
            var other = Log.Create(DepositEventData(), DepositRequests.DepositContractAddress,
                new byte[32]);

            Assert.False(DepositRequests.IsDepositLog(other));
            Assert.Empty(DepositRequests.CollectRequestData(new List<Log> { other }));
        }

        [Fact]
        public void Given_ADepositContractLogWithNoTopics_When_Collected_Then_ItIsIgnored()
        {
            Assert.False(DepositRequests.IsDepositLog(
                Log.Create(DepositEventData(), DepositRequests.DepositContractAddress)));
        }

        [Theory]
        [InlineData(575)]
        [InlineData(577)]
        public void Given_ADepositLogOfTheWrongLength_When_Extracted_Then_TheBlockIsRefused(int length)
        {
            Assert.Throws<MalformedDepositLogException>(
                () => DepositRequests.ExtractDepositData(new byte[length]));
        }

        [Fact]
        public void Given_ADepositLogWhoseHeadOffsetIsWrong_When_Extracted_Then_TheBlockIsRefused()
        {
            var data = DepositEventData();
            Array.Copy(Word(161), 0, data, 0, 32);

            var refusal = Assert.Throws<MalformedDepositLogException>(
                () => DepositRequests.ExtractDepositData(data));
            Assert.Contains("pubkey offset", refusal.Message);
        }

        [Fact]
        public void Given_ADepositLogWhoseFieldLengthIsWrong_When_Extracted_Then_TheBlockIsRefused()
        {
            var data = DepositEventData();
            Array.Copy(Word(47), 0, data, 160, 32);

            var refusal = Assert.Throws<MalformedDepositLogException>(
                () => DepositRequests.ExtractDepositData(data));
            Assert.Contains("pubkey size", refusal.Message);
        }

        [Fact]
        public void Given_ADepositLogWhosePubkeyOffsetAliasesToTheCorrectOffset_When_Extracted_Then_TheBlockIsRefused()
        {
            var data = DepositEventData();
            Array.Copy(WordAbove32Bits(160), 0, data, 0, 32);

            var refusal = Assert.Throws<MalformedDepositLogException>(
                () => DepositRequests.ExtractDepositData(data));
            Assert.Contains("pubkey offset", refusal.Message);
        }

        /// <summary>
        /// The size head is read by the same routine, so it aliases the same way: a declared
        /// pubkey length of <c>2^32 + 48</c> truncates to the 48 EIP-6110 requires.
        /// </summary>
        [Fact]
        public void Given_ADepositLogWhosePubkeySizeAliasesToTheCorrectSize_When_Extracted_Then_TheBlockIsRefused()
        {
            var data = DepositEventData();
            Array.Copy(WordAbove32Bits(48), 0, data, 160, 32);

            var refusal = Assert.Throws<MalformedDepositLogException>(
                () => DepositRequests.ExtractDepositData(data));
            Assert.Contains("pubkey size", refusal.Message);
        }

        [Fact]
        public void Given_ADepositLogWhoseCredentialsOffsetAliasesToTheCorrectOffset_When_Extracted_Then_TheBlockIsRefused()
        {
            var data = DepositEventData();
            Array.Copy(WordAbove32Bits(256), 0, data, 32, 32);

            var refusal = Assert.Throws<MalformedDepositLogException>(
                () => DepositRequests.ExtractDepositData(data));
            Assert.Contains("withdrawal credentials offset", refusal.Message);
        }

        [Fact]
        public void Given_ADepositLogWhosePubkeyOffsetHasTheSignBitSet_When_Extracted_Then_TheBlockIsRefused()
        {
            var data = DepositEventData();
            Array.Copy(WordWithTheSignBitSet(160), 0, data, 0, 32);

            var refusal = Assert.Throws<MalformedDepositLogException>(
                () => DepositRequests.ExtractDepositData(data));
            Assert.Contains("pubkey offset", refusal.Message);
        }

        private static byte[] WordAbove32Bits(int aliasedValue)
        {
            var word = Word(aliasedValue);
            word[27] = 0x01;
            return word;
        }

        private static byte[] WordWithTheSignBitSet(int lowBits)
        {
            var word = Word(lowBits);
            word[28] = 0x80;
            return word;
        }
    }
}
