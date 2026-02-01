using System.Numerics;

namespace Nethereum.AccountAbstraction.Bundler.Validation.ERC7562
{
    public enum EntityType
    {
        None,
        Sender,
        Factory,
        Paymaster,
        Aggregator
    }

    public class EntityInfo
    {
        public string Address { get; set; } = "";
        public EntityType Type { get; set; }
        public bool IsStaked { get; set; }
        public BigInteger StakeAmount { get; set; }
        public ulong UnstakeDelaySec { get; set; }

        public static EntityInfo Create(EntityType type, string address, bool isStaked = false, BigInteger? stake = null, ulong unstakeDelay = 0)
        {
            return new EntityInfo
            {
                Address = address?.ToLowerInvariant() ?? "",
                Type = type,
                IsStaked = isStaked,
                StakeAmount = stake ?? BigInteger.Zero,
                UnstakeDelaySec = unstakeDelay
            };
        }

        public static EntityInfo CreateSender(string address, bool isStaked = false, BigInteger? stake = null, ulong unstakeDelay = 0)
            => Create(EntityType.Sender, address, isStaked, stake, unstakeDelay);

        public static EntityInfo CreateFactory(string address, bool isStaked = false, BigInteger? stake = null, ulong unstakeDelay = 0)
            => Create(EntityType.Factory, address, isStaked, stake, unstakeDelay);

        public static EntityInfo CreatePaymaster(string address, bool isStaked = false, BigInteger? stake = null, ulong unstakeDelay = 0)
            => Create(EntityType.Paymaster, address, isStaked, stake, unstakeDelay);

        public static EntityInfo CreateAggregator(string address, bool isStaked = false, BigInteger? stake = null, ulong unstakeDelay = 0)
            => Create(EntityType.Aggregator, address, isStaked, stake, unstakeDelay);
    }
}
