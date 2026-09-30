using System.Collections.Generic;
using System.Text.Json;
using Nethereum.CoreChain.Rpc;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Rpc
{
    public class BlockAccessListRpcMapperTests
    {
        private const string Address = "0xa94f5374fce5edbc8e2a8697c15331677e6ebf0b";

        [Fact]
        public void Given_AnAccountThatChangedEveryWay_When_Projected_Then_EachFieldTakesTheHexFormItsSchemaGives()
        {
            var json = Serialize(BlockAccessListRpcMapper.ToDto(new List<AccountChanges> { EveryKindOfChange() }));

            Assert.Equal(
                "[{\"address\":\"" + Address + "\"," +
                "\"storageChanges\":[{\"key\":\"0x0000000000000000000000000000000000000000000000000000000000000001\"," +
                "\"changes\":[{\"index\":\"0x2\",\"value\":\"0x0000000000000000000000000000000000000000000000000000000000000003\"}]}]," +
                "\"storageReads\":[\"0x0000000000000000000000000000000000000000000000000000000000000004\"]," +
                "\"balanceChanges\":[{\"index\":\"0x1\",\"value\":\"0x5\"}]," +
                "\"nonceChanges\":[{\"index\":\"0x1\",\"value\":\"0x6\"}]," +
                "\"codeChanges\":[{\"index\":\"0x1\",\"code\":\"0x6000\"}]}]",
                json);
        }

        [Fact]
        public void Given_TheSameNumberAsASlotAndAsAnIndex_When_Projected_Then_TheSlotIsFullWidthAndTheIndexIsMinimal()
        {
            var dto = BlockAccessListRpcMapper.ToDto(new List<AccountChanges> { EveryKindOfChange() })[0];

            Assert.Equal("0x0000000000000000000000000000000000000000000000000000000000000001", dto.StorageChanges[0].Key);
            Assert.Equal("0x1", dto.BalanceChanges[0].Index);
        }

        [Fact]
        public void Given_AnAccountThatOnlyChangedItsBalance_When_Projected_Then_TheFiveUntouchedListsAreStillPresentAndEmpty()
        {
            var account = new AccountChanges(Address);
            account.BalanceChanges.Add(new BalanceChange(0, EvmUInt256.Zero));

            var json = Serialize(BlockAccessListRpcMapper.ToDto(new List<AccountChanges> { account }));

            Assert.Equal(
                "[{\"address\":\"" + Address + "\"," +
                "\"storageChanges\":[],\"storageReads\":[]," +
                "\"balanceChanges\":[{\"index\":\"0x0\",\"value\":\"0x0\"}]," +
                "\"nonceChanges\":[],\"codeChanges\":[]}]",
                json);
        }

        // EIP-7928: "When no state changes are present, this field is the empty RLP list `0xc0`". An empty
        [Fact]
        public void Given_ABlockThatChangedNothing_When_Projected_Then_TheAnswerIsAnEmptyArray()
        {
            Assert.Equal("[]", Serialize(BlockAccessListRpcMapper.ToDto(new List<AccountChanges>())));
        }

        private static AccountChanges EveryKindOfChange()
        {
            var account = new AccountChanges(Address);
            var slot = new SlotChanges(new EvmUInt256(1UL));
            slot.Changes.Add(new StorageChange(2, new EvmUInt256(3UL)));
            account.StorageChanges.Add(slot);
            account.StorageReads.Add(new EvmUInt256(4UL));
            account.BalanceChanges.Add(new BalanceChange(1, new EvmUInt256(5UL)));
            account.NonceChanges.Add(new NonceChange(1, 6));
            account.CodeChanges.Add(new CodeChange(1, new byte[] { 0x60, 0x00 }));
            return account;
        }

        private static string Serialize(List<AccountAccessDto> accounts) =>
            JsonSerializer.Serialize(accounts, typeof(List<AccountAccessDto>), CoreChainJsonContext.Default);
    }
}
