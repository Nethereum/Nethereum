# EIP-7623 Implementation Plan

## Overview

EIP-7623 "Increase calldata cost" changes transaction gas accounting to establish a floor for calldata-heavy transactions.

---

## Part 1: Constants

### Spec
```
STANDARD_TOKEN_COST = 4
TOTAL_COST_FLOOR_PER_TOKEN = 10
```

### Current Code (GasConstants.cs)
```csharp
public const int TX_DATA_ZERO_GAS = 4;
public const int TX_DATA_NON_ZERO_GAS = 16;
```

### Required Changes
Add to `GasConstants.cs`:
```csharp
// EIP-7623: Calldata floor (Prague)
public const int STANDARD_TOKEN_COST = 4;  // Same as TX_DATA_ZERO_GAS
public const int TOTAL_COST_FLOOR_PER_TOKEN = 10;
```

**File:** `src/Nethereum.EVM/Gas/GasConstants.cs`

---

## Part 2: Token Calculation

### Spec
```
tokens_in_calldata = zero_bytes + (nonzero_bytes * 4)
```

Note: Each zero byte = 1 token, each non-zero byte = 4 tokens.

### Implementation
Create helper method:
```csharp
public static BigInteger CalculateTokensInCalldata(byte[] data)
{
    if (data == null || data.Length == 0)
        return 0;

    int zeroBytes = 0;
    int nonZeroBytes = 0;
    foreach (var b in data)
    {
        if (b == 0) zeroBytes++;
        else nonZeroBytes++;
    }
    return zeroBytes + (nonZeroBytes * 4);
}
```

**File:** `tests/Nethereum.EVM.UnitTests/GeneralStateTests/GeneralStateTestRunner.cs`

---

## Part 3: Transaction Validity Check (Pre-Execution)

### Spec
Transaction is INVALID if:
```
gas_limit < max(intrinsic_gas, 21000 + TOTAL_COST_FLOOR_PER_TOKEN * tokens)
```

### Current Code
The test runner checks:
```csharp
if (gasLimit < intrinsicGas) { /* reject */ }
```

### Required Changes
For Prague+, add floor check:
```csharp
var intrinsicGas = CalculateIntrinsicGas(...);
BigInteger minGasRequired = intrinsicGas;

if (IsPragueOrLater(targetHardfork))
{
    var tokens = CalculateTokensInCalldata(dataBytes);
    var floor = G_TRANSACTION + (TOTAL_COST_FLOOR_PER_TOKEN * tokens);
    if (isContractCreation)
        floor += G_TXCREATE;  // 32000 for contract creation base
    minGasRequired = BigInteger.Max(intrinsicGas, floor);
}

if (gasLimit < minGasRequired)
{
    // Transaction invalid - insufficient gas
}
```

**File:** `tests/Nethereum.EVM.UnitTests/GeneralStateTests/GeneralStateTestRunner.cs`
**Location:** Around line 294

---

## Part 4: Gas Used Calculation (Post-Execution)

### Spec
```
tx.gasUsed = 21000 + max(
    STANDARD_TOKEN_COST * tokens + execution_gas + contract_creation_cost,
    TOTAL_COST_FLOOR_PER_TOKEN * tokens
)
```

Where:
- `execution_gas` = EVM execution gas minus refunds
- `contract_creation_cost` = 32000 + 2 * words(initcode) for contract creations

### Current Code
Gas is calculated as:
```csharp
gasUsed = intrinsicGas + executionGasUsed - refund;
```

### Required Changes
After execution completes, apply floor:
```csharp
if (IsPragueOrLater(targetHardfork))
{
    var tokens = CalculateTokensInCalldata(dataBytes);

    // Standard cost portion (excluding base 21000)
    var standardCost = (STANDARD_TOKEN_COST * tokens) + executionGasUsed;
    if (isContractCreation)
    {
        int initcodeWords = (dataBytes.Length + 31) / 32;
        standardCost += G_TXCREATE + (initcodeWords * 2);
    }

    // Floor cost portion
    var floorCost = TOTAL_COST_FLOOR_PER_TOKEN * tokens;

    // Apply max
    var variableCost = BigInteger.Max(standardCost, floorCost);
    gasUsed = G_TRANSACTION + variableCost;

    // Ensure we don't exceed gas limit
    if (gasUsed > gasLimit)
        gasUsed = gasLimit;
}
```

**File:** `tests/Nethereum.EVM.UnitTests/GeneralStateTests/GeneralStateTestRunner.cs`
**Location:** After execution, before refund calculation

---

## Part 5: Refund Handling

### Spec
The `execution_gas_used` in the formula is "EVM execution gas minus refunds".

### Required Changes
Refunds should be subtracted from `execution_gas_used` BEFORE the max() comparison:
```csharp
var executionGasAfterRefund = executionGasUsed - refund;
// Then use executionGasAfterRefund in the formula
```

---

## Part 6: Failed Execution Handling

### Spec
When execution fails, all gas is consumed (no refunds apply).

### Current Behavior
On failure: `gasUsed = gasLimit`

### Required Changes
For Prague+, even on failure, the floor should be respected:
```csharp
if (!executionSuccess)
{
    gasUsed = gasLimit;  // All gas consumed
    // Floor is automatically satisfied since gasLimit >= floor (validated pre-execution)
}
```

No change needed - the pre-execution validation ensures gasLimit >= floor.

---

## Implementation Order

1. **Add constants** to GasConstants.cs
2. **Add helper method** CalculateTokensInCalldata()
3. **Update pre-execution check** (Part 3) - validate gas limit against floor
4. **Update post-execution calculation** (Part 4) - apply max() formula
5. **Test with stackOverflowM1PUSH** - verify fix
6. **Run full stStackTests** - check for regressions
7. **Run full Prague test suite** - ensure no other tests break

---

## Test Cases to Verify

| Test | Expected Behavior |
|------|-------------------|
| stackOverflowM1PUSH | All 31 variants should pass |
| stackOverflow | Should still pass |
| stackOverflowDUP | Should still pass |
| stackOverflowM1 | Should still pass |
| stackOverflowM1DUP | Should still pass |
| stackOverflowPUSH | Should still pass |
| stackOverflowSWAP | Should still pass |
| Other Prague tests | No regressions |

---

## Verification Steps

1. Compare Cancun vs Prague expected state roots in test files
2. Calculate expected gas difference based on EIP-7623 formula
3. Verify sender balance difference matches gas calculation
4. Ensure intrinsic gas validation rejects invalid transactions

---

## Files to Modify

1. `src/Nethereum.EVM/Gas/GasConstants.cs` - Add EIP-7623 constants
2. `tests/Nethereum.EVM.UnitTests/GeneralStateTests/GeneralStateTestRunner.cs`:
   - Add CalculateTokensInCalldata() helper
   - Update pre-execution gas validation
   - Update post-execution gas calculation
