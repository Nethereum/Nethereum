# Wallet & UI Libraries — Nethereum 7.0

The Nethereum wallet and UI-component libraries — the encrypted-vault account SDK, the EIP-1193 RPC-request handlers, the platform-agnostic MVVM component stack (Blazor / Avalonia / MAUI heads), the dApp connectors (MetaMask, WalletConnect, Reown AppKit, EIP-6963) and the MudBlazor helpers — are **established packages that shipped in the 6.x line and are carried forward unchanged in surface into 7.0**. There are no new wallet/UI packages. What 7.0 changes here is targeted: every wallet/UI package README was rebuilt to be accurate to the source; the transaction-simulation and EVM-replay paths were moved onto Amsterdam's fork-resolving `ChainForkResolver` so a preview or replay runs the rules of the block's own fork rather than a fixed default; the transaction views now surface native ETH transfers; verified on-chain balances now share one light-client network table; and the whole stack was migrated onto the shared chain-node / sync and `IStateReader` abstractions introduced elsewhere in 7.0.

## Wallet core & encrypted vault (`Nethereum.Wallet`)

Established since 6.x; carried forward. The account/vault SDK: four account types (`PrivateKeyWalletAccount`, `MnemonicWalletAccount`, `ViewOnlyWalletAccount`, `SmartContractWalletAccount`, each `IWalletAccount`), BIP32/BIP39 HD derivation (`MinimalHDWallet`, from `Nethereum.Accounts`), an AES-256 password-encrypted `WalletVault` (`DefaultAes256EncryptionStrategy` / `BouncyCastleAes256EncryptionStrategy`), in-memory and file vault services (`InMemoryWalletVaultService`, `FileWalletVaultService` behind `IWalletVaultService`), account grouping (`AccountGroup`), ChainList-backed chain management (`ChainManagementService`), dApp permission management (`IDappPermissionService`), pending-transaction and gas-config services, 4byte data decoding, and the `NethereumWalletHostProvider` host that implements `IWalletContext`.

* Vault round-trip — encrypt a vault holding mnemonic and private-key accounts, decrypt in a fresh instance, recover both addresses
* HD derivation — BIP39 mnemonic to BIP32 keys at `m/44'/60'/0'/0/{index}`
* Keystore encryption strategy — round-trip and wrong-password rejection

## Wallet RPC requests — EIP-1193 handlers (`Nethereum.Wallet.RpcRequests`)

Established since 6.x; carried forward. EIP-1193 JSON-RPC method handlers that bridge a dApp to the wallet, all extending `RpcMethodHandlerBase` and registered via `WalletRpcHandlerRegistration.RegisterAll`.

* Wired handlers — account access (`EthAccountsHandler`, `EthRequestAccountsHandler`), chain management (`WalletAddEthereumChainHandler`, `WalletSwitchEthereumChainHandler`, and `EthChainIdHandler` answering `eth_chainId` from `IWalletContext.ChainId`, defaulting to `0x1`), signing (`PersonalSignHandler`, `EthSignTypedDataV4Handler` with domain-chainId validation), transactions (`EthSendTransactionHandler`), and permissions (`WalletRequestPermissionsHandler`, `WalletGetPermissionsHandler`, `WalletRevokePermissionsHandler`)
* Six registered handlers are **not-implemented stubs that return `-32601`**: `WalletRegisterOnboardingHandler`, `WalletWatchAssetHandler`, `EthDecryptHandler`, `EthGetEncryptionPublicKeyHandler`, `Web3ClientVersionHandler`, `EthSubscribeHandler`

*Test status: the handlers have no dedicated automated tests in-repo; the surface above is documented from source (`WalletRpcHandlerRegistration.cs:8`), not from a passing test.*

## MVVM UI component libraries (`Nethereum.Wallet.UI.Components` + heads)

Established since 6.x; carried forward. A platform-agnostic MVVM core (`net10.0`, CommunityToolkit.Mvvm) of view models, registries (`IComponentRegistry`, `IAccountCreationRegistry`, `IDashboardPluginRegistry`, `IAccountDetailsRegistry`) and services, consumed by the framework heads. The Blazor head exposes a single drop-in `NethereumWallet` razor component with parameters `OnConnected`, `Width`, `Height`, `DrawerBehavior`, `ResponsiveBreakpoint`, `SidebarWidth`, `ShowLogo`, `ShowApplicationName`, `ShowNetworkInHeader`, `ShowAccountDetailsInHeader`; the Avalonia head exposes a `NethereumWallet` `UserControl` with a `Create(IServiceProvider)` factory.

Published heads: `Nethereum.Wallet.UI.Components.Blazor`, `.Maui`, `.Trezor`, `.Blazor.Trezor` (plus the base). The Avalonia head (`Nethereum.Wallet.UI.Components.Avalonia`) is **source-only** — consumed by the demo, not published to NuGet.

* Create-wallet MVVM flow — `NethereumWalletViewModel` create/unlock, password validation, service-exception handling
* Vault-creation view-model state — password match / strength / minimum-length gating
* Wallet text-field validation — required / email / address / private-key field rules

## Fork-aware transaction simulation & EVM replay (7.0)

The transaction state-change preview and the Solidity/EVM replay debugger now resolve the hardfork configuration at the transaction's own block through Amsterdam's `ChainForkResolver` (`DefaultChainForkResolver.Default`) instead of a fixed `HardforkConfig.Default`, so simulated and replayed execution runs the block's actual fork rules.

* `StateChangesPreviewService` (`Nethereum.Wallet`) gains a `ChainForkResolver` overload and resolves the config per block; the original constructor is preserved
* `EvmDebugService` (`Nethereum.Blazor.Solidity`) gains a `ChainForkResolver` overload and constructs its `EVMSimulator` with the resolved config; the original constructor is preserved

*Test status: no dedicated automated test in-repo exercises the fork-resolution path in these two services; this is a shipped code change without a traced passing test.*

## Native ETH transfer surfacing (7.0)

The Blazor transaction card and the send-transaction state-change preview now decode and display native ETH transfers found in a transaction's logs, using `EthTransferLogExtensions.DecodeEthTransfer` and rendering amounts via `Web3.Convert.FromWei`.

* `TransactionCard.razor` renders an "ETH Transfers" section with from → to and amount per decoded transfer
* `StateChangesPreview.razor` labels and renders decoded ETH transfers inline in the preview log list

*Test status: UI razor changes with no automated test in-repo.*

## Verified on-chain balances (`Nethereum.Wallet`)

`VerifiedBalanceService` now builds its light-client configuration from the shared `LightClientNetworks.CreateConfig` table (Consensus.LightClient) instead of duplicating per-chain genesis-validators-roots inline and fetching the beacon fork version separately.

*Test status: no dedicated automated test in-repo; refactor onto shared config.*

## Shared chain-node / sync & IStateReader migration (7.0)

The wallet and UI libraries were migrated, as consumers, onto the shared chain-node and sync abstractions and the `IStateReader` / explicit `HardforkConfig` model introduced elsewhere in 7.0, and the Avalonia component library was made to compile again (four missing usings) as part of the `Nethereum.slnx` restore.

* Consumer migration to shared chain-node/sync + `IStateReader` (Wallet)
* Avalonia component library compile fix + solution restore (`net` TFM adjustment)

## dApp connectors & interop (carried forward)

Established since 6.x; carried forward, with from-source-accurate READMEs in 7.0 and no material code change in-range. `Nethereum.Metamask` and `Nethereum.Metamask.Blazor` (MetaMask interop), `Nethereum.WalletConnect` (WalletConnect), `Nethereum.Reown.AppKit.Blazor` (Reown AppKit), `Nethereum.EIP6963WalletInterop` (EIP-6963 multi-wallet discovery), `Nethereum.MudBlazorComponents` (MudBlazor helpers), `Nethereum.UI` (the base `IEthereumHostProvider` / `SelectedEthereumHostProviderService` abstraction), `Nethereum.Blazor` (Blazor host-provider integration) and the Trezor packages (`Nethereum.Wallet.Trezor`, `.UI.Components.Trezor`, `.UI.Components.Blazor.Trezor`).

*Test status: no dedicated in-repo tests for these connector packages; README surface is documented from source.*

## Reference demo apps

Two runnable sample apps under `src/demos/` (source-only reference apps, not NuGet packages) show the wallet/UI stack:

- **`Nethereum.Wallet.Blazor.Demo`** — a Blazor WebAssembly + MudBlazor host that drops in the `NethereumWallet` component and exposes its configuration live: size presets (desktop/tablet/mobile), `DrawerBehavior` (responsive / always-show / always-hidden), responsive breakpoint and sidebar width, header display toggles, dark mode, brand-theme selection and language switching. Includes a `TokenTransferDemo` page alongside the embedded wallet.
- **`Nethereum.Wallet.Avalonia.Demo`** — a native Avalonia desktop host whose `MainWindow` sets its content to `NethereumWallet.Create(App.Services)`, driving the same MVVM view models through the Avalonia component head.
