using System.Collections.Generic;

namespace Nethereum.EVM.UnitTests.GeneralStateTests
{
    /// <summary>
    /// JSON fixture for a single mainnet block regression cell. Captures the
    /// minimum data needed to replay a known-divergence block on top of an
    /// <see cref="Nethereum.CoreChain.Storage.InMemory.InMemoryStateStore"/>
    /// seeded with only the accounts the block actually touches.
    ///
    /// Tier 1 (today): <see cref="PreState"/> + <see cref="PostAssertions"/>
    /// — replay through BlockProcessor, assert balances / nonces / storage
    /// slots / precompile presence match canonical. Catches gas accounting
    /// + state-transition bugs at the per-account level without needing a
    /// full Patricia witness.
    ///
    /// Tier 2 (follow-up): add Merkle proofs of touched accounts against
    /// the parent state root, recompute the new state root using the
    /// witness only, compare to <see cref="MainnetBlockHeaderFixture.StateRoot"/>.
    /// </summary>
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

    /// <summary>
    /// Field-level header capture so fixtures stay readable and we don't
    /// need a header-RLP builder tool. The test reconstructs a
    /// <c>BlockHeader</c> from these fields directly. All fields are hex
    /// strings (with or without 0x prefix) for the byte-array values.
    /// </summary>
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

    /// <summary>
    /// Per-account post-block assertions. Any field that is null is not
    /// checked (fixture authors only assert what they want pinned).
    /// </summary>
    public sealed class MainnetAccountAssertion
    {
        public string Balance { get; set; }
        public string Nonce { get; set; }
        public string CodeHash { get; set; }
        public bool? Exists { get; set; }
        public Dictionary<string, string> Storage { get; set; }
    }
}
