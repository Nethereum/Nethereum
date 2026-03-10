# Commit Skill

Systematic commit workflow for Nethereum. Performs deep code audit, validates README, presents changes for approval, then commits.

Follow the full process defined in `docs/COMMIT_GUIDELINES.md`.

## Workflow

### 1. Identify Scope

Determine which project(s) or feature area to commit. Run `git status` and `git diff --stat` to understand what has changed. Group changes by project boundary — never mix unrelated changes.

### 2. Deep Code Audit

Use the Agent tool to run parallel audit agents on the changed files. Each agent should read the actual source and report findings. The audit covers ALL of the following categories:

---

#### A. Security Audit

**Injection & Input Validation:**
- No SQL injection (parameterised queries only, no string concatenation in SQL)
- No command injection (no unsanitised user input in Process.Start, shell commands)
- No XSS (all user-facing output encoded/sanitised)
- No path traversal (validate file paths, no `..` exploitation)
- No deserialisation vulnerabilities (no `BinaryFormatter`, no untrusted `TypeNameHandling`)
- Input validation at ALL system boundaries (RPC handlers, API endpoints, user input)

**Secrets & Credentials:**
- No hardcoded private keys, mnemonics, API keys, passwords, or connection strings
- No secrets in logs (check all `_logger.Log*` calls for sensitive data)
- No credentials in exception messages

**Cryptographic Safety:**
- Proper use of cryptographic APIs (no custom crypto implementations)
- Secure random number generation (`RandomNumberGenerator`, not `Random`)
- No weak hashing (no MD5/SHA1 for security purposes)
- Private key material zeroed after use where possible

**Network & Transport:**
- HTTPS preferred over HTTP for external endpoints
- Timeout configuration on all HTTP clients
- No unbounded request/response sizes
- Certificate validation not disabled

**Concurrency & Resource Safety:**
- No race conditions in shared state
- Proper `CancellationToken` propagation
- `IDisposable` resources properly disposed (`using` statements)
- No deadlocks from `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` on async code
- Thread-safe collections used where appropriate (`ConcurrentDictionary`, not `Dictionary` with locks)

---

#### B. SOLID Principles

**Single Responsibility:**
- Each class has one reason to change
- Methods do one thing (< 30 lines preferred, flag > 50)
- No "God classes" that handle everything
- Services don't mix business logic with infrastructure concerns

**Open/Closed:**
- New behaviour added via extension, not modification of existing classes
- Strategy/template patterns used where behaviour varies
- No switch statements on type that should be polymorphism

**Liskov Substitution:**
- Subtypes are substitutable for their base types
- Interface implementations honour the contract (no `NotImplementedException` in production code)
- No violation of preconditions/postconditions in derived classes

**Interface Segregation:**
- Interfaces are focused and cohesive
- No "fat interfaces" forcing implementers to stub unused methods
- Clients don't depend on methods they don't use

**Dependency Inversion:**
- High-level modules depend on abstractions, not concretions
- Dependencies injected via constructor, not `new`'d internally
- No service locator anti-pattern (no `IServiceProvider.GetService` inside business logic)
- Proper DI registration in `ServiceCollectionExtensions`

---

#### C. .NET & C# Standards

**Naming Conventions:**
- PascalCase for public members, types, namespaces, properties, methods, events
- camelCase for local variables, parameters
- `_camelCase` for private fields
- `I` prefix for interfaces, no `Impl` suffix for implementations
- `Async` suffix on all async methods
- No Hungarian notation

**Async/Await:**
- Async all the way through — no sync-over-async (`Task.Result`, `.Wait()`, `.GetAwaiter().GetResult()`)
- `ConfigureAwait(false)` in library code (not in ASP.NET/Blazor code)
- `CancellationToken` accepted and propagated in all async methods
- No `async void` except event handlers
- `ValueTask` used where appropriate for hot paths

**Error Handling:**
- Specific exception types (not bare `throw new Exception()`)
- No empty catch blocks (at minimum log the exception)
- `catch (OperationCanceledException) when (ct.IsCancellationRequested)` pattern for cancellation
- No exceptions for control flow
- Guard clauses with `ArgumentNullException.ThrowIfNull` or null checks at method entry

**Memory & Performance:**
- No unnecessary allocations in hot paths
- `Span<byte>` / `ReadOnlySpan<byte>` used for byte manipulation where possible
- String concatenation uses `StringBuilder` or interpolation, not `+` in loops
- Collections pre-sized when count is known (`new List<T>(capacity)`)
- No LINQ in tight loops where a simple `for` is clearer and faster

**Nullability:**
- Nullable reference types handled correctly (`?` annotations where appropriate)
- No `null!` suppressions without justification
- Null checks at public API boundaries

**Modern C# Features (where target framework supports):**
- Pattern matching over type checks + casts
- `is not null` over `!= null` where appropriate
- Record types for immutable DTOs
- `using` declarations over `using` blocks where cleaner

---

#### D. Nethereum-Specific Standards

**Contract Interactions:**
- ALWAYS use typed contract services from `Nethereum.Contracts.Standards` (ERC20ContractService, ERC721ContractService, etc.)
- NEVER write raw ABI JSON strings for standard contracts
- Use `BlockParameter` overloads for historical queries

**Hex & Numeric Types:**
- Use `HexBigInteger` for uint256 values, not string parsing
- Use `Nethereum.Hex.HexConvertors.Extensions` for hex conversion
- Use `UnitConversion.Convert.FromWei()` / `.ToWei()` for ETH conversion
- Gas and value fields remain `string` to preserve full uint256 precision in storage entities

**RPC Patterns:**
- RPC handlers follow existing patterns in `Rpc/Handlers/`
- Request/response types use proper JSON serialisation attributes
- Error responses use standard JSON-RPC error codes

**Architecture Patterns:**
- MVVM with `ObservableObject` for UI components
- Repository pattern for data access
- Extension method registration via `ServiceCollectionExtensions`
- Processing services follow `BackgroundService` pattern
- Block progress tracked via `IBlockProgressRepository`

**Project Standards:**
- `.csproj` uses `dir.props` import (no inline Version for Nethereum packages)
- Version set via `$(NethereumVersion)` from `buildConf/Version.props`
- Multi-target frameworks follow `buildConf/Frameworks.props` patterns
- `PackageReference Update` (not `Include`) when overriding versions from `Generic.props`

---

#### E. Code Quality

**Dead Code:**
- No unused `using` statements
- No commented-out code blocks
- No unreachable code after `return`/`throw`
- No unused private methods or fields
- No unused parameters (unless interface requirement)

**Complexity:**
- Cyclomatic complexity reasonable (flag methods with > 10 branches)
- No deeply nested conditionals (> 3 levels) — extract methods or use early returns
- Guard clauses / early returns over deep nesting

**Consistency:**
- Follows patterns established in adjacent files in the same project
- Brace style consistent with project (Allman style for C#)
- File organisation matches project conventions (namespaces match folder structure)

**Documentation:**
- No unnecessary XML doc comments (unless public API that needs them)
- No comments that repeat what the code says
- Complex algorithms have brief explanatory comments
- README updated if public API surface changed

---

### 3. Build Verification

```bash
cd <project-directory>
dotnet build --no-restore 2>&1 | grep -E "error CS|Build succeeded|Build FAILED"
```

Must show zero `error CS` lines and `Build succeeded`.

### 4. Validate README (if project has one)

Per `docs/PACKAGE_DOCUMENTATION_GUIDE.md`:

- [ ] All referenced classes/methods exist in source code
- [ ] No local file paths (`C:\Users\...`)
- [ ] No source code line numbers (e.g., `File.cs:42`, `Line 34`)
- [ ] Architecture diagrams match actual class hierarchy
- [ ] Entity field lists match actual source properties
- [ ] New features/files documented
- [ ] Removed features/files cleaned from README
- [ ] No hallucinated capabilities
- [ ] Dependencies listed match actual `.csproj` references

### 5. Present Audit Summary & Changes for Approval

Show the user:

**Audit Results:**
- Security: PASS/FAIL (list any findings)
- SOLID: PASS/FAIL (list any findings)
- .NET Standards: PASS/FAIL (list any findings)
- Nethereum Standards: PASS/FAIL (list any findings)
- Code Quality: PASS/FAIL (list any findings)
- Build: PASS/FAIL
- README: PASS/FAIL/N/A

**Files to stage (N files):**

```
Modified:
- file1.cs
- file2.cs

New:
- file3.cs

Deleted:
- file4.cs
```

**Commit message:**
```
ProjectArea: concise title

- Change 1
- Change 2
```

### 6. Wait for Explicit Approval

Do NOT proceed until the user explicitly approves. They may:
- Approve as-is
- Ask to fix audit findings first
- Request changes to file list or commit message
- Ask to split into multiple commits

### 7. Execute

```bash
git add <files>
git commit -m "Title" -m "- Detail 1" -m "- Detail 2"
```

### 4b. Documentation Propagation Check

When committing changes to test files or source code, check for documentation impact:

#### Tagged Test Changes (`[NethereumDocExample]`)

If any changed file contains `[NethereumDocExample]` attributes:

1. **Search for tagged tests** in the diff:
   ```bash
   git diff --cached -- '*.cs' | grep -E "\[NethereumDocExample|DocSection\."
   ```

2. **For modified tagged tests** — the test code IS the documentation example. If the test logic changed:
   - [ ] Update the corresponding README code example in `src/{Package}/README.md`
   - [ ] Update the corresponding guide page in `Nethereum.Docusaurus/website/docs/`
   - [ ] Update the corresponding Claude Code skill in `.claude/skills/`
   - [ ] Verify the playground example still matches (if one exists)

3. **For new tagged tests** — a new documentation example was added:
   - [ ] Add the code example to the package README
   - [ ] Add to the guide page (or flag that guide needs updating)
   - [ ] Add to the skill (or flag that skill needs updating)

4. **Present propagation status** in the audit summary:
   ```
   Documentation Propagation: PASS/NEEDS-UPDATE
   - README: ✅ updated / ⚠️ needs update (list which)
   - Guide pages: ✅ updated / ⚠️ needs update / N/A
   - Skills: ✅ updated / ⚠️ needs update / N/A
   ```

#### New Public API Without Documentation

If any changed source file in `src/` adds new public classes, methods, or properties:

1. **Check if the new API has a tagged test**:
   ```bash
   grep -r "NethereumDocExample" tests/ --include="*.cs" | grep "<use-case-slug>"
   ```

2. **If no tagged test exists**, flag it:
   ```
   ⚠️ New public API without documentation:
   - ClassName.NewMethod() in Nethereum.Package — needs [NethereumDocExample] test
   ```

3. The commit can proceed, but the gap should be logged for follow-up.

#### Attribute Reference

The `[NethereumDocExample]` attribute lives in `tests/Nethereum.XUnitEthereumClients/NethereumDocExampleAttribute.cs`:
```csharp
[NethereumDocExample(DocSection.CoreFoundation, "use-case-slug", "Human-readable title")]
```

`DocSection` enum values: `CoreFoundation`, `Signing`, `SmartContracts`, `DeFi`, `EvmSimulator`, `InProcessNode`, `AccountAbstraction`, `DataIndexing`, `MudFramework`, `WalletUI`, `Consensus`, `ClientExtensions`

---

## Rules

- NEVER add Co-Authored-By lines
- NEVER commit with build errors
- NEVER commit with unresolved security findings (user must explicitly accept risk)
- NEVER mix unrelated changes in one commit
- ALWAYS use project prefix in title (MUD:, EVM:, CoreChain:, BlockchainProcessing:, etc.)
- ALWAYS present full audit summary and file list before committing
- ALWAYS fix audit findings before presenting for commit, or explicitly flag them as accepted risk
