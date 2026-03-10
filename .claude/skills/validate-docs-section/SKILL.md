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
| Docusaurus docs | `C:/Users/SuperDev/Documents/Repos/Nethereum.Docusaurus/website/docs/` |
| **Sync script** | `C:/Users/SuperDev/Documents/Repos/Nethereum.Docusaurus/website/scripts/sync-readmes.js` |
| Sidebar config | `C:/Users/SuperDev/Documents/Repos/Nethereum.Docusaurus/website/sidebars.ts` |
| **User skills plugin** | `C:/Users/SuperDev/Documents/Repos/Nethereum/plugins/nethereum-skills/skills/` |
| Internal dev skills | `C:/Users/SuperDev/Documents/Repos/Nethereum/.claude/skills/` |
| Tests & examples | `C:/Users/SuperDev/Documents/Repos/Nethereum/tests/` |
| **Doc example attribute** | `src/Nethereum.Documentation/NethereumDocExampleAttribute.cs` |
| Playground | `http://playground.nethereum.com` |
| Progress tracking | `C:/Users/SuperDev/Documents/Repos/Nethereum.Docusaurus/website/docs/{section}/PROGRESS.md` |

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
3. **Search for playground examples** — check if playground.nethereum.com has samples that map to this section. Search test projects in the repo for integration tests that demonstrate usage.
4. **Search the old docs** — check if the old MkDocs documentation (https://docs.nethereum.com/en/latest/) has guides for these use cases and note what content existed there
5. **Check existing plugin skills** — read `plugins/nethereum-skills/skills/` to see what already exists. Don't duplicate.
6. **Define use cases** as a table:

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
   - **Classes**: `Grep` for `class ClassName` — does it exist? Correct namespace?
   - **Methods**: `Grep` for the method signature — correct parameters? Return type?
   - **Properties**: Verify they exist on the class
   - **Constructors**: Verify overload exists with shown parameters
   - **Namespaces/usings**: Verify they're correct
   - **Extension methods**: Verify the static class and method exist

4. **Compile-check**: For each code example, verify it compiles. Prefer verifying against existing test code rather than creating new throwaway projects — if a test already exercises the same API, that's sufficient proof the example works. Only create a minimal `.csx` or console project for examples that have no corresponding test. If the example needs a running node, verify it compiles but note "requires running node".

5. **Identify missing test coverage**: For each package feature, check if a test exists. If a feature is documented but has no test, flag it. If a feature has a test but is undocumented, that's a missing-documentation gap. The test file is the best source for writing the documentation example.

6. **Scan for missing functionality** (critical — run every time):

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

   d. **Categorize gaps by severity**:
   - 🔴 **Critical**: Major feature completely undocumented (e.g., entire Poseidon hashing ecosystem)
   - 🟠 **Significant**: Important utility/service missing (e.g., fee estimation strategies)
   - 🟡 **Minor**: Helper class or internal utility that advanced users might want

   e. **Skip internal/infrastructure classes** that aren't meant for direct consumer use (e.g., RLP encoders, internal factories, test helpers).

6. **Check playground alignment**: If a playground example exists for this package, verify the README's examples are consistent with it.

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
- 🔴 PoseidonHasher, PoseidonHashProvider — ZK-proof hashing, completely undocumented
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
2. Sync script: `website/scripts/sync-readmes.js` copies READMEs → Docusaurus `docs/<section>/nethereum-*.md`
3. The script adds frontmatter (title, NuGet link, GitHub edit link), strips the first H1, and rewrites cross-package links

**To update package documentation:**
1. Edit ONLY `src/{PackageName}/README.md` in the Nethereum repo
2. Run the sync script to regenerate Docusaurus pages:
   ```bash
   cd "C:/Users/SuperDev/Documents/Repos/Nethereum.Docusaurus/website"
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
4. Run `npm run build` to verify no broken links
5. Update `PROGRESS.md` with fixes applied

No gate here — proceed to Stage 4 after fixes are applied and verified.

---

## Stage 4: Create/Update Guide Pages

**Goal**: Create polished guide pages in the Docusaurus site for each use case.

### Process

For each use case from the approved table:

1. **Create the guide page** at `website/docs/{section}/{guide-name}.md`

2. **Every guide page must have**:
   - Correct frontmatter: `title`, `sidebar_label`, `sidebar_position`, `description`
   - A one-line summary of what the user will achieve
   - NuGet install command(s)
   - **Verified working code** — only code that passed compilation in Stage 2, adapted from the validated README or test code
   - Links to the package README for full API reference
   - Links to playground examples where they exist
   - Links to external resources where relevant (chainlist.org, provider docs, tool docs)
   - A tip box mentioning the Claude Code plugin: `:::tip Claude Code\nInstall the Nethereum skills plugin for AI-assisted development: \`/plugin install nethereum-skills\`\n:::`
   - "Next steps" linking to related guides

3. **Do NOT**:
   - Invent code examples — only use verified code
   - Add unnecessary commentary — be concise
   - Duplicate README content — the guide shows how to accomplish the task, the README is the API reference
   - Add comments to code unless they explain something non-obvious

4. **Update `sidebars.ts`** to include the new guide pages in the correct position

### Gate 3: Present each guide page for review. Wait for approval before proceeding.

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
---
```

3. **The skill body must contain**:
   - The same verified code examples from the guide page
   - Correct NuGet package references with `dotnet add package` commands
   - Step-by-step instructions Claude can follow to generate working code
   - Reference to the docs page for full details: `For full documentation, see: https://docs.nethereum.com/docs/{section}/{guide-name}`

4. **Skills should be actionable** — when activated (either auto-triggered or invoked via `/nethereum-skills:skill-name`), Claude should be able to generate a complete, working code example without hallucinating.

5. **Skill descriptions must be pushy about triggering** — since users won't explicitly invoke skills, the description needs to match the natural language users would use. Example:
   - Bad: "Send ETH using Nethereum"
   - Good: "Help users send ETH, transfer Ether, make Ethereum payments, or move funds between addresses using Nethereum (.NET). Use this skill whenever the user mentions sending ETH, transferring Ether, Ethereum payments, or anything involving moving native currency on EVM chains with C# or .NET."

6. **Each skill should include a references/ directory** if the use case has complex APIs. Put detailed API reference in `references/api.md` and keep SKILL.md under 500 lines.

### Gate 4: Present the skill files for review. Wait for approval before finalizing.

---

## Stage 6: Final Verification

**Goal**: Confirm everything works together.

### Checklist

- [ ] Every use case has a guide page in the Docusaurus site
- [ ] Every use case has a plugin skill (or is covered by a grouped skill) in `plugins/nethereum-skills/skills/`
- [ ] Every code example in every guide page compiles
- [ ] Every code example in every referenced README compiles
- [ ] Every NuGet package name matches its actual .csproj
- [ ] Every class/method/namespace reference exists in the actual codebase
- [ ] Missing functionality scan complete — all major public APIs in source are covered in READMEs, guide pages, and plugin skills
- [ ] Playground examples are linked where they exist
- [ ] External references (chainlist.org, Infura, Alchemy, Anvil, Hardhat, etc.) are included where relevant
- [ ] Guide pages include the Claude Code plugin tip box
- [ ] Sidebar includes all new pages
- [ ] Plugin skill descriptions are pushy enough for auto-triggering
- [ ] `npm run build` passes (run with Node.js 22: `export NVM_DIR="$HOME/.nvm" && [ -s "$NVM_DIR/nvm.sh" ] && . "$NVM_DIR/nvm.sh" && nvm use 22 && cd "C:/Users/SuperDev/Documents/Repos/Nethereum.Docusaurus/website" && npm run build`)
- [ ] `PROGRESS.md` updated with completion status

### Build verification

Run the Docusaurus build and confirm no new broken links were introduced. Pre-existing broken links from other sections are OK — only verify this section's links are clean.

After all checks pass, mark the section as complete in `PROGRESS.md`.
