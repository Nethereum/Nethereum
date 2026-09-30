# Build & Solution — Nethereum 7.0

Nethereum 7.0 moves the repository onto the modern XML solution format: a single `Nethereum.slnx` replaces the legacy `Nethereum.sln`, now enumerating 247 projects (the new EVM.Core, DevP2P, sync, hosting and Zisk projects). The multi-target framework matrix is unchanged from 6.1.0 — `net10.0` and every framework group were already in place — so the build story for 7.0 is the solution migration and a version roll, not a re-platforming of the target frameworks.

## Target frameworks

* `buildConf/Frameworks.props` is byte-for-byte identical to 6.1.0 — no framework variables were added, removed or retargeted in 7.0.
* `net10.0` and the framework groups referenced across the tree (`ServerFrameworks`, `WalletUIFrameworks`, `AppChainFrameworks = net8.0;net10.0`, `CoreChainFrameworks = net8.0;net9.0;net10.0`, `UnityFrameworks`, `UIFrameworks*`, `ENSFrameworks`, …) all already existed at 6.1.0. They are pre-existing, not new-in-7.0.
* The other shared build props (`buildConf/Generic.props`, `buildConf/Generic-CodeGen.props`, `buildConf/net35.props`) are unchanged since 6.1.0. There is no repository-root `Directory.Build.*` or `Directory.Packages.props`; the only `Directory.Build.props` files are per-sample files under `aspire/` and `src/demos/`.

## Solution & build scripts

* New `Nethereum.slnx` (XML solution format) replaces `Nethereum.sln`, which is retired. The `.slnx` enumerates 247 `.csproj` references, against 199 in the 6.1.0 `.sln` (+48 net — the new EVM.Core, DevP2P, DevP2P.Sync, EVM.Zisk, ChainNode.Hosting and related projects, offset by the removed AppChain P2P/Sync projects).
* Pack scripts extended for 7.0: `src/nuget-fast.ps1` and `src/nuget.bat` now pack the 19 new-in-7.0 packages (`Nethereum.EVM.Core`, `Nethereum.EVM.Precompiles`, `Nethereum.DevP2P`, `Nethereum.DevP2P.Sync`, `Nethereum.ChainNode.Hosting`, `Nethereum.MainnetChain`(+`.Server`), `Nethereum.CoreChain.Freezer`, `Nethereum.Freezer`, `Nethereum.Zisk.Core`, `Nethereum.WebAuthn`(+`.Blazor`/`.Windows`), `Nethereum.AccountAbstraction.WebAuthn`, `Nethereum.AccountAbstraction.Bundler.InProcess`, `Nethereum.AppChain.Server.Core`, `Nethereum.Explorer.Anchoring`, `Nethereum.DID`(+`.EthrDID`)) and drop the deleted `Nethereum.AppChain.Sync`. The guest-ELF `Nethereum.EVM.Zisk` and the un-packaged prover-server exes (`Nethereum.Zisk.Prover.Server`, `Nethereum.BlockProver.Server`) are intentionally excluded.

## Version

* `buildConf/Version.props`: `VersionMajor` `6` → `7`, `VersionMinor` `1` → `0`, i.e. **6.1.0 → 7.0.0** (`VersionPatch` stays `0`). The `-preview` NuGet suffix is applied at pack time via `ReleaseSuffix` (both pack scripts pass `/property:ReleaseSuffix`), so the release publishes as `7.0.0-preview` when the runner sets it.
* Versioning scheme unchanged: `NugetVersion` derives from `VersionMajor.VersionMinor.VersionPatch` with an optional `ReleaseSuffix`, and `NethereumVersionPreview` appends `-preview`.
