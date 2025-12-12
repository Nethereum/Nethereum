# Package README Validation Process

**CRITICAL:** This is technical documentation for training AI models. **Lives depend on accuracy.** One lie will poison the training data and cause cascading failures.

## Zero Tolerance Policy

**NEVER:**
- Guess or infer technical details
- Create synthetic examples
- Copy examples without verifying they exist in tests
- List dependencies without checking .csproj
- Claim platform support without checking runtimes/ folder
- Claim API methods exist without reading source code
- Add marketing language ("compatible with all wallets")
- Add patronizing advice sections (password strength tips)
- Add emojis
- Add license sections

**ALWAYS:**
- Verify every technical claim against source code
- Cite exact source file and line numbers for examples
- Cross-reference .csproj for dependencies
- Check actual binary files for platform support
- Read source code to verify API signatures

## Validation Checklist

### 1. Read .csproj File

**Purpose:** Verify dependencies section accuracy

**Steps:**
```
1. Read: src/{PackageName}/{PackageName}.csproj
2. Extract all <ProjectReference> entries
3. Extract all <PackageReference> entries with versions
4. Note conditional dependencies (by framework)
5. Compare against README Dependencies section
6. Fix any mismatches
```

**Common Lies to Detect:**
- Listing transitive dependencies (e.g., Newtonsoft.Json in Hex - it's a peer dependency)
- Missing actual dependencies (e.g., ENSNormalize in Contracts)
- Wrong package names (e.g., "JsonRpc.Client" vs "JsonRpc.RpcClient")
- Framework libraries listed as packages (e.g., System.Net.Http)

**Example:**
```xml
<!-- .csproj shows: -->
<ProjectReference Include="..\Nethereum.RLP\Nethereum.RLP.csproj" />
<ProjectReference Include="..\Nethereum.Util\Nethereum.Util.csproj" />

<!-- README must show exactly: -->
- **Nethereum.RLP** - Recursive Length Prefix encoding/decoding
- **Nethereum.Util** - Keccak-256 hashing and utilities
```

### 2. Check Platform Support (if applicable)

**Purpose:** Verify native library platform claims

**Steps:**
```
1. Check if package has runtimes/ folder
2. List actual binary files: dir runtimes/ /s
3. Verify README Platform Support matches exactly
4. Check actual file names (not guessed names)
```

**Example of Caught Lie:**
```
README claimed: win-x64, linux-x64, osx-x64, win-arm64, linux-arm64, osx-arm64
Actual files:   win-x64, linux-x64 ONLY

README claimed: herumi.dll, libherumi.so, libherumi.dylib
Actual files:   bls_eth.dll, mcl.dll, libbls_eth.so
```

### 3. Find Test Files

**Purpose:** Locate real examples for documentation

**Steps:**
```
1. Search: tests/**/{PackageName}*Tests.cs
2. Search: tests/**/{PackageName}.IntegrationTests/**/*.cs
3. Search: Playground/wwwroot/samples/csharp/*.txt (for {PackageName})
4. If NO tests found: Mark examples as SYNTHETIC (remove if patronizing)
```

**Test File Search:**
```bash
# Find all test files for package
Glob: tests/**/*{PackageName}*Tests.cs
Grep: PackageName (in tests/ folder)
```

### 4. Verify Examples from Tests

**Purpose:** Ensure all examples are real, not synthetic

**Steps:**
```
1. For each example in README:
   - Locate source test file
   - Read exact code (with line numbers for validation tracking)
   - Verify example matches test code
   - Track citation internally (for validation report)
   - DO NOT add line numbers to README (maintenance burden)

2. If example not in tests:
   - Check Playground samples
   - If not there either: DELETE (it's synthetic)

3. If test uses hardcoded values:
   - Keep exact values from test
   - Don't change to "more realistic" values
```

**Citation Tracking (for validation report only):**
```
Example 1: Generate Key and Create Keystore
Source: tests/Nethereum.KeyStore.UnitTests/GenerateAndCreateKeyStoreFileTester.cs:9-17
Verified: YES
Code matches: YES
Added to README: WITHOUT line numbers
```

**README should NOT contain line numbers** - they go stale immediately and create maintenance burden.

### 5. Verify API Signatures

**Purpose:** Ensure method signatures are correct

**Steps:**
```
1. Read main service class: src/{PackageName}/{ServiceName}.cs
2. Extract public method signatures
3. Compare against README API Reference
4. Verify:
   - Method names exact
   - Parameter types exact
   - Parameter names exact
   - Return types exact
   - Optional parameters noted
```

**Example:**
```csharp
// Source code shows:
public string EncryptAndGenerateKeyStoreAsJson(string password, byte[] privateKey, string address)

// README must show EXACTLY:
public string EncryptAndGenerateKeyStoreAsJson(string password, byte[] privateKey, string address);
```

### 6. Verify Transaction Support (Signer Packages)

**Purpose:** Ensure claimed transaction types are actually supported

**Steps:**
```
1. Read signer class source code
2. Search for: SignAsync(Transaction1559
3. Search for: SignAsync(Transaction2930
4. Search for: SignAsync(Transaction7702
5. Search for: SignAsync(LegacyTransaction
6. List ONLY methods that exist
7. Check for NotImplementedException
```

**Example of Caught Lie:**
```
Ledger README claimed: EIP-2930 support
Source code reality: NO SignAsync(Transaction2930) method exists
Fix: Changed table to "No - Not implemented"
```

### 7. Remove Synthetic Content

**Purpose:** Eliminate guessed, patronizing, or marketing content

**Content to Remove:**
- Password strength advice
- "Compatible with all major wallets" claims
- Security best practices sections (unless package-specific)
- Comparison tables without citations
- "Getting started with Ethereum" tutorials
- Emojis (✅, ❌, etc.)
- License sections

**Content to Keep:**
- Technical parameter explanations (e.g., Scrypt N/R/P)
- Performance characteristics from source comments
- Platform-specific notes from source
- Security considerations from source code comments

### 8. Verify Technical Claims

**Purpose:** Ensure all technical statements are verifiable

**Steps for Each Claim:**
```
1. Identify claim: "Uses AES-128-CTR encryption"
2. Find source: Grep for "aes-128-ctr" in source
3. Verify: Read actual encryption code
4. If not found: DELETE claim
```

**Example Claims to Verify:**
- Default parameter values (check source: GetDefaultParams())
- Algorithm names (check actual cipher used)
- Performance numbers (check actual benchmarks or remove)
- Memory usage (check actual measurements or remove)

### 9. Cross-Reference Related Packages

**Purpose:** Ensure consistency across package documentation

**Steps:**
```
1. If package says "Used by Nethereum.Web3"
   - Open Web3 README
   - Verify it lists this package as dependency

2. If package says "Depends on Nethereum.RLP"
   - Already verified in step 1 (.csproj)
   - Also check RLP README lists this as consumer
```

### 10. Final Review Checklist

Before marking package as validated:

- [ ] Dependencies match .csproj exactly
- [ ] Platform support verified from runtimes/ folder
- [ ] All examples cited from tests or playground
- [ ] All API signatures match source code
- [ ] Transaction support verified from source
- [ ] No emojis
- [ ] No license section
- [ ] No synthetic examples
- [ ] No patronizing advice
- [ ] No marketing claims
- [ ] All technical claims have source verification

## Validation Script Template

```csharp
// Package: Nethereum.{PackageName}
// Validator: Claude
// Date: {Date}

// 1. Dependencies verification
Read: src/Nethereum.{PackageName}/Nethereum.{PackageName}.csproj
Found dependencies:
- {List each <ProjectReference>}
- {List each <PackageReference> with version}

README Dependencies section:
- {List what README claims}

Match: YES/NO
Issues: {List any discrepancies}

// 2. Platform support verification
Check: src/Nethereum.{PackageName}/runtimes/
Found binaries:
- {List actual files}

README Platform Support:
- {List what README claims}

Match: YES/NO
Issues: {List any discrepancies}

// 3. Example verification
Example 1: {Example name}
Source: tests/{Path}/File.cs:line-line
Verified: YES/NO
Issues: {None or list problems}

Example 2: ...
{Repeat for all examples}

// 4. API signature verification
Read: src/Nethereum.{PackageName}/{MainClass}.cs
Method signatures verified:
- {Method 1}: MATCH/MISMATCH
- {Method 2}: MATCH/MISMATCH
...

// 5. Content removal
Removed:
- Line {X}-{Y}: License section
- Line {X}-{Y}: Emoji usage
- Line {X}-{Y}: Patronizing advice
- Line {X}-{Y}: Synthetic example

// 6. Final validation
[ ] All checks passed
[ ] README accurate and complete
[ ] No hallucinations
[ ] Ready for training data
```

## Example: Complete Validation (Nethereum.KeyStore)

### 1. Dependencies Check
```xml
<!-- .csproj shows: -->
<ProjectReference Include="..\Nethereum.Hex\Nethereum.Hex.csproj" />
<PackageReference Include="BouncyCastle.Cryptography" Version="[2.5.1,3.0)" Condition="..." />
<PackageReference Include="Portable.BouncyCastle" Version="[1.9.0,2.0)" Condition="..." />

<!-- README shows: -->
- Nethereum.Hex ✓
- BouncyCastle.Cryptography or Portable.BouncyCastle (conditional) ✓
MATCH: YES
```

### 2. Platform Support
Not applicable (no runtimes/ folder)

### 3. Example Verification

**Example 1: Generate Key and Create Keystore**
```
Source: tests/Nethereum.KeyStore.UnitTests/GenerateAndCreateKeyStoreFileTester.cs:9-17
Code matches: YES
Line numbers in README: NO (maintenance burden)
Validation tracked: YES
```

**Example 2: Custom Scrypt Parameters**
```
Source: Nethereum.Playground/wwwroot/samples/csharp/1021.txt:12-25
Code matches: YES
Line numbers in README: NO (maintenance burden)
Validation tracked: YES
Comment about "lower N for WASM" verified in source: YES
```

### 4. API Signature Verification
```
Read: src/Nethereum.KeyStore/KeyStoreScryptService.cs:1-68

Method: EncryptAndGenerateKeyStoreAsJson
Source: public string EncryptAndGenerateKeyStoreAsJson(string password, byte[] privateKey, string address)
README: public string EncryptAndGenerateKeyStoreAsJson(string password, byte[] privateKey, string address);
MATCH: YES

Method: DecryptKeyStoreFromJson
Source: public byte[] DecryptKeyStoreFromJson(string password, string json)
README: public byte[] DecryptKeyStoreFromJson(string password, string json);
MATCH: YES

Default parameters:
Source: new ScryptParams {Dklen = 32, N = 262144, R = 1, P = 8}; (line 35)
README: N = 262144, R = 1, P = 8, Dklen = 32
MATCH: YES
```

### 5. Removed Synthetic Content
```
- Lines 301-311: Password strength advice (patronizing)
- Lines 325-335: "Never Store Passwords" advice (patronizing)
- Line 353-355: License section
- Emojis throughout (✅, ❌)
```

### 6. Removed Marketing Claims
```
- "Compatible with all major Ethereum wallets" → Changed to "standard format used across Ethereum ecosystem"
- Removed wallet compatibility list (MEW, MetaMask, Geth) - this is about the standard, not specific tools
```

### 7. Technical Claims Verified
```
Claim: "AES-128-CTR encryption"
Source: KeyStoreCrypto.cs uses "aes-128-ctr" cipher
VERIFIED: YES

Claim: "Default N=262144"
Source: KeyStoreScryptService.cs:35 - GetDefaultParams()
VERIFIED: YES

Claim: "MAC prevents tampering"
Source: DecryptScrypt checks MAC before decryption
VERIFIED: YES
```

### Final Result
```
VALIDATION COMPLETE
Confidence: 100%
All examples from real tests: YES
All API signatures verified: YES
All technical claims verified: YES
No synthetic content: YES
No marketing BS: YES
Ready for training: YES
```

## Common Failure Patterns

### Failure 1: Synthetic Examples
**Symptom:** Example code not found in any test file
**Detection:** Search fails to find matching code in tests/
**Fix:** DELETE example or find real test

### Failure 2: Wrong Dependencies
**Symptom:** README lists packages not in .csproj
**Detection:** .csproj comparison fails
**Fix:** Update README to match .csproj exactly

### Failure 3: False Platform Support
**Symptom:** README claims platforms with no binaries
**Detection:** runtimes/ folder missing claimed files
**Fix:** Update README to list only actual platforms

### Failure 4: Hallucinated API
**Symptom:** README shows methods that don't exist
**Detection:** Source code search fails to find method
**Fix:** Remove or correct API documentation

### Failure 5: Patronizing Content
**Symptom:** Sections giving obvious advice
**Detection:** Manual review finds "password strength" type content
**Fix:** DELETE entire section

## Emergency Protocol

If validation reveals **multiple lies** in a README:

1. **STOP** - Do not continue with other packages
2. **Document all lies found**
3. **Analyze failure pattern** - Why did I hallucinate?
4. **Rewrite README from scratch** - Use only verified sources
5. **Complete validation checklist** - Verify every single claim
6. **Get user confirmation** - Show what was wrong and how fixed

## Success Metrics

Package validation is COMPLETE when:

1. Every dependency verified against .csproj
2. Every example cited from tests/playground
3. Every API signature matched to source
4. Every technical claim has source proof
5. Zero emojis
6. Zero license sections
7. Zero synthetic examples
8. Zero patronizing advice
9. Zero marketing claims
10. User confirms: "This is correct"

## Quality Gates

**CANNOT PROCEED** to next package until current package passes:
- [ ] Dependency verification
- [ ] Example citation verification
- [ ] API signature verification
- [ ] Technical claim verification
- [ ] Content quality check (no fluff)

**ONE HALLUCINATION = STOP EVERYTHING**

If you catch yourself about to guess, infer, or create a synthetic example: **STOP. READ THE SOURCE CODE FIRST.**
