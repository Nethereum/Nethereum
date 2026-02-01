using System.Collections.Generic;
using System.Numerics;
using Nethereum.EVM;

namespace Nethereum.AccountAbstraction.Bundler.Validation.ERC7562
{
    public class StorageSlotAccess
    {
        public string ContractAddress { get; set; } = "";
        public BigInteger Slot { get; set; }
        public bool IsWrite { get; set; }
        public bool IsTransient { get; set; }
        public EntityType AccessedBy { get; set; }
        public int Depth { get; set; }
    }

    public class OpcodeExecution
    {
        public Instruction Opcode { get; set; }
        public string ExecutedAt { get; set; } = "";
        public int Depth { get; set; }
        public EntityType ExecutedBy { get; set; }
        public int ProgramCounter { get; set; }
    }

    public class CallInfo
    {
        public string From { get; set; } = "";
        public string To { get; set; } = "";
        public BigInteger Value { get; set; }
        public byte[] Data { get; set; } = [];
        public int Depth { get; set; }
        public EntityType CalledBy { get; set; }
    }

    public class ERC7562Violation
    {
        public string Rule { get; set; } = "";
        public string Message { get; set; } = "";
        public string Address { get; set; } = "";
        public Instruction? Opcode { get; set; }
        public BigInteger? Slot { get; set; }
        public EntityType? Entity { get; set; }

        public override string ToString() => $"[{Rule}] {Message} at {Address}";

        public static ERC7562Violation FromOpcode(string rule, string message, Instruction opcode, ERC7562ValidationContext context)
        {
            return new ERC7562Violation
            {
                Rule = rule,
                Message = message,
                Address = context.CurrentAddress,
                Opcode = opcode,
                Entity = context.CurrentEntity
            };
        }

        public static ERC7562Violation FromStorage(string rule, string message, string address, BigInteger slot, ERC7562ValidationContext context)
        {
            return new ERC7562Violation
            {
                Rule = rule,
                Message = message,
                Address = address,
                Slot = slot,
                Entity = context.CurrentEntity
            };
        }

        public static ERC7562Violation FromCall(string rule, string message, string address, ERC7562ValidationContext context)
        {
            return new ERC7562Violation
            {
                Rule = rule,
                Message = message,
                Address = address,
                Entity = context.CurrentEntity
            };
        }
    }

    public class ERC7562ValidationContext
    {
        public EntityInfo? Sender { get; set; }
        public EntityInfo? Factory { get; set; }
        public EntityInfo? Paymaster { get; set; }
        public EntityInfo? Aggregator { get; set; }

        public string EntryPointAddress { get; set; } = "";
        public EntityType CurrentEntity { get; set; }
        public int CallDepth { get; set; }
        public string CurrentAddress { get; set; } = "";
        public bool IsDeploymentPhase { get; set; }

        public List<StorageSlotAccess> StorageAccesses { get; } = new();
        public List<OpcodeExecution> OpcodeExecutions { get; } = new();
        public List<CallInfo> Calls { get; } = new();
        public HashSet<string> AccessedAddresses { get; } = new();

        public int Create2Count { get; set; }
        public int CreateCount { get; set; }
        public string DeployedSenderAddress { get; set; } = "";

        public Dictionary<string, HashSet<BigInteger>> AssociatedSlots { get; } = new();
        public Dictionary<BigInteger, string> KeccakPreimages { get; } = new();
        public List<ERC7562Violation> Violations { get; } = new();

        public bool StrictMode { get; set; } = true;
        public bool AllowRip7212Precompile { get; set; } = false;

        public void AddViolation(string rule, string message, string? address = null, Instruction? opcode = null, BigInteger? slot = null)
        {
            Violations.Add(new ERC7562Violation
            {
                Rule = rule,
                Message = message,
                Address = address ?? CurrentAddress,
                Opcode = opcode,
                Slot = slot,
                Entity = CurrentEntity
            });
        }

        public bool HasViolations => Violations.Count > 0;

        public EntityInfo? GetEntityInfo(EntityType type)
        {
            return type switch
            {
                EntityType.Sender => Sender,
                EntityType.Factory => Factory,
                EntityType.Paymaster => Paymaster,
                EntityType.Aggregator => Aggregator,
                _ => null
            };
        }

        public EntityInfo? GetCurrentEntityInfo() => GetEntityInfo(CurrentEntity);

        public static bool AddressEquals(string? a, string? b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            return a.ToLowerInvariant() == b.ToLowerInvariant();
        }

        public bool IsEntityAddress(string? address)
        {
            if (string.IsNullOrEmpty(address)) return false;

            return AddressEquals(Sender?.Address, address) ||
                   AddressEquals(Factory?.Address, address) ||
                   AddressEquals(Paymaster?.Address, address) ||
                   AddressEquals(Aggregator?.Address, address);
        }

        public EntityType? GetEntityTypeForAddress(string? address)
        {
            if (string.IsNullOrEmpty(address)) return null;

            if (AddressEquals(Sender?.Address, address)) return EntityType.Sender;
            if (AddressEquals(Factory?.Address, address)) return EntityType.Factory;
            if (AddressEquals(Paymaster?.Address, address)) return EntityType.Paymaster;
            if (AddressEquals(Aggregator?.Address, address)) return EntityType.Aggregator;

            return null;
        }

        public void TrackAssociatedSlot(string contractAddress, BigInteger slot)
        {
            var normalized = contractAddress.ToLowerInvariant();
            if (!AssociatedSlots.ContainsKey(normalized))
            {
                AssociatedSlots[normalized] = new HashSet<BigInteger>();
            }
            AssociatedSlots[normalized].Add(slot);
        }

        public bool IsAssociatedSlot(string contractAddress, BigInteger slot)
        {
            var normalized = contractAddress.ToLowerInvariant();
            return AssociatedSlots.TryGetValue(normalized, out var slots) && slots.Contains(slot);
        }

        public void UpdateCurrentEntity(string address)
        {
            var entityType = GetEntityTypeForAddress(address);
            if (entityType.HasValue)
            {
                CurrentEntity = entityType.Value;
            }
            CurrentAddress = address;
        }

        public static ERC7562ValidationContext Create(
            string entryPoint,
            EntityInfo sender,
            EntityInfo? factory = null,
            EntityInfo? paymaster = null,
            EntityInfo? aggregator = null)
        {
            return new ERC7562ValidationContext
            {
                EntryPointAddress = entryPoint?.ToLowerInvariant() ?? "",
                Sender = sender,
                Factory = factory,
                Paymaster = paymaster,
                Aggregator = aggregator,
                CurrentEntity = factory != null ? EntityType.Factory : EntityType.Sender,
                IsDeploymentPhase = factory != null
            };
        }
    }
}
