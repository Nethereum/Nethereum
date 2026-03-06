using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Mud;

namespace Nethereum.AppChain.IntegrationTests.E2E.Tables
{
    public class PlayerTableRecord : TableRecord<PlayerTableRecord.PlayerKey, PlayerTableRecord.PlayerValue>
    {
        public PlayerTableRecord() : base("Player")
        {
        }

        public class PlayerKey
        {
            [Parameter("address", "playerId", 1)]
            public string PlayerId { get; set; } = "";
        }

        public class PlayerValue
        {
            [Parameter("uint256", "score", 1)]
            public BigInteger Score { get; set; }

            [Parameter("uint32", "level", 2)]
            public int Level { get; set; }

            [Parameter("uint256", "lastActive", 3)]
            public BigInteger LastActive { get; set; }

            [Parameter("bool", "isActive", 4)]
            public bool IsActive { get; set; }

            [Parameter("string", "name", 5)]
            public string Name { get; set; } = "";
        }
    }
}
