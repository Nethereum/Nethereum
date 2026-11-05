# Indexing & Explorer — Nethereum 7.1

Nethereum 7.1 locks down the Explorer's ABI upload endpoints and gives `Nethereum.Explorer.Anchoring` a package README and a strong-name signature. The blockchain storage and processing packages change only in packaging.

## Nethereum.Explorer — ABI Upload Endpoints

- `POST /api/contracts/{address}/abi` and `POST /api/contracts/batch` reject every request with `401` when `ApiKey` is not set; before this, an unset key let anyone upload. The key is compared in constant time before the body is read.
- A body that is not JSON gets `415`, an empty body or invalid JSON gets `400`.
- The `api` rate limit (100 requests per minute) applies per client IP address instead of to all clients together. `RateLimiterOptions.ConfigureApiRateLimit()` sets it up for a host that registers its own rate limiter.

## Nethereum.Explorer.Anchoring — Package README

- The package now packs a README covering `AddAnchorExplorerServices`, `IAnchorExplorerService` and its default and null implementations, and `AnchorExplorerNavContributor`.

## Breaking Changes

- An Explorer without `ApiKey` no longer accepts ABI uploads. Set `ApiKey` to keep the upload endpoints working.

## Build & Packaging

- `Nethereum.Explorer.Anchoring` is strong-name signed with the shared key, like the other libraries.
- The blockchain storage, processing and token storage packages require `Newtonsoft.Json` 13.0.4 or later, and `Nethereum.DataServices` and `Nethereum.Sourcify.Database` pack their READMEs (see 00).
