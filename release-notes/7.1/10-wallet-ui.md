# Wallet & UI — Nethereum 7.1

Nethereum 7.1 changes how the wallet stores its vault: new vaults are AES-256-GCM with a per-vault salt, and older vaults are migrated when they are unlocked. Mnemonics are validated on create and import, and the wallet warns about 7.0 accounts whose stored phrase gives a non-standard seed. The dapp-facing request handlers and the WalletConnect client fix six bugs, the send screen validates amounts from the start, and the Blazor Server SIWE provider checks a signature on every request.

## Security audit

Key storage (six rounds), Sign-In with Ethereum and the dapp-facing request handlers were audited before 7.1; each finding was fixed with a test that fails before the fix.

* The AES vault strategies (the MAUI wallet's default) were AES-256-CBC with no MAC and one constant salt for every vault, so a vault file could be altered without detection and the same password derived the same key in every installation; vaults are now AES-256-GCM with a per-vault salt, 600,000 PBKDF2-SHA256 iterations and an authenticated header, and older vaults are migrated on unlock through a temporary file and an atomic replace
* The Blazor Server SIWE provider stored the challenge when it was issued and never checked a signature, so anyone could start a sign-in for any address, fail the signature and present the challenge text as the access token; the token now carries the signature and is verified on every request
* A request from another tab could change the selected dapp while a handler awaited, so a chain-add or account prompt was shown for, and granted to, the wrong origin

## Nethereum.Wallet — Vault Encryption

- `Aes256GcmEncryptionStrategy` is the new default (`WalletVaultServiceBase.GetEncryptionStrategy()`; it was `KeyStoreEncryptionStrategy`). It writes AES-256-GCM with a per-vault salt, PBKDF2-HMAC-SHA256 at `DefaultIterations` (600,000; decrypt accepts up to `MaxIterations`, 10,000,000) and an authenticated header. `Aes256GcmEncryptionStrategy.IsCurrentFormat(byte[])` tells the formats apart, and `Aes256GcmEncryptionStrategy(bool forceBouncyCastle)` selects the BouncyCastle implementation.
- A vault in the 7.0 AES-CBC or keystore format still unlocks. When the configured strategy can write the new format, the vault is rewritten in it on unlock (`MigrateToCurrentFormatAsync`). `FileWalletVaultService` writes every save to a temporary file and replaces the vault file in one move. If the decrypted accounts are inconsistent, it is left in its format and `VaultIntegrityCheckFailed` is `true`; later saves keep the old format.
- `DefaultAes256EncryptionStrategy` and `BouncyCastleAes256EncryptionStrategy` are obsolete. Both now write the `Aes256GcmEncryptionStrategy` format and still read their old AES-256-CBC format.
- `AddNethereumWalletMauiComponents` registers `Aes256GcmEncryptionStrategy` (it registered `BouncyCastleAes256EncryptionStrategy`).
- `WalletVault.IsConsistentAsync()` and `HasUnrecognisedContent` report whether the decrypted accounts are consistent, and `SetEncryptionStrategy(IEncryptionStrategy)` sets the strategy used on the next save.
- A failed unlock no longer writes `VaultUnlock.log` or the exception to the console.
- `PrivateKeyWalletAccount.Settings` is a `PrivateKeyAccountSettings` whose `ToString()` does not print the key.

## Nethereum.Wallet — Mnemonics

- `MnemonicWalletAccountFactory(bool validateMnemonic)` validates the phrase (English word list, length, checksum) and derives from its canonical form. Phrases already stored in a vault load as before, with the 7.0 seed.
- `MnemonicInfo.RequiresStandardSeedMigration` and `MnemonicWalletAccount.RequiresStandardSeedMigration` flag a stored phrase whose 7.0 seed differs from the standard BIP-39 seed. `WalletVault.MnemonicsWithLegacySeed`, `WalletVault.AccountsWithLegacySeed` and `WalletVaultServiceBase.HasAccountsWithLegacySeed` list them.
- The Blazor and Avalonia wallet components show a warning after unlock while such accounts exist (`WalletDashboardViewModel.ShowLegacySeedWarning`, `DismissLegacySeedWarning()`) and a hint on the mnemonic screens; the Avalonia account list marks them too.

## Nethereum.Wallet.RpcRequests and Nethereum.WalletConnect — Dapp Requests

- The dapp context is read once per request, before any await. Before this, a request from another tab could change the selected dapp while a handler waited, and a chain-add or account prompt named, and granted to, the wrong origin.
- `eth_signTypedData_v4` checks that the requested address is the selected account before prompting, as `personal_sign` does.
- `personal_sign` treats only `0x`-prefixed strings as hex, so text such as "cafe" is signed as text.
- `eth_requestAccounts` on a locked wallet asks to unlock instead of returning an empty list.
- `wallet_switchEthereumChain` answers `-32602` for a `chainId` that is not a `0x`-prefixed hex string, instead of leaving the call unanswered.
- The WalletConnect dapp client requests the chains the dapp asked for, not always `eip155:1`, and `SelectedChainId` follows the chains the session approved, so a dapp on another chain alone can connect.

## Nethereum.Wallet.UI.Components — Send and Sign Screens

- The send screen gets the native token's balance as soon as it loads, so an amount no longer fails with "insufficient balance" until the token is picked again. An amount typed before the balance arrives becomes valid when it does, and an account or chain change updates the balance the same way.
- The typed-data signing prompt shows the declared domain and message members, built from the same parse the signature hash uses. Undeclared keys are not signed, so they are no longer shown.

## Nethereum.Blazor and Nethereum.UI — SIWE Sign-In

- `SiweAuthenticationServerStateProvider` accepts a user only when the stored session exists, the message is within its time window, and the signature in the token is valid; it checks this on every request. Before this, the challenge text was stored when it was issued and was accepted as a token without a signature. An unreadable token is treated as signed out.
- `NethereumSiweAuthenticatorService` removes the pending session when the signature check fails and gains `AuthenticateWithSignatureAsync(SiweMessage)`, which returns the message and its signature.

## Breaking Changes

- A vault saved by 7.1 is in the AES-256-GCM format, which 7.0 cannot read. A 7.0 vault is converted the first time 7.1 unlocks it.
- A subclass of `WalletVaultServiceBase` that relied on the default `GetEncryptionStrategy()` now gets `Aes256GcmEncryptionStrategy` instead of `KeyStoreEncryptionStrategy`.
- SIWE tokens written by the 7.0 Blazor Server provider are no longer accepted, so users sign in again.
- `PrivateKeyWalletAccount.Settings` returns a `PrivateKeyAccountSettings` instead of an anonymous object; its `PrivateKey` property is unchanged.
- `eth_requestAccounts` on a locked wallet prompts to unlock instead of returning `[]`.

## Build & Packaging

- The wallet and UI packages require `Newtonsoft.Json` 13.0.4 or later. `Nethereum.Wallet.Trezor` and `Nethereum.Wallet.UI.Components.Avalonia` pack their READMEs, and `Nethereum.Blazor.Solidity` and `Nethereum.Reown.AppKit.Blazor` are strong-name signed (see 00).
