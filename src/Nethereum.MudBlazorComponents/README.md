# Nethereum.MudBlazorComponents

Blazor components in MudBlazor for simple data input for smart contracts and MUD (mud.dev) tables. Used by Nethereum code generator to create complete Blazor pages per smart contract with deployment, query, and transaction UI.

## Overview

Nethereum.MudBlazorComponents provides Razor components that the Nethereum code generator uses to create full Blazor pages for smart contracts. The code generator scans contract ABIs and generates pages with these components already configured for each function.

**Code Generation Integration:**

When using Nethereum code generator with `generatorType: "BlazorPageService"`, a complete Blazor page is generated per smart contract containing:
- ContractDeploymentComponent for deploying the contract
- QueryFunctionComponent for each view/pure function
- TransactionFunctionComponent for each state-changing function
- MudDevTableComponent for MUD table operations (when applicable)

**Components:**
- **QueryFunctionComponent** - Query any smart contract by providing FunctionMessageType, SmartContractAddress, and Output type, or a service type and query method
- **TransactionFunctionComponent** - Submit transactions using service to decode all event outputs after submission, or provide event types for decoding
- **ContractDeploymentComponent** - Deploy smart contracts with constructor parameters
- **MudDevTableComponent** - Query by key and upsert table values using TableServiceType
- **MudDevTableServicesComponent** - Scans assembly for all TableServiceTypes and creates collection of MudTableComponents per table

## Installation

```bash
dotnet add package Nethereum.MudBlazorComponents
```

Or via Package Manager Console:

```powershell
Install-Package Nethereum.MudBlazorComponents
```

## Dependencies

**Package References:**
- Microsoft.AspNetCore.Components.WebAssembly 9.*
- MudBlazor 8.*

**Project References:**
- Nethereum.Blazor (EIP-6963 and wallet integration)
- Nethereum.Mud.Contracts (MUD table contracts)
- Nethereum.Mud (MUD protocol support)
- Nethereum.UI (`IEthereumHostProvider` abstraction)
- Nethereum.Web3 (Web3 API)

**Target Framework:**
- net9.0

## Code Generator Configuration

### Generator Settings for Blazor Pages

Configure Nethereum code generator to generate Blazor pages using MudBlazorComponents:

```json
[
  {
    "paths": ["out/ERC20.sol/Standard_Token.json"],
    "generatorConfigs": [
      {
        "baseNamespace": "MyProject.Contracts",
        "basePath": "MyProject.Contracts",
        "codeGenLang": 0,
        "sharedTypesNamespace": "SharedTypes",
        "sharedTypes": ["events", "errors"],
        "generatorType": "ContractDefinition"
      },
      {
        "baseNamespace": "MyProject.Contracts",
        "basePath": "MyProject.Blazor",
        "codeGenLang": 0,
        "sharedTypesNamespace": "SharedTypes",
        "generatorType": "BlazorPageService"
      }
    ]
  }
]
```

**Generator Types:**
- `ContractDefinition` - Generates typed contract messages (DTOs, functions, events)
- `BlazorPageService` - Generates Blazor pages using MudBlazorComponents
- `UnityRequest` - Generates Unity-compatible request classes

### Generated Blazor Page Example

From `Standard_Token.json` ABI, the generator creates `standard_token.razor`:

```razor
@using System.Numerics
@using Nethereum.UI
@using MyProject.Contracts.Standard_Token
@using MyProject.Contracts.Standard_Token.ContractDefinition

@page "/standard_token"
@rendermode InteractiveWebAssembly
@inject SelectedEthereumHostProviderService selectedHostProviderService

<MudContainer MaxWidth="MaxWidth.Medium" Class="mt-4">

    <MudText Typo="Typo.h5" Class="mb-4">Standard_Token</MudText>

    <MudTextField @bind-Value="ContractAddress"
                  Label="Standard_Token Contract Address"
                  Variant="Variant.Outlined"
                  Class="mb-4" />

    <ContractDeploymentComponent TDeploymentMessage="StandardTokenDeployment"
        HostProvider="selectedHostProviderService"
        ServiceType="typeof(StandardTokenService)"
        ContractAddressChanged="ContractAddressChanged" />

    <QueryFunctionComponent TFunctionMessage="AllowanceFunction"
                           TFunctionOutput="BigInteger"
        Title="allowance"
        ContractAddress="@ContractAddress"
        HostProvider="selectedHostProviderService"
        ServiceType="typeof(StandardTokenService)"
        ServiceMethodName="AllowanceQueryAsync" />

    <TransactionFunctionComponent TFunctionMessage="ApproveFunction"
        Title="approve"
        ContractAddress="@ContractAddress"
        HostProvider="selectedHostProviderService"
        ServiceType="typeof(StandardTokenService)"
        ServiceRequestMethodName="ApproveRequestAsync"
        ServiceRequestAndWaitForReceiptMethodName="ApproveRequestAndWaitForReceiptAsync" />

    <QueryFunctionComponent TFunctionMessage="BalanceOfFunction"
                           TFunctionOutput="BigInteger"
        Title="balanceOf"
        ContractAddress="@ContractAddress"
        HostProvider="selectedHostProviderService"
        ServiceType="typeof(StandardTokenService)"
        ServiceMethodName="BalanceOfQueryAsync" />

    <TransactionFunctionComponent TFunctionMessage="TransferFunction"
        Title="transfer"
        ContractAddress="@ContractAddress"
        HostProvider="selectedHostProviderService"
        ServiceType="typeof(StandardTokenService)"
        ServiceRequestMethodName="TransferRequestAsync"
        ServiceRequestAndWaitForReceiptMethodName="TransferRequestAndWaitForReceiptAsync" />

</MudContainer>

@code {
    private string ContractAddress;

    private void ContractAddressChanged(string address)
    {
        ContractAddress = address;
    }
}
```

### Shared Types Support

The code generator supports creating shared folders for common types (structs, events, errors) across multiple contracts:

```json
{
  "sharedTypesNamespace": "SharedTypes",
  "sharedTypes": ["events", "errors"]
}
```

This creates:
```
MyProject.Contracts/
  SharedTypes/
    Events/
      TransferEventDTO.cs
    Errors/
      InsufficientBalanceError.cs
  Standard_Token/
    ContractDefinition/
      StandardTokenDeployment.cs
      TransferFunction.cs
```

## Component Details

### ContractDeploymentComponent<TDeploymentMessage>

Deploys smart contracts with auto-generated constructor parameter inputs.

**Type Parameters:**
- `TDeploymentMessage` - Generated deployment message class (e.g., `MyContractDeployment`)

**Parameters:**
```csharp
[Parameter] public string Title { get; set; }
[Parameter] public SelectedEthereumHostProviderService HostProvider { get; set; }
[Parameter] public TDeploymentMessage DeploymentMessage { get; set; }
[Parameter] public string ContractAddress { get; set; }
[Parameter] public EventCallback<string> ContractAddressChanged { get; set; }
[Parameter] public Type ServiceType { get; set; }
[Parameter] public IEnumerable<Type> AdditionalEventTypes { get; set; }
```

**Features:**
- Auto-generates inputs for constructor parameters
- Optional deployment settings (gas, gas price, nonce, amount to send)
- Displays deployed contract address
- Shows transaction receipt with decoded events
- Form validation

**From:** `src/Nethereum.MudBlazorComponents/ContractDeploymentComponent.razor:115`

**Example:**

```razor
<ContractDeploymentComponent
    TDeploymentMessage="ERC20Deployment"
    HostProvider="HostProvider"
    Title="Deploy ERC20 Token"
    @bind-ContractAddress="tokenAddress"
    ServiceType="typeof(ERC20Service)"
    AdditionalEventTypes="new[] { typeof(TransferEventDTO), typeof(ApprovalEventDTO) }" />
```

### TransactionFunctionComponent<TFunctionMessage>

Sends contract transactions with auto-generated function parameter inputs.

**Type Parameters:**
- `TFunctionMessage` - Generated function message class (e.g., `TransferFunction`)

**Parameters:**
```csharp
[Parameter] public string Title { get; set; }
[Parameter] public string ContractAddress { get; set; }
[Parameter] public SelectedEthereumHostProviderService HostProvider { get; set; }
[Parameter] public Type ServiceType { get; set; }
[Parameter] public string ServiceRequestMethodName { get; set; }
[Parameter] public string ServiceRequestAndWaitForReceiptMethodName { get; set; }
[Parameter] public bool UseContractHandlerDirectly { get; set; }
[Parameter] public Func<object, TFunctionMessage, Task<string>> ExecuteSend { get; set; }
[Parameter] public Func<object, TFunctionMessage, Task<TransactionReceipt>> ExecuteSendAndWait { get; set; }
[Parameter] public Func<Exception, object, string> HandleCustomError { get; set; }
[Parameter] public IEnumerable<Type> AdditionalEventTypes { get; set; }
```

**Features:**
- Auto-generates inputs for function parameters
- Two execution modes: "Send Transaction" and "Send and Wait for Receipt"
- Optional transaction settings (gas, gas price, nonce, amount to send)
- Displays transaction hash and receipt
- Custom error handling support
- Decodes contract custom errors

**From:** `src/Nethereum.MudBlazorComponents/TransactionFunctionComponent.razor:129`

**Example with Custom Execute:**

```razor
<TransactionFunctionComponent
    TFunctionMessage="TransferFunction"
    ContractAddress="@tokenAddress"
    HostProvider="HostProvider"
    ExecuteSend="SendTransferAsync" />

@code {
    private async Task<string> SendTransferAsync(object service, TransferFunction input)
    {
        var erc20 = service as ERC20Service;
        return await erc20.TransferRequestAsync(input);
    }
}
```

**From:** `src/Nethereum.MudBlazorComponents/TransactionFunctionComponent.razor:140`

### QueryFunctionComponent<TFunctionMessage, TFunctionOutput>

Queries contract view/pure functions and displays results.

**Type Parameters:**
- `TFunctionMessage` - Generated function message class (e.g., `BalanceOfFunction`)
- `TFunctionOutput` - Return type (e.g., `BigInteger`, `string`, or custom DTO)

**Parameters:**
```csharp
[Parameter] public string Title { get; set; }
[Parameter] public string ContractAddress { get; set; }
[Parameter] public SelectedEthereumHostProviderService HostProvider { get; set; }
[Parameter] public Type ServiceType { get; set; }
[Parameter] public string ServiceMethodName { get; set; }
[Parameter] public bool UseContractHandlerDirectly { get; set; }
[Parameter] public Func<object, TFunctionMessage, Task<TFunctionOutput>> ExecuteQuery { get; set; }
[Parameter] public Func<Exception, object, string> HandleCustomError { get; set; }
[Parameter] public TFunctionMessage FunctionInput { get; set; }
```

**Features:**
- Auto-generates inputs for query parameters
- Displays formatted query results
- Optional call settings (from address, amount to send for payable functions)
- Custom error handling

**From:** `src/Nethereum.MudBlazorComponents/QueryFunctionComponent.razor:82`

**Example with Custom Types:**

```razor
<QueryFunctionComponent
    TFunctionMessage="GetUserInfoFunction"
    TFunctionOutput="UserInfoOutputDTO"
    ContractAddress="@contractAddress"
    HostProvider="HostProvider"
    ServiceType="typeof(MyContractService)"
    ServiceMethodName="GetUserInfoQueryAsync" />
```

### MudDevTableComponent<TTableService>

Manages MUD table records (query and update).

**Type Parameters:**
- `TTableService` - Generated MUD table service class

**Parameters:**
```csharp
[Parameter] public string ContractAddress { get; set; }
[Parameter] public SelectedEthereumHostProviderService HostProvider { get; set; }
```

**Features:**
- Auto-generates key input form (for non-singleton tables)
- Loads record values from onchain storage
- Auto-generates value input form
- Saves updated values to onchain storage
- Displays transaction receipt

**From:** `src/Nethereum.MudBlazorComponents/MudDevTableComponent.razor:15`

**Example:**

```razor
<MudDevTableComponent
    TTableService="PlayerTableService"
    ContractAddress="@worldAddress"
    HostProvider="HostProvider" />
```

### MudDevTableServicesComponent

Auto-discovers and displays all MUD table services in an assembly.

**Parameters:**
```csharp
[Parameter] public string Title { get; set; }
[Parameter] public Assembly SearchAssembly { get; set; }
[Parameter] public SelectedEthereumHostProviderService HostProvider { get; set; }
[Parameter] public string ContractAddress { get; set; }
[Parameter] public EventCallback<string> ContractAddressChanged { get; set; }
```

**Features:**
- Scans assembly for `ITableServiceBase` implementations
- Creates a `MudDevTableComponent` for each table service
- Single contract address input for all tables

**From:** `src/Nethereum.MudBlazorComponents/MudDevTableServicesComponent.razor:6`

**Example:**

```razor
@using System.Reflection

<MudDevTableServicesComponent
    Title="Game Tables"
    SearchAssembly="typeof(PlayerTableService).Assembly"
    HostProvider="HostProvider"
    @bind-ContractAddress="worldAddress" />

@code {
    private string worldAddress = "0x...";
}
```

**From:** `src/Nethereum.MudBlazorComponents/MudDevTableServicesComponent.razor:23`

## Input Components

### StructInput

Dynamically generates form inputs for struct/class types.

**Features:**
- Reflection-based input generation
- Supports primitive types (int, string, bool, etc.)
- Supports BigInteger, addresses, bytes
- Nested struct support
- Array support (via ArrayInput)
- Property exclusion

**From:** `src/Nethereum.MudBlazorComponents/StructInput.razor`

### ArrayInput

Generates list inputs for array parameters.

**Features:**
- Add/remove array elements
- Type-specific inputs for each element
- Nested array support

**From:** `src/Nethereum.MudBlazorComponents/ArrayInput.razor`

## Output Components

### ResultOutput

Displays query results and transaction receipts with formatting.

**Features:**
- Formats primitive types
- Displays struct properties
- Decodes event logs
- Shows transaction receipt details

**From:** `src/Nethereum.MudBlazorComponents/ResultOutput.razor`

### FormattedValue

Formats individual values for display.

**From:** `src/Nethereum.MudBlazorComponents/FormattedValue.razor`

## DynamicRouteService

Discovers components with specific naming patterns and generates navigation items.

```csharp
public class DynamicRouteService
{
    public List<NavItem> GetGeneratedRoutes(string suffixFilter = "_gen")
    {
        // Scans assemblies for components ending with suffix (e.g., "_gen")
        // Returns NavItem list for menu generation
    }
}
```

**Use Case:** Auto-generate navigation menus for code-generated contract pages.

**From:** `src/Nethereum.MudBlazorComponents/DynamicRouteService.cs:11`

**Example:**

```razor
@inject DynamicRouteService RouteService

<MudNavMenu>
    @foreach (var item in RouteService.GetGeneratedRoutes())
    {
        <MudNavLink Href="@item.Href" Icon="@item.Icon">@item.Title</MudNavLink>
    }
</MudNavMenu>
```

## Manual Component Usage (Advanced)

While components are primarily used by the code generator, they can be used manually:

### Manual Deployment Component

```razor
<ContractDeploymentComponent TDeploymentMessage="SoftTokenDeployment"
    HostProvider="selectedHostProviderService"
    ServiceType="typeof(SoftTokenService)"
    ContractAddressChanged="ContractAddressChanged" />
```

### Manual Query Component

```razor
<QueryFunctionComponent TFunctionMessage="AllowanceFunction"
                       TFunctionOutput="BigInteger"
    Title="allowance"
    ContractAddress="@ContractAddress"
    HostProvider="selectedHostProviderService"
    ServiceType="typeof(SoftTokenService)"
    ServiceMethodName="AllowanceQueryAsync" />
```

### Manual Transaction Component

```razor
<TransactionFunctionComponent TFunctionMessage="ApproveFunction"
    Title="approve"
    ContractAddress="@ContractAddress"
    HostProvider="selectedHostProviderService"
    ServiceType="typeof(SoftTokenService)"
    ServiceRequestMethodName="ApproveRequestAsync"
    ServiceRequestAndWaitForReceiptMethodName="ApproveRequestAndWaitForReceiptAsync" />
```

### MUD Table Component

```razor
<MudDevTableComponent TService="ItemsTableService"
    ContractAddress="@ContractAddress"
    HostProvider="HostProvider" />
```

### MUD Table Services Component

Scans assembly and creates UI for all discovered table services:

```razor
<MudDevTableServicesComponent
    Title="MUD World Tables"
    SearchAssembly="typeof(PlayerTableService).Assembly"
    HostProvider="HostProvider"
    @bind-ContractAddress="worldAddress" />
```

## Setup Requirements

### Add MudBlazor to Your Project

Generated Blazor pages require MudBlazor to be configured:

```csharp
// Program.cs
using MudBlazor.Services;

builder.Services.AddMudServices();
```

```html
<!-- App.razor or _Layout.cshtml -->
<link href="https://fonts.googleapis.com/css?family=Roboto:300,400,500,700&display=swap" rel="stylesheet" />
<link href="_content/MudBlazor/MudBlazor.min.css" rel="stylesheet" />
<script src="_content/MudBlazor/MudBlazor.min.js"></script>
```

### Add Wallet Provider

Generated pages inject `SelectedEthereumHostProviderService`:

```csharp
// Program.cs
using Nethereum.Blazor;

builder.Services.AddSingleton<SelectedEthereumHostProviderService>();
```

## Using Contract Handlers Directly

For simple use cases without custom contract services:

```razor
<TransactionFunctionComponent
    TFunctionMessage="TransferFunction"
    ContractAddress="@tokenAddress"
    HostProvider="HostProvider"
    UseContractHandlerDirectly="true" />
```

When `UseContractHandlerDirectly="true"`, the component uses Nethereum's generic contract handlers instead of typed service classes.

**From:** `src/Nethereum.MudBlazorComponents/TransactionFunctionComponent.razor:137`

## Custom Error Handling

Handle contract custom errors (Solidity revert reasons):

```razor
<TransactionFunctionComponent
    TFunctionMessage="TransferFunction"
    ContractAddress="@tokenAddress"
    HostProvider="HostProvider"
    ServiceType="typeof(ERC20Service)"
    HandleCustomError="FormatError" />

@code {
    private string FormatError(Exception ex, object service)
    {
        if (ex is SmartContractCustomErrorRevertException revert)
        {
            var erc20 = service as ERC20Service;
            var decoded = erc20.FindCustomErrorException(revert);
            return $"Contract Error: {decoded?.ToString() ?? revert.Message}";
        }
        return ex.Message;
    }
}
```

**From:** `src/Nethereum.MudBlazorComponents/TransactionFunctionComponent.razor:142`

## Optional Transaction Settings

All transaction and deployment components include collapsible "Optional Settings" panels:

- **Amount to Send (ETH)** - Value to send with transaction (for payable functions)
- **Gas (Units)** - Gas limit
- **Nonce** - Transaction nonce (for manual nonce management)
- **Gas Price (Gwei)** - Legacy transaction gas price
- **Max Fee Per Gas (Gwei)** - EIP-1559 max fee
- **Max Priority Fee (Gwei)** - EIP-1559 priority fee (tip)

**From:** `src/Nethereum.MudBlazorComponents/TransactionFunctionComponent.razor:28`

## Excluded Properties

The following properties are automatically excluded from input forms as they're managed separately:

```csharp
// Deployment
FromAddress, AmountToSend, Gas, GasPrice, Nonce,
MaxFeePerGas, MaxPriorityFeePerGas, TransactionType,
AccessList, AuthorisationList

// Transactions (same as above)

// Queries
FromAddress, AmountToSend, Gas, GasPrice, Nonce,
MaxFeePerGas, MaxPriorityFeePerGas, TransactionType,
AccessList, AuthorisationList
```

**From:** `src/Nethereum.MudBlazorComponents/ContractDeploymentComponent.razor:223`

## Styling and Theming

Components use MudBlazor's default theme. Customize via MudThemeProvider:

```razor
<MudThemeProvider Theme="customTheme" />

@code {
    private MudTheme customTheme = new MudTheme()
    {
        Palette = new PaletteLight()
        {
            Primary = Colors.Blue.Default,
            Secondary = Colors.Green.Default
        }
    };
}
```

## Limitations

1. **Reflection-Based** - Components use reflection to generate UI, which may have performance implications for large forms
2. **MudBlazor Dependency** - Requires MudBlazor UI framework
3. **Type Support** - Some complex types may not have automatic input generation
4. **Custom Validation** - Limited to basic type validation (addresses, numbers)

## Troubleshooting

### Form Not Showing Inputs

**Issue:** StructInput doesn't generate any form fields.

**Solution:** Ensure properties have public getters/setters and are not in the excluded list.

### "No suitable method found" Error

**Issue:** `InvalidOperationException` when using ServiceType with method names.

**Solution:** Verify method signature matches expected parameters:
```csharp
// Transaction
Task<string> MethodNameAsync(TFunctionMessage input)

// Transaction with wait
Task<TransactionReceipt> MethodNameAsync(TFunctionMessage input, CancellationTokenSource cts)

// Query
Task<TOutput> MethodNameAsync(TFunctionMessage input)
Task<TOutput> MethodNameAsync(TFunctionMessage input, BlockParameter block)
```

**From:** `src/Nethereum.MudBlazorComponents/TransactionFunctionComponent.razor:249`

### MUD Tables Not Discovered

**Issue:** `MudDevTableServicesComponent` shows no tables.

**Solution:** Ensure:
1. Table services implement `ITableServiceBase`
2. Correct assembly is passed to `SearchAssembly` parameter
3. Classes are public and non-abstract

**From:** `src/Nethereum.MudBlazorComponents/MudDevTableServicesComponent.razor:42`

## Related Packages

- **Nethereum.Generator.Console** - Code generator CLI for creating Blazor pages
- **MudBlazor** - Material Design component library for Blazor
- **Nethereum.Blazor** - EIP-6963 wallet integration
- **Nethereum.Mud** - MUD protocol support
- **Nethereum.Mud.Contracts** - MUD table contract definitions
- **Nethereum.UI** - `IEthereumHostProvider` abstraction
- **Nethereum.Web3** - Web3 API for Ethereum

## Additional Resources

- [Nethereum Code Generation Documentation](https://docs.nethereum.com/en/latest/nethereum-codegen-vscodesolidity/)
- [MudBlazor Documentation](https://mudblazor.com/)
- [MUD Framework](https://mud.dev/)
- [Nethereum Documentation](http://docs.nethereum.com)
