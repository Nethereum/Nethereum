# Nethereum.GnosisSafe

Nethereum service for interacting with Gnosis Safe (now Safe) multisig wallet contracts. Provides EIP-712 typed transaction signing, multi-signature transaction building, and seamless integration with any Nethereum contract service through custom account and contract handler implementations.

## Installation

```bash
dotnet add package Nethereum.GnosisSafe
```

## Core Components

### GnosisSafeService

Main service for interacting with Gnosis Safe contracts (GnosisSafeService.cs:13-738).

**Contract Functions:**
- **Owner Management**: AddOwnerWithThresholdRequestAsync, RemoveOwnerRequestAsync, SwapOwnerRequestAsync, GetOwnersQueryAsync, IsOwnerQueryAsync
- **Threshold Management**: ChangeThresholdRequestAsync, GetThresholdQueryAsync
- **Module Management**: EnableModuleRequestAsync, DisableModuleRequestAsync, IsModuleEnabledQueryAsync, GetModulesPaginatedQueryAsync
- **Transaction Execution**: ExecTransactionRequestAsync, ExecTransactionFromModuleRequestAsync
- **Configuration**: SetGuardRequestAsync, SetFallbackHandlerRequestAsync
- **Queries**: NonceQueryAsync, GetChainIdQueryAsync, DomainSeparatorQueryAsync, VersionQueryAsync

**Transaction Building:**

```csharp
// GnosisSafeService.cs:31-39
public async Task<ExecTransactionFunction> BuildTransactionAsync(
    EncodeTransactionDataFunction transactionData,
    BigInteger chainId,
    bool estimateSafeTxGas = false, params string[] privateKeySigners)
{
    var nonce = await NonceQueryAsync().ConfigureAwait(false);
    transactionData.SafeNonce = nonce;
    return BuildTransaction(transactionData, chainId, privateKeySigners);
}
```

**Multi-Signature Transaction Building:**

```csharp
// GnosisSafeService.cs:63-82
public async Task<ExecTransactionFunction> BuildMultiSignatureTransactionAsync<TFunctionMessage>(
    EncodeTransactionDataFunction transactionData,
    TFunctionMessage functionMessage,
    BigInteger chainId,
    bool estimateSafeTxGas = false, params string[] privateKeySigners)
    where TFunctionMessage : FunctionMessage, new()
{
    var nonce = await NonceQueryAsync().ConfigureAwait(false);
    if (estimateSafeTxGas)
    {
        var toContract = transactionData.To;
        var estimateHandler = Web3.Eth.GetContractTransactionHandler<TFunctionMessage>();
        functionMessage.FromAddress = this.ContractHandler.ContractAddress;
        var gasEstimateSafe = await estimateHandler.EstimateGasAsync(toContract, functionMessage);
        transactionData.SafeTxGas = gasEstimateSafe;
    }

    transactionData.Data = functionMessage.GetCallData();
    transactionData.SafeNonce = nonce;
    return BuildTransaction(transactionData, chainId, privateKeySigners);
}
```

**MultiSend Support:**

```csharp
// GnosisSafeService.cs:41-50
public Task<ExecTransactionFunction> BuildMultiSendTransactionAsync(
    EncodeTransactionDataFunction transactionData,
    BigInteger chainId,
    string privateKeySigner,
    bool estimateSafeTxGas = false, params IMultiSendInput[] multiSendInputs)
{
    transactionData.Operation = (int)ContractOperationType.DelegateCall;
    var multiSendFunction = new MultiSendFunction(multiSendInputs);
    return BuildMultiSignatureTransactionAsync(transactionData, multiSendFunction, chainId,
        estimateSafeTxGas, privateKeySigner);
}
```

### EIP-712 Typed Data Signing

**Type Definition Creation:**

```csharp
// GnosisSafeService.cs:147-159
public static TypedData<GnosisSafeEIP712Domain> GetGnosisSafeTypedDefinition(
    BigInteger chainId, string verifyingContractAddress)
{
    return new TypedData<GnosisSafeEIP712Domain>
    {
        Domain = new GnosisSafeEIP712Domain
        {
            ChainId = chainId,
            VerifyingContract = verifyingContractAddress
        },
        Types = MemberDescriptionFactory.GetTypesMemberDescription(
            typeof(GnosisSafeEIP712Domain), typeof(EncodeTransactionDataFunction)),
        PrimaryType = "SafeTx",
    };
}
```

**Transaction Hash Computation:**

```csharp
// GnosisSafeService.cs:181-186
public static byte[] GetEncodedTransactionDataHash(
    EncodeTransactionDataFunction transactionData,
    BigInteger chainId,
    string verifyingContractAddress)
{
    var typedDefinition = GetGnosisSafeTypedDefinition(chainId, verifyingContractAddress);
    return Eip712TypedDataEncoder.Current.EncodeAndHashTypedData(transactionData, typedDefinition);
}
```

**Safe Hashes Computation:**

```csharp
// GnosisSafeService.cs:132-145
public static SafeHashes GetSafeHashes(
    EncodeTransactionDataFunction transactionData,
    BigInteger chainId,
    string verifyingContractAddress)
{
    var typedDefinition = GetGnosisSafeTypedDefinition(chainId, verifyingContractAddress);
    var safeDomainHash = Eip712TypedDataEncoder.Current.HashDomainSeparator(typedDefinition);
    var safeMessageHash = Eip712TypedDataEncoder.Current.HashStruct(
        transactionData, typedDefinition.PrimaryType, typeof(EncodeTransactionDataFunction));
    var safeTxnHash = Eip712TypedDataEncoder.Current.EncodeAndHashTypedData(
        transactionData, typedDefinition);
    return new SafeHashes
    {
        SafeDomainHash = safeDomainHash,
        SafeMessageHash = safeMessageHash,
        SafeTxnHash = safeTxnHash
    };
}
```

**Multi-Signature Signing:**

```csharp
// GnosisSafeService.cs:198-213
public List<SafeSignature> SignMultipleEncodedTransactionDataHash(
    byte[] hashEncoded, params string[] privateKeySigners)
{
    var messageSigner = new EthereumMessageSigner();
    var signatures = new List<SafeSignature>();

    foreach (var privateKey in privateKeySigners)
    {
        var publicAddress = EthECKey.GetPublicAddress(privateKey);
        var signatureString = messageSigner.Sign(hashEncoded, privateKey);
        signatureString = ConvertSignatureStringToGnosisVFormat(signatureString);

        signatures.Add(new SafeSignature()
        {
            Address = publicAddress,
            Signature = signatureString
        });
    }

    return signatures;
}
```

**Gnosis V Format Conversion:**

Gnosis Safe requires signature V values to be offset by +4 from standard Ethereum V values.

```csharp
// GnosisSafeService.cs:215-225
public static string ConvertSignatureStringToGnosisVFormat(string signatureString)
{
    var signature = MessageSigner.ExtractEcdsaSignature(signatureString);
    var v = signature.V.ToBigIntegerFromRLPDecoded();
    if (VRecoveryAndChainCalculations.IsEthereumV((int)v))
    {
        signature.V = new[] { (byte)(v + 4) };
        signatureString = signature.CreateStringSignature();
    }
    return signatureString;
}
```

**Signature Ordering:**

Gnosis Safe requires signatures to be ordered by signer address.

```csharp
// GnosisSafeService.cs:258-268
public byte[] GetCombinedSignaturesInOrder(IEnumerable<SafeSignature> signatures)
{
    var signaturesFormatted = signatures.Select(
        x => ConvertSignatureStringToGnosisVFormat(x.Signature)).ToList();
    var orderedSignatures = signaturesFormatted.OrderBy(x => x.ToLower());
    var fullSignatures = "0x";
    foreach (var signature in orderedSignatures)
    {
        fullSignatures += signature.RemoveHexPrefix();
    }
    return fullSignatures.HexToByteArray();
}
```

### SafeExecTransactionContractHandler

Custom ContractHandler that wraps any contract service to execute transactions through a Gnosis Safe (SafeExecTransactionContractHandler.cs:28-124).

**Creation:**

```csharp
// SafeExecTransactionContractHandler.cs:30-36
public static SafeExecTransactionContractHandler CreateFromExistingContractService<T>(
    T service, string safeAddress, params string[] privateKeySigners)
    where T:ContractWeb3ServiceBase
{
    var contractAddress = service.ContractAddress;
    var ethApiContractService = service.Web3;
    var handler = new SafeExecTransactionContractHandler(
        contractAddress, safeAddress, ethApiContractService,
        service.ContractHandler.AddressFrom, privateKeySigners);
    return handler;
}
```

**Transaction Wrapping:**

All SendRequestAsync and SendRequestAndWaitForReceiptAsync calls are automatically wrapped in Safe execTransaction calls.

```csharp
// SafeExecTransactionContractHandler.cs:67-75
public override async Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync<TEthereumContractFunctionMessage>(
    TEthereumContractFunctionMessage transactionMessage = null,
    CancellationTokenSource tokenSource = null)
{
    if (transactionMessage == null) transactionMessage = new TEthereumContractFunctionMessage();
    var execTransactionFunction = await CreateExecTransactionFunction(transactionMessage);

    return await SafeService.ExecTransactionRequestAndWaitForReceiptAsync(execTransactionFunction);
}
```

**Extension Method:**

```csharp
// SafeExecTransactionContractHandler.cs:21-25
public static void ChangeContractHandlerToSafeExecTransaction<T>(
    this T service, string safeAddress, params string[] privateKeySigners)
    where T : ContractWeb3ServiceBase
{
    service.ContractHandler = SafeExecTransactionContractHandler
        .CreateFromExistingContractService(service, safeAddress, privateKeySigners);
}
```

### SafeAccount

Account implementation that automatically configures contract services to use SafeExecTransactionContractHandler (SafeAccount.cs:7-21).

```csharp
// SafeAccount.cs:7-20
public class SafeAccount : Nethereum.Web3.Accounts.Account, IContractServiceConfigurableAccount
{
    public SafeAccount(string safeAddress, BigInteger chainId, string privateKey)
        : base(privateKey, chainId)
    {
        SafeAddress = safeAddress;
    }

    public string SafeAddress { get; }

    public void ConfigureContractHandler<T>(T contractService)
        where T : ContractWeb3ServiceBase
    {
        contractService.ChangeContractHandlerToSafeExecTransaction(SafeAddress, this.PrivateKey);
    }
}
```

### Contract Definitions

**EncodeTransactionDataFunction** (ExtendedContractDefinition.cs:14-118)

EIP-712 SafeTx struct representing a Gnosis Safe transaction:

```csharp
[Function("encodeTransactionData", "bytes")]
[Struct("SafeTx")]
public class EncodeTransactionDataFunctionBase : FunctionMessage
{
    [Parameter("address", "to", 1)]
    public string To { get; set; }

    [Parameter("uint256", "value", 2)]
    public BigInteger Value { get; set; }

    [Parameter("bytes", "data", 3)]
    public byte[] Data { get; set; }

    [Parameter("uint8", "operation", 4)]
    public byte Operation { get; set; }

    [Parameter("uint256", "safeTxGas", 5)]
    public BigInteger SafeTxGas { get; set; }

    [Parameter("uint256", "baseGas", 6)]
    public BigInteger BaseGas { get; set; }

    [Parameter("uint256", "gasPrice", 7)]
    public BigInteger SafeGasPrice { get; set; }

    [Parameter("address", "gasToken", 8)]
    public string GasToken { get; set; }

    [Parameter("address", "refundReceiver", 9)]
    public string RefundReceiver { get; set; }

    [Parameter("uint256", "nonce", 10)]
    public BigInteger SafeNonce { get; set; }
}
```

**ExecTransactionFunction** (GnosisSafeDefinition.cs:95-120)

Function for executing Safe transactions with signatures:

```csharp
[Function("execTransaction", "bool")]
public class ExecTransactionFunctionBase : FunctionMessage
{
    [Parameter("address", "to", 1)]
    public virtual string To { get; set; }

    [Parameter("uint256", "value", 2)]
    public virtual BigInteger Value { get; set; }

    [Parameter("bytes", "data", 3)]
    public virtual byte[] Data { get; set; }

    [Parameter("uint8", "operation", 4)]
    public virtual byte Operation { get; set; }

    [Parameter("uint256", "safeTxGas", 5)]
    public virtual BigInteger SafeTxGas { get; set; }

    [Parameter("uint256", "baseGas", 6)]
    public virtual BigInteger BaseGas { get; set; }

    [Parameter("uint256", "gasPrice", 7)]
    public virtual BigInteger SafeGasPrice { get; set; }

    [Parameter("address", "gasToken", 8)]
    public virtual string GasToken { get; set; }

    [Parameter("address", "refundReceiver", 9)]
    public virtual string RefundReceiver { get; set; }

    [Parameter("bytes", "signatures", 10)]
    public virtual byte[] Signatures { get; set; }
}
```

**GnosisSafeEIP712Domain** (GnosisSafeEIP712Domain.cs:7-16)

Custom EIP-712 domain for Safe transactions:

```csharp
[Struct("EIP712Domain")]
public class GnosisSafeEIP712Domain : IDomain
{
    [Parameter("uint256", "chainId", 1)]
    public virtual BigInteger? ChainId { get; set; }

    [Parameter("address", "verifyingContract", 2)]
    public virtual string VerifyingContract { get; set; }
}
```

## Usage Examples

### Basic Safe Transaction

```csharp
using Nethereum.Web3;
using Nethereum.GnosisSafe;
using Nethereum.GnosisSafe.ContractDefinition;

var web3 = new Web3("https://mainnet.infura.io/v3/YOUR_INFURA_KEY");
var safeAddress = "0x..."; // Your Safe address
var chainId = 1;

var safeService = new GnosisSafeService(web3, safeAddress);

// Build transaction
var transactionData = new EncodeTransactionDataFunction
{
    To = "0x...", // Target contract
    Value = 0,
    Data = new byte[] { }, // Encoded function call
    Operation = 0, // 0 = Call, 1 = DelegateCall
    SafeTxGas = 0,
    BaseGas = 0,
    SafeGasPrice = 0,
    GasToken = "0x0000000000000000000000000000000000000000",
    RefundReceiver = "0x0000000000000000000000000000000000000000"
};

// Sign with multiple owners
var privateKey1 = "0x...";
var privateKey2 = "0x...";

var execTx = await safeService.BuildTransactionAsync(
    transactionData, chainId, false, privateKey1, privateKey2);

// Execute
var receipt = await safeService.ExecTransactionRequestAndWaitForReceiptAsync(execTx);
```

### Using SafeAccount with Contract Service

```csharp
using Nethereum.Web3;
using Nethereum.GnosisSafe;
using Nethereum.Contracts;

var privateKey = "0x...";
var safeAddress = "0x...";
var chainId = 1;

// Create SafeAccount
var safeAccount = new SafeAccount(safeAddress, chainId, privateKey);
var web3 = new Web3(safeAccount, "https://mainnet.infura.io/v3/YOUR_INFURA_KEY");

// Any contract service will automatically use Safe execution
var erc20Address = "0x...";
var erc20Service = new Nethereum.Contracts.Standards.ERC20.ERC20ContractService(
    web3.Eth, erc20Address);

// This transfer will be executed through the Safe
var transferReceipt = await erc20Service.TransferRequestAndWaitForReceiptAsync(
    "0x...", // to
    1000000  // amount
);
```

### Manual SafeExecTransactionContractHandler

```csharp
using Nethereum.Web3;
using Nethereum.GnosisSafe;
using Nethereum.Contracts.Standards.ERC20;

var web3 = new Web3("https://mainnet.infura.io/v3/YOUR_INFURA_KEY");
var safeAddress = "0x...";
var contractAddress = "0x...";

var erc20Service = new ERC20ContractService(web3.Eth, contractAddress);

// Change to Safe execution
var privateKeys = new[] { "0x...", "0x..." };
erc20Service.ChangeContractHandlerToSafeExecTransaction(safeAddress, privateKeys);

// All transactions now go through Safe
var receipt = await erc20Service.TransferRequestAndWaitForReceiptAsync("0x...", 1000);
```

### MultiSend Transaction

```csharp
using Nethereum.GnosisSafe;
using Nethereum.Contracts.TransactionHandlers.MultiSend;

var safeService = new GnosisSafeService(web3, safeAddress);

// Create multiple transactions
var multiSendInputs = new List<IMultiSendInput>
{
    new MultiSendInput
    {
        Operation = MultiSendOperationType.Call,
        To = "0x...",
        Value = 0,
        Data = new byte[] { } // Encoded function call
    },
    new MultiSendInput
    {
        Operation = MultiSendOperationType.Call,
        To = "0x...",
        Value = 0,
        Data = new byte[] { } // Another encoded function call
    }
};

var transactionData = new EncodeTransactionDataFunction
{
    To = "0x...", // MultiSend contract address
    Value = 0,
    SafeTxGas = 0,
    BaseGas = 0,
    SafeGasPrice = 0,
    GasToken = "0x0000000000000000000000000000000000000000",
    RefundReceiver = "0x0000000000000000000000000000000000000000"
};

var execTx = await safeService.BuildMultiSendTransactionAsync(
    transactionData, chainId, privateKey, false, multiSendInputs.ToArray());

var receipt = await safeService.ExecTransactionRequestAndWaitForReceiptAsync(execTx);
```

### Signing Transaction Data for Off-Chain Coordination

```csharp
using Nethereum.GnosisSafe;

var safeService = new GnosisSafeService(web3, safeAddress);

var transactionData = new EncodeTransactionDataFunction
{
    To = "0x...",
    Value = 0,
    Data = new byte[] { },
    Operation = 0,
    SafeTxGas = 0,
    BaseGas = 0,
    SafeGasPrice = 0,
    GasToken = "0x0000000000000000000000000000000000000000",
    RefundReceiver = "0x0000000000000000000000000000000000000000",
    SafeNonce = await safeService.NonceQueryAsync()
};

// Sign with current account's private key (EIP-712)
var signature = await safeService.SignEncodedTransactionDataAsync(
    transactionData, chainId, convertToSafeVFormat: true);

// Share signature with other owners for off-chain coordination
Console.WriteLine($"Signature: {signature}");
```

### Computing Transaction Hashes

```csharp
using Nethereum.GnosisSafe;

var transactionData = new EncodeTransactionDataFunction
{
    To = "0x...",
    Value = 0,
    Data = new byte[] { },
    Operation = 0,
    SafeTxGas = 0,
    BaseGas = 0,
    SafeGasPrice = 0,
    GasToken = "0x0000000000000000000000000000000000000000",
    RefundReceiver = "0x0000000000000000000000000000000000000000",
    SafeNonce = 5
};

// Get all Safe-related hashes
var safeHashes = GnosisSafeService.GetSafeHashes(
    transactionData, chainId, safeAddress);

Console.WriteLine($"Domain Hash: {safeHashes.SafeDomainHash.ToHex()}");
Console.WriteLine($"Message Hash: {safeHashes.SafeMessageHash.ToHex()}");
Console.WriteLine($"Transaction Hash: {safeHashes.SafeTxnHash.ToHex()}");
```

### Owner Management

```csharp
var safeService = new GnosisSafeService(web3, safeAddress);

// Get current owners
var owners = await safeService.GetOwnersQueryAsync();
var threshold = await safeService.GetThresholdQueryAsync();

Console.WriteLine($"Safe has {owners.Count} owners with threshold {threshold}");

// Add owner (requires Safe transaction execution)
var newOwner = "0x...";
var newThreshold = threshold + 1;

// This call must be executed through the Safe itself
var addOwnerTx = new AddOwnerWithThresholdFunction
{
    Owner = newOwner,
    Threshold = newThreshold
};
```

### Module Management

```csharp
var safeService = new GnosisSafeService(web3, safeAddress);

// Check if module is enabled
var moduleAddress = "0x...";
var isEnabled = await safeService.IsModuleEnabledQueryAsync(moduleAddress);

// Get all modules
var modules = await safeService.GetModulesPaginatedQueryAsync(
    "0x0000000000000000000000000000000000000001", // Start
    10 // Page size
);
```

## Key Concepts

### Multi-Signature Workflow

1. **Transaction Creation**: Define transaction parameters (to, value, data, operation, gas parameters)
2. **Nonce Assignment**: Safe nonce is automatically fetched and assigned
3. **EIP-712 Hash**: Transaction data is hashed according to EIP-712 standard
4. **Signature Collection**: Each owner signs the hash with their private key
5. **Signature Ordering**: Signatures must be ordered by signer address (ascending)
6. **V Value Conversion**: V values are offset by +4 for Gnosis Safe compatibility
7. **Execution**: execTransaction is called with combined ordered signatures

### Signature Format

Gnosis Safe uses a custom V value format:
- Ethereum standard: v = 27 or 28
- Gnosis Safe format: v = 31 or 32 (standard + 4)

The ConvertSignatureStringToGnosisVFormat method (GnosisSafeService.cs:215-225) handles this conversion automatically.

### Operation Types

- **0 (Call)**: Standard external call
- **1 (DelegateCall)**: Delegate call (executes in Safe's context, used for MultiSend)

### Gas Parameters

Safe transactions support advanced gas configuration:
- **safeTxGas**: Gas allocated for the Safe transaction
- **baseGas**: Gas costs independent of the transaction execution
- **gasPrice**: Gas price used for refund calculation
- **gasToken**: Token address for gas payment (address(0) for ETH)
- **refundReceiver**: Address receiving gas payment

## Dependencies

- **Nethereum.Web3**: Core Web3 functionality
- **Nethereum.Signer.EIP712**: EIP-712 typed structured data signing

## Platform Support

Supports all Nethereum target frameworks. Async methods require .NET Framework 4.5+, .NET Standard 1.1+, or .NET Core 1.0+.

## References

- [Gnosis Safe Documentation](https://docs.safe.global/)
- [Safe Contracts](https://github.com/safe-global/safe-contracts)
- [EIP-712: Typed Structured Data Hashing and Signing](https://eips.ethereum.org/EIPS/eip-712)
