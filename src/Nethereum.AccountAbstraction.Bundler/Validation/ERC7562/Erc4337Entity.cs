using System.Numerics;

using Nethereum.Documentation;
namespace Nethereum.AccountAbstraction.Bundler.Validation.ERC7562
{
    [NethereumDocExample(DocSection.AccountAbstraction, "bundler", "EntityType - the four ERC-7562 entity roles")]
    public enum EntityType
    {
        None,
        Sender,
        Factory,
        Paymaster,
        Aggregator
    }

    public static class EntityTypeExtensions
    {
        /// <summary>
        /// Maps an <see cref="EntityType"/> to the lower-case entity name used by the
        /// ERC-7562 spec (and eth-infinitism's reference bundler) in validation error text,
        /// e.g. "account uses banned opcode: ORIGIN". "Sender" is reported as "account" to
        /// match that wording.
        /// </summary>
        public static string ToErc7562EntityName(this EntityType entityType) => entityType switch
        {
            EntityType.Sender => "account",
            EntityType.Factory => "factory",
            EntityType.Paymaster => "paymaster",
            EntityType.Aggregator => "aggregator",
            _ => "none"
        };
    }

    [NethereumDocExample(DocSection.AccountAbstraction, "bundler", "Erc4337Entity - an entity's address and stake, as validation sees it")]
    public class Erc4337Entity
    {
        public string Address { get; set; } = "";
        public EntityType Type { get; set; }
        public bool IsStaked { get; set; }
        public BigInteger StakeAmount { get; set; }
        public ulong UnstakeDelaySec { get; set; }

        public static Erc4337Entity Create(EntityType type, string address, bool isStaked = false, BigInteger? stake = null, ulong unstakeDelay = 0)
        {
            return new Erc4337Entity
            {
                Address = address?.ToLowerInvariant() ?? "",
                Type = type,
                IsStaked = isStaked,
                StakeAmount = stake ?? BigInteger.Zero,
                UnstakeDelaySec = unstakeDelay
            };
        }

        public static Erc4337Entity CreateSender(string address, bool isStaked = false, BigInteger? stake = null, ulong unstakeDelay = 0)
            => Create(EntityType.Sender, address, isStaked, stake, unstakeDelay);

        public static Erc4337Entity CreateFactory(string address, bool isStaked = false, BigInteger? stake = null, ulong unstakeDelay = 0)
            => Create(EntityType.Factory, address, isStaked, stake, unstakeDelay);

        public static Erc4337Entity CreatePaymaster(string address, bool isStaked = false, BigInteger? stake = null, ulong unstakeDelay = 0)
            => Create(EntityType.Paymaster, address, isStaked, stake, unstakeDelay);

        public static Erc4337Entity CreateAggregator(string address, bool isStaked = false, BigInteger? stake = null, ulong unstakeDelay = 0)
            => Create(EntityType.Aggregator, address, isStaked, stake, unstakeDelay);
    }
}
