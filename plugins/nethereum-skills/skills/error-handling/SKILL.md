---
name: error-handling
description: Handle smart contract reverts and decode custom error types using Nethereum (.NET). Use this skill whenever the user asks about contract errors, revert reasons, custom Solidity errors, SmartContractCustomErrorRevertException, error decoding, failed transaction reasons, or any revert handling in C#/.NET.
user-invocable: true
---

# Smart Contract Error Handling

NuGet: `Nethereum.Web3`

```bash
dotnet add package Nethereum.Web3
```

## Define Custom Error DTOs

Map Solidity custom errors to C# classes using `[Error]` and `[Parameter]` attributes.

```csharp
using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

// Solidity: error InsufficientBalance(address account, uint256 balance, uint256 required)
[Error("InsufficientBalance")]
public class InsufficientBalanceError
{
    [Parameter("address", "account", 1)]
    public string Account { get; set; }

    [Parameter("uint256", "balance", 2)]
    public BigInteger Balance { get; set; }

    [Parameter("uint256", "required", 3)]
    public BigInteger Required { get; set; }
}

// Solidity: error Unauthorized(address caller)
[Error("Unauthorized")]
public class UnauthorizedError
{
    [Parameter("address", "caller", 1)]
    public string Caller { get; set; }
}
```

## Catch and Decode a Specific Error

```csharp
try
{
    var receipt = await contractHandler
        .SendRequestAndWaitForReceiptAsync(transferFunction);
}
catch (SmartContractCustomErrorRevertException ex)
{
    if (ex.IsCustomErrorFor<InsufficientBalanceError>())
    {
        var error = ex.DecodeError<InsufficientBalanceError>();
        Console.WriteLine(
            $"Insufficient: {error.Account} has {error.Balance}, needs {error.Required}");
    }
    else if (ex.IsCustomErrorFor<UnauthorizedError>())
    {
        var error = ex.DecodeError<UnauthorizedError>();
        Console.WriteLine($"Unauthorized: {error.Caller}");
    }
}
```

## Auto-Decode with ContractServiceBase

Code-generated contract services register all error types. Use `FindCustomErrorException()`:

```csharp
catch (SmartContractCustomErrorRevertException ex)
{
    var typedException = myContractService.FindCustomErrorException(ex);
    if (typedException != null)
    {
        Console.WriteLine($"Error: {typedException.DecodedError}");
        Console.WriteLine($"Error ABI: {typedException.ErrorABI.Name}");
    }
}
```

## Typed Exception Factory

```csharp
var errorTypes = new[] { typeof(InsufficientBalanceError), typeof(UnauthorizedError) };

catch (SmartContractCustomErrorRevertException ex)
{
    var typed = SmartContractCustomErrorTypedFactory.CreateTypedException(ex, errorTypes);
    if (typed is SmartContractCustomErrorRevertException<InsufficientBalanceError> balanceEx)
    {
        var error = balanceEx.CustomError;
        Console.WriteLine($"Need {error.Required}, have {error.Balance}");
    }
}
```

## Get Error Reason from Failed Transaction

```csharp
var errorReason = await web3.Eth.GetContractTransactionErrorReason
    .SendRequestAsync(failedTransactionHash);
Console.WriteLine($"Transaction failed: {errorReason}");
```

## Decode Error from Raw Hex Data

```csharp
var exception = new SmartContractCustomErrorRevertException(hexEncodedErrorData);
if (exception.IsCustomErrorFor<InsufficientBalanceError>())
{
    var error = exception.DecodeError<InsufficientBalanceError>();
}
```

## Key Types

- `SmartContractCustomErrorRevertException` -- base exception with `ExceptionEncodedData`
- `SmartContractCustomErrorRevertException<TError>` -- generic typed with `CustomError` property
- `SmartContractCustomErrorRevertExceptionErrorDecoded` -- decoded with `DecodedError` string
- `SmartContractCustomErrorTypedFactory` -- factory for typed exceptions from error type list
- `ContractServiceBase.FindCustomErrorException()` -- auto-decode using registered types
- `web3.Eth.GetContractTransactionErrorReason` -- error reason from mined failed tx

## Common Patterns

| Task | Method |
|------|--------|
| Check error type | `ex.IsCustomErrorFor<TError>()` |
| Decode to typed object | `ex.DecodeError<TError>()` |
| Decode to default string | `ex.DecodeErrorToDefaultString(errorABI)` |
| Auto-decode from service | `contractService.FindCustomErrorException(ex)` |
| Factory decode | `SmartContractCustomErrorTypedFactory.CreateTypedException(ex, types)` |
| Error from failed tx | `web3.Eth.GetContractTransactionErrorReason.SendRequestAsync(hash)` |
