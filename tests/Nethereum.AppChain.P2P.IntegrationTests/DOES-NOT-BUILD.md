# This project does not build, and that is deliberate

It drives `Nethereum.AppChain.P2P.DotNetty`, which was deleted when AppChains moved to DevP2P as
their only transport. It is kept on disk **for reference**, out of `Nethereum.slnx`, because the
scenarios it describes are the specification for the Clique HA work that replaces it.

Building it directly produces about eighteen errors — missing `Nethereum.CoreChain.P2P`,
`Nethereum.AppChain.P2P.BlockHandling`, `Nethereum.AppChain.P2P.DotNetty`, and the
`P2PBlockHandler` / `P2PBlockBroadcaster` / `P2PMessageDispatcher` types. Nothing builds it: it is
not in the solution and no CI workflow names it.

## What is worth reading here

`CliqueClusterTests` describes four behaviours in BDD form — turn-based production picks the correct
signer, in-turn difficulty exceeds out-of-turn, the out-of-turn wiggle delay is applied, and a
produced block's signer is recovered on validation. Those are the semantics the HA work must
reproduce, which is why the project survives.

`CliqueClusterFixture` is the 3-signer bring-up whose shape a DevP2P harness has to match.

`CliqueLoadTests` is thinner than its name: three tests, two assertions in the whole file, and
`LoadTest_SustainedLoad_5Seconds` asserts nothing. It is a throughput harness, not coverage.

## What replaced the parts that were real

Clique's turn selection and difficulty are now tested against the engine itself in
`tests/Nethereum.Consensus.Clique.UnitTests` — pure arithmetic that never needed a cluster. The
tests that lived here for the same rules recomputed the rule inside the test and asserted it against
itself, so they passed whether or not the engine was correct.

## Before reviving it

Re-point the fixture at `DevChainNode` rather than a DotNetty transport, and give the load tests
assertions or delete them. Until then, treat this directory as documentation.
