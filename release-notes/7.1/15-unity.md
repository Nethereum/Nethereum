# Unity — Nethereum 7.1

Nethereum 7.1 fixes wallet detection in Unity WebGL builds that use `Nethereum.Unity.EIP6963`. The Unity packages have no other changes in 7.1.

## Nethereum.Unity.EIP6963 — Wallet Availability Check

- `NethereumEIP6963.jslib` now defines `isAvailable` on `window.NethereumEIP6963Interop`. `EIP6963_IsAvailable` already called it, so in 7.0 `EIP6963WebglTaskRequestInterop.CheckAvailabilityAsync()` hit a JavaScript error instead of answering. It now returns `true` once at least one EIP-6963 wallet has announced itself.

## Build & Packaging

- The package tags of `Nethereum.Unity`, `Nethereum.Unity.EIP6963` and `Nethereum.Unity.Metamask` spell `Nethereum` correctly.
