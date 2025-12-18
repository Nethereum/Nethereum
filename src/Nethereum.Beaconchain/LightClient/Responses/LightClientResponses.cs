using System.Collections.Generic;

#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#else
using Newtonsoft.Json;
#endif

namespace Nethereum.Beaconchain.LightClient.Responses
{
    public class LightClientBootstrapResponse
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("version")]
#else
        [JsonProperty("version")]
#endif
        public string Version { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("data")]
#else
        [JsonProperty("data")]
#endif
        public LightClientBootstrapData Data { get; set; }
    }

    public class LightClientBootstrapData
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("header")]
#else
        [JsonProperty("header")]
#endif
        public LightClientHeaderDto Header { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("current_sync_committee")]
#else
        [JsonProperty("current_sync_committee")]
#endif
        public SyncCommitteeDto CurrentSyncCommittee { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("current_sync_committee_branch")]
#else
        [JsonProperty("current_sync_committee_branch")]
#endif
        public List<string> CurrentSyncCommitteeBranch { get; set; }
    }

    public class LightClientUpdateResponse
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("version")]
#else
        [JsonProperty("version")]
#endif
        public string Version { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("data")]
#else
        [JsonProperty("data")]
#endif
        public LightClientUpdateData Data { get; set; }
    }

    public class LightClientUpdateData
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("attested_header")]
#else
        [JsonProperty("attested_header")]
#endif
        public LightClientHeaderDto AttestedHeader { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("next_sync_committee")]
#else
        [JsonProperty("next_sync_committee")]
#endif
        public SyncCommitteeDto NextSyncCommittee { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("next_sync_committee_branch")]
#else
        [JsonProperty("next_sync_committee_branch")]
#endif
        public List<string> NextSyncCommitteeBranch { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("finalized_header")]
#else
        [JsonProperty("finalized_header")]
#endif
        public LightClientHeaderDto FinalizedHeader { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("finality_branch")]
#else
        [JsonProperty("finality_branch")]
#endif
        public List<string> FinalityBranch { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("sync_aggregate")]
#else
        [JsonProperty("sync_aggregate")]
#endif
        public SyncAggregateDto SyncAggregate { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("signature_slot")]
#else
        [JsonProperty("signature_slot")]
#endif
        public string SignatureSlot { get; set; }
    }

    public class LightClientFinalityUpdateResponse
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("version")]
#else
        [JsonProperty("version")]
#endif
        public string Version { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("data")]
#else
        [JsonProperty("data")]
#endif
        public LightClientFinalityUpdateData Data { get; set; }
    }

    public class LightClientFinalityUpdateData
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("attested_header")]
#else
        [JsonProperty("attested_header")]
#endif
        public LightClientHeaderDto AttestedHeader { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("finalized_header")]
#else
        [JsonProperty("finalized_header")]
#endif
        public LightClientHeaderDto FinalizedHeader { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("finality_branch")]
#else
        [JsonProperty("finality_branch")]
#endif
        public List<string> FinalityBranch { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("sync_aggregate")]
#else
        [JsonProperty("sync_aggregate")]
#endif
        public SyncAggregateDto SyncAggregate { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("signature_slot")]
#else
        [JsonProperty("signature_slot")]
#endif
        public string SignatureSlot { get; set; }
    }

    public class LightClientOptimisticUpdateResponse
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("version")]
#else
        [JsonProperty("version")]
#endif
        public string Version { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("data")]
#else
        [JsonProperty("data")]
#endif
        public LightClientOptimisticUpdateData Data { get; set; }
    }

    public class LightClientOptimisticUpdateData
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("attested_header")]
#else
        [JsonProperty("attested_header")]
#endif
        public LightClientHeaderDto AttestedHeader { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("sync_aggregate")]
#else
        [JsonProperty("sync_aggregate")]
#endif
        public SyncAggregateDto SyncAggregate { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("signature_slot")]
#else
        [JsonProperty("signature_slot")]
#endif
        public string SignatureSlot { get; set; }
    }

    public class LightClientHeaderDto
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("beacon")]
#else
        [JsonProperty("beacon")]
#endif
        public BeaconBlockHeaderDto Beacon { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("execution")]
#else
        [JsonProperty("execution")]
#endif
        public ExecutionPayloadHeaderDto Execution { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("execution_branch")]
#else
        [JsonProperty("execution_branch")]
#endif
        public List<string> ExecutionBranch { get; set; }
    }

    public class BeaconBlockHeaderDto
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("slot")]
#else
        [JsonProperty("slot")]
#endif
        public string Slot { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("proposer_index")]
#else
        [JsonProperty("proposer_index")]
#endif
        public string ProposerIndex { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("parent_root")]
#else
        [JsonProperty("parent_root")]
#endif
        public string ParentRoot { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("state_root")]
#else
        [JsonProperty("state_root")]
#endif
        public string StateRoot { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("body_root")]
#else
        [JsonProperty("body_root")]
#endif
        public string BodyRoot { get; set; }
    }

    public class ExecutionPayloadHeaderDto
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("parent_hash")]
#else
        [JsonProperty("parent_hash")]
#endif
        public string ParentHash { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("fee_recipient")]
#else
        [JsonProperty("fee_recipient")]
#endif
        public string FeeRecipient { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("state_root")]
#else
        [JsonProperty("state_root")]
#endif
        public string StateRoot { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("receipts_root")]
#else
        [JsonProperty("receipts_root")]
#endif
        public string ReceiptsRoot { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("logs_bloom")]
#else
        [JsonProperty("logs_bloom")]
#endif
        public string LogsBloom { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("prev_randao")]
#else
        [JsonProperty("prev_randao")]
#endif
        public string PrevRandao { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("block_number")]
#else
        [JsonProperty("block_number")]
#endif
        public string BlockNumber { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("gas_limit")]
#else
        [JsonProperty("gas_limit")]
#endif
        public string GasLimit { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("gas_used")]
#else
        [JsonProperty("gas_used")]
#endif
        public string GasUsed { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("timestamp")]
#else
        [JsonProperty("timestamp")]
#endif
        public string Timestamp { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("extra_data")]
#else
        [JsonProperty("extra_data")]
#endif
        public string ExtraData { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("base_fee_per_gas")]
#else
        [JsonProperty("base_fee_per_gas")]
#endif
        public string BaseFeePerGas { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("block_hash")]
#else
        [JsonProperty("block_hash")]
#endif
        public string BlockHash { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("transactions_root")]
#else
        [JsonProperty("transactions_root")]
#endif
        public string TransactionsRoot { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("withdrawals_root")]
#else
        [JsonProperty("withdrawals_root")]
#endif
        public string WithdrawalsRoot { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("blob_gas_used")]
#else
        [JsonProperty("blob_gas_used")]
#endif
        public string BlobGasUsed { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("excess_blob_gas")]
#else
        [JsonProperty("excess_blob_gas")]
#endif
        public string ExcessBlobGas { get; set; }
    }

    public class SyncCommitteeDto
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("pubkeys")]
#else
        [JsonProperty("pubkeys")]
#endif
        public List<string> PubKeys { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("aggregate_pubkey")]
#else
        [JsonProperty("aggregate_pubkey")]
#endif
        public string AggregatePubKey { get; set; }
    }

    public class SyncAggregateDto
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("sync_committee_bits")]
#else
        [JsonProperty("sync_committee_bits")]
#endif
        public string SyncCommitteeBits { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("sync_committee_signature")]
#else
        [JsonProperty("sync_committee_signature")]
#endif
        public string SyncCommitteeSignature { get; set; }
    }

    public class StateForkResponse
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("data")]
#else
        [JsonProperty("data")]
#endif
        public StateForkData Data { get; set; }
    }

    public class StateForkData
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("previous_version")]
#else
        [JsonProperty("previous_version")]
#endif
        public string PreviousVersion { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("current_version")]
#else
        [JsonProperty("current_version")]
#endif
        public string CurrentVersion { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("epoch")]
#else
        [JsonProperty("epoch")]
#endif
        public string Epoch { get; set; }
    }
}
