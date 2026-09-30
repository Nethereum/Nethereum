using System.Collections.Generic;

namespace Nethereum.EVM.UnitTests.GeneralStateTests
{
    public sealed class MainnetBlockFixture
    {
        public long BlockNumber { get; set; }
        public string Scenario { get; set; }
        public MainnetBlockHeaderFixture Header { get; set; }
        public List<string> TransactionsRlp { get; set; } = new();
        public List<MainnetBlockHeaderFixture> Uncles { get; set; } = new();
        public Dictionary<string, MainnetAccountFixture> PreState { get; set; } = new();
        public Dictionary<string, MainnetAccountAssertion> PostAssertions { get; set; } = new();
    }

    public sealed class MainnetBlockHeaderFixture
    {
        public string ParentHash { get; set; }
        public string UnclesHash { get; set; }
        public string Coinbase { get; set; }
        public string StateRoot { get; set; }
        public string TransactionsRoot { get; set; }
        public string ReceiptsRoot { get; set; }
        public string LogsBloom { get; set; }
        public string Difficulty { get; set; }
        public string Number { get; set; }
        public string GasLimit { get; set; }
        public string GasUsed { get; set; }
        public string Timestamp { get; set; }
        public string ExtraData { get; set; }
        public string MixHash { get; set; }
        public string Nonce { get; set; }
        public string BaseFee { get; set; }
        public string WithdrawalsRoot { get; set; }
        public string ParentBeaconBlockRoot { get; set; }
    }

    public sealed class MainnetAccountFixture
    {
        public string Balance { get; set; } = "0x0";
        public string Nonce { get; set; } = "0x0";
        public string Code { get; set; }
        public Dictionary<string, string> Storage { get; set; } = new();
    }

    public sealed class MainnetAccountAssertion
    {
        public string Balance { get; set; }
        public string Nonce { get; set; }
        public string CodeHash { get; set; }
        public bool? Exists { get; set; }
        public Dictionary<string, string> Storage { get; set; }
    }
}
