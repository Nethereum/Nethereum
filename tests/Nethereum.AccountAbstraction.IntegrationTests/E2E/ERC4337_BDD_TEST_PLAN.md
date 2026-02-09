# ERC-4337 Account Abstraction BDD Test Plan

## Overview

This test plan follows the BDD (Behavior-Driven Development) pattern using the EIP-spec-test skill structure for comprehensive ERC-4337 specification compliance testing.

**Target:** `Nethereum.AccountAbstraction.IntegrationTests.E2E`
**Fixture:** `DevChainBundlerFixture`

---

## Test Naming Convention

```
Given_[Precondition]_When_[Action]_Then_[ExpectedOutcome]
```

## Test Attributes Pattern

```csharp
[Fact]
[Trait("Category", "ERC4337-Validation")]
[Trait("ErrorCode", "AA25")]  // The specific error code being tested
```

---

# 1. AA Error Codes

## 1.1 AA11 - Sender Already Constructed

### Scenario: InitCode provided for already-deployed account

```csharp
[Fact]
[Trait("Category", "ERC4337-Validation")]
[Trait("ErrorCode", "AA11")]
public async Task Given_AccountAlreadyDeployed_When_InitCodeProvided_Then_RevertsWithAA11()
{
    // GIVEN: An account that is already deployed on-chain
    var accountKey = EthECKey.GenerateKey();
    var ownerAddress = accountKey.GetPublicAddress();
    var accountAddress = await DeployAccountAsync(ownerAddress, salt: 1101);

    // Verify account is deployed
    var code = await _fixture.GetCodeAsync(accountAddress);
    Assert.True(code.Length > 0, "Precondition: Account must be deployed");

    // WHEN: Submitting a UserOp with initCode for the already-deployed account
    var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt: 1101);
    var userOp = new UserOperation
    {
        Sender = accountAddress,
        Nonce = 1,  // Next nonce after deployment
        InitCode = initCode,  // Should not be provided for deployed account
        CallData = Array.Empty<byte>(),
        CallGasLimit = 50000,
        VerificationGasLimit = 500000,
        PreVerificationGas = 50000,
        MaxFeePerGas = 2000000000,
        MaxPriorityFeePerGas = 1000000000
    };

    var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

    // THEN: Bundler rejects with AA11 or EntryPoint reverts with FailedOp(AA11)
    var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
    {
        await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
    });

    Assert.Contains("AA11", ex.Message);
}
```

---

## 1.2 AA13 - InitCode Failed or OOG

### Scenario: Factory reverts during account creation

```csharp
[Fact]
[Trait("Category", "ERC4337-Validation")]
[Trait("ErrorCode", "AA13")]
public async Task Given_FactoryReverts_When_InitCodeExecuted_Then_RevertsWithAA13()
{
    // GIVEN: A UserOp with initCode pointing to a factory that will revert
    var accountKey = EthECKey.GenerateKey();
    var ownerAddress = accountKey.GetPublicAddress();

    // Deploy a "bad factory" that always reverts
    var badFactoryAddress = await DeployRevertingFactoryAsync();

    // Create initCode that calls the bad factory
    var initCode = BuildInitCode(badFactoryAddress, ownerAddress, salt: 1301);
    var accountAddress = ComputeAccountAddress(badFactoryAddress, ownerAddress, salt: 1301);

    await _fixture.FundAccountAsync(accountAddress, 3m);

    var userOp = new UserOperation
    {
        Sender = accountAddress,
        Nonce = 0,
        InitCode = initCode,
        CallData = Array.Empty<byte>(),
        CallGasLimit = 50000,
        VerificationGasLimit = 500000,
        PreVerificationGas = 50000,
        MaxFeePerGas = 2000000000,
        MaxPriorityFeePerGas = 1000000000
    };

    var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

    // WHEN: Submitting the UserOp
    // THEN: Should fail with AA13 (initCode failed)
    var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
    {
        await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
    });

    Assert.Contains("AA13", ex.Message);
}
```

### Scenario: InitCode runs out of gas

```csharp
[Fact]
[Trait("Category", "ERC4337-Validation")]
[Trait("ErrorCode", "AA13")]
public async Task Given_InitCodeOOG_When_VerificationGasInsufficient_Then_RevertsWithAA13()
{
    // GIVEN: A UserOp with insufficient verificationGasLimit for initCode execution
    var accountKey = EthECKey.GenerateKey();
    var ownerAddress = accountKey.GetPublicAddress();
    var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt: 1302);

    await _fixture.FundAccountAsync(accountAddress, 3m);

    var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt: 1302);

    var userOp = new UserOperation
    {
        Sender = accountAddress,
        Nonce = 0,
        InitCode = initCode,
        CallData = Array.Empty<byte>(),
        CallGasLimit = 50000,
        VerificationGasLimit = 100,  // Way too low for contract deployment
        PreVerificationGas = 50000,
        MaxFeePerGas = 2000000000,
        MaxPriorityFeePerGas = 1000000000
    };

    var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

    // WHEN: Submitting the UserOp
    // THEN: Should fail with AA13 (initCode OOG)
    var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
    {
        await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
    });

    Assert.Contains("AA13", ex.Message);
}
```

---

## 1.3 AA22 - Expired Signature (validUntil in past)

### Scenario: UserOp with expired validUntil timestamp

```csharp
[Fact]
[Trait("Category", "ERC4337-Validation")]
[Trait("ErrorCode", "AA22")]
public async Task Given_ValidUntilInPast_When_UserOpSubmitted_Then_RevertsWithAA22()
{
    // GIVEN: A deployed account with a UserOp that has expired (validUntil < now)
    var accountKey = EthECKey.GenerateKey();
    var ownerAddress = accountKey.GetPublicAddress();
    var accountAddress = await DeployAccountAsync(ownerAddress, salt: 2201);

    await _fixture.FundAccountAsync(accountAddress, 3m);

    // Create UserOp with validUntil in the past (1 hour ago)
    var pastTimestamp = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds();

    var userOp = new UserOperation
    {
        Sender = accountAddress,
        Nonce = 1,
        CallData = Array.Empty<byte>(),
        CallGasLimit = 50000,
        VerificationGasLimit = 200000,
        PreVerificationGas = 50000,
        MaxFeePerGas = 2000000000,
        MaxPriorityFeePerGas = 1000000000
    };

    // Sign with validUntil timestamp embedded in signature/validation data
    var packedOp = await SignWithValidationData(userOp, accountKey, validAfter: 0, validUntil: pastTimestamp);

    // WHEN: Submitting the expired UserOp
    // THEN: Should fail with AA22 (expired)
    var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
    {
        await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
    });

    Assert.Contains("AA22", ex.Message);
}
```

---

## 1.4 AA23 - Not Yet Valid (validAfter in future)

### Scenario: UserOp with validAfter timestamp in the future

```csharp
[Fact]
[Trait("Category", "ERC4337-Validation")]
[Trait("ErrorCode", "AA23")]
public async Task Given_ValidAfterInFuture_When_UserOpSubmitted_Then_RevertsWithAA23()
{
    // GIVEN: A deployed account with a UserOp that is not yet valid (validAfter > now)
    var accountKey = EthECKey.GenerateKey();
    var ownerAddress = accountKey.GetPublicAddress();
    var accountAddress = await DeployAccountAsync(ownerAddress, salt: 2301);

    await _fixture.FundAccountAsync(accountAddress, 3m);

    // Create UserOp with validAfter in the future (1 hour from now)
    var futureTimestamp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();

    var userOp = new UserOperation
    {
        Sender = accountAddress,
        Nonce = 1,
        CallData = Array.Empty<byte>(),
        CallGasLimit = 50000,
        VerificationGasLimit = 200000,
        PreVerificationGas = 50000,
        MaxFeePerGas = 2000000000,
        MaxPriorityFeePerGas = 1000000000
    };

    // Sign with validAfter timestamp embedded in signature/validation data
    var packedOp = await SignWithValidationData(userOp, accountKey, validAfter: futureTimestamp, validUntil: 0);

    // WHEN: Submitting the not-yet-valid UserOp
    // THEN: Should fail with AA23 (not yet valid)
    var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
    {
        await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
    });

    Assert.Contains("AA23", ex.Message);
}
```

---

## 1.5 AA41 - Insufficient Verification Gas (Account Validation OOG)

### Scenario: Account validateUserOp runs out of gas

```csharp
[Fact]
[Trait("Category", "ERC4337-Validation")]
[Trait("ErrorCode", "AA41")]
public async Task Given_InsufficientVerificationGas_When_AccountValidates_Then_RevertsWithAA41()
{
    // GIVEN: A deployed account with a UserOp that has too little verificationGasLimit
    var accountKey = EthECKey.GenerateKey();
    var ownerAddress = accountKey.GetPublicAddress();
    var accountAddress = await DeployAccountAsync(ownerAddress, salt: 4101);

    await _fixture.FundAccountAsync(accountAddress, 3m);

    var userOp = new UserOperation
    {
        Sender = accountAddress,
        Nonce = 1,
        CallData = Array.Empty<byte>(),
        CallGasLimit = 50000,
        VerificationGasLimit = 1000,  // Too low for ECDSA verification
        PreVerificationGas = 50000,
        MaxFeePerGas = 2000000000,
        MaxPriorityFeePerGas = 1000000000
    };

    var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

    // WHEN: Submitting the UserOp with insufficient verification gas
    // THEN: Should fail with AA41 (verification OOG)
    var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
    {
        await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
    });

    Assert.Contains("AA41", ex.Message);
}
```

---

## 1.6 AA42 - Insufficient Call Gas (Execution OOG)

### Scenario: Account execution runs out of gas

```csharp
[Fact]
[Trait("Category", "ERC4337-Validation")]
[Trait("ErrorCode", "AA42")]
public async Task Given_InsufficientCallGas_When_ExecutionRuns_Then_RevertsWithAA42()
{
    // GIVEN: A deployed account with callData that requires more gas than callGasLimit
    var accountKey = EthECKey.GenerateKey();
    var ownerAddress = accountKey.GetPublicAddress();
    var accountAddress = await DeployAccountAsync(ownerAddress, salt: 4201);

    await _fixture.FundAccountAsync(accountAddress, 3m);

    // Create callData that performs expensive operations (e.g., deploy a contract)
    var expensiveCallData = BuildExpensiveCallData();

    var userOp = new UserOperation
    {
        Sender = accountAddress,
        Nonce = 1,
        CallData = expensiveCallData,
        CallGasLimit = 1000,  // Way too low for the expensive operation
        VerificationGasLimit = 200000,
        PreVerificationGas = 50000,
        MaxFeePerGas = 2000000000,
        MaxPriorityFeePerGas = 1000000000
    };

    var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

    // WHEN: Submitting the UserOp
    await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
    var result = await _fixture.BundlerService.ExecuteBundleAsync();

    // THEN: Execution fails with AA42 or UserOperationRevertReason event
    Assert.False(result?.Success ?? true);
    // Or check for UserOperationRevertReasonEventDTO
}
```

---

## 1.7 AA51 - Paymaster Deposit Too Low

### Scenario: Paymaster has insufficient EntryPoint deposit

```csharp
[Fact]
[Trait("Category", "ERC4337-Validation")]
[Trait("ErrorCode", "AA51")]
public async Task Given_PaymasterLowDeposit_When_SponsoringOp_Then_RevertsWithAA51()
{
    // GIVEN: A paymaster with very low deposit (not enough to cover gas)
    var paymasterService = await DeployTestPaymasterAsync();

    // Deposit only a tiny amount (not enough for any operation)
    await paymasterService.DepositRequestAndWaitForReceiptAsync(
        new DepositFunction { AmountToSend = 1 });  // 1 wei

    var accountKey = EthECKey.GenerateKey();
    var ownerAddress = accountKey.GetPublicAddress();
    var accountAddress = await DeployAccountAsync(ownerAddress, salt: 5101);

    var userOp = new UserOperation
    {
        Sender = accountAddress,
        Nonce = 1,
        CallData = Array.Empty<byte>(),
        CallGasLimit = 50000,
        VerificationGasLimit = 200000,
        PreVerificationGas = 50000,
        MaxFeePerGas = 2000000000,
        MaxPriorityFeePerGas = 1000000000,
        Paymaster = paymasterService.ContractAddress,
        PaymasterVerificationGasLimit = 100000,
        PaymasterPostOpGasLimit = 100000,
        PaymasterData = Array.Empty<byte>()
    };

    var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

    // WHEN: Submitting the UserOp
    // THEN: Should fail with AA51 (paymaster deposit too low)
    var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
    {
        await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
    });

    Assert.Contains("AA51", ex.Message);
}
```

---

## 1.8 AA52 - Paymaster Validation Failed

### Scenario: Paymaster validatePaymasterUserOp returns failure

```csharp
[Fact]
[Trait("Category", "ERC4337-Validation")]
[Trait("ErrorCode", "AA52")]
public async Task Given_PaymasterRejectsOp_When_Validating_Then_RevertsWithAA52()
{
    // GIVEN: A paymaster that rejects operations (returns SIG_VALIDATION_FAILED)
    var rejectingPaymasterService = await DeployRejectingPaymasterAsync();

    await rejectingPaymasterService.DepositRequestAndWaitForReceiptAsync(
        new DepositFunction { AmountToSend = Web3.Convert.ToWei(10) });

    var accountKey = EthECKey.GenerateKey();
    var ownerAddress = accountKey.GetPublicAddress();
    var accountAddress = await DeployAccountAsync(ownerAddress, salt: 5201);

    var userOp = new UserOperation
    {
        Sender = accountAddress,
        Nonce = 1,
        CallData = Array.Empty<byte>(),
        CallGasLimit = 50000,
        VerificationGasLimit = 200000,
        PreVerificationGas = 50000,
        MaxFeePerGas = 2000000000,
        MaxPriorityFeePerGas = 1000000000,
        Paymaster = rejectingPaymasterService.ContractAddress,
        PaymasterVerificationGasLimit = 100000,
        PaymasterPostOpGasLimit = 100000,
        PaymasterData = Array.Empty<byte>()
    };

    var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

    // WHEN: Submitting the UserOp
    // THEN: Should fail with AA52 (paymaster validation failed)
    var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
    {
        await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
    });

    Assert.Contains("AA52", ex.Message);
}
```

---

## 1.9 AA53 - Paymaster PostOp Failed

### Scenario: Paymaster postOp reverts during execution

```csharp
[Fact]
[Trait("Category", "ERC4337-Validation")]
[Trait("ErrorCode", "AA53")]
public async Task Given_PaymasterPostOpReverts_When_ExecutingOp_Then_EmitsAA53Event()
{
    // GIVEN: A paymaster that passes validation but reverts in postOp
    var revertingPostOpPaymasterService = await DeployRevertingPostOpPaymasterAsync();

    await revertingPostOpPaymasterService.DepositRequestAndWaitForReceiptAsync(
        new DepositFunction { AmountToSend = Web3.Convert.ToWei(10) });

    var accountKey = EthECKey.GenerateKey();
    var ownerAddress = accountKey.GetPublicAddress();
    var accountAddress = await DeployAccountAsync(ownerAddress, salt: 5301);

    var userOp = new UserOperation
    {
        Sender = accountAddress,
        Nonce = 1,
        CallData = Array.Empty<byte>(),
        CallGasLimit = 50000,
        VerificationGasLimit = 200000,
        PreVerificationGas = 50000,
        MaxFeePerGas = 2000000000,
        MaxPriorityFeePerGas = 1000000000,
        Paymaster = revertingPostOpPaymasterService.ContractAddress,
        PaymasterVerificationGasLimit = 100000,
        PaymasterPostOpGasLimit = 100000,
        PaymasterData = Array.Empty<byte>()
    };

    var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

    // WHEN: The operation passes validation but postOp reverts
    await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
    var result = await _fixture.BundlerService.ExecuteBundleAsync();

    // THEN: Transaction completes but with PostOpRevertReason event (AA53)
    var receipt = result.TransactionReceipt;
    var postOpRevertEvents = receipt.DecodeAllEvents<PostOpRevertReasonEventDTO>();
    Assert.True(postOpRevertEvents.Count > 0, "PostOp should have reverted with AA53");
}
```

---

# 2. Timestamp Validation

## 2.1 Valid Time Window

### Scenario: UserOp within valid time window succeeds

```csharp
[Fact]
[Trait("Category", "ERC4337-Validation")]
[Trait("Feature", "TimestampValidation")]
public async Task Given_ValidTimeWindow_When_WithinWindow_Then_Succeeds()
{
    // GIVEN: A UserOp with validAfter < now < validUntil
    var accountKey = EthECKey.GenerateKey();
    var ownerAddress = accountKey.GetPublicAddress();
    var accountAddress = await DeployAccountAsync(ownerAddress, salt: 2001);

    await _fixture.FundAccountAsync(accountAddress, 3m);

    var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    var validAfter = now - 3600;   // 1 hour ago
    var validUntil = now + 3600;   // 1 hour from now

    var userOp = new UserOperation
    {
        Sender = accountAddress,
        Nonce = 1,
        CallData = Array.Empty<byte>(),
        CallGasLimit = 50000,
        VerificationGasLimit = 200000,
        PreVerificationGas = 50000,
        MaxFeePerGas = 2000000000,
        MaxPriorityFeePerGas = 1000000000
    };

    var packedOp = await SignWithValidationData(userOp, accountKey, validAfter, validUntil);

    // WHEN: Submitting within the valid time window
    await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
    var result = await _fixture.BundlerService.ExecuteBundleAsync();

    // THEN: Operation succeeds
    Assert.True(result?.Success, "Operation within valid time window should succeed");
}
```

### Scenario: Zero timestamps mean no time restriction

```csharp
[Fact]
[Trait("Category", "ERC4337-Validation")]
[Trait("Feature", "TimestampValidation")]
public async Task Given_ZeroTimestamps_When_Submitted_Then_Succeeds()
{
    // GIVEN: A UserOp with validAfter=0 and validUntil=0 (no time restrictions)
    var accountKey = EthECKey.GenerateKey();
    var ownerAddress = accountKey.GetPublicAddress();
    var accountAddress = await DeployAccountAsync(ownerAddress, salt: 2002);

    await _fixture.FundAccountAsync(accountAddress, 3m);

    var userOp = new UserOperation
    {
        Sender = accountAddress,
        Nonce = 1,
        CallData = Array.Empty<byte>(),
        CallGasLimit = 50000,
        VerificationGasLimit = 200000,
        PreVerificationGas = 50000,
        MaxFeePerGas = 2000000000,
        MaxPriorityFeePerGas = 1000000000
    };

    // validAfter=0, validUntil=0 means always valid
    var packedOp = await SignWithValidationData(userOp, accountKey, validAfter: 0, validUntil: 0);

    // WHEN: Submitting with no time restrictions
    await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
    var result = await _fixture.BundlerService.ExecuteBundleAsync();

    // THEN: Operation succeeds
    Assert.True(result?.Success, "Operation with no time restrictions should succeed");
}
```

---

# 3. Paymaster Failures

## 3.1 Paymaster Deposit Exhausted Mid-Bundle

### Scenario: Paymaster runs out of deposit during batch execution

```csharp
[Fact]
[Trait("Category", "ERC4337-Validation")]
[Trait("Feature", "PaymasterDeposit")]
public async Task Given_PaymasterLimitedDeposit_When_BatchExhaustsDeposit_Then_LaterOpsFail()
{
    // GIVEN: A paymaster with deposit sufficient for only 1 operation
    var paymasterService = await DeployTestPaymasterAsync();

    // Deposit enough for ~1 operation only
    await paymasterService.DepositRequestAndWaitForReceiptAsync(
        new DepositFunction { AmountToSend = Web3.Convert.ToWei(0.01m) });

    var packedOps = new List<PackedUserOperation>();

    // Create 3 operations, all using the same paymaster
    for (int i = 0; i < 3; i++)
    {
        var accountKey = EthECKey.GenerateKey();
        var ownerAddress = accountKey.GetPublicAddress();
        var accountAddress = await DeployAccountAsync(ownerAddress, salt: (ulong)(3100 + i));

        var userOp = new UserOperation
        {
            Sender = accountAddress,
            Nonce = 1,
            CallData = Array.Empty<byte>(),
            CallGasLimit = 100000,
            VerificationGasLimit = 200000,
            PreVerificationGas = 50000,
            MaxFeePerGas = 2000000000,
            MaxPriorityFeePerGas = 1000000000,
            Paymaster = paymasterService.ContractAddress,
            PaymasterVerificationGasLimit = 100000,
            PaymasterPostOpGasLimit = 100000,
            PaymasterData = Array.Empty<byte>()
        };

        var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);
        packedOps.Add(packedOp);
    }

    // WHEN: Submitting all operations
    foreach (var op in packedOps)
    {
        try
        {
            await _fixture.BundlerService.SendUserOperationAsync(op, _fixture.EntryPointService.ContractAddress);
        }
        catch (InvalidOperationException)
        {
            // Expected for later ops when deposit is exhausted
        }
    }

    // THEN: First operation may succeed, later ones should fail with AA51
    // (Exact behavior depends on bundler preflight simulation)
}
```

---

# 4. Factory/InitCode Failures

## 4.1 Factory Not Deployed

### Scenario: InitCode references non-existent factory

```csharp
[Fact]
[Trait("Category", "ERC4337-Validation")]
[Trait("ErrorCode", "AA13")]
public async Task Given_FactoryNotDeployed_When_InitCodeExecuted_Then_RevertsWithAA13()
{
    // GIVEN: InitCode pointing to a non-existent factory address
    var accountKey = EthECKey.GenerateKey();
    var ownerAddress = accountKey.GetPublicAddress();

    var nonExistentFactory = "0x0000000000000000000000000000000000dead01";
    var initCode = BuildInitCode(nonExistentFactory, ownerAddress, salt: 4001);
    var accountAddress = "0x" + Sha3Keccack.Current.CalculateHash(initCode).ToHex().Substring(24);

    await _fixture.FundAccountAsync(accountAddress, 3m);

    var userOp = new UserOperation
    {
        Sender = accountAddress,
        Nonce = 0,
        InitCode = initCode,
        CallData = Array.Empty<byte>(),
        CallGasLimit = 50000,
        VerificationGasLimit = 500000,
        PreVerificationGas = 50000,
        MaxFeePerGas = 2000000000,
        MaxPriorityFeePerGas = 1000000000
    };

    var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

    // WHEN: Submitting with non-existent factory
    // THEN: Should fail with AA13 (initCode failed - no code at factory)
    var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
    {
        await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
    });

    Assert.Contains("AA13", ex.Message);
}
```

## 4.2 Factory Returns Wrong Address

### Scenario: Factory creates account at different address than sender

```csharp
[Fact]
[Trait("Category", "ERC4337-Validation")]
[Trait("ErrorCode", "AA14")]
public async Task Given_FactoryReturnsWrongAddress_When_InitCodeExecuted_Then_RevertsWithAA14()
{
    // GIVEN: A factory that creates an account at a different address than expected
    var wrongAddressFactoryService = await DeployWrongAddressFactoryAsync();

    var accountKey = EthECKey.GenerateKey();
    var ownerAddress = accountKey.GetPublicAddress();

    // Factory will create at a different address than what we claim as sender
    var claimedSender = "0x1111111111111111111111111111111111111111";
    var initCode = wrongAddressFactoryService.GetCreateAccountInitCode(ownerAddress, salt: 4201);

    await _fixture.FundAccountAsync(claimedSender, 3m);

    var userOp = new UserOperation
    {
        Sender = claimedSender,  // This won't match where factory actually creates
        Nonce = 0,
        InitCode = initCode,
        CallData = Array.Empty<byte>(),
        CallGasLimit = 50000,
        VerificationGasLimit = 500000,
        PreVerificationGas = 50000,
        MaxFeePerGas = 2000000000,
        MaxPriorityFeePerGas = 1000000000
    };

    var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

    // WHEN: Submitting where factory creates at wrong address
    // THEN: Should fail with AA14 (initCode must return sender)
    var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
    {
        await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
    });

    Assert.Contains("AA14", ex.Message);
}
```

---

# 5. Execution Failures

## 5.1 Account Execution Reverts

### Scenario: CallData execution reverts but doesn't abort bundle

```csharp
[Fact]
[Trait("Category", "ERC4337-Execution")]
[Trait("Feature", "ExecutionRevert")]
public async Task Given_CallDataReverts_When_Executed_Then_EmitsRevertEventButBundleSucceeds()
{
    // GIVEN: A deployed account with callData that will revert
    var accountKey = EthECKey.GenerateKey();
    var ownerAddress = accountKey.GetPublicAddress();
    var accountAddress = await DeployAccountAsync(ownerAddress, salt: 5001);

    await _fixture.FundAccountAsync(accountAddress, 3m);

    // CallData that calls a reverting function
    var revertingCallData = BuildRevertingCallData();

    var userOp = new UserOperation
    {
        Sender = accountAddress,
        Nonce = 1,
        CallData = revertingCallData,
        CallGasLimit = 100000,
        VerificationGasLimit = 200000,
        PreVerificationGas = 50000,
        MaxFeePerGas = 2000000000,
        MaxPriorityFeePerGas = 1000000000
    };

    var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

    // WHEN: Executing the operation
    await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
    var result = await _fixture.BundlerService.ExecuteBundleAsync();

    // THEN: Bundle transaction succeeds, but UserOp emits revert event
    Assert.NotNull(result?.TransactionReceipt);
    Assert.Equal((BigInteger)1, result.TransactionReceipt.Status.Value);

    var revertEvents = result.TransactionReceipt.DecodeAllEvents<UserOperationRevertReasonEventDTO>();
    Assert.True(revertEvents.Count > 0, "Should emit UserOperationRevertReason event");
}
```

## 5.2 Partial Batch Success

### Scenario: Some operations in batch succeed, others fail

```csharp
[Fact]
[Trait("Category", "ERC4337-Execution")]
[Trait("Feature", "PartialBatch")]
public async Task Given_MixedBatch_When_Executed_Then_SuccessfulOpsComplete()
{
    // GIVEN: A batch with one valid operation and one that will fail execution
    var key1 = EthECKey.GenerateKey();
    var key2 = EthECKey.GenerateKey();

    var account1 = await DeployAccountAsync(key1.GetPublicAddress(), salt: 5201);
    var account2 = await DeployAccountAsync(key2.GetPublicAddress(), salt: 5202);

    await _fixture.FundAccountAsync(account1, 3m);
    await _fixture.FundAccountAsync(account2, 3m);

    // Op1: Valid, will succeed
    var userOp1 = new UserOperation
    {
        Sender = account1,
        Nonce = 1,
        CallData = Array.Empty<byte>(),
        CallGasLimit = 50000,
        VerificationGasLimit = 200000,
        PreVerificationGas = 50000,
        MaxFeePerGas = 2000000000,
        MaxPriorityFeePerGas = 1000000000
    };

    // Op2: Will revert during execution
    var userOp2 = new UserOperation
    {
        Sender = account2,
        Nonce = 1,
        CallData = BuildRevertingCallData(),
        CallGasLimit = 50000,
        VerificationGasLimit = 200000,
        PreVerificationGas = 50000,
        MaxFeePerGas = 2000000000,
        MaxPriorityFeePerGas = 1000000000
    };

    var packedOp1 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp1, key1);
    var packedOp2 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp2, key2);

    // WHEN: Submitting both operations
    await _fixture.BundlerService.SendUserOperationAsync(packedOp1, _fixture.EntryPointService.ContractAddress);
    await _fixture.BundlerService.SendUserOperationAsync(packedOp2, _fixture.EntryPointService.ContractAddress);
    var result = await _fixture.BundlerService.ExecuteBundleAsync();

    // THEN: Bundle succeeds, op1 succeeds, op2 emits revert event
    Assert.NotNull(result?.TransactionReceipt);

    var successEvents = result.TransactionReceipt.DecodeAllEvents<UserOperationEventEventDTO>();
    var revertEvents = result.TransactionReceipt.DecodeAllEvents<UserOperationRevertReasonEventDTO>();

    Assert.True(successEvents.Count >= 1, "At least one operation should succeed");
}
```

---

# 6. Aggregator Support

## 6.1 Basic Aggregator Validation

### Scenario: UserOps with aggregated signature

```csharp
[Fact]
[Trait("Category", "ERC4337-Aggregator")]
[Trait("Feature", "AggregatedSignature")]
public async Task Given_AggregatedUserOps_When_ValidAggregateSignature_Then_AllSucceed()
{
    // GIVEN: Multiple UserOps using the same aggregator with a valid aggregate signature
    var aggregatorService = await DeployBLSAggregatorAsync();

    var keys = new[] { EthECKey.GenerateKey(), EthECKey.GenerateKey() };
    var accounts = new List<string>();
    var userOps = new List<UserOperation>();

    for (int i = 0; i < 2; i++)
    {
        var accountAddress = await DeployAccountWithAggregatorAsync(
            keys[i].GetPublicAddress(),
            aggregatorService.ContractAddress,
            salt: (ulong)(6001 + i));
        accounts.Add(accountAddress);
        await _fixture.FundAccountAsync(accountAddress, 3m);

        userOps.Add(new UserOperation
        {
            Sender = accountAddress,
            Nonce = 1,
            CallData = Array.Empty<byte>(),
            CallGasLimit = 50000,
            VerificationGasLimit = 200000,
            PreVerificationGas = 50000,
            MaxFeePerGas = 2000000000,
            MaxPriorityFeePerGas = 1000000000
        });
    }

    // Create aggregate signature for all operations
    var aggregateSignature = await CreateAggregateSignatureAsync(aggregatorService, userOps, keys);
    var packedOps = await PackOpsWithAggregatorAsync(userOps, aggregatorService.ContractAddress, aggregateSignature);

    // WHEN: Submitting aggregated operations via handleAggregatedOps
    var handleAggregatedOpsFunction = new HandleAggregatedOpsFunction
    {
        OpsPerAggregator = new List<UserOpsPerAggregator>
        {
            new UserOpsPerAggregator
            {
                Aggregator = aggregatorService.ContractAddress,
                UserOps = packedOps,
                Signature = aggregateSignature
            }
        },
        Beneficiary = _fixture.BundlerAccount.Address
    };

    var receipt = await _fixture.EntryPointService.HandleAggregatedOpsRequestAndWaitForReceiptAsync(handleAggregatedOpsFunction);

    // THEN: All operations succeed
    Assert.Equal((BigInteger)1, receipt.Status.Value);

    var successEvents = receipt.DecodeAllEvents<UserOperationEventEventDTO>();
    Assert.Equal(2, successEvents.Count);
}
```

## 6.2 Invalid Aggregate Signature

### Scenario: Aggregator rejects invalid aggregate signature

```csharp
[Fact]
[Trait("Category", "ERC4337-Aggregator")]
[Trait("ErrorCode", "AA96")]
public async Task Given_InvalidAggregateSignature_When_Validated_Then_RevertsWithAA96()
{
    // GIVEN: UserOps with an invalid aggregate signature
    var aggregatorService = await DeployBLSAggregatorAsync();

    var accountKey = EthECKey.GenerateKey();
    var accountAddress = await DeployAccountWithAggregatorAsync(
        accountKey.GetPublicAddress(),
        aggregatorService.ContractAddress,
        salt: 6201);
    await _fixture.FundAccountAsync(accountAddress, 3m);

    var userOp = new UserOperation
    {
        Sender = accountAddress,
        Nonce = 1,
        CallData = Array.Empty<byte>(),
        CallGasLimit = 50000,
        VerificationGasLimit = 200000,
        PreVerificationGas = 50000,
        MaxFeePerGas = 2000000000,
        MaxPriorityFeePerGas = 1000000000
    };

    // Use an invalid signature (random bytes)
    var invalidSignature = new byte[64];
    new Random().NextBytes(invalidSignature);

    var packedOps = await PackOpsWithAggregatorAsync(
        new[] { userOp },
        aggregatorService.ContractAddress,
        invalidSignature);

    // WHEN: Submitting with invalid aggregate signature
    // THEN: Should fail with AA96 (aggregator validation failed)
    await Assert.ThrowsAsync<SmartContractCustomErrorRevertException>(async () =>
    {
        var handleAggregatedOpsFunction = new HandleAggregatedOpsFunction
        {
            OpsPerAggregator = new List<UserOpsPerAggregator>
            {
                new UserOpsPerAggregator
                {
                    Aggregator = aggregatorService.ContractAddress,
                    UserOps = packedOps,
                    Signature = invalidSignature
                }
            },
            Beneficiary = _fixture.BundlerAccount.Address
        };

        await _fixture.EntryPointService.HandleAggregatedOpsRequestAndWaitForReceiptAsync(handleAggregatedOpsFunction);
    });
}
```

---

# 7. Mempool Operations

## 7.1 Operation Replacement (Higher Gas)

### Scenario: Replace pending operation with higher gas price

```csharp
[Fact]
[Trait("Category", "ERC4337-Mempool")]
[Trait("Feature", "Replacement")]
public async Task Given_PendingOperation_When_ReplacedWithHigherGas_Then_NewOpAccepted()
{
    // GIVEN: A pending operation in the mempool
    var accountKey = EthECKey.GenerateKey();
    var ownerAddress = accountKey.GetPublicAddress();
    var accountAddress = await DeployAccountAsync(ownerAddress, salt: 7001);

    await _fixture.FundAccountAsync(accountAddress, 5m);

    var originalOp = new UserOperation
    {
        Sender = accountAddress,
        Nonce = 1,
        CallData = Array.Empty<byte>(),
        CallGasLimit = 50000,
        VerificationGasLimit = 200000,
        PreVerificationGas = 50000,
        MaxFeePerGas = 1000000000,  // 1 Gwei
        MaxPriorityFeePerGas = 500000000
    };

    var packedOriginal = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(originalOp, accountKey);
    await _fixture.BundlerService.SendUserOperationAsync(packedOriginal, _fixture.EntryPointService.ContractAddress);

    // WHEN: Submitting replacement with higher gas (same sender, same nonce)
    var replacementOp = new UserOperation
    {
        Sender = accountAddress,
        Nonce = 1,  // Same nonce
        CallData = new byte[] { 0x01 },  // Different calldata
        CallGasLimit = 50000,
        VerificationGasLimit = 200000,
        PreVerificationGas = 50000,
        MaxFeePerGas = 1500000000,  // 1.5 Gwei (50% higher)
        MaxPriorityFeePerGas = 750000000
    };

    var packedReplacement = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(replacementOp, accountKey);
    await _fixture.BundlerService.SendUserOperationAsync(packedReplacement, _fixture.EntryPointService.ContractAddress);

    // THEN: Replacement is accepted and original is dropped
    var result = await _fixture.BundlerService.ExecuteBundleAsync();
    Assert.True(result?.Success);

    // Verify the replacement was executed (check calldata hash or similar)
}
```

## 7.2 Duplicate Rejection

### Scenario: Reject duplicate operation (same hash)

```csharp
[Fact]
[Trait("Category", "ERC4337-Mempool")]
[Trait("Feature", "DuplicateRejection")]
public async Task Given_OperationInMempool_When_DuplicateSubmitted_Then_Rejected()
{
    // GIVEN: An operation already in the mempool
    var accountKey = EthECKey.GenerateKey();
    var ownerAddress = accountKey.GetPublicAddress();
    var accountAddress = await DeployAccountAsync(ownerAddress, salt: 7201);

    await _fixture.FundAccountAsync(accountAddress, 3m);

    var userOp = new UserOperation
    {
        Sender = accountAddress,
        Nonce = 1,
        CallData = Array.Empty<byte>(),
        CallGasLimit = 50000,
        VerificationGasLimit = 200000,
        PreVerificationGas = 50000,
        MaxFeePerGas = 2000000000,
        MaxPriorityFeePerGas = 1000000000
    };

    var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

    // First submission succeeds
    await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);

    // WHEN: Submitting the exact same operation again
    // THEN: Should be rejected as duplicate
    var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
    {
        await _fixture.BundlerService.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
    });

    Assert.Contains("duplicate", ex.Message.ToLower());
}
```

## 7.3 Replacement Rejected (Insufficient Gas Bump)

### Scenario: Reject replacement that doesn't increase gas enough

```csharp
[Fact]
[Trait("Category", "ERC4337-Mempool")]
[Trait("Feature", "ReplacementRejection")]
public async Task Given_PendingOperation_When_ReplacedWithInsufficientGasBump_Then_Rejected()
{
    // GIVEN: A pending operation in the mempool
    var accountKey = EthECKey.GenerateKey();
    var ownerAddress = accountKey.GetPublicAddress();
    var accountAddress = await DeployAccountAsync(ownerAddress, salt: 7301);

    await _fixture.FundAccountAsync(accountAddress, 5m);

    var originalOp = new UserOperation
    {
        Sender = accountAddress,
        Nonce = 1,
        CallData = Array.Empty<byte>(),
        CallGasLimit = 50000,
        VerificationGasLimit = 200000,
        PreVerificationGas = 50000,
        MaxFeePerGas = 2000000000,  // 2 Gwei
        MaxPriorityFeePerGas = 1000000000
    };

    var packedOriginal = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(originalOp, accountKey);
    await _fixture.BundlerService.SendUserOperationAsync(packedOriginal, _fixture.EntryPointService.ContractAddress);

    // WHEN: Submitting replacement with insufficient gas bump (< 10% increase)
    var replacementOp = new UserOperation
    {
        Sender = accountAddress,
        Nonce = 1,  // Same nonce
        CallData = new byte[] { 0x01 },
        CallGasLimit = 50000,
        VerificationGasLimit = 200000,
        PreVerificationGas = 50000,
        MaxFeePerGas = 2100000000,  // Only 5% increase
        MaxPriorityFeePerGas = 1050000000
    };

    var packedReplacement = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(replacementOp, accountKey);

    // THEN: Replacement should be rejected
    var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
    {
        await _fixture.BundlerService.SendUserOperationAsync(packedReplacement, _fixture.EntryPointService.ContractAddress);
    });

    Assert.Contains("replacement", ex.Message.ToLower());
}
```

---

# 8. 2D Nonce Management

## 8.1 Independent Nonce Keys

### Scenario: Operations with different nonce keys execute independently

```csharp
[Fact]
[Trait("Category", "ERC4337-Nonce")]
[Trait("Feature", "2DNonce")]
public async Task Given_DifferentNonceKeys_When_Submitted_Then_ExecuteIndependently()
{
    // GIVEN: A deployed account
    var accountKey = EthECKey.GenerateKey();
    var ownerAddress = accountKey.GetPublicAddress();
    var accountAddress = await DeployAccountAsync(ownerAddress, salt: 8001);

    await _fixture.FundAccountAsync(accountAddress, 5m);

    // Use two different nonce keys
    BigInteger nonceKey1 = 1;
    BigInteger nonceKey2 = 2;

    // Get nonces for both keys (should both be 0)
    var nonce1 = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, nonceKey1);
    var nonce2 = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, nonceKey2);

    Assert.Equal(BigInteger.Zero, nonce1);
    Assert.Equal(BigInteger.Zero, nonce2);

    // Encode 2D nonces: key (192 bits) | sequence (64 bits)
    var fullNonce1 = (nonceKey1 << 64) | 0;
    var fullNonce2 = (nonceKey2 << 64) | 0;

    var userOp1 = new UserOperation
    {
        Sender = accountAddress,
        Nonce = fullNonce1,
        CallData = Array.Empty<byte>(),
        CallGasLimit = 50000,
        VerificationGasLimit = 200000,
        PreVerificationGas = 50000,
        MaxFeePerGas = 2000000000,
        MaxPriorityFeePerGas = 1000000000
    };

    var userOp2 = new UserOperation
    {
        Sender = accountAddress,
        Nonce = fullNonce2,
        CallData = Array.Empty<byte>(),
        CallGasLimit = 50000,
        VerificationGasLimit = 200000,
        PreVerificationGas = 50000,
        MaxFeePerGas = 2000000000,
        MaxPriorityFeePerGas = 1000000000
    };

    var packedOp1 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp1, accountKey);
    var packedOp2 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp2, accountKey);

    // WHEN: Executing both operations (can be in same bundle or separate)
    await _fixture.BundlerService.SendUserOperationAsync(packedOp1, _fixture.EntryPointService.ContractAddress);
    await _fixture.BundlerService.SendUserOperationAsync(packedOp2, _fixture.EntryPointService.ContractAddress);
    var result = await _fixture.BundlerService.ExecuteBundleAsync();

    // THEN: Both succeed, and both nonce keys are incremented
    Assert.True(result?.Success);

    var newNonce1 = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, nonceKey1);
    var newNonce2 = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, nonceKey2);

    Assert.Equal(BigInteger.One, newNonce1);
    Assert.Equal(BigInteger.One, newNonce2);
}
```

---

# Helper Methods Required

```csharp
// Required helper methods for test implementation

private async Task<string> DeployAccountAsync(string ownerAddress, ulong salt)
{
    var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
    var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);

    // Deploy via UserOp with nonce 0
    var deployOp = new UserOperation
    {
        Sender = accountAddress,
        Nonce = 0,
        InitCode = initCode,
        // ... standard gas values
    };

    // Execute deployment
    // ...

    return accountAddress;
}

private async Task<string> DeployRevertingFactoryAsync()
{
    // Deploy a factory contract that always reverts
    // Bytecode: PUSH1 0x00 PUSH1 0x00 REVERT
    var bytecode = "0x6000600FD";
    // ...
}

private async Task<string> DeployRejectingPaymasterAsync()
{
    // Deploy a paymaster that returns SIG_VALIDATION_FAILED (1)
    // ...
}

private async Task<string> DeployRevertingPostOpPaymasterAsync()
{
    // Deploy a paymaster that passes validation but reverts in postOp
    // ...
}

private async Task<PackedUserOperation> SignWithValidationData(
    UserOperation userOp,
    EthECKey accountKey,
    long validAfter,
    long validUntil)
{
    // Pack validAfter and validUntil into the signature/validation response
    // Per ERC-4337: validationData = (validAfter << 160) | (validUntil << 208) | aggregator
    // ...
}

private byte[] BuildRevertingCallData()
{
    // CallData that will cause the account's execute() to revert
    // e.g., call to a contract that always reverts
    // ...
}

private byte[] BuildExpensiveCallData()
{
    // CallData that requires significant gas (e.g., deploy a contract)
    // ...
}

private byte[] BuildInitCode(string factory, string owner, ulong salt)
{
    // Build initCode: factory address (20 bytes) + createAccount calldata
    // ...
}
```

---

# Test Categories Summary

| Category | Trait | Count |
|----------|-------|-------|
| AA Error Codes | `ErrorCode=AA*` | 9 |
| Timestamp Validation | `Feature=TimestampValidation` | 3 |
| Paymaster Failures | `Feature=Paymaster*` | 4 |
| Factory/InitCode | `Feature=Factory*` | 3 |
| Execution Failures | `Feature=Execution*` | 3 |
| Aggregator Support | `Category=ERC4337-Aggregator` | 3 |
| Mempool Operations | `Category=ERC4337-Mempool` | 4 |
| 2D Nonce | `Feature=2DNonce` | 2 |
| **Total** | | **31** |

---

# Running Tests

```bash
# Run all ERC-4337 validation tests
cd tests/Nethereum.AccountAbstraction.IntegrationTests
dotnet test --filter "Category=ERC4337-Validation" -v normal

# Run specific error code tests
dotnet test --filter "ErrorCode=AA22" -v normal

# Run all mempool tests
dotnet test --filter "Category=ERC4337-Mempool" -v normal

# Run all aggregator tests
dotnet test --filter "Category=ERC4337-Aggregator" -v normal
```

---

# Implementation Priority

## Phase 1: Critical (Week 1)
1. AA Error Codes (AA11, AA13, AA22, AA23)
2. Timestamp Validation (validAfter, validUntil)

## Phase 2: High (Week 2)
3. AA Error Codes (AA41, AA42, AA51, AA52, AA53)
4. Paymaster Failures

## Phase 3: Medium (Week 3)
5. Factory/InitCode Failures
6. Execution Failures
7. Mempool Operations

## Phase 4: Enhancement (Week 4)
8. Aggregator Support
9. 2D Nonce Management
10. Edge cases and stress tests
