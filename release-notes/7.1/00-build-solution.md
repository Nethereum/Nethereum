# Build, Solution & Packaging — Nethereum 7.1

Nethereum 7.1 packs 139 packages at 7.1.0 from a pack list that names every library, with `PackageReadmeFile` on the packages that have a README, correct descriptions, tags and assembly titles, host executables that no longer try to become packages, and `Nethereum.AppChain.Server` packaged as a tool. The solution gains the Engine API hosting library and the new conformance, microbenchmark and portable-precompile test projects, a Forgejo workflow runs the build and test lanes in a container, and the Linux RapidSnark native build moves to a repository script.

## Version

- 7.1 packages are versioned 7.1.0. `buildConf/Version.props` carries `VersionMajor` 7 and `VersionMinor` 0 in the tree; the release cut sets `VersionMinor` to 1, and `NugetVersion` and `NethereumVersion` follow (`<major>.<minor>.<patch>`, or with `-<ReleaseSuffix>` when a suffix is passed to the pack script)
- Packages whose project sets `<Version>$(NethereumVersionPreview)</Version>` publish as `7.1.0-preview`. The preview packages are:
  - `Nethereum.AppChain`, `Nethereum.AppChain.Sequencer`, `Nethereum.AppChain.Policy`, `Nethereum.AppChain.Anchoring`, `Nethereum.AppChain.Anchoring.Postgres`, `Nethereum.AppChain.Server.Core`, `Nethereum.AppChain.Server`
  - `Nethereum.AccountAbstraction.AppChain`, `Nethereum.Explorer.Anchoring`
  - `Nethereum.MainnetChain`, `Nethereum.MainnetChain.Server`
- Every other package publishes as `7.1.0`. `Nethereum.DevP2P`, `Nethereum.DevP2P.Sync`, `Nethereum.EngineApi.Hosting` (new in 7.1), `Nethereum.ChainNode.Hosting` and `Nethereum.Zisk.Core` use `$(NethereumVersion)` and are stable. Every project packed by `nuget.bat` sets `<Version>` rather than `<VersionPrefix>`
- A preview package may depend only on packages that are published in the same release; the dependency set below is what makes that true

## Packaging — Pack List

`src/nuget.bat` packs 139 package directories (129 in 7.0). Ten are added; none is removed.

- `Nethereum.AppChain.Anchoring` and `Nethereum.AppChain.Anchoring.Postgres`: `Nethereum.AppChain.Sequencer` and `Nethereum.Explorer.Anchoring` depend on them, and 7.0 published the dependents without them
- `Nethereum.AppChain.Policy`, `Nethereum.AccountAbstraction.AppChain` and `Nethereum.Consensus.Clique`: libraries the AppChain packages reference, previously not packed
- `Nethereum.AppChain.Server`: the AppChain host, packed as a tool (`<IsPackable>true</IsPackable>` set in its project)
- `Nethereum.AccountAbstraction.SimpleAccount`, `Nethereum.ABI.ABIInfoSqliteStorage` and `Nethereum.Wallet.UI.Components.Avalonia`: libraries that had no package at 7.0; the Avalonia components ship as a package, not as source only
- `Nethereum.EngineApi.Hosting`, a new library in 7.1. The Unity packages stay out of `nuget.bat`: they build in their own pipeline against a local Unity Editor

## Packaging — Project Metadata

Every library with a README now packs it, so the package page on nuget.org shows it.

- `PackageReadmeFile` and a `README.md` pack item added to 22 packages: `Nethereum.AccountAbstraction.Bundler.RocksDB`, `Nethereum.Beaconchain`, `Nethereum.Blazor.Solidity`, `Nethereum.ChainNode.Hosting`, `Nethereum.DataServices`, `Nethereum.DevP2P`, `Nethereum.DevP2P.Sync`, `Nethereum.EVM.Precompiles`, `Nethereum.EVM.Precompiles.Bls`, `Nethereum.EVM.Precompiles.Kzg`, `Nethereum.EngineApi.Hosting`, `Nethereum.MainnetChain`, `Nethereum.Maui.AndroidUsb`, `Nethereum.Model.SSZ`, `Nethereum.Quorum`, `Nethereum.Siwe`, `Nethereum.Siwe.Core`, `Nethereum.Sourcify.Database`, `Nethereum.Zisk.Core`, `Nethereum.ZkProofs`, `Nethereum.ZkProofs.Snarkjs.Blazor`, `Nethereum.ZkProofsVerifier`; and to 14 more: `Nethereum.ABI.ABIInfoSqliteStorage`, `Nethereum.AppChain.Anchoring.Postgres`, `Nethereum.AppChain.Server.Core`, `Nethereum.ENS`, `Nethereum.Explorer.Anchoring`, `Nethereum.Parity`, `Nethereum.Parity.Reactive`, `Nethereum.Rsk`, `Nethereum.StandardNonFungibleTokenERC721`, `Nethereum.StandardTokenEIP20`, `Nethereum.Wallet.Trezor`, `Nethereum.Wallet.UI.Components.Avalonia`, `Nethereum.Web3`, `Nethereum.ZkProofs.Snarkjs`
- 23 packages are now strong-name signed with the shared key, like the rest: `Nethereum.AccountAbstraction.Bundler.RocksDB`, `Nethereum.AppChain.Server`, `Nethereum.AppChain.Server.Core`, `Nethereum.Blazor.Solidity`, `Nethereum.ChainNode.Hosting`, `Nethereum.CoreChain.Freezer`, `Nethereum.CoreChain.RocksDB`, `Nethereum.DevChain.Server`, `Nethereum.DevP2P`, `Nethereum.DevP2P.Sync`, `Nethereum.EVM`, `Nethereum.EVM.Contracts`, `Nethereum.EVM.Core`, `Nethereum.Explorer.Anchoring`, `Nethereum.Freezer`, `Nethereum.HdWallet` (not on .NET Framework, where NBitcoin is not strong-named), `Nethereum.MainnetChain`, `Nethereum.MainnetChain.Server`, `Nethereum.MudBlazorComponents`, `Nethereum.Reown.AppKit.Blazor`, `Nethereum.Signer.Ledger`, `Nethereum.Signer.Trezor`, `Nethereum.Zisk.Core`
- `Nethereum.Zisk.Core` gains a `Description`, `AssemblyTitle`, `AssemblyName` and `PackageId`, so it no longer publishes with a blank description
- `Netherum` corrected to `Nethereum` in the `PackageTags` of every package and in the `Description` of eight (`Nethereum.Besu`, `Nethereum.Geth`, `Nethereum.Parity`, `Nethereum.Rsk`, `Nethereum.Signer.AWSKeyManagement`, `Nethereum.Signer.AzureKeyVault`, `Nethereum.Signer.Ledger`, `Nethereum.Signer.Trezor`), which makes the packages findable by their own name
- `AssemblyTitle` copy-paste fixed: `Nethereum.EIP6963WalletInterop` (was `Nethereum.Metamask`) and `Nethereum.Quorum` (was `Nethereum.RPC.Quorum`)
- `IsPackable` is `false` on the host executables `Nethereum.EVM.Zisk` (the guest ELF), `Nethereum.Zisk.Prover.Server` and `Nethereum.BlockProver.Server`, so a repository-wide `dotnet pack` no longer produces them
- `Nethereum.MainnetChain.Server` sets `GarbageCollectionAdaptationMode` to 0

## Packaging — Dependency Set

- `Nethereum.AppChain.Server.Core` no longer references `Nethereum.Aspire.ServiceDefaults`, a sample-hosting project that is not in the pack list, so the package no longer depends on something nuget.org does not have
- `Nethereum.DevChain` and `Nethereum.MainnetChain` reference the new `Nethereum.EngineApi.Hosting`, so the JWT-authenticated Engine API endpoint is one library shared by both
- Every package that uses `Newtonsoft.Json` requires 13.0.4 or later (`[13.0.4,14)`; it was `[11.0.2,14)`, and NuGet resolved 11.0.2 for a project without its own reference). The Unity builds keep the Newtonsoft.Json that Unity bundles
- `Nethereum.HdWallet` moves to NBitcoin 10.0.16 and targets netstandard2.0 and net461; net452 is dropped
- No project in `src` references `Nethereum.Documentation`, and `Nethereum.Signer.Bls.Herumi` is referenced at `$(NethereumVersion)`, the two 7.0.0 dependencies on unpublished versions are absent from the project files

## Solution — Nethereum.slnx

`Nethereum.slnx` lists 256 projects (245 in 7.0). Added: `Nethereum.EngineApi.Hosting`, `Nethereum.Wallet.UI.Components.UnitTests`, `Nethereum.Wallet.UI.Components.Avalonia.UnitTests`, `Nethereum.EVM.Microbench`, `Nethereum.EVM.Precompiles.Bn254PortableTests`, `Nethereum.EVM.Precompiles.ModExpPortableTests`, `Nethereum.EEST.Conformance`, `Nethereum.EEST.ConformanceRunner`, `Nethereum.Mud.Repositories.Postgres.UnitTests`, `NetDapps` and `Nethereum.HybridWebView`. The RSK entry points at `src/Nethereum.Rsk/Nethereum.Rsk.csproj` (the path had the wrong case on case-sensitive file systems). One test project was removed from the repository: `Nethereum.AppChain.P2P.IntegrationTests`.

## CI

- `.forgejo/workflows/ci.yml` is new: a `build` lane on every push, and `unit`, `full` and `Zisk` lanes (workflow dispatch, nightly `full`) that run `ci/run-tests.sh` inside the `nethereum-ci` image, split over the areas `evm`, `eest`, `corechain`, `appchain`, `devp2p`, `aa`, `zk` and `rest`
- `.github/workflows/build-rapidsnark.yml` builds the Linux RapidSnark library with `scripts/build-rapidsnark-linux.sh x64` in place of inline steps, and re-runs when that script changes

## Breaking Changes

None in public API. Package consumers should note:

- `Nethereum.EVM.Zisk`, `Nethereum.Zisk.Prover.Server` and `Nethereum.BlockProver.Server` are not packable; they were never meant to be installed from NuGet
- Packages are versioned 7.1.0 (previews 7.1.0-preview). `Nethereum.DevP2P` and `Nethereum.DevP2P.Sync` are no longer previews: a project that pinned `7.0.0-preview` moves to `7.1.0`; for `Nethereum.MainnetChain` and the AppChain packages it moves to `7.1.0-preview`
- `Nethereum.HdWallet` no longer has a net452 build
- The 23 newly signed assemblies now have a strong name, so code compiled against their unsigned 7.0 builds is recompiled
