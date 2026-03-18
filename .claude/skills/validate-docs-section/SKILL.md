---
name: validate-docs-section
description: Validate and perfect a Nethereum documentation section end-to-end. Use when working on docs sections (getting-started, core-foundation, signing, smart-contracts, defi, evm-simulator, devchain, account-abstraction, data-indexing, mud-framework, wallet-ui, consensus, client-extensions). Covers use case definition, NuGet README verification against source code with compilation, guide page creation, Claude Code plugin skill creation per use case, sidebar updates, and build verification. Trigger when user mentions validating docs, fixing a docs section, creating guides, or perfecting documentation for any Nethereum section.
argument-hint: [section-name]
---

# Validate Documentation Section

You are perfecting the documentation for a section of the Nethereum Docusaurus site. This is a staged workflow with user approval gates — never skip a gate.

**Golden rule: ZERO HALLUCINATION. Every class name, method name, namespace, parameter, and code example must be verified against actual source code. Code examples must compile.**

## Paths

| What | Path |
|------|------|
| Nethereum source | `C:/Users/SuperDev/Documents/Repos/Nethereum/src/` |
| Package READMEs | `C:/Users/SuperDev/Documents/Repos/Nethereum/src/{Package}/README.md` |
| Docusaurus docs | `C:/Users/SuperDev/Documents/Repos/Nethereum.Documentation/docs/` |
| **Sync script** | `C:/Users/SuperDev/Documents/Repos/Nethereum.Documentation/scripts/sync-readmes.js` |
| Sidebar config | `C:/Users/SuperDev/Documents/Repos/Nethereum.Documentation/sidebars.ts` |
| **User skills plugin** | `C:/Users/SuperDev/Documents/Repos/Nethereum/plugins/nethereum-skills/skills/` |
| Internal dev skills | `C:/Users/SuperDev/Documents/Repos/Nethereum/.claude/skills/` |
| Tests & examples | `C:/Users/SuperDev/Documents/Repos/Nethereum/tests/` |
| **Doc example attribute** | `src/Nethereum.Documentation/NethereumDocExampleAttribute.cs` |
| Playground | `http://playground.nethereum.com` |
| Progress tracking | `C:/Users/SuperDev/Documents/Repos/Nethereum.Documentation/docs/{section}/PROGRESS.md` |

---

## CRITICAL: Sidebar Structure Standard

Every section in `sidebars.ts` MUST follow this consistent structure:

```typescript
{
  type: 'category',
  label: 'Section Name',
  items: [
    'section/overview',                    // 1. Overview page (always first)
    {
      type: 'category',
      label: 'Guide Sub-Group Name',       // 2. Guide categories (grouped by learning progression)
      collapsed: false,                     //    First group expanded, rest collapsed
      items: [
        'section/guide-topic-a',           //    Ordered for learner journey, NOT alphabetically
        'section/guide-topic-b',
      ],
    },
    // ... more guide sub-groups as needed
    {
      type: 'category',
      label: 'Package Reference',          // 3. Package Reference (ALWAYS last, ALWAYS this label)
      items: [
        'section/nethereum-package-a',     //    Auto-generated from READMEs via sync-readmes.js
        'section/nethereum-package-b',
      ],
    },
  ],
}
```

### Rules

1. **Overview always first** — `{section}/overview` is the entry point
2. **Guides grouped by learning progression** — NOT one flat list. Group into sub-categories that reflect a learner's journey:
   - **Essentials / Getting Started** — the first things a learner needs (collapsed: false)
   - **Deep Dives / Advanced** — topics they explore after mastering essentials
   - **Specialized** — encoding, transport, infrastructure topics
3. **Guide ordering within groups follows the learning path**: What does a learner need first? What builds on what? Example: Query Balance → Unit Conversion → Fee Estimation → Send ETH (you need to understand balances before sending, units before fees, fees before sending)
4. **Package Reference always last** — all `nethereum-*.md` pages go here. These are auto-generated from READMEs and serve as API reference, not learning material
5. **`code-generation` and similar reference-style pages** go in the Guides group closest to their topic, not at the top level
6. **Sub-groups within Package Reference** are OK for large sections (e.g., JSON-RPC Transport, Networking, Storage providers)

### Guide Sub-Group Examples (by section)

**Core Foundation:**
- Essentials: Query Balance, Unit Conversion, Fee Estimation, Send Ether, Send Transactions, Query Blocks
- Transaction Deep Dives: Transaction Types, Hash, Recovery, Replacement, Pending, Decode
- Keys, Signing & Encoding: Keys & Accounts, Message Signing, ABI, Hex, Address Utils, RLP
- Transport & Streaming: RPC Transport, Real-Time Streaming

**Signing & Key Management:**
- Guides: EIP-712 Signing, HD Wallets, Keystore, Hardware Wallets, Cloud KMS

**Smart Contracts:**
- Guides: Smart Contract Interaction, Deploy a Contract, ERC-20 Tokens, Code Generation, Events, Multicall, Error Handling, Built-in Standards, CREATE2

**DevChain:**
- Guides: DevChain Quickstart

### Overview Must Link to Guides

Every `overview.md` MUST include a guide table at the bottom listing all guides in the section, grouped by sub-category. This is how learners discover guides from the overview page. Format:

```markdown
## Guides

### Sub-Group Name

| Guide | What You'll Learn |
|---|---|
| [Guide Title](guide-slug) | One-line description |
```

---

## CRITICAL: Simple Path First Structure

**Every guide MUST lead with the simplest `web3.Eth.*` approach before showing advanced options.** The design philosophy is: `web3.Eth.*` is a complete simple path for every common task. A developer should read the first 30 seconds of any guide, copy the simple code, and have it work — fees, nonce, gas all handled automatically. Deeper APIs are there when needed but never required.

### The `:::tip The Simple Way` Pattern

Every guide that involves a `web3.Eth` operation MUST open with a tip callout showing 2-5 lines of the simplest working code:

```markdown
:::tip The Simple Way
\`\`\`csharp
var receipt = await web3.Eth.GetEtherTransferService()
    .TransferEtherAndWaitForReceiptAsync("0xRecipient", 1.11m);
\`\`\`
That's it. Fees, gas, nonce — all automatic.
:::
```

**Rules for the tip:**
- Maximum 5 lines of code (excluding `var web3 = ...` setup)
- Must be a complete, working call — not a fragment
- Must explicitly state what's automatic ("Fees, gas, nonce — all automatic")
- For read-only guides, state: "No gas, signing, or fees needed — these are read-only calls."

### Section Labeling for Advanced Content

Advanced or optional sections MUST be clearly labeled so beginners know they can skip them:

- **"## More Control: Explicit Fee Parameters"** — not just "## EIP-1559 Fee Parameters"
- **"## Advanced: Transfer Entire Balance"** — not just "## Transfer Entire Balance"
- **"## Advanced: Find All Owned NFTs via Transfer Logs"** — not just "### Find All Tokens Owned"
- Add intro text: "The sections below are optional — use them only when you need to override the automatic behavior."

### The `web3.Eth` Simple Path Map

The overview page for any section that uses `web3.Eth` MUST include a simple path table. The Core Foundation reference table:

| Task | Simple Path |
|------|-------------|
| Get ETH balance | `web3.Eth.GetBalance.SendRequestAsync(address)` |
| Get ERC-20 balance | `web3.Eth.ERC20.GetContractService(addr).BalanceOfQueryAsync(owner)` |
| Get ERC-721 balance | `web3.Eth.ERC721.GetContractService(addr).BalanceOfQueryAsync(owner)` |
| Send ETH | `web3.Eth.GetEtherTransferService().TransferEtherAndWaitForReceiptAsync(to, amount)` |
| Send transaction | `web3.Eth.TransactionManager.SendTransactionAndWaitForReceiptAsync(input)` |
| Get block | `web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(num)` |
| Get transaction | `web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(hash)` |
| Get receipt | `web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(hash)` |
| Convert units | `Web3.Convert.FromWei(value)` / `Web3.Convert.ToWei(value)` |
| Resolve ENS | `web3.Eth.GetEnsService().ResolveAddressAsync("vitalik.eth")` |
| Multicall batch | `web3.Eth.GetMultiQueryHandler()` |
| Delegate EOA (EIP-7702) | `web3.Eth.GetEIP7022AuthorisationService().AuthoriseRequestAndWaitForReceiptAsync(contract)` |
| Check if smart account | `web3.Eth.GetEIP7022AuthorisationService().IsDelegatedAccountAsync(address)` |
| Get delegate contract | `web3.Eth.GetEIP7022AuthorisationService().GetDelegatedAccountAddressAsync(address)` |
| Revoke delegation | `web3.Eth.GetEIP7022AuthorisationService().RemoveAuthorisationRequestAndWaitForReceiptAsync()` |

**Key message after the table:** "For every row above, Nethereum handles gas estimation, nonce management, EIP-1559 fee calculation, and transaction signing automatically. You only override when you need to."

### Built-in Services to Surface

The overview must mention these built-in typed services — no ABI needed:
- **`web3.Eth.ERC20`** — balances, transfers, allowances, metadata
- **`web3.Eth.ERC721`** — NFT ownership, metadata, enumeration
- **`web3.Eth.ERC1155`** — multi-token balances and batch operations
- **`web3.Eth.GetEIP7022AuthorisationService()`** — EIP-7702 delegation lifecycle (delegate, check, get delegate, revoke)
- **`EIP7022SponsorAuthorisationService`** — sponsored delegation (another account pays gas)

### Fee Estimation Framing

The fee estimation guide MUST open with: "Fees are automatic. You probably don't need this guide." The structure should be:
1. "The Default: You Probably Don't Need This Guide" — show zero-config transfer
2. "When You Need More Control" — scenarios that justify reading further
3. Strategy comparison table FIRST (so they can pick), then details for each
4. Legacy mode last

### Read-Only Query Callouts

Any guide that covers read-only operations (balance queries, block queries, transaction lookups) MUST note: "These are all read-only queries — no gas, no signing, no fees needed."

### EIP-7702 Service Coverage

EIP-7702 is a first-class Nethereum feature with dedicated high-level services. The EIP-7702 guide and any overview referencing it MUST surface the FULL lifecycle:
- **Delegate** — `AuthoriseRequestAndWaitForReceiptAsync(contract)`
- **Check if smart account** — `IsDelegatedAccountAsync(address)`
- **Get delegate contract** — `GetDelegatedAccountAddressAsync(address)`
- **Revoke delegation** — `RemoveAuthorisationRequestAndWaitForReceiptAsync()`
- **Sponsored delegation** — `EIP7022SponsorAuthorisationService` (sponsor pays gas)
- **Batch sponsorship** — `AuthoriseBatchSponsoredRequestAndWaitForReceiptAsync(keys, contract)`
- **Inline authorization** — attach `AuthorisationList` to any `FunctionMessage` to delegate + execute in one transaction
- **Gas calculation** — `AuthorisationGasCalculator.CalculateGasForAuthorisationDelegation()` (automatic in transaction manager)
- **Hardware wallet/KMS support** — all external signers support Type 4 via `IEthExternalSigner.SignAsync()`

### Guide Table Completeness

The overview guide tables MUST list EVERY guide in the section. During Core Foundation validation, the EIP-7702 guide (sidebar_position 13) was missing from the Transaction Deep Dives table — this is the exact kind of gap to catch. After creating or updating any guide, verify it appears in the overview tables.

---

## CRITICAL: Guide Quality Standard

**A guide is NOT a code dump with headers.** Every guide must teach, not just show.

### What makes a guide vs a code dump

**Code dump** (BAD):
```
## Encode a String
\`\`\`csharp
var encoded = RlpEncoder.EncodeElement(dogBytes);
\`\`\`
## Encode an Integer
\`\`\`csharp
var encoded = RlpEncoder.EncodeElement(valueBytes);
\`\`\`
```

**Guide** (GOOD):
```
## Why RLP?

RLP is how Ethereum serializes data for the wire — transactions, blocks, and
state trie nodes are all RLP-encoded before hashing or transmitting. You'll
encounter RLP when building raw transactions, verifying Merkle proofs, or
working with the state trie directly.

Most developers never call RLP directly — `Web3` handles it for you when
sending transactions. Use these APIs when you need to:
- Build raw signed transactions offline
- Verify block header proofs
- Decode data returned by `debug_traceTransaction`

## Encode Structured Data

RLP handles two things: byte arrays and lists of byte arrays. Everything
in Ethereum gets reduced to one of these.

\`\`\`csharp
// Encode a string — first convert to bytes, then RLP-wrap
string dog = "dog";
byte[] encoded = RlpEncoder.EncodeElement(dog.ToBytesForRLPEncoding());
\`\`\`

The encoded output is `0x83646f67` — the prefix `0x83` means "byte string
of length 3", followed by the UTF-8 bytes of "dog".
```

### The Guide Quality Checklist

Every guide MUST have these elements. Score each guide against this list:

1. **Opening context** (2-3 sentences): What problem does this solve? When would a developer reach for this?
2. **Prerequisites**: What do you need before starting? (packages, accounts, running node, etc.)
3. **Mental model**: How does this concept work at a high level? Not implementation details — the "why" and "how it fits" into Ethereum/Nethereum.
4. **Progressive examples**: Start simple, build complexity. Each example builds on the previous one.
5. **Guiding text between every code block (CRITICAL — no code dumps)**: Every code block MUST have at least 1-2 sentences BEFORE it explaining what we're about to do and why, and at least 1 sentence AFTER explaining the result, what to watch for, or how this connects to the next step. A guide that is just `## Header → code block → ## Header → code block` is a code dump, not a guide. The text must teach — explain concepts, warn about gotchas, connect to the reader's mental model. All explanatory text must be factual and verifiable against actual Nethereum source code — never hallucinate API behavior, parameter names, or default values.
6. **Real-world scenarios**: Use realistic values and contexts, not just "hello world". Show addresses, token amounts, contract names that feel like real usage.
7. **Decision guidance**: When there are multiple approaches, explain when to use which. Tables are great for this.
8. **Common mistakes/gotchas**: What trips people up? What error will they see if they forget X?
9. **What to do next**: Connect this guide to related guides with context ("Now that you can sign transactions, you'll want to [estimate gas fees](./guide-fee-estimation) to avoid overpaying.")
10. **No orphan code**: Every code block must be reachable from a real scenario. If code can't be motivated by a user story, it doesn't belong in a guide (put it in the README/API reference instead).

### Guides Must Reflect a Learning Journey

Guides are NOT independent articles — they form a connected path through the section. A learner reads them in order and each guide builds on the previous one.

**The Core Foundation section is the validated reference template.** When validating any section, read the Core Foundation guides first to see the standard in action. Then apply the same patterns.

**Journey requirements:**

1. **Next Steps must follow the learning sequence** — the first link in "Next Steps" should be the NEXT guide in the sidebar order. Additional links can point to related topics, but the primary link guides the learner forward through the progression.

2. **Prose must be helpful and factual, never hallucinated** — every explanation must come from:
   - Verified source code behavior (e.g., "EIP-1559 is the default since version 4.3.1" — checked in `TransactionManagerBase.cs`)
   - Playground sample comments (verified working text)
   - Ethereum specification facts (e.g., "1 Gwei = 10^9 Wei")
   - Observable API behavior (e.g., "returns null if the transaction hasn't been mined yet")

   NEVER write commentary that sounds authoritative but isn't verifiable. If you're unsure about behavior, check the source code first.

3. **Context flows between guides** — early guides can mention concepts explored in later guides ("We'll cover fee estimation in detail in the [Fee Estimation guide](guide-fee-estimation) — for now, Nethereum handles it automatically"). Later guides can reference earlier ones ("As we saw in [Query Balance](guide-query-balance), balances are returned in Wei").

4. **sidebar_position values must match the learning order** within each sub-group. Essentials: 1-6, Deep Dives: 7-12, Keys/Encoding: 13-18, Transport: 19-20 (for Core Foundation as example).

5. **No standalone guides** — every guide must have at least 2 links in Next Steps connecting it to other guides in the section. At least one link should point forward in the sequence.

### Guide Opening Pattern (CRITICAL)

Every guide MUST open with 2-3 sentences that answer WHY and WHEN before any code appears. The opening establishes context for the learner and connects to what they already know from previous guides.

**BAD opening** (jumps straight to code):
```markdown
# Query Blocks and Transactions

## Connect to Ethereum

\`\`\`csharp
var web3 = new Web3("https://mainnet.infura.io/v3/YOUR-PROJECT-ID");
\`\`\`
```

**GOOD opening** (establishes WHY and connects to journey):
```markdown
# Query Blocks and Transactions

After sending transactions (as covered in [Transfer Ether](guide-send-eth) and
[Send Transactions](guide-send-transaction)), you'll want to inspect what happened
on-chain. This guide covers querying blocks, looking up transactions by hash,
reading receipts to check success/failure, and detecting whether an address is
a contract or a regular account.
```

**Pattern examples from the validated Core Foundation guides:**

| Guide | Opening Pattern |
|-------|----------------|
| Query Balance | `:::tip The Simple Way` with 3-line ETH/ERC-20/ERC-721 patterns + "The most common first step in any Ethereum application..." |
| Unit Conversion | Already focused on the simple path (Web3.Convert) — no changes needed |
| Fee Estimation | "Fees are automatic. When you send a transaction with `web3.Eth`, Nethereum estimates EIP-1559 fees for you." Then "The Default: You Probably Don't Need This Guide" section |
| Send ETH | `:::tip The Simple Way` with 2-line transfer + "Sending ETH from one address to another is the most fundamental write operation..." Advanced sections under "## More Control" |
| Send Transactions | "The `TransactionManager` handles gas estimation, nonce management, and EIP-1559 fees automatically — you just provide the recipient, data, and optionally a value." |
| Query Blocks | "All queries in this guide are **read-only** — no gas, signing, or fees needed." |
| EIP-7702 | `:::tip The Simple Way` with delegate + check + get delegate + revoke (4 operations) + "For sponsored delegation, use `EIP7022SponsorAuthorisationService`" |
| Transaction Types | "Most of the time, Nethereum picks the right type automatically — you only need this guide when constructing raw transactions..." |
| Transaction Hash | "Every signed transaction has a deterministic hash — you can calculate it before broadcasting..." |
| Decode Transactions | "When you retrieve a transaction from the blockchain (as in [Query Blocks](guide-query-blocks)), its `Input` field contains the raw ABI-encoded function call..." |

**The formula:**
1. State what problem this solves (1 sentence)
2. Connect to what the learner already knows from previous guides (1 sentence with link)
3. Preview what this guide covers (1 sentence listing the specific topics)

### Anti-Patterns to Avoid

These problems were identified during Core Foundation validation and must be checked in every section:

1. **Assert-style code in guides** — test code like `Assert.Equal(expected, actual)` or undefined variables from test fixtures does NOT belong in guide examples. Guides show realistic application code, not test assertions. If the source is a test, adapt it to show `Console.WriteLine` or meaningful variable usage.

2. **Repeated boilerplate without cross-reference** — if 5 guides all start with `var web3 = new Web3(url)`, the later guides should say "Connect to Ethereum as shown in [Getting Started](../getting-started/first-project)" or simply show the line with a brief note, not a full "## Connect to Ethereum" section every time.

3. **Reference disguised as a guide** — a page that lists every transaction type with its fields but doesn't teach when to use each one is a reference page, not a guide. Guides answer "which one should I pick?" with decision tables and scenarios.

4. **Dead-end guides** — guides that end abruptly without Next Steps or that only link to unrelated sections. Every guide must connect back into its section's learning path.

5. **Orphan concepts** — mentioning a concept (like "EIP-1559 fees") without either explaining it or linking to the guide that does. The learner should never hit an unexplained term.

6. **Code dumps** — a section that is just `## Header` followed immediately by a code block, repeated for every feature, with no explanatory text between them. Every code block MUST have at least 1-2 sentences BEFORE it explaining what we're about to do and why, and optionally a sentence AFTER explaining the output, what to watch for, or what the code connects to. The text must guide the reader through the code, not just label it. All explanatory text must reference actual Nethereum API behavior verified against source code — never hallucinate behavior or parameter names.

7. **Entirely hallucinated READMEs** — a README that documents a completely fictional API surface. During DeFi validation, the `Nethereum.X402` README was 100% hallucinated: `X402Service`, `X402Client`, `X402PaymentHeader`, `X402PaymentProposal`, `X402PaymentRequired` attribute, `IPaymentValidator`, `FacilitatorDiscoveryClient`, `IPaymentEventHandler`, `AddX402()` — NONE existed in source code. The actual API (`X402HttpClient`, `X402Middleware`, `RoutePaymentConfig`, `X402TransferWithAuthorisation3009Service`) was completely different. **Stage 2 MUST verify every class name against source, not spot-check.** When a README smells wrong (aspirational API design, no matching tests), treat the entire file as suspect and rewrite from integration tests.

8. **Wrong generic type parameters** — `MultiSendInput` vs `MultiSendFunctionInput<TFunctionMessage>` is not a minor typo — it's a completely different API pattern. During DeFi validation, the GnosisSafe README used a non-existent `MultiSendInput` class and `MultiSendOperationType` enum (actual: `ContractOperationType`), and property `To` (actual: `Target`). **Always verify generic type signatures, not just class names.** Search for `class ClassName` AND `class ClassName<` to catch generic variants.

9. **Wrong method return types** — the Uniswap README showed `CalculatePricesFromSqrtPriceX96` (plural) returning a tuple with `.Item1`/`.Item2`. The actual method is `CalculatePriceFromSqrtPriceX96` (singular) returning a single `decimal`. **Verify method signatures including return types, not just names.** A method that returns `decimal` is fundamentally different from one returning `(decimal, decimal)`.

10. **Overview simple path tables propagating hallucinations** — when a README is hallucinated, the overview page's simple path table, guide tip callouts, and skill examples all copy the same wrong API. **After fixing any README, immediately check and fix all downstream artifacts**: overview table, guide pages, plugin skills. Search for the old (wrong) class/method names across all doc files to catch every instance.

### Guide vs README distinction

- **README** = API reference. Lists all public methods, parameters, return types. Complete but not pedagogical. Lives in `src/{Package}/README.md`.
- **Guide** = Task-oriented tutorial. Teaches how to accomplish a specific goal. Explains concepts. Lives in `docs/{section}/guide-*.md`.
- A guide should LINK to the README for "full API reference", not duplicate it.
- A guide covers ~20% of the API but explains WHY and WHEN for 100% of what it shows.

### Skills must also teach

Plugin skills (`plugins/nethereum-skills/skills/`) serve AI models as well as users. A skill must contain enough context that an AI model understands:
- What problem this solves
- When to use this approach vs alternatives
- What packages are needed and why
- Complete, working code patterns (verified against tests)

---

## Test-Driven Documentation

**Every code example in guides, skills, and READMEs must be backed by a passing test tagged with `[NethereumDocExample]`.**

### The `[NethereumDocExample]` Attribute

Located in `src/Nethereum.Documentation/NethereumDocExampleAttribute.cs` (namespace `Nethereum.Documentation`). Uses a `DocSection` enum to ensure section names match exactly:

```csharp
[Fact]
[NethereumDocExample(DocSection.CoreFoundation, "send-eth", "Transfer ETH with EIP-1559 fees", Order = 2)]
public async void ShouldTransferEtherEIP1559() { ... }
```

**Parameters:**
- `DocSection section` — enum: `CoreFoundation`, `Signing`, `SmartContracts`, `DeFi`, `EvmSimulator`, `InProcessNode`, `AccountAbstraction`, `DataIndexing`, `MudFramework`, `WalletUI`, `Consensus`, `ClientExtensions`
- `string useCase` — slug matching the guide/skill name (e.g., `"send-eth"`, `"fee-estimation"`, `"erc20-tokens"`)
- `string title` — human-readable title for the example
- `string SkillName` — optional, defaults to useCase (since guide/skill/test use cases should align)
- `int Order` — ordering within a use case (when multiple tests per use case)

### Workflow

When creating doc examples:
1. **Search for existing tests** matching the use case — tag them with `[NethereumDocExample]`
2. **If no test exists**, create one in the appropriate test project and tag it
3. **Guide pages and skills extract code from tagged tests** — never invent examples
4. **The attribute is extractable via reflection** — tools can discover all doc examples by scanning for `[NethereumDocExample]` across test assemblies

### Commit Integration

The `/commit` skill (`.claude/commands/commit.md`) enforces documentation propagation:
- When tagged tests change → README, guide, and skill updates are checked
- When new public API is added without a tagged test → flagged for follow-up
- This creates a closed loop: code change → test update → docs update → commit

### Adding to test projects

The attribute lives in the standalone `Nethereum.Documentation` project (netstandard2.0, zero dependencies). To use it:

**For xUnit test projects** (already referencing `Nethereum.XUnitEthereumClients`):
1. The attribute is available transitively — `XUnitEthereumClients` references `Nethereum.Documentation`
2. Add `using Nethereum.Documentation;` to the test file

**For console test projects or any other project**:
1. Add `<ProjectReference Include="..\..\src\Nethereum.Documentation\Nethereum.Documentation.csproj" />` to the `.csproj`
2. Add `using Nethereum.Documentation;` to the source file
3. Works with any target framework (netstandard2.0 compatible)

## Plugin Architecture

User-facing skills are distributed as a **Claude Code Plugin** at `plugins/nethereum-skills/`. This is a single installable plugin that bundles all Nethereum user skills together. Users install it once with `/plugin install nethereum-skills` and all skills become auto-discoverable.

```
plugins/nethereum-skills/
├── .claude-plugin/
│   └── plugin.json              ← manifest (name, version, description, keywords)
└── skills/
    ├── send-eth/
    │   └── SKILL.md
    ├── erc20/
    │   └── SKILL.md
    ├── events/
    │   └── SKILL.md
    └── ...                      ← one skill per use case (or grouped tightly related use cases)
```

After installation, skills are:
- **Auto-triggered** by Claude based on context (user asks about ERC-20 → `erc20` skill activates)
- **Directly invocable** via `/nethereum-skills:send-eth`, `/nethereum-skills:erc20`, etc.
- **Listed** in the user's available skills

Internal development skills (like this one) stay in `.claude/skills/` — they are NOT part of the plugin.

## Resumability

This workflow can span multiple sessions. At the start of each invocation:

1. Check if `PROGRESS.md` exists for this section
2. If yes, read it and resume from where you left off
3. If no, start from Stage 1

After completing each stage, update `PROGRESS.md` with:
- Which stage was completed
- Key decisions made (approved use cases, identified issues, etc.)
- What comes next

---

## Stage 1: Define Use Cases

**Goal**: Identify every real-world task a developer would want to accomplish with this section's packages.

### Process

1. **Read the existing docs** for this section in the Docusaurus site
2. **Read the package READMEs** for every package in this section
3. **Read the actual source code** — don't trust READMEs alone. Grep for public classes, check what APIs exist that aren't documented. The source is truth.
4. **Search for playground examples** — check if playground.nethereum.com has samples that map to this section. Search test projects in the repo for integration tests that demonstrate usage.
5. **Search the old docs** — check if the old MkDocs documentation (https://docs.nethereum.com/en/latest/) has guides for these use cases and note what content existed there
6. **Check existing plugin skills** — read `plugins/nethereum-skills/skills/` to see what already exists. Don't duplicate.
7. **Think as a user** — what would someone Google? "how to send ETH C#", "decode EIP-712 typed data .NET", "abi.encodePacked equivalent C#". Each search query is a potential use case.
8. **Define use cases** as a table:

| # | Use Case | Guide Page | Plugin Skill | NuGet Packages | Playground Link |
|---|----------|-----------|-------------|----------------|-----------------|

Each use case should be:
- A concrete task a developer wants to do ("Send ETH to an address", not "Learn about transactions")
- Sized appropriately — small focused tasks get their own row, large topics can be one row
- Mapped to exactly one guide page in the docs
- Mapped to a plugin skill (one skill per use case, or one skill covering a few tightly related use cases — use judgment)
- Linked to playground examples where they exist

Also note any **external references** the guide pages should link to:
- [chainlist.org](https://chainlist.org/) for finding public RPC endpoints
- Provider sign-up pages (Infura, Alchemy, Chainlink, etc.)
- Related tools (Foundry/Anvil, Hardhat, Remix, etc.)
- Ethereum documentation (ethereum.org) for concept explanations

### Gate 1: Present the use case table to the user. Wait for approval before proceeding.

---

## Stage 2: Validate NuGet Package READMEs

**Goal**: Every README referenced by the use cases must be 100% accurate. Every code example must compile.

### Process

For each NuGet package referenced in the use case table:

1. **Find the README**: `src/{PackageName}/README.md`
2. **Find the .csproj**: Verify the package name matches the actual project file
3. **Find the test projects**: Search `tests/` and `consoletests/` for test files covering this package. These are the most reliable source of verified, working code. Map them:

   | Test File | Type | What It Tests | Network? |
   |-----------|------|---------------|----------|
   | tests/Nethereum.XXX.UnitTests/SomeTest.cs | Unit | Feature X | No |
   | tests/Nethereum.XXX.IntegrationTests/OtherTest.cs | Integration | Feature Y | Yes |

   Common test project locations:
   - `tests/Nethereum.{Package}.UnitTests/` — unit tests (no network)
   - `tests/Nethereum.{Package}.IntegrationTests/` — integration tests (need devchain)
   - `tests/Nethereum.Contracts.IntegrationTests/` — many packages tested here (ERC20, Multicall, ErrorReason, etc.)
   - `tests/Nethereum.Signer.UnitTests/` — transaction signing, EIP-712, EIP-155
   - `consoletests/` — demo/console test programs

4. **Cross-reference every code example** in the README against source code:

   For each code snippet in the README:
   - **Classes**: `Grep` for `class ClassName` AND `class ClassName<` — does it exist? Correct namespace? Is it generic?
   - **Methods**: `Grep` for the method signature — correct parameters? **Correct return type?** A method returning `decimal` is NOT the same as one returning a tuple.
   - **Properties**: Verify they exist on the class — check the actual property name (e.g., `Target` vs `To`)
   - **Constructors**: Verify overload exists with shown parameters
   - **Namespaces/usings**: Verify they're correct
   - **Extension methods**: Verify the static class and method exist
   - **Enums**: Verify enum type names AND member names (e.g., `ContractOperationType` vs hallucinated `MultiSendOperationType`)

   **CRITICAL: If more than 2 classes in a README cannot be found in source, the entire README is likely hallucinated.** Stop spot-checking and instead:
   1. Map the ACTUAL public API surface from source code (`Grep` for all `public class/interface/enum`)
   2. Read integration tests to understand how the API is actually used
   3. Rewrite the README from scratch using tests as the source of truth

5. **Compile-check**: For each code example, verify it compiles. Prefer verifying against existing test code rather than creating new throwaway projects — if a test already exercises the same API, that's sufficient proof the example works. Only create a minimal `.csx` or console project for examples that have no corresponding test. If the example needs a running node, verify it compiles but note "requires running node".

6. **Identify missing test coverage**: For each package feature, check if a test exists. If a feature is documented but has no test, flag it. If a feature has a test but is undocumented, that's a missing-documentation gap. The test file is the best source for writing the documentation example.

7. **Scan for missing functionality** (critical — run every time):

   For each package, systematically compare what's in the source vs what's in the README:

   a. **List all public classes** in the package source directory (`src/{PackageName}/`). Use `Grep` for `public class`, `public static class`, `public abstract class`, `public interface`, `public enum`, `public record`.

   b. **Cross-reference against the README**: For each public class/interface/enum found in source, check if it appears anywhere in the README. Build a table:

   | Class/Interface | Source File | In README? | Importance |
   |----------------|-------------|------------|------------|

   c. **Flag missing high-value APIs**: Focus on classes that represent major features users would want to discover:
   - New EIP/ERC implementations (e.g., EIP-7702 transaction types, EIP-2612 permit)
   - Cryptographic primitives (e.g., Poseidon hashing, BLS signatures)
   - Service classes that solve common developer tasks (e.g., fee estimation, nonce management)
   - Extension methods that add convenience (e.g., parameter conversion helpers)
   - Error handling types (e.g., custom exceptions for contract reverts)
   - **High-level convenience classes** like `ABIEncode` that wrap low-level primitives — these are what users actually want

   d. **Categorize gaps by severity**:
   - 🔴 **Critical**: Major feature completely undocumented (e.g., `ABIEncode` class, EIP-712 encoding in ABI package)
   - 🟠 **Significant**: Important utility/service missing (e.g., fee estimation strategies)
   - 🟡 **Minor**: Helper class or internal utility that advanced users might want

   e. **Skip internal/infrastructure classes** that aren't meant for direct consumer use (e.g., internal factories, test helpers).

8. **Check playground alignment**: If a playground example exists for this package, verify the README's examples are consistent with it.

### Output per package

```
## Package: Nethereum.XXX
README: src/Nethereum.XXX/README.md
.csproj: ✅ Package name matches
Status: ✅ Valid / ⚠️ Issues Found / ❌ Major Problems

### Verified APIs
- ClassName.MethodName — ✅ src/Nethereum.XXX/File.cs:123
- ClassName.OtherMethod — ✅ src/Nethereum.XXX/File.cs:456

### Compilation Results
- Example 1 (line 30-45): ✅ Compiles
- Example 2 (line 60-80): ❌ Error CS1061: 'Web3' does not contain 'FakeMethod'

### Issues Found
- Line 45: `SomeClass.FakeMethod()` — ❌ does not exist. Actual: `RealMethod()`
- Line 67: Missing parameter `BlockParameter` — actual signature requires it

### Missing Functionality (not in README)
- 🔴 ABIEncode — high-level abi.encode/abi.encodePacked equivalent, completely undocumented
- 🔴 Eip712TypedDataEncoder — EIP-712 typed data encoding/hashing, not in ABI guide
- 🟠 FeeSuggestionService — EIP-1559 fee estimation, undocumented
- 🟡 WaitStrategy — retry/polling utility, undocumented

### Fixes Required
1. [exact description of each fix with before/after code]
2. [missing functionality to add with draft content]
```

### Gate 2: Present the full validation report. Wait for approval before applying fixes.

---

## Stage 3: Fix README Issues

**Goal**: Apply all approved fixes to the README files in the Nethereum repo.

### CRITICAL: Doc Page Generation Pipeline

**Docusaurus package doc pages (`nethereum-*.md`) are AUTO-GENERATED from READMEs.** NEVER manually edit the generated doc pages — they will be overwritten.

The pipeline works like this:
1. Source of truth: `src/{PackageName}/README.md` in the **Nethereum** repo
2. Sync script: `scripts/sync-readmes.js` copies READMEs → Docusaurus `docs/<section>/nethereum-*.md`
3. The script adds frontmatter (title, NuGet link, GitHub edit link), strips the first H1, and rewrites cross-package links

**To update package documentation:**
1. Edit ONLY `src/{PackageName}/README.md` in the Nethereum repo
2. Run the sync script to regenerate Docusaurus pages:
   ```bash
   cd "C:/Users/SuperDev/Documents/Repos/Nethereum.Documentation"
   node scripts/sync-readmes.js "C:/Users/SuperDev/Documents/Repos/Nethereum"
   ```
3. Verify with `npm run build`

**Files you CAN manually create/edit in the Docusaurus repo:**
- Guide pages (e.g., `docs/{section}/guide-*.md`) — these are NOT generated by the sync script
- `overview.md` files — section overview pages
- `PROGRESS.md` — progress tracking
- `sidebars.ts` — sidebar configuration

**Files you must NEVER manually edit:**
- Any `docs/{section}/nethereum-*.md` file — these are regenerated by `sync-readmes.js`

### Process

1. Apply each approved fix to `src/{PackageName}/README.md`
2. Re-run compilation checks on fixed examples to confirm they now compile
3. Run `sync-readmes.js` to regenerate Docusaurus pages
4. **Propagate fixes to ALL downstream artifacts** — when a README class/method name changes, search for the OLD (wrong) name across:
   - `docs/{section}/guide-*.md` — guide pages may reference the wrong API
   - `docs/{section}/overview.md` — simple path tables may use the wrong class/method names
   - `plugins/nethereum-skills/skills/*/SKILL.md` — plugin skills may have copied the wrong examples
   - Fix every occurrence. This is the most commonly missed step — hallucinated names spread to every artifact that references the README.
5. Run `npm run build` to verify no broken links
6. Update `PROGRESS.md` with fixes applied

No gate here — proceed to Stage 4 after fixes are applied and verified.

---

## Stage 4: Create/Update Guide Pages

**Goal**: Create polished guide pages that TEACH, not just show code.

### Process

For each use case from the approved table:

1. **Create the guide page** at `docs/{section}/{guide-name}.md`

2. **Apply the Guide Quality Checklist** (from the top of this document). Every guide MUST score 8/10 or higher on:
   - [ ] `:::tip The Simple Way` callout at the top (if guide involves a `web3.Eth` operation)
   - [ ] Opening context (what problem, when would you use this)
   - [ ] Prerequisites
   - [ ] Mental model / conceptual explanation
   - [ ] Progressive examples (simple → complex — simple path FIRST)
   - [ ] Explanation between every code block
   - [ ] Real-world scenarios and realistic values
   - [ ] Advanced/optional sections clearly labeled ("## More Control:", "## Advanced:")
   - [ ] Decision guidance (when to use which approach)
   - [ ] Common mistakes / gotchas
   - [ ] Connected "Next steps"
   - [ ] No orphan code blocks
   - [ ] Read-only guides note "no gas, signing, or fees needed"

3. **Every guide page must also have**:
   - Correct frontmatter: `title`, `sidebar_label`, `sidebar_position`, `description`
   - NuGet install command(s)
   - **Verified working code** — only code that passed compilation in Stage 2, adapted from the validated README or test code
   - Links to the package README for full API reference
   - Links to playground examples where they exist
   - Links to external resources where relevant (chainlist.org, ethereum.org, provider docs)
   - **DO NOT** add Claude Code plugin tip boxes (`:::tip Claude Code ... /plugin install ...`) to guide pages — plugin installation instructions will be provided separately in dedicated setup documentation
   - "Next steps" linking to related guides WITH CONTEXT (not just a list of links)

4. **Self-review as a user**: After writing, re-read the guide and ask:
   - "If I'd never used Nethereum, would I understand WHY I'm doing each step?"
   - "If I hit an error, does this guide help me figure out what went wrong?"
   - "Do I know when to use this approach vs the alternatives?"
   - If the answer to any is "no", the guide needs more context.

5. **Do NOT**:
   - Invent code examples — only use verified code
   - Dump code without context — every block needs motivation
   - Duplicate README content — the guide teaches the task, the README is the API reference
   - Write a guide that's just headers + code blocks — that's a reference, not a guide

6. **Update `sidebars.ts`** following the Sidebar Structure Standard:
   - Group guides into meaningful sub-categories (Essentials, Deep Dives, etc.)
   - First sub-group should have `collapsed: false`
   - All `nethereum-*.md` package pages go under a "Package Reference" sub-category (ALWAYS last)
   - Order guides within each sub-group by learning progression, NOT alphabetically
   - Update `sidebar_position` in each guide's frontmatter to match the new order

7. **Update the section `overview.md`** to include a guide table at the bottom, grouped by sub-category, linking every guide with a one-line description of what the learner will learn.

8. **Verify the learning journey** — read all guides in sidebar order and confirm:
   - Each guide's Next Steps points forward to the next guide in sequence as its first link
   - Later guides reference concepts from earlier ones where appropriate
   - No guide feels like a dead end — every guide connects to at least 2 others

### Gate 3: Present each guide page for review. Wait for approval before proceeding.

---

## Stage 4b: Validate the Learning Journey

**Goal**: Read every guide in the section IN ORDER as a learner would, and verify the journey makes sense.

This stage was established during Core Foundation validation and is MANDATORY for every section.

### Process

1. **Read every guide in sidebar order** — start from the first guide in the first sub-group and read through to the last. For each guide, evaluate:

   | Check | What to look for |
   |-------|-----------------|
   | Simple path first | Does it have a `:::tip The Simple Way` callout (if applicable)? Does it lead with the simplest `web3.Eth` approach? |
   | Opening | Does it explain WHY and WHEN in 2-3 sentences before any code? |
   | Advanced labeling | Are optional/advanced sections clearly marked ("More Control:", "Advanced:")? |
   | Connection backward | Does it reference earlier guides where concepts were introduced? |
   | Connection forward | Does Next Steps point to the next guide in sequence as its first link? |
   | Factual prose | Is every claim verifiable against source code or Ethereum specs? |
   | No code dumps (CRITICAL) | Does every code block have guiding text BEFORE it (what and why) and AFTER it (result, gotcha, or connection)? A guide that is just header→code→header→code is a code dump and MUST be rewritten with teaching prose. All prose must reference actual verified API behavior — never hallucinate. |
   | No assert-style code | Are examples realistic app code, not test assertions? |
   | No orphan concepts | Is every technical term either explained or linked? |
   | Read-only callout | For query guides: does it note "no gas, signing, or fees needed"? |
   | Service coverage | Are all relevant high-level services surfaced? (ERC20/721/1155, EIP-7702 delegate/check/revoke, ENS, Multicall) |

2. **Build a journey report** — for each guide, note:
   ```
   Guide: guide-name.md (position N)
   Opening: ✅ Good / ❌ Missing WHY/WHEN / ⚠️ Weak
   Backward links: ✅ References guide-X, guide-Y / ❌ No backward references
   Forward links: ✅ Next Steps → guide-next / ❌ Dead end or wrong target
   Prose quality: ✅ Factual / ⚠️ Vague claims / ❌ Hallucinated content
   Issues: [specific problems]
   ```

3. **Fix all issues** — apply the Guide Opening Pattern and Anti-Patterns checklist from the Guide Quality Standard section above.

4. **Re-read the full journey** after fixes to confirm it flows naturally.

### What "flows naturally" means

A learner should be able to:
- Start at the overview, see the guide table, and know where to begin
- Read the first guide and understand what they'll learn and why it matters
- Follow Next Steps links from guide to guide without confusion
- At any point, understand how the current topic relates to what came before
- Never encounter an unexplained concept that was covered in an earlier guide they skipped

### No gate here — proceed to Stage 5 after the journey validates clean.

---

## Stage 5: Create Plugin Skills

**Goal**: Create a user-facing plugin skill for each use case (or group of tightly related use cases).

Skills go in the **plugin** directory, NOT in `.claude/skills/` (that's for internal dev skills only).

### Process

For each skill to create:

1. **Create the skill directory**: `plugins/nethereum-skills/skills/{skill-name}/`
2. **Write SKILL.md** with:

```yaml
---
name: {skill-name}
description: {what it does and when to trigger — be specific and slightly pushy about triggering so Claude activates it automatically when the user's task matches}
user-invocable: true
---
```

3. **The skill body must contain**:
   - **Context paragraph**: What problem this solves and when to use it (so AI models understand the WHY)
   - The same verified code examples from the guide page
   - Correct NuGet package references with `dotnet add package` commands
   - **Decision guidance**: When there are alternatives, explain which to pick and why
   - Step-by-step instructions Claude can follow to generate working code
   - Reference to the docs page for full details: `For full documentation, see: https://docs.nethereum.com/docs/{section}/{guide-name}`

4. **Skills should be actionable** — when activated (either auto-triggered or invoked via `/nethereum-skills:skill-name`), Claude should be able to generate a complete, working code example without hallucinating.

5. **Skill descriptions must be pushy about triggering** — since users won't explicitly invoke skills, the description needs to match the natural language users would use. Example:
   - Bad: "Send ETH using Nethereum"
   - Good: "Help users send ETH, transfer Ether, make Ethereum payments, or move funds between addresses using Nethereum (.NET). Use this skill whenever the user mentions sending ETH, transferring Ether, Ethereum payments, or anything involving moving native currency on EVM chains with C# or .NET."

6. **Each skill should include a references/ directory** if the use case has complex APIs. Put detailed API reference in `references/api.md` and keep SKILL.md under 500 lines.

### Gate 4: Present the skill files for review. Wait for approval before finalizing.

---

## Stage 5b: Cross-Reference Global Pages

**Goal**: After completing a section's guides and skills, update the three global pages that reference every section's use cases and packages. These pages are the main entry points for users — if they're stale, users won't discover the section's content.

### The Three Global Pages

| Page | Path | What It Contains |
|------|------|-----------------|
| **What Do You Want to Do?** | `docs/what-do-you-want-to-do.md` | Every use case across all sections, grouped by category. Each row links to a guide. |
| **Component Catalog** | `docs/component-catalog.md` | "Quick Start by Use Case" table (use cases → packages) + "All Packages by Category" tables (packages → descriptions with links) |
| **Landing Page** | `src/pages/index.tsx` | "Documentation Sections" grid (section cards with descriptions) + "What Do You Want to Do?" task groups (subset of top use cases) |

### Process

For each global page, perform these checks:

#### 1. What Do You Want to Do? (`what-do-you-want-to-do.md`)

- **Find the section's category** (e.g., "## Data Services", "## DeFi & Protocols")
- **Compare each row to the section's use case table from Stage 1**:
  - **Add** any use cases from the section that are missing from this page
  - **Update** rows whose descriptions no longer match the actual API/guide content
  - **Delete** rows for guides/features that were removed or merged
  - **Fix links** — every row should link to the correct guide page, not just the package reference
- **Verify the framing matches the section's key value proposition** — e.g., TokenServices should lead with "multicall over known tokens, no indexer needed", not just "scan token balances"

#### 2. Component Catalog (`component-catalog.md`)

Two tables need checking:

**Quick Start by Use Case** (top of file):
- Same add/update/delete process as what-do-you-want-to-do.md
- Every package mentioned must have a working link to its doc page
- Missing packages in the section should be added with links

**All Packages by Category** (bottom of file):
- Find the section's category table (e.g., "### Data Processing & Storage")
- **Verify every package in the section appears** — compare against the section's sidebar Package Reference list
- **Add missing packages** with correct links and descriptions
- **Fix broken links** — verify each `[Package Name](section/slug)` resolves
- **Update descriptions** to match the current README

#### 3. Landing Page (`src/pages/index.tsx`)

Two arrays need checking:

**`sections` array** (~line 23):
- Find the section card (e.g., `title: 'Data Services'`)
- **Update the description** to match the section's current value proposition
- **Verify the link** points to the correct overview page

**`taskGroups` array** (~line 127):
- Find the category (e.g., `category: 'Data & Indexing'`)
- **Compare tasks** to the section's top 2-3 use cases
- **Update task descriptions and links** to match current guides
- **Add/remove tasks** if the section's scope changed

### Decision Rules

| Situation | Action |
|-----------|--------|
| Section has a use case NOT in the global pages | ADD it to all 3 pages |
| Global page has a row for a deleted/merged guide | DELETE or UPDATE it |
| Package exists in section but missing from catalog | ADD to the catalog's category table |
| Package exists in catalog but not in any section sidebar | Verify if it belongs elsewhere or was removed |
| Task description doesn't match the guide's framing | UPDATE to match the guide's key value proposition |
| Link points to package reference instead of guide | UPDATE to point to the guide (guides are where users learn) |

### No gate here — these are mechanical updates. Proceed to Stage 6 after applying all fixes.

---

## Stage 6: Final Verification

**Goal**: Confirm everything works together.

### Checklist

**Sidebar Structure:**
- [ ] Section uses Overview → Guide Sub-Groups → Package Reference structure
- [ ] Guides grouped into meaningful sub-categories (not one flat list)
- [ ] First guide sub-group has `collapsed: false`
- [ ] Package Reference is the LAST sub-category with label "Package Reference"
- [ ] Guide ordering within each sub-group follows the learning progression
- [ ] `sidebar_position` values in frontmatter match the sidebar order

**Simple Path First:**
- [ ] Overview has "The Simple Path" section with `web3.Eth` table showing all simple-path operations
- [ ] Overview mentions built-in typed services (ERC20, ERC721, ERC1155, EIP-7702 authorisation service)
- [ ] Every guide with a `web3.Eth` operation has a `:::tip The Simple Way` callout
- [ ] Fee estimation guide leads with "fees are automatic" framing
- [ ] Advanced/optional sections are clearly labeled ("## More Control:", "## Advanced:")
- [ ] Read-only query guides note "no gas, signing, or fees needed"
- [ ] EIP-7702 full lifecycle surfaced: delegate, check, get delegate, revoke, sponsored, batch

**Guide Quality & Learning Journey:**
- [ ] Every use case has a guide page in the Docusaurus site
- [ ] Every guide listed in the overview guide tables (no orphan guides missing from tables)
- [ ] Every guide scores 8/10+ on the Guide Quality Checklist
- [ ] Guides teach (explain WHY and WHEN), not just show code
- [ ] Every explanation is factual and verifiable against source code — no hallucinated commentary
- [ ] Next Steps in each guide point forward through the learning sequence
- [ ] Later guides reference earlier ones where concepts build on each other
- [ ] No guide is a dead end — every guide connects to at least 2 others

**Overview Page:**
- [ ] Section overview includes a guide table grouped by sub-category
- [ ] Guide table has "What You'll Learn" column with one-line descriptions

**Content Accuracy:**
- [ ] Every code example in every guide page compiles
- [ ] Every code example in every referenced README compiles
- [ ] Every NuGet package name matches its actual .csproj
- [ ] Every class/method/namespace reference exists in the actual codebase
- [ ] Missing functionality scan complete — all major public APIs in source are covered

**Completeness:**
- [ ] Every use case has a plugin skill (or is covered by a grouped skill) in `plugins/nethereum-skills/skills/`
- [ ] Playground examples are linked where they exist
- [ ] External references (chainlist.org, Infura, Alchemy, Anvil, Hardhat, etc.) are included where relevant
- [ ] Guide pages do NOT contain Claude Code plugin tip boxes (installation instructions go in dedicated setup docs)
- [ ] Plugin skill descriptions are pushy enough for auto-triggering
- [ ] Skills contain enough WHY context for AI models to make good decisions

**Global Page Cross-References (Stage 5b):**
- [ ] `what-do-you-want-to-do.md` — section's use cases present, descriptions match guides, links work
- [ ] `component-catalog.md` Quick Start table — use cases present with correct links
- [ ] `component-catalog.md` Packages by Category — all section packages listed with working links
- [ ] `src/pages/index.tsx` sections array — section card description matches current content
- [ ] `src/pages/index.tsx` taskGroups array — top use cases present with correct links
- [ ] No stale references to deleted/renamed guides in any global page

**Build:**
- [ ] `npm run build` passes: `cd "C:/Users/SuperDev/Documents/Repos/Nethereum.Documentation" && npm run build`
- [ ] `PROGRESS.md` updated with completion status

### Guide Quality Gate

Before marking a section complete, re-read every guide as if you're a developer who:
1. Has never used Nethereum
2. Knows C# but not Ethereum deeply
3. Wants to accomplish a specific task
4. Will give up if the docs don't help within 2 minutes

If any guide fails this test, rewrite it before proceeding.

### Build verification

Run the Docusaurus build and confirm no new broken links were introduced. Pre-existing broken links from other sections are OK — only verify this section's links are clean.

After all checks pass, mark the section as complete in `PROGRESS.md`.
