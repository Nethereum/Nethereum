using System.Collections.Generic;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json;
using Stj = System.Text.Json.Serialization;

namespace Nethereum.CoreChain.Tracing
{
    public class OpcodeTraceConfig
    {
        [JsonProperty("enableMemory")]
        public bool EnableMemory { get; set; }

        [JsonProperty("disableStack")]
        public bool DisableStack { get; set; }

        [JsonProperty("disableStorage")]
        public bool DisableStorage { get; set; }

        [JsonProperty("enableReturnData")]
        public bool EnableReturnData { get; set; }

        [JsonProperty("limit")]
        public int Limit { get; set; }
    }

    public class StateOverride
    {
        [JsonProperty("balance")]
        public HexBigInteger Balance { get; set; }

        [JsonProperty("nonce")]
        public string Nonce { get; set; }

        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("state")]
        public Dictionary<string, string> State { get; set; }

        [JsonProperty("stateDiff")]
        public Dictionary<string, string> StateDiff { get; set; }
    }

    public class OpcodeTraceResult
    {
        [JsonProperty("gas")]
        public ulong Gas { get; set; }

        [JsonProperty("failed")]
        public bool Failed { get; set; }

        [JsonProperty("returnValue")]
        public string ReturnValue { get; set; }

        [JsonProperty("structLogs")]
        public List<OpcodeTraceStep> StructLogs { get; set; }
    }

    public class OpcodeTraceStep
    {
        [JsonProperty("pc")]
        public ulong Pc { get; set; }

        [JsonProperty("op")]
        public string Op { get; set; }

        [JsonProperty("gas")]
        public ulong Gas { get; set; }

        [JsonProperty("gasCost")]
        public ulong GasCost { get; set; }

        [JsonProperty("memory")]
        [Stj.JsonIgnore(Condition = Stj.JsonIgnoreCondition.WhenWritingNull)]
        public string Memory { get; set; }

        [JsonProperty("memSize")]
        [Stj.JsonIgnore]
        public int MemSize { get; set; }

        [JsonProperty("stack")]
        [Stj.JsonIgnore(Condition = Stj.JsonIgnoreCondition.WhenWritingNull)]
        public List<HexBigInteger> Stack { get; set; }

        [JsonProperty("storage")]
        [Stj.JsonIgnore(Condition = Stj.JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, string> Storage { get; set; }

        [JsonProperty("depth")]
        public int Depth { get; set; }

        [JsonProperty("refund")]
        [Stj.JsonIgnore(Condition = Stj.JsonIgnoreCondition.WhenWritingDefault)]
        public ulong Refund { get; set; }

        [JsonProperty("error")]
        [Stj.JsonIgnore(Condition = Stj.JsonIgnoreCondition.WhenWritingNull)]
        public string Error { get; set; }

        [JsonProperty("address")]
        [Stj.JsonIgnore]
        public string Address { get; set; }
    }

    public class CallTraceResult
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("from")]
        public string From { get; set; }

        [JsonProperty("to")]
        public string To { get; set; }

        [JsonProperty("value")]
        public HexBigInteger Value { get; set; }

        [JsonProperty("gas")]
        public HexBigInteger Gas { get; set; }

        [JsonProperty("gasUsed")]
        public HexBigInteger GasUsed { get; set; }

        [JsonProperty("input")]
        public string Input { get; set; }

        [JsonProperty("output")]
        public string Output { get; set; }

        [JsonProperty("error")]
        [Stj.JsonIgnore(Condition = Stj.JsonIgnoreCondition.WhenWritingNull)]
        public string Error { get; set; }

        [JsonProperty("revertReason")]
        [Stj.JsonIgnore(Condition = Stj.JsonIgnoreCondition.WhenWritingNull)]
        public string RevertReason { get; set; }

        [JsonProperty("calls")]
        [Stj.JsonIgnore(Condition = Stj.JsonIgnoreCondition.WhenWritingNull)]
        public List<CallTraceResult> Calls { get; set; }
    }

    public class PrestateTraceResult
    {
        [JsonProperty("pre")]
        public Dictionary<string, PrestateAccountInfo> Pre { get; set; }

        [JsonProperty("post")]
        public Dictionary<string, PrestateAccountInfo> Post { get; set; }
    }

    public class PrestateAccountInfo
    {
        [JsonProperty("balance")]
        public HexBigInteger Balance { get; set; }

        [JsonProperty("nonce")]
        public long Nonce { get; set; }

        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("storage")]
        public Dictionary<string, string> Storage { get; set; }
    }

    public class TraceExecutionResult
    {
        public Program Program { get; set; }
        public CallInput CallInput { get; set; }
        public ExecutionStateService StateService { get; set; }
        public bool IsContractCreation { get; set; }
        public bool IsSimpleTransfer { get; set; }
        public long TotalGasUsed { get; set; }
    }
}
