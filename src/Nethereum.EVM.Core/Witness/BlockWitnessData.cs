using System.Collections.Generic;
using System.Linq;
using Nethereum.EVM.Hardforks;

namespace Nethereum.EVM.Witness
{
    public partial class BlockWitnessData
    {
        public long BlockNumber { get; set; }
        public long Timestamp { get; set; }
        public long BaseFee { get; set; }
        public long BlockGasLimit { get; set; }
        public long ChainId { get; set; }
        public string Coinbase { get; set; }
        public byte[] Difficulty { get; set; }
        public byte[] PreStateRoot { get; set; }
        public byte[] ParentHash { get; set; }
        public byte[] ExtraData { get; set; }
        public byte[] MixHash { get; set; }
        public byte[] Nonce { get; set; }

        public List<BlockWithdrawal> Withdrawals { get; set; }
        public long? BlobGasUsed { get; set; }
        public long? ExcessBlobGas { get; set; }
        public byte[] ParentBeaconBlockRoot { get; set; }
        public byte[] RequestsHash { get; set; }

        public ulong? SlotNumber { get; set; }

        public List<BlockWitnessTransaction> Transactions { get; set; } = new List<BlockWitnessTransaction>();

        public List<Model.AccountChanges> DeclaredBlockAccessList { get; set; }

        public List<WitnessAccount> Accounts { get; set; } = new List<WitnessAccount>();

        public BlockFeatureConfig Features { get; set; }


        public bool VerifyWitnessProofs { get; set; }

        public bool ComputePostStateRoot { get; set; }

        public bool ProduceBlockCommitments { get; set; }

        /// <summary>
        /// EIP-7002 §Specification: <i>"If there is no code at
        /// <c>WITHDRAWAL_REQUEST_PREDEPLOY_ADDRESS</c>, the corresponding block MUST be marked
        /// invalid."</i> A witness built around a single transaction rather than a block - a
        /// state-test fixture, whose pre-state describes one transaction's world and never the
        /// consensus-layer predeploys - has no request queue for that rule to be about.
        /// </summary>
        public bool SkipsRequestSystemCalls { get; set; }
    }

    public class BlockFeatureConfig
    {
        public HardforkName Fork { get; set; } = HardforkName.Unspecified;
        public WitnessStateTreeType StateTree { get; set; } = WitnessStateTreeType.Patricia;
        public WitnessHashFunction HashFunction { get; set; } = WitnessHashFunction.Keccak;

        public static BlockFeatureConfig Cancun => new BlockFeatureConfig
        {
            Fork = HardforkName.Cancun
        };

        public static BlockFeatureConfig Prague => new BlockFeatureConfig
        {
            Fork = HardforkName.Prague
        };

        public static BlockFeatureConfig Osaka => new BlockFeatureConfig
        {
            Fork = HardforkName.Osaka
        };

        public static BlockFeatureConfig BinaryBlake3(HardforkName fork = HardforkName.Osaka) => new BlockFeatureConfig
        {
            Fork = fork,
            StateTree = WitnessStateTreeType.Binary, HashFunction = WitnessHashFunction.Blake3
        };

        public static BlockFeatureConfig BinaryPoseidon(HardforkName fork = HardforkName.Osaka) => new BlockFeatureConfig
        {
            Fork = fork,
            StateTree = WitnessStateTreeType.Binary, HashFunction = WitnessHashFunction.Poseidon
        };
    }

    public class BlockWithdrawal
    {
        public ulong Index { get; set; }
        public ulong ValidatorIndex { get; set; }
        public string Address { get; set; }
        public ulong AmountInGwei { get; set; }
    }

    public class BlockWitnessTransaction
    {
        public string From { get; set; }
        public byte[] RlpEncoded { get; set; }

        public List<string> AuthorisationAuthorities { get; set; }
    }
}
