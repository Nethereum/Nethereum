using Xunit;

// This assembly's fixtures (DevChainBundlerFixture, BundlerTestFixture/SharedEthereumFixture,
// ERC7579TestFixture, ModularAccountBundlerFixture, InProcessBundlerHost-backed fixtures) each stand
// up their own in-process DevChain node and/or bundler. xUnit runs different [Collection]s in
// parallel by default, which lets these instances contend on the process's RPC/bundler/devchain
// state at the same time and produces non-deterministic pass/fail results across runs (task #48).
// Disabling cross-collection parallelization for this assembly makes every collection's fixture
// fully spin up, run its tests, and tear down before the next one starts.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
