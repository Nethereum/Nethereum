using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Mud;

namespace Nethereum.AppChain.IntegrationTests.E2E.Tables
{
    public class GameItemTableRecord : TableRecord<GameItemTableRecord.GameItemKey, GameItemTableRecord.GameItemValue>
    {
        public GameItemTableRecord() : base("GameItem")
        {
        }

        public class GameItemKey
        {
            [Parameter("uint32", "itemId", 1)]
            public int ItemId { get; set; }
        }

        public class GameItemValue
        {
            [Parameter("uint8", "itemType", 1)]
            public byte ItemType { get; set; }

            [Parameter("uint32", "power", 2)]
            public int Power { get; set; }

            [Parameter("uint32", "rarity", 3)]
            public int Rarity { get; set; }

            [Parameter("address", "owner", 4)]
            public string Owner { get; set; } = "";

            [Parameter("bool", "equipped", 5)]
            public bool Equipped { get; set; }

            [Parameter("string", "name", 6)]
            public string Name { get; set; } = "";
        }
    }

    public static class ItemType
    {
        public const byte Weapon = 0;
        public const byte Armor = 1;
        public const byte Consumable = 2;
        public const byte Quest = 3;
    }

    public static class Rarity
    {
        public const int Common = 1;
        public const int Uncommon = 2;
        public const int Rare = 3;
        public const int Epic = 4;
        public const int Legendary = 5;
    }
}
