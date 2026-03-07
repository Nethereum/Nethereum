# Nethereum 6.0.0 Documentation Plan

## Vision

Create comprehensive, modern documentation for Nethereum 6.0.0 that serves three audiences:

1. **Newcomers** — developers new to Ethereum who need concept explanations alongside code
2. **Experienced .NET devs** — know C# but not Ethereum, need practical getting-started guides
3. **Ethereum devs new to .NET** — know Ethereum but want to use .NET, need Nethereum-specific patterns

The documentation should teach blockchain concepts where needed (what is gas? what is ABI? how does a wallet work in a browser?), show Nethereum's approach, and link to the 99 project READMEs as living API documentation.

## Architecture

### Source of Truth Hierarchy

```
docs.nethereum.com (Docusaurus)     ← Guides, concepts, tutorials, architecture
    ↓ links to
COMPONENTS.md                        ← Full package catalog (130+ packages)
    ↓ links to
src/*/README.md (99 files)           ← Living API docs maintained with the code
    ↓ links to
Playground (playground.nethereum.com) ← Executable samples
```

**Key principle**: Never duplicate content from project READMEs. The docs explain _why_ and _when_, the READMEs explain _how_ and _what_. Each doc page ends with "Deep Dive" links pointing to the relevant project READMEs.

### Tooling

- **Docusaurus 3** (React-based, already chosen — repo is `Nethereum.Docusaurus`)
- **Site URL**: docs.nethereum.com
- **Versioning**: Start with 6.0.0, version future releases
- **Search**: Algolia DocSearch or built-in
- **Auto-sync**: CI job to pull project READMEs into a "Reference" section

## Content Structure

### Part 1: Getting Started

| Page | Status | Description |
|---|---|---|
| **Welcome / What is Nethereum** | NEW | Overview matching README intro, links to COMPONENTS.md |
| **Installation** | NEW | `dotnet add package`, templates pack, Unity setup, Aspire template |
| **Your First Project** | NEW | Step-by-step: create project, connect to a chain, read balance, send ETH |
| **Choosing How to Connect** | NEW | Public RPCs vs Infura/Alchemy vs local DevChain vs IPC — when to use what |

### Part 2: Ethereum Concepts for .NET Developers

Concept pages that explain the _what_ and _why_ before showing Nethereum code. Each concept links to the relevant how-to guide.

| Page | Status | Description |
|---|---|---|
| **Accounts & Keys** | NEW | Private keys, public keys, addresses, checksums. How Ethereum accounts differ from traditional auth. EOAs vs smart accounts. |
| **Transactions** | NEW | What a transaction contains (nonce, gas, to, value, data). Legacy vs EIP-1559 vs EIP-2930 vs EIP-4844 vs EIP-7702. When you need to sign vs send. |
| **Gas & Fees** | NEW | Gas units, gas price, max fee, priority fee, base fee. EIP-1559 explained. How to estimate, how to set limits. |
| **Smart Contracts** | NEW | What they are, ABI as the interface, bytecode vs source, deployment vs interaction. Function calls vs transactions vs events. |
| **ABI Encoding** | REWRITE | What ABI encoding is, types (uint256, address, bytes, tuples, arrays), how Nethereum maps them to C# types. Old doc exists. |
| **Events & Logs** | NEW | How events work on-chain, topics vs data, bloom filters, why indexing matters. |
| **Tokens** | NEW | ERC-20 (fungible), ERC-721 (NFT), ERC-1155 (multi-token). What they standardise and why typed services matter. |
| **Blocks & Chain** | NEW | Block structure, block number, finality, confirmations, reorganisations. |
| **JSON-RPC** | REWRITE | What JSON-RPC is, common methods (`eth_call`, `eth_sendRawTransaction`, etc.), how Nethereum wraps them. Old doc exists. |
| **Wallets & Browser Integration** | NEW | How browser wallets work (injected providers, EIP-6963 discovery), MetaMask, WalletConnect. How Blazor interop works. |
| **Sign-In with Ethereum (SIWE)** | NEW | EIP-4361 concept: why sign a message instead of a password, session management, nonce, domain binding. |
| **ENS (Ethereum Name Service)** | NEW | What ENS does, forward/reverse resolution, registration, how it maps to smart contracts. |
| **Account Abstraction** | NEW | The problem with EOAs, ERC-4337 UserOperations, EntryPoint, Bundlers, Paymasters. ERC-7579 modular accounts (validators, executors, hooks, session keys). |
| **MUD Framework** | NEW | What autonomous worlds are, the Store/World architecture, systems lifecycle (registration, discovery, access control), tables (schema, encoding, events), why on-chain state matters. |
| **Merkle Trees & State Proofs** | NEW | How Ethereum stores state (MPT), what proofs are, why light clients need them, storage proofs. |
| **Application Chains** | NEW | Why build an app chain, what domain-specific data means, data exit guarantees, relationship to L1/L2. |

### Part 3: How-To Guides

Practical, task-oriented guides. Each one is self-contained with complete code examples.

#### Basics
| Page | Status | Source |
|---|---|---|
| **Connect to Ethereum** | NEW | Web3 constructor, HTTP/WS/IPC, public nodes, Infura, DevChain |
| **Check Balances** | REWRITE | `GetBalance`, unit conversion (Wei/Gwei/ETH). Old doc exists. |
| **Transfer ETH** | REWRITE | `TransferEtherAndWaitForReceiptAsync`, gas estimation, receipts. Old doc exists. |
| **Unit Conversion** | REWRITE | `UnitConversion.Convert.FromWei()`, Gwei, custom decimals. Old doc exists. |
| **Work with Hex Types** | NEW | `HexBigInteger`, hex strings, conversions |

#### Accounts & Signing
| Page | Status | Source |
|---|---|---|
| **Account Types** | REWRITE | Account, ManagedAccount, ExternalAccount — when to use each. Old doc exists. |
| **Create & Load Accounts** | REWRITE | From private key, from keystore, from HD wallet. Old doc exists. |
| **HD Wallets** | REWRITE | BIP32/BIP39/BIP44, derive addresses, mnemonic management. Old doc exists. |
| **Keystores** | REWRITE | Create, encrypt, decrypt, Web3 Secret Storage. Old doc exists. |
| **Sign Messages** | REWRITE | Personal sign, EIP-191, verify signature, recover address. Old doc exists. |
| **Sign EIP-712 Typed Data** | NEW | Domain separator, type hashes, structured data signing. Links to `Nethereum.Signer.EIP712/README.md`. |
| **Hardware Wallets (Trezor/Ledger)** | NEW | Setup, signing, transaction types. Links to project READMEs. |
| **Cloud KMS (AWS/Azure)** | NEW | Configuration, signing, key management. Links to project READMEs. |

#### Smart Contracts
| Page | Status | Source |
|---|---|---|
| **Code Generation** | REWRITE | VS Code extension, CLI tool, MSBuild task. Generated files explained. Old doc exists. |
| **Deploy a Contract** | REWRITE | Typed deployment, constructor parameters, receipt, address. Old doc exists. |
| **Call Functions (Read)** | REWRITE | Query handlers, call messages, block parameter. Old doc exists. |
| **Send Transactions (Write)** | REWRITE | Transaction handlers, gas estimation, receipts. Old doc exists. |
| **Events & Log Processing** | REWRITE | Event DTOs, `GetAllChangesAsync`, filters, topics. Old doc exists. |
| **Estimating Gas** | REWRITE | `EstimateGasAsync`, custom limits, why estimates can be wrong. Old doc exists. |
| **Managing Nonces** | REWRITE | Nonce managers, pending vs confirmed, concurrent transactions. Old doc exists. |
| **Multicall Batching** | NEW | `MultiQueryBatchRpcHandler`, `CreateMulticallInputOutputRpcBatchItems`, batch reads. |
| **Untyped Contract Interaction** | REWRITE | Dynamic function calls without code generation. Old doc exists. |
| **Receipt Status & Error Handling** | REWRITE | Revert reasons, custom errors, decoding. Old doc exists. |

#### Tokens
| Page | Status | Source |
|---|---|---|
| **ERC-20 Tokens** | REWRITE | `ERC20ContractService` — balance, transfer, approve, allowance. Old doc exists. |
| **ERC-721 NFTs** | NEW | `ERC721ContractService` — mint, transfer, tokenURI, ownerOf. |
| **ERC-1155 Multi-Tokens** | NEW | `ERC1155ContractService` — batch operations, URI. |
| **Token Discovery & Pricing** | NEW | `Nethereum.TokenServices` — CoinGecko, token lists, multicall scanning. Links to README. |

#### DeFi & Protocols
| Page | Status | Source |
|---|---|---|
| **ENS Name Resolution** | NEW | Forward/reverse resolution, registration. Links to `Nethereum.Contracts` ENS services. |
| **Sign-In with Ethereum (SIWE)** | NEW | Full flow: create message, sign, verify, session. Links to `Nethereum.Siwe/README.md`. |
| **Uniswap Trading** | NEW | V2/V3/V4 swaps, quoting, Permit2 approvals. Links to `Nethereum.Uniswap/README.md`. |
| **x402 Payments** | NEW | Client setup, server middleware, facilitator. Links to `Nethereum.X402/README.md`. |
| **Gnosis Safe Multi-Sig** | NEW | Create safe, propose transaction, sign, execute. Links to `Nethereum.GnosisSafe/README.md`. |

#### Wallet & Browser Integration
| Page | Status | Source |
|---|---|---|
| **MetaMask in Blazor** | REWRITE | Setup, connect, sign, send. Old doc exists. Links to template. |
| **EIP-6963 Multi-Wallet Discovery** | NEW | How wallet discovery works, Blazor interop. Links to `Nethereum.Blazor/README.md`. |
| **WalletConnect / Reown** | NEW | Setup, pairing, session management. Links to `Nethereum.WalletConnect/README.md`. |
| **Dynamic Contract Interaction (No Codegen)** | NEW | DynamicQueryFunction, DynamicTransactionFunction in Blazor. Links to `Nethereum.Blazor/README.md`. |
| **Building a Wallet App** | NEW | MVVM architecture, ViewModels, platform renderers, state changes preview. Links to Wallet READMEs. |

#### Account Abstraction
| Page | Status | Source |
|---|---|---|
| **Create a UserOperation** | NEW | Build, sign, estimate gas, send. Links to `Nethereum.AccountAbstraction/README.md`. |
| **Run a Bundler** | NEW | Mempool, reputation, gas estimation, submission. Links to Bundler README. |
| **ERC-7579 Modular Accounts** | NEW | Deploy smart account, install modules, session keys. Links to README. |
| **Paymasters (Sponsored Gas)** | NEW | Verifying paymaster, gas sponsorship. |

#### MUD
| Page | Status | Source |
|---|---|---|
| **MUD Getting Started** | NEW | What MUD is, World/Store/Systems, how Nethereum integrates. |
| **Register and Call Systems** | NEW | System lifecycle, registration, access control, function calls. |
| **Work with Tables** | NEW | Schema, encoding, store events, CRUD operations. |
| **Index MUD Data to Postgres** | NEW | Processing pipeline, normalisation, hosted service. Links to `Nethereum.Mud.Repositories.Postgres/README.md`. |
| **Query MUD Tables** | NEW | `DynamicTablePredicateBuilder`, SQL queries, REST API. |
| **MUD UI in Blazor** | NEW | `MudBlazorComponents` — table viewer, deploy, interact. Links to README. |
| **Code Generation for MUD** | NEW | Generate table services from MUD World. |

#### Local Development
| Page | Status | Source |
|---|---|---|
| **DevChain Quick Start** | NEW | Start DevChain, pre-funded accounts, auto-mine. Links to `Nethereum.DevChain/README.md`. |
| **DevChain Server (HTTP)** | NEW | HTTP JSON-RPC server, connect MetaMask/Foundry/Hardhat. Links to `Nethereum.DevChain.Server/README.md`. |
| **EVM Simulator** | NEW | Run bytecode, trace calls, extract state changes, debug step-by-step. Links to `Nethereum.EVM/README.md`. |
| **Time Manipulation** | NEW | `evm_increaseTime`, `evm_setNextBlockTimestamp` for testing. |
| **Aspire Dev Environment** | NEW | `dotnet new nethereum-devchain`, full stack setup. Links to Aspire template README. |

#### Data & Indexing
| Page | Status | Source |
|---|---|---|
| **Blockchain Processing** | REWRITE | Block/transaction/log crawling, progress tracking, reorg detection. Old doc exists. Links to `Nethereum.BlockchainProcessing/README.md`. |
| **Index to PostgreSQL** | NEW | `AddPostgresBlockchainProcessor()`, EF Core setup, schema. |
| **Index to SQL Server** | NEW | `AddSqlServerBlockchainProcessor()` setup. |
| **Index to SQLite** | NEW | `AddSqliteBlockchainProcessor()` for lightweight scenarios. |
| **Token Transfer Indexing** | NEW | ERC-20/721/1155 transfers, balance aggregation. Links to Token.Postgres README. |
| **Build a Blockchain Explorer** | NEW | Explorer architecture, Blazor Server, ABI resolution. Links to `Nethereum.Explorer/README.md`. |
| **WebSocket Streaming** | REWRITE | `eth_subscribe`, new blocks, new pending transactions, logs. Old doc exists. |
| **Reactive Extensions (Rx.NET)** | NEW | Polling, streaming, composing observables. |

#### Advanced
| Page | Status | Source |
|---|---|---|
| **Application Chains** | NEW | Architecture, setup, sequencing, P2P, anchoring. Links to AppChain READMEs. |
| **Consensus Light Client** | NEW | Sync committee verification, state proofs, balance validation. Links to `Nethereum.Consensus.LightClient/README.md`. |
| **AOT / System.Text.Json** | NEW | `Nethereum.JsonRpc.SystemTextJsonRpcClient`, trimming, Native AOT. |
| **RPC Interceptors** | NEW | Custom interceptor pipeline, caching, logging, retrying. |
| **Custom RPC Providers** | NEW | Build a custom `IClient`, IPC, WebSocket, in-memory. |
| **Transaction Types Deep Dive** | NEW | Legacy, EIP-2930, EIP-1559, EIP-4844 (blobs), EIP-7702 (code delegation). Links to `Nethereum.Model/README.md`. |

### Part 4: Architecture Reference

| Page | Status | Source |
|---|---|---|
| **Component Catalog** | EXISTS | Link/embed COMPONENTS.md |
| **Project Structure** | NEW | Repository layout, build system, multi-targeting |
| **Package Dependency Graph** | NEW | Which packages depend on what, minimal installs |
| **Release Notes** | EXISTS | RELEASE_NOTES_6.0.0.md and future releases |

### Part 5: Unity

Dedicated section because Unity has its own ecosystem, repos, and patterns.

| Page | Status | Source |
|---|---|---|
| **Unity Introduction** | REWRITE | What's different about Unity + Ethereum, installation options. Old doc exists. |
| **Unity Package Setup** | NEW | Install via git URL, compiled libraries, framework targets. |
| **Coroutine-Based RPC** | REWRITE | UnityRpcRequest, yield return pattern. Old doc exists. |
| **ERC-20 in Unity** | REWRITE | Deploy, transfer, balance. Old doc exists. |
| **MetaMask in WebGL** | NEW | Browser wallet integration for Unity WebGL builds. |
| **EIP-6963 Wallet Discovery** | NEW | Multi-wallet support in Unity. Links to `Nethereum.Unity.EIP6963/README.md`. |
| **Cross-Platform Architecture** | NEW | Coroutines vs async, native vs WebGL, shared logic. |

### Part 6: Templates & Samples

| Page | Status | Source |
|---|---|---|
| **Templates Overview** | NEW | All `dotnet new` templates listed and explained |
| **Smart Contract Template** | NEW | Walkthrough of `smartcontract` template |
| **ERC721/1155 OpenZeppelin Template** | NEW | Walkthrough of `nethereum-erc721-oz` |
| **Blazor MetaMask Template** | NEW | Walkthrough of `nethereum-mm-blazor` |
| **SIWE Template** | NEW | Walkthrough of `nethereum-siwe` |
| **Aspire DevChain Template** | NEW | Walkthrough of `nethereum-devchain` |
| **Playground Samples** | NEW | Guide to using playground.nethereum.com |

## Content Inventory

### Existing Assets (99 project READMEs)

These are substantial living documentation — average 500-800 lines with code examples:

| README | Lines | Quality |
|---|---|---|
| `Nethereum.Contracts/README.md` | 1,538 | Comprehensive — covers all contract services, standards, multicall |
| `Nethereum.BlockchainProcessing/README.md` | 1,287 | Detailed — full processing pipeline with examples |
| `Nethereum.Wallet/README.md` | 962 | Good — MVVM architecture, services, state changes |
| `Nethereum.Blazor/README.md` | 899 | Good — EIP-6963, dynamic components, authentication |
| `Nethereum.Web3/README.md` | 857 | Good — getting started, all entry points |
| `Nethereum.Uniswap/README.md` | 827 | Good — V2/V3/V4, Permit2, code examples |
| `Nethereum.EVM/README.md` | 787 | Good — opcodes, tracing, debugging |
| `Nethereum.Mud/README.md` | 767 | Good — tables, queries, store events |
| `Nethereum.X402/README.md` | 762 | Good — client, server, facilitator |
| `Nethereum.Signer.EIP712/README.md` | 672 | Good — typed data signing |
| `Nethereum.AccountAbstraction/README.md` | 460 | Good — UserOps, gas estimation |
| + 88 more READMEs | 100-400 each | Varying quality |

### Old Docs (Rewrite Candidates)

Topics from the old docs worth rewriting for 6.0.0:

| Old Topic | Reuse? | Notes |
|---|---|---|
| Getting started | CONCEPTS ONLY | Code is outdated, structure is good |
| Transferring Ether | CONCEPTS ONLY | API has changed |
| Accounts | CONCEPTS ONLY | Missing ExternalAccount, HD wallet light |
| Smart contract interaction | CONCEPTS ONLY | Needs typed services focus |
| Events & logs | CONCEPTS ONLY | Missing new event processing patterns |
| Code generation | PARTIAL | VS Code flow is similar, CLI updated |
| Block processing | CONCEPTS ONLY | Architecture completely changed |
| HD Wallets | CONCEPTS ONLY | API similar but new features |
| Keystores | CONCEPTS ONLY | Same concepts, updated API |
| Signing messages | CONCEPTS ONLY | Same concepts |
| Unit conversion | CONCEPTS ONLY | Same API |
| Subscriptions/streaming | CONCEPTS ONLY | WebSocket client updated |
| Unity introduction | CONCEPTS ONLY | New package system, EIP-6963 |
| Blazor | CONCEPTS ONLY | Completely rewritten with EIP-6963 |

### Content That Doesn't Exist Yet (Priority NEW)

These are the most valuable new docs — topics not covered anywhere:

1. **Ethereum concepts for .NET developers** — the conceptual foundation
2. **Account Abstraction guide** — ERC-4337/7579 is new and complex
3. **MUD guides** — autonomous worlds are unique to Nethereum
4. **DevChain/CoreChain guides** — in-process Ethereum node is new
5. **Explorer guide** — how to build/customise the Blazor explorer
6. **x402 / Uniswap / DeFi** — new protocol integrations
7. **Data indexing pipeline** — new multi-provider EF Core architecture
8. **Aspire orchestration** — new DevOps story
9. **Application chains** — new architecture (Preview)
10. **Light client / state proofs** — new verification capabilities

## Implementation Plan

### Phase 1: Foundation (Weeks 1-2)

Set up the Docusaurus site and create the essential getting-started path.

- [ ] Set up Docusaurus 3 project in `Nethereum.Docusaurus`
- [ ] Configure theme, navigation, search
- [ ] Create landing page (adapted from README.md)
- [ ] **Getting Started section** (4 pages)
  - Welcome / What is Nethereum
  - Installation
  - Your First Project
  - Choosing How to Connect
- [ ] **Basics How-To** (4 pages)
  - Connect to Ethereum
  - Check Balances
  - Transfer ETH
  - Unit Conversion
- [ ] Import COMPONENTS.md as Architecture Reference
- [ ] Set up CI/CD for deployment to docs.nethereum.com

### Phase 2: Concepts (Weeks 3-4)

The conceptual foundation — this is what makes the docs unique and valuable.

- [ ] **Ethereum Concepts for .NET Developers** (16 pages)
  - Accounts & Keys
  - Transactions
  - Gas & Fees
  - Smart Contracts
  - ABI Encoding
  - Events & Logs
  - Tokens (ERC-20, 721, 1155)
  - Blocks & Chain
  - JSON-RPC
  - Wallets & Browser Integration
  - SIWE
  - ENS
  - Account Abstraction
  - MUD Framework
  - Merkle Trees & State Proofs
  - Application Chains

### Phase 3: Core How-To Guides (Weeks 5-8)

The practical meat of the documentation.

- [ ] **Accounts & Signing** (8 pages)
- [ ] **Smart Contracts** (10 pages)
- [ ] **Tokens** (4 pages)
- [ ] **Code Generation** (1 page — rewrite)

### Phase 4: Advanced How-To Guides (Weeks 9-12)

- [ ] **DeFi & Protocols** (5 pages)
- [ ] **Wallet & Browser Integration** (5 pages)
- [ ] **Account Abstraction** (4 pages)
- [ ] **MUD** (7 pages)
- [ ] **Local Development** (5 pages)
- [ ] **Data & Indexing** (8 pages)

### Phase 5: Ecosystem (Weeks 13-16)

- [ ] **Unity** (7 pages)
- [ ] **Templates & Samples** (7 pages)
- [ ] **Advanced** (6 pages)
- [ ] **Architecture Reference** (4 pages)

### Phase 6: Polish & Launch

- [ ] Review all cross-links
- [ ] Validate all code examples compile
- [ ] Set up auto-sync for project READMEs
- [ ] SEO optimisation
- [ ] Announce with 6.0.0 release

## Page Template

Every documentation page should follow this structure:

```markdown
---
title: Page Title
sidebar_label: Short Label
description: One-line description for SEO
---

# Page Title

Brief introduction — what this page covers and why it matters.

## Concept (if applicable)

Explain the Ethereum/blockchain concept in plain language.
Use diagrams where helpful.

## Nethereum Approach

How Nethereum implements this. Key classes, patterns, decisions.

## Step-by-Step

### Prerequisites
What you need installed/configured.

### Code Example
Complete, runnable code with explanations.

### Expected Output
What the user should see.

## Common Patterns

Variations, tips, gotchas.

## Deep Dive

- [Project README](link) — detailed API documentation
- [Playground Sample](link) — executable example
- [Related Guide](link) — next steps
```

## Metrics

Target for launch:
- ~100 documentation pages
- All 99 project READMEs linked
- All playground samples referenced
- Every "I want to..." row in COMPONENTS.md has a corresponding doc page
- Every template has a walkthrough

## Release Sync Pipeline

Project READMEs live in the main `Nethereum/Nethereum` repo. They are synced to the Docusaurus site **on release only**, so docs always match the published version.

### Trigger

A GitHub Actions job in the main Nethereum repo, triggered by:
- A new GitHub release (tag push like `v6.0.0`)
- Manual workflow dispatch (for hotfixes)

### Steps

```
1. Collect READMEs
   - Find all src/*/README.md files (99+)
   - Find COMPONENTS.md, RELEASE_NOTES_*.md

2. Generate frontmatter
   - For each README, auto-generate Docusaurus frontmatter:
     ---
     title: Nethereum.Web3
     sidebar_label: Web3
     description: High-level entry point for Ethereum interaction
     sidebar_position: <derived from COMPONENTS.md order>
     ---
   - Prepend frontmatter to the copied markdown

3. Copy to Docusaurus repo
   - Clone/checkout Nethereum.Docusaurus
   - Copy processed READMEs → docs/reference/<PackageName>.md
   - Copy COMPONENTS.md → docs/reference/component-catalog.md
   - Copy RELEASE_NOTES_6.0.0.md → docs/releases/6.0.0.md

4. Version snapshot (Docusaurus versioned docs)
   - Run: npx docusaurus docs:version 6.0.0
   - This snapshots the entire docs/ folder as version 6.0.0

5. Commit & deploy
   - Commit to Nethereum.Docusaurus main branch
   - GitHub Pages auto-deploys from the build output

6. CNAME
   - docs.nethereum.com points to GitHub Pages
   - (Currently points to readthedocs — switch when Docusaurus site is ready)
```

### Sync Script (Node.js)

A ~50-line script (`scripts/sync-readmes.js`) that:
- Reads all `src/*/README.md` files
- Extracts package name from the path
- Looks up description from COMPONENTS.md
- Generates frontmatter
- Writes to `docs/reference/<PackageName>.md`
- Generates a `_category_.json` for the reference sidebar

### Between Releases

- Guide pages link to GitHub (`master` branch) for bleeding-edge README content
- Each reference page footer: "This page was synced from [source](https://github.com/Nethereum/Nethereum/blob/master/src/Nethereum.Web3/README.md) at version 6.0.0"
- Users can always check the latest on GitHub

### GitHub Actions Workflow (Nethereum repo)

```yaml
name: Sync Docs on Release
on:
  release:
    types: [published]
  workflow_dispatch:

jobs:
  sync-docs:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Collect and process READMEs
        run: node scripts/sync-readmes.js

      - name: Clone Docusaurus repo
        uses: actions/checkout@v4
        with:
          repository: Nethereum/Nethereum.Docusaurus
          path: docusaurus
          token: ${{ secrets.DOCS_DEPLOY_TOKEN }}

      - name: Copy processed docs
        run: cp -r processed-docs/* docusaurus/docs/reference/

      - name: Version and deploy
        working-directory: docusaurus
        run: |
          npm ci
          npx docusaurus docs:version ${{ github.event.release.tag_name }}
          npm run build

      - name: Push to Docusaurus repo
        working-directory: docusaurus
        run: |
          git add -A
          git commit -m "Sync docs for ${{ github.event.release.tag_name }}"
          git push
```

## Notes

- Old docs at `Nethereum.Docusaurus/olddocs/` are reference only — too outdated to migrate, but concepts and structure can inform the rewrite
- Project READMEs are the living source of truth for API details — docs link to them, never duplicate
- Playground samples should be referenced as "try it live" from relevant doc pages
- The docs should be AI-friendly — clear structure, good metadata, complete code examples — so that AI models can help users effectively
- `docs.nethereum.com` CNAME currently points to readthedocs — switch to GitHub Pages when Docusaurus site is ready
