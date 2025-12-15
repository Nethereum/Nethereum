# Nethereum.Wallet.UI.Components.Blazor.Trezor

Blazor Razor components and prompt dialogs for Trezor hardware wallet integration. Provides browser-based UI for PIN entry, passphrase prompts, and account management for Trezor devices.

## Installation

```bash
dotnet add package Nethereum.Wallet.UI.Components.Blazor.Trezor
```

## Target Framework

- net9.0

## Supported Platform

- browser (WebAssembly/Server)

## Dependencies

### NuGet Packages
- Microsoft.AspNetCore.Components.Web 9.0.6

### Nethereum Packages
- Nethereum.Wallet.UI.Components.Blazor - Base Blazor components and services
- Nethereum.Wallet.UI.Components.Trezor - Trezor ViewModels and business logic

Source: Nethereum.Wallet.UI.Components.Blazor.Trezor.csproj:15-26

## Overview

This package implements browser-based Trezor hardware wallet support for Blazor applications. It provides:

1. **Hardware Prompts** - PIN and passphrase entry dialogs for Trezor device interaction
2. **Account Creation UI** - Razor components for creating accounts from Trezor devices
3. **Account Management UI** - Components for viewing and managing Trezor account details
4. **Device Management UI** - Group details views for managing multiple accounts from a single Trezor
5. **Service Registration** - DI extensions for complete Trezor Blazor integration

## Service Registration

**AddTrezorWalletBlazorComponents** - Complete DI registration

```csharp
public static IServiceCollection AddTrezorWalletBlazorComponents(this IServiceCollection services)
{
    // Account metadata
    services.AddSingleton<IAccountMetadataViewModel, TrezorAccountMetadataProvider>();

    // ViewModels
    services.AddTransient<TrezorAccountCreationViewModel>();
    services.AddTransient<IAccountCreationViewModel>(sp => sp.GetRequiredService<TrezorAccountCreationViewModel>());
    services.AddTransient<TrezorVaultAccountCreationViewModel>();
    services.AddTransient<IAccountCreationViewModel>(sp => sp.GetRequiredService<TrezorVaultAccountCreationViewModel>());
    services.AddTransient<TrezorAccountDetailsViewModel>();
    services.AddTransient<IAccountDetailsViewModel, TrezorAccountDetailsViewModel>();
    services.AddTransient<TrezorGroupDetailsViewModel>();
    services.AddTransient<IGroupDetailsViewModel, TrezorGroupDetailsViewModel>();

    // Localizers
    services.TryAddSingleton<IComponentLocalizer<TrezorAccountCreationViewModel>, TrezorAccountCreationLocalizer>();
    services.TryAddSingleton<IComponentLocalizer<TrezorVaultAccountCreationViewModel>, TrezorVaultAccountCreationLocalizer>();
    services.TryAddSingleton<IComponentLocalizer<TrezorPinPrompt>, TrezorPinPromptLocalizer>();
    services.TryAddSingleton<IComponentLocalizer<TrezorPassphrasePrompt>, TrezorPassphrasePromptLocalizer>();
    services.TryAddSingleton<IComponentLocalizer<TrezorAccountDetailsViewModel>, TrezorAccountDetailsLocalizer>();
    services.TryAddSingleton<IComponentLocalizer<TrezorGroupDetailsViewModel>, TrezorGroupDetailsLocalizer>();

    // Prompt handler
    services.AddTransient<BlazorTrezorPromptHandler>();
    services.AddTransient<ITrezorPromptHandler, BlazorTrezorPromptHandler>();

    // Registry contributor
    services.AddSingleton<IWalletUIRegistryContributor, TrezorWalletUIRegistryContributor>();

    return services;
}
```
Source: Extensions/ServiceCollectionExtensions.cs:18-43

## Registry Contributor

**TrezorWalletUIRegistryContributor** - Registers Trezor components with wallet UI system

```csharp
public sealed class TrezorWalletUIRegistryContributor : IWalletUIRegistryContributor
{
    public void Configure(IServiceProvider serviceProvider)
    {
        var creationRegistry = serviceProvider.GetService<IAccountCreationRegistry>();
        creationRegistry?.Register<TrezorAccountCreationViewModel, TrezorAccountCreation>();
        creationRegistry?.Register<TrezorVaultAccountCreationViewModel, TrezorVaultAccountCreation>();

        var accountDetailsRegistry = serviceProvider.GetService<IAccountDetailsRegistry>();
        accountDetailsRegistry?.Register<TrezorAccountDetailsViewModel, TrezorAccountDetails>();

        var groupDetailsRegistry = serviceProvider.GetService<IGroupDetailsRegistry>();
        groupDetailsRegistry?.Register<TrezorGroupDetailsViewModel, TrezorGroupDetails>();
    }
}
```
Source: Extensions/TrezorWalletUIRegistryContributor.cs:11-25

Maps ViewModels to Razor components:
- TrezorAccountCreationViewModel → TrezorAccountCreation.razor
- TrezorVaultAccountCreationViewModel → TrezorVaultAccountCreation.razor
- TrezorAccountDetailsViewModel → TrezorAccountDetails.razor
- TrezorGroupDetailsViewModel → TrezorGroupDetails.razor

Source: Extensions/TrezorWalletUIRegistryContributor.cs:16-23

## Trezor Prompt Handler

**BlazorTrezorPromptHandler** - Implements `ITrezorPromptHandler` for browser-based prompts

```csharp
public class BlazorTrezorPromptHandler : ITrezorPromptHandler
{
    private readonly IWalletDialogAccessor _dialogAccessor;
    private string? _cachedPassphrase;

    public async Task<string> GetPinAsync()
    {
        var pin = await ShowDialogAsync<TrezorPinPrompt>();
        if (pin == null)
        {
            throw new OperationCanceledException("PIN prompt canceled by user");
        }
        return pin;
    }

    public async Task<string> GetPassphraseAsync()
    {
        if (_cachedPassphrase != null)
        {
            return _cachedPassphrase;
        }

        var passphrase = await ShowDialogAsync<TrezorPassphrasePrompt>();
        if (passphrase == null)
        {
            throw new OperationCanceledException("Passphrase prompt canceled by user");
        }

        _cachedPassphrase = passphrase;
        return passphrase;
    }

    public Task ButtonAckAsync(string context) => Task.CompletedTask;
}
```
Source: Prompts/BlazorTrezorPromptHandler.cs:14-67

**Dialog Service Handling:**

Waits for MudBlazor DialogService with 5-second timeout:
```csharp
private async Task<IDialogService> WaitForDialogServiceAsync()
{
    var deadline = DateTime.UtcNow.AddSeconds(5);
    while (true)
    {
        var dialogService = _dialogAccessor.DialogService;
        if (dialogService != null)
        {
            return dialogService;
        }

        if (DateTime.UtcNow >= deadline)
        {
            throw new InvalidOperationException("Dialog service is not available.");
        }

        await Task.Delay(50).ConfigureAwait(false);
    }
}
```
Source: Prompts/BlazorTrezorPromptHandler.cs:122-141

**Dialog Display:**

Uses `WalletBlazorDispatcher` for thread-safe UI updates:
```csharp
private async Task<string?> ShowDialogAsync<TComponent>()
    where TComponent : IComponent
{
    var dialogService = await WaitForDialogServiceAsync().ConfigureAwait(false);

    IDialogReference dialog;
    try
    {
        dialog = await WalletBlazorDispatcher.RunAsync(() =>
        {
            return dialogService.ShowAsync<TComponent>(string.Empty);
        });
    }
    catch (Exception ex)
    {
        Log($"Error creating dialog {typeof(TComponent).Name}: {ex.Message}");
        throw;
    }

    DialogResult result;
    try
    {
        result = await WalletBlazorDispatcher.RunAsync(() =>
        {
            return dialog.Result;
        });
    }
    catch (Exception ex)
    {
        Log($"Error awaiting dialog result {typeof(TComponent).Name}: {ex.Message}");
        throw;
    }

    if (result == null || result.Canceled)
    {
        return null;
    }

    var data = result.Data switch
    {
        string text => text,
        _ => string.Empty
    };

    return data;
}
```
Source: Prompts/BlazorTrezorPromptHandler.cs:69-120

**Passphrase Caching:**

Caches passphrase for session to avoid repeated prompts.
Source: Prompts/BlazorTrezorPromptHandler.cs:44-48,58

## Prompt Components

### TrezorPinPrompt

Modal dialog for entering Trezor PIN using position-based entry.

**Dialog Configuration:**
```csharp
private static readonly DialogOptions DialogOptions = new()
{
    CloseOnEscapeKey = true,
    FullWidth = true,
    MaxWidth = MaxWidth.ExtraSmall,
    Position = DialogPosition.Center
};
```
Source: Prompts/TrezorPinPrompt.razor:71-77

**PIN Grid Layout:**
```csharp
private readonly string[] keypadLayout = new[] { "7", "8", "9", "4", "5", "6", "1", "2", "3" };
```
Source: Prompts/TrezorPinPrompt.razor:79

Displays 3x3 grid of masked buttons (●) representing Trezor device layout.
Source: Prompts/TrezorPinPrompt.razor:40-51

**PIN Entry:**
```csharp
private string pinValue = string.Empty;
private string MaskedPin => new string('•', pinValue.Length);
private bool CanSubmit => pinValue.Length >= 1;

private void AppendDigit(string digit)
{
    if (!CanAppendDigit(digit))
    {
        return;
    }
    pinValue += digit;
}

private bool CanAppendDigit(string digit) => pinValue.Length < 9 && digit.Length == 1 && char.IsDigit(digit[0]);
```
Source: Prompts/TrezorPinPrompt.razor:80-96

**Maximum Length:** 9 digits
Source: Prompts/TrezorPinPrompt.razor:96

**Clear Function:**
```csharp
private void ClearPin() => pinValue = string.Empty;
```
Source: Prompts/TrezorPinPrompt.razor:98

**Submit:**
```csharp
private void Submit()
{
    if (CanSubmit)
    {
        MudDialog.Close(DialogResult.Ok(pinValue));
    }
}
```
Source: Prompts/TrezorPinPrompt.razor:100-106

**Cancel:**
```csharp
private void Cancel() => MudDialog.Close(DialogResult.Cancel());
```
Source: Prompts/TrezorPinPrompt.razor:108

**Localization Keys:**
- Title - "Enter Trezor PIN"
- Description - "Use the PIN layout shown on your Trezor to enter the position digits."
- Helper - "Each digit corresponds to the position displayed on your device. Never share the visual layout."
- PinHelper - "Digits 1-9 only. The order must match what you enter on your Trezor."
- ClearButtonText - "Clear"
- CancelButtonText - "Cancel"
- ConfirmButtonText - "Confirm"

Source: Prompts/TrezorPinPromptLocalizer.cs:29-41

### TrezorPassphrasePrompt

Modal dialog for entering optional BIP-39 passphrase (25th word).

**Dialog Configuration:**
```csharp
private static readonly DialogOptions DialogOptions = new()
{
    CloseOnEscapeKey = true,
    FullWidth = true,
    MaxWidth = MaxWidth.ExtraSmall,
    Position = DialogPosition.Center
};
```
Source: Prompts/TrezorPassphrasePrompt.razor:54-60

**Passphrase Entry:**
```csharp
private string Passphrase { get; set; } = string.Empty;
private bool showPassphrase;

private void ToggleVisibility() => showPassphrase = !showPassphrase;
```
Source: Prompts/TrezorPassphrasePrompt.razor:62-65

**Input Field:**
```razor
<MudTextField @bind-Value="Passphrase"
              InputType="@(showPassphrase ? InputType.Text : InputType.Password)"
              Immediate="true"
              Adornment="Adornment.End"
              AdornmentIcon="@(showPassphrase ? Icons.Material.Filled.Visibility : Icons.Material.Filled.VisibilityOff)"
              OnAdornmentClick="ToggleVisibility" />
```
Source: Prompts/TrezorPassphrasePrompt.razor:21-30

**Submit:**
```csharp
private void Submit() => MudDialog.Close(DialogResult.Ok(Passphrase ?? string.Empty));
```
Source: Prompts/TrezorPassphrasePrompt.razor:67

Empty passphrase is valid (standard wallet).
Source: Prompts/TrezorPassphrasePrompt.razor:67

**Localization Keys:**
- Title - "Enter Passphrase"
- Description - "If your Trezor wallet uses a passphrase, enter it exactly as configured."
- PassphraseLabel - "Passphrase"
- PassphrasePlaceholder - "Optional passphrase"
- PassphraseHelper - "Leave blank if you use the standard wallet."
- PassphraseInfo - "Passphrases are case sensitive. Wrong values create a different hidden wallet."
- CancelButtonText - "Cancel"
- ConfirmButtonText - "Continue"

Source: Prompts/TrezorPassphrasePromptLocalizer.cs:28-39

## Account Creation Components

### TrezorAccountCreation

Multi-step wizard for creating account from new Trezor device.

**Form Steps:**
```csharp
private enum FormStep
{
    Connect = 0,
    Select = 1,
    Confirm = 2
}
```
Source: WalletAccounts/TrezorAccountCreation.razor:229-234

**Step 1: Connect**

Wallet name entry and address discovery:
```razor
<WalletTextField @bind-Value="ViewModel.WalletName"
                 LabelKey="@Keys.WalletNameLabel"
                 PlaceholderKey="@Keys.WalletNamePlaceholder"
                 HelpKey="@Keys.WalletNameHelper" />

<MudNumericField T="int"
                 @bind-Value="ViewModel.DiscoveryStartIndex"
                 Label="@Localizer.GetString(Keys.StartIndexLabel)"
                 Min="0" />

<MudButton OnClick="HandleScanAddresses">
    @Localizer.GetString(Keys.ScanButtonText)
</MudButton>

<MudNumericField T="int"
                 @bind-Value="ViewModel.SingleIndexInput"
                 Label="@Localizer.GetString(Keys.SingleIndexLabel)"
                 Min="0" />

<MudButton OnClick="HandleLoadSingleIndex">
    @Localizer.GetString(Keys.LoadIndexButtonText)
</MudButton>
```
Source: WalletAccounts/TrezorAccountCreation.razor:33-98

**Scan Addresses:**
```csharp
private async Task HandleScanAddresses()
{
    try
    {
        isScanning = true;
        scanError = null;
        StateHasChanged();

        await ViewModel.DiscoverAsync(System.Threading.CancellationToken.None);
        if (ViewModel.Previews.Any())
        {
            CurrentStep = FormStep.Select;
        }
    }
    catch (Exception ex)
    {
        scanError = ex.Message;
    }
    finally
    {
        isScanning = false;
        StateHasChanged();
    }
}
```
Source: WalletAccounts/TrezorAccountCreation.razor:282-310

Scans 5 addresses starting from `DiscoveryStartIndex`.
Source: ViewModels/TrezorAccountCreationViewModel.cs:99

**Load Single Index:**
```csharp
private async Task HandleLoadSingleIndex()
{
    try
    {
        isScanning = true;
        scanError = null;
        StateHasChanged();

        await ViewModel.LoadSingleIndexAsync(System.Threading.CancellationToken.None);
        if (ViewModel.Previews.Any())
        {
            CurrentStep = FormStep.Select;
        }
    }
    catch (Exception ex)
    {
        scanError = ex.Message;
    }
    finally
    {
        isScanning = false;
        StateHasChanged();
    }
}
```
Source: WalletAccounts/TrezorAccountCreation.razor:312-340

**Initialization:**
```csharp
protected override void OnInitialized()
{
    ViewModel.Reset();
    ViewModel.PrepareForNewDevice();
    SetupFormSteps();
}
```
Source: WalletAccounts/TrezorAccountCreation.razor:241-246

Generates unique device ID and default wallet name.
Source: ViewModels/TrezorAccountCreationViewModel.cs:82-93

**Step 2: Select**

Address selection from previews:
```razor
@foreach (var preview in ViewModel.Previews)
{
    <MudCard Class="@GetPreviewCardClasses(preview)"
             @onclick="@(() => SelectPreview(preview))">
        <MudCardContent>
            <MudChip Color="@(IsSelected(preview) ? Color.Primary : Color.Default)">
                @($"#{preview.Index}")
            </MudChip>
            <MudText Typo="Typo.caption">
                @GetDerivationPath(preview.Index)
            </MudText>
            <WalletAddressDisplay Address="@preview.Address" />
        </MudCardContent>
    </MudCard>
}
```
Source: WalletAccounts/TrezorAccountCreation.razor:125-160

**Derivation Path Display:**
```csharp
private static string GetDerivationPath(uint index) => $"m/44'/60'/{index}'/0/0";
```
Source: WalletAccounts/TrezorAccountCreation.razor:362

BIP-44 Ethereum path format.
Source: WalletAccounts/TrezorAccountCreation.razor:362

**Selection:**
```csharp
private void SelectPreview(TrezorDerivationPreview preview)
{
    ViewModel.SelectedIndex = preview.Index;
    ViewModel.SelectedAddress = preview.Address;
}

private bool IsSelected(TrezorDerivationPreview preview) =>
    ViewModel.SelectedAddress == preview.Address;
```
Source: WalletAccounts/TrezorAccountCreation.razor:364-371

**Step 3: Confirm**

Account label and summary:
```razor
<WalletTextField @bind-Value="ViewModel.AccountLabel"
                 LabelKey="@Keys.AccountLabelField"
                 HelpKey="@Keys.AccountLabelHelper" />

<MudPaper>
    <MudStack Spacing="2">
        <MudText Typo="Typo.caption">@Localizer.GetString(Keys.SelectedAddressLabel)</MudText>
        <MudText Typo="Typo.body1">@ViewModel.SelectedAddress</MudText>

        <MudText Typo="Typo.caption">@Localizer.GetString(Keys.DeviceSummaryLabel)</MudText>
        <MudText Typo="Typo.body1">@ViewModel.DeviceId</MudText>

        <MudText Typo="Typo.caption">@Localizer.GetString(Keys.IndexSummaryLabel)</MudText>
        <MudText Typo="Typo.body1">@ViewModel.SelectedIndex</MudText>
    </MudStack>
</MudPaper>
```
Source: WalletAccounts/TrezorAccountCreation.razor:188-212

**Create Account:**
```csharp
private async Task CreateAccount()
{
    if (!ViewModel.CanCreateAccount)
    {
        return;
    }

    if (OnAccountCreated.HasDelegate)
    {
        await OnAccountCreated.InvokeAsync();
    }
}
```
Source: WalletAccounts/TrezorAccountCreation.razor:411-422

**Component Parameters:**
```csharp
[Parameter] public required TrezorAccountCreationViewModel ViewModel { get; set; }
[Parameter] public EventCallback OnAccountCreated { get; set; }
[Parameter] public EventCallback OnBackToAccountSelection { get; set; }
[Parameter] public EventCallback OnBackToLogin { get; set; }
[Parameter] public bool ShowBackToAccountSelection { get; set; } = true;
[Parameter] public bool ShowBackToLogin { get; set; }
[Parameter] public bool IsCompactMode { get; set; }
[Parameter] public int ComponentWidth { get; set; } = 400;
```
Source: WalletAccounts/TrezorAccountCreation.razor:220-227

**Responsive Layout:**
```csharp
private bool IsCompactLayout => IsCompactMode || ComponentWidth < 600;
private int GetPreviewSpacing() => IsCompactLayout ? 1 : 2;
```
Source: WalletAccounts/TrezorAccountCreation.razor:342-344

### TrezorVaultAccountCreation

Multi-step wizard for deriving account from existing Trezor device in vault.

**Form Steps:**
```csharp
private enum FormStep
{
    SelectDevice = 0,
    Discover = 1,
    Confirm = 2
}
```
Source: WalletAccounts/Trezor/TrezorVaultAccountCreation.razor:246-251

**Step 1: Select Device**

Choose from available Trezor devices:
```razor
<WalletSelect T="string"
              @bind-Value="ViewModel.SelectedDeviceId"
              Items="@ViewModel.Devices.Select(d => d.DeviceId)"
              LabelKey="@Keys.SelectDeviceLabel"
              DisplaySelector="@GetDeviceDisplayName" />
```
Source: WalletAccounts/Trezor/TrezorVaultAccountCreation.razor:53-60

**Device Display:**
```csharp
private string GetDeviceDisplayName(string? deviceId) => ViewModel.GetDeviceDisplayName(deviceId);
```
Source: WalletAccounts/Trezor/TrezorVaultAccountCreation.razor:436

Returns: `"Device Name (3 accounts)"` format.
Source: ViewModels/TrezorVaultAccountCreationViewModel.cs:162-164

**Initialization:**
```csharp
protected override async Task OnInitializedAsync()
{
    ViewModel.Reset();
    SetupFormSteps();
    await ViewModel.LoadDevicesAsync();
}
```
Source: WalletAccounts/Trezor/TrezorVaultAccountCreation.razor:258-263

Loads device summary with account counts and next suggested indices.
Source: ViewModels/TrezorVaultAccountCreationViewModel.cs:67-109

**Empty State:**
```razor
@if (!ViewModel.HasDevices)
{
    <WalletInfoCard Severity="WalletInfoCard.WalletInfoSeverity.Info"
                    Title="@Localizer.GetString(Keys.NoDevicesTitle)"
                    Description="@Localizer.GetString(Keys.NoDevicesDescription)" />
}
```
Source: WalletAccounts/Trezor/TrezorVaultAccountCreation.razor:15-22

Only visible if vault contains at least one Trezor account.
Source: ViewModels/TrezorVaultAccountCreationViewModel.cs:56-63

**Step 2: Discover**

Similar to TrezorAccountCreation but with pre-selected device:
```razor
<MudNumericField T="int"
                 @bind-Value="ViewModel.DiscoveryStartIndex" />

<MudButton OnClick="HandleScanAddresses">
    @Localizer.GetString(Keys.ScanButtonText)
</MudButton>

<MudNumericField T="int"
                 @bind-Value="ViewModel.SingleIndexInput" />

<MudButton OnClick="HandleLoadSingleIndex">
    @Localizer.GetString(Keys.LoadIndexButtonText)
</MudButton>
```
Source: WalletAccounts/Trezor/TrezorVaultAccountCreation.razor:76-127

Auto-suggests next available index for selected device.
Source: ViewModels/TrezorVaultAccountCreationViewModel.cs:196-208

**Step 3: Confirm**

Same as TrezorAccountCreation - account label and summary.
Source: WalletAccounts/Trezor/TrezorVaultAccountCreation.razor:195-221

## Account Management Components

### TrezorAccountDetails

View and manage individual Trezor account details.

**View Sections:**
```csharp
private enum ViewSection
{
    Overview,
    EditName
}
```
Source: WalletAccounts/Trezor/TrezorAccountDetails.razor:159-163

**Overview Section:**
```razor
<WalletAddressDisplay Address="@trezorAccount.Address"
                      ShowFullAddress="true" />

<MudPaper Class="wallet-detail-card">
    <MudStack Spacing="2">
        <MudText Typo="Typo.subtitle1">@trezorAccount.Name</MudText>
        <MudChip>@($"#{trezorAccount.Index}")</MudChip>

        <MudText Typo="Typo.caption">@Localizer.GetString(Keys.DeviceIdLabel)</MudText>
        <MudText Typo="Typo.body2">@trezorAccount.DeviceId</MudText>

        <MudText Typo="Typo.caption">@Localizer.GetString(Keys.IndexLabel)</MudText>
        <MudText Typo="Typo.body2">@trezorAccount.Index (@FormatDerivationPath(trezorAccount.Index))</MudText>
    </MudStack>
</MudPaper>
```
Source: WalletAccounts/Trezor/TrezorAccountDetails.razor:65-106

**Derivation Path:**
```csharp
private static string FormatDerivationPath(uint index) => $"m/44'/60'/{index}'/0/0";
```
Source: WalletAccounts/Trezor/TrezorAccountDetails.razor:281

**Action Buttons:**
```razor
<WalletBarActionButton Icon="@Icons.Material.Filled.Edit"
                       Text="@Localizer.GetString(Keys.AccountNameLabel)"
                       OnClick="@(() => NavigateToSection(ViewSection.EditName))" />

<WalletBarActionButton Icon="@Icons.Material.Filled.Delete"
                       Text="@Localizer.GetString(Keys.RemoveAccountButton)"
                       Class="wallet-button-danger"
                       OnClick="ViewModel.RemoveAccountAsync" />
```
Source: WalletAccounts/Trezor/TrezorAccountDetails.razor:32-38

**Edit Name Section:**
```razor
<WalletTextField @bind-Value="ViewModel.EditingAccountName"
                 LabelKey="@TrezorAccountDetailsLocalizer.Keys.AccountNameLabel"
                 PlaceholderKey="@TrezorAccountDetailsLocalizer.Keys.AccountNamePlaceholder"
                 Required="true" />
```
Source: WalletAccounts/Trezor/TrezorAccountDetails.razor:112-118

**Save Account Name:**
```csharp
private async Task HandlePrimaryAction()
{
    await ViewModel.SaveAccountNameAsync();
    if (string.IsNullOrEmpty(ViewModel.ErrorMessage))
    {
        currentSection = ViewSection.Overview;
    }
}
```
Source: WalletAccounts/Trezor/TrezorAccountDetails.razor:214-221

**Initialization:**
```csharp
protected override async Task OnInitializedAsync()
{
    if (ViewModel is INotifyPropertyChanged notifyPropertyChanged)
    {
        notifyPropertyChanged.PropertyChanged += OnViewModelPropertyChanged;
    }

    if (Account != null)
    {
        await ViewModel.InitializeAsync(Account);
    }
}
```
Source: WalletAccounts/Trezor/TrezorAccountDetails.razor:167-178

Subscribes to property changes for reactive UI updates.
Source: WalletAccounts/Trezor/TrezorAccountDetails.razor:169-172

**Component Parameters:**
```csharp
[Parameter] public IWalletAccount? Account { get; set; }
[Parameter] public bool IsCompactMode { get; set; }
[Parameter] public int ComponentWidth { get; set; } = 400;
[Parameter] public EventCallback OnExit { get; set; }
```
Source: WalletAccounts/Trezor/TrezorAccountDetails.razor:154-157

### TrezorGroupDetails

Manage all accounts from a single Trezor device.

**Overview Card:**
```razor
<MudCard Class="wallet-detail-card">
    <MudCardContent>
        <MudChip Color="Color.Primary">
            @Localizer.GetString(Keys.HardwareTypeLabel)
        </MudChip>
        <MudChip Color="Color.Secondary">
            @($"{ViewModel.Accounts.Count} {Localizer.GetString(Keys.AccountCountLabel)}")
        </MudChip>

        <MudText Typo="Typo.caption">@Localizer.GetString(Keys.NextIndexLabel)</MudText>
        <MudText Typo="Typo.h6">@ViewModel.NextIndex</MudText>

        <MudText Typo="Typo.caption">@Localizer.GetString(Keys.AccountListTitle)</MudText>
        <MudText Typo="Typo.h6">@ViewModel.Accounts.Count</MudText>
    </MudCardContent>
</MudCard>
```
Source: WalletAccounts/Trezor/TrezorGroupDetails.razor:58-95

**Action Buttons:**
```razor
<WalletBarActionButton Icon="@Icons.Material.Filled.Edit"
                       Text="@Localizer.GetString(Keys.RenameButton)"
                       Disabled="@(ViewModel.IsLoading || ViewModel.IsAdding)"
                       OnClick="@ViewModel.BeginEditDeviceLabel" />

<WalletBarActionButton Icon="@Icons.Material.Filled.Refresh"
                       Text="@Localizer.GetString(Keys.RefreshButton)"
                       OnClick="ViewModel.RefreshAsync" />

<WalletBarActionButton Icon="@Icons.Material.Filled.Add"
                       Text="@Localizer.GetString(Keys.AddAccountButton)"
                       OnClick="ViewModel.AddNextAccountAsync" />
```
Source: WalletAccounts/Trezor/TrezorGroupDetails.razor:29-43

**Device Label Editing:**
```razor
@if (ViewModel.IsEditingLabel)
{
    <WalletTextField @bind-Value="ViewModel.EditingDeviceLabel"
                     LabelKey="@TrezorGroupDetailsLocalizer.Keys.DeviceLabelField"
                     HelpKey="@TrezorGroupDetailsLocalizer.Keys.DeviceLabelHelper" />

    <MudButton OnClick="ViewModel.SaveDeviceLabelAsync">
        @Localizer.GetString(Keys.SaveDeviceLabelButton)
    </MudButton>
    <MudButton OnClick="@ViewModel.CancelEditDeviceLabel">
        @Localizer.GetString(Keys.CancelEditButton)
    </MudButton>
}
```
Source: WalletAccounts/Trezor/TrezorGroupDetails.razor:98-119

**Account List:**
```razor
@foreach (var account in ViewModel.Accounts)
{
    <AccountCard Account="account"
                 AccountDisplayName="@GetAccountDisplayName(account)"
                 FormattedAddress="@account.Address"
                 IsCompactMode="@IsCompactLayout" />

    <MudText Typo="Typo.caption">@FormatDerivationPath(account.Index)</MudText>
}
```
Source: WalletAccounts/Trezor/TrezorGroupDetails.razor:126-140

Displays derivation path below each account card.
Source: WalletAccounts/Trezor/TrezorGroupDetails.razor:137

**Account Display Name:**
```csharp
private static string GetAccountDisplayName(TrezorWalletAccount account) =>
    string.IsNullOrWhiteSpace(account.Name) ? $"Account {account.Index}" : account.Name;
```
Source: WalletAccounts/Trezor/TrezorGroupDetails.razor:229-230

**Derivation Path:**
```csharp
private static string FormatDerivationPath(uint index) => $"m/44'/60'/0'/0/{index}";
```
Source: WalletAccounts/Trezor/TrezorGroupDetails.razor:232

**Initialization:**
```csharp
protected override async Task OnInitializedAsync()
{
    if (ViewModel is INotifyPropertyChanged notifyPropertyChanged)
    {
        notifyPropertyChanged.PropertyChanged += OnViewModelPropertyChanged;
    }

    if (!string.IsNullOrEmpty(GroupId))
    {
        await ViewModel.InitializeAsync(GroupId, Accounts);
    }
}
```
Source: WalletAccounts/Trezor/TrezorGroupDetails.razor:184-195

**Component Parameters:**
```csharp
[Parameter] public string? GroupId { get; set; }
[Parameter] public IReadOnlyList<IWalletAccount> Accounts { get; set; } = Array.Empty<IWalletAccount>();
[Parameter] public bool IsCompactMode { get; set; }
[Parameter] public int ComponentWidth { get; set; } = 400;
[Parameter] public EventCallback OnExit { get; set; }
```
Source: WalletAccounts/Trezor/TrezorGroupDetails.razor:178-182

## Localization

All components support English (en-US) and Spanish (es-ES) localization.

**TrezorPinPromptLocalizer** - 10 localization keys
**TrezorPassphrasePromptLocalizer** - 9 localization keys

Plus all ViewModels have their own localizers from Nethereum.Wallet.UI.Components.Trezor:
- TrezorAccountCreationLocalizer - 40+ keys
- TrezorVaultAccountCreationLocalizer - 38+ keys
- TrezorAccountDetailsLocalizer - 30+ keys
- TrezorGroupDetailsLocalizer - 34+ keys

Source: Extensions/ServiceCollectionExtensions.cs:30-35

## Usage Example

### Basic Setup

```csharp
// Program.cs or Startup.cs
builder.Services.AddNethereumWalletBlazorComponents();  // Base Blazor components
builder.Services.AddTrezorWalletBlazorComponents();     // Trezor Blazor support

// Add Trezor device service
builder.Services.AddScoped<ITrezorDeviceDiscoveryService, TrezorDeviceDiscoveryService>();
builder.Services.AddScoped<TrezorWalletAccountService>();
```

The registry contributor automatically registers all Trezor components when the service provider is built.

### Component Usage

**New Device Creation:**
```razor
<TrezorAccountCreation ViewModel="@trezorAccountCreationViewModel"
                       OnAccountCreated="HandleAccountCreated"
                       OnBackToAccountSelection="BackToSelection"
                       ShowBackToAccountSelection="true" />
```

**Existing Device Account:**
```razor
<TrezorVaultAccountCreation ViewModel="@trezorVaultViewModel"
                            OnAccountCreated="HandleAccountCreated"
                            OnBackToAccountSelection="BackToSelection" />
```

**Account Details:**
```razor
<TrezorAccountDetails Account="@selectedAccount"
                      OnExit="HandleExit" />
```

**Device Group:**
```razor
<TrezorGroupDetails GroupId="@deviceId"
                    Accounts="@trezorAccounts"
                    OnExit="HandleExit" />
```

## Related Packages

### Dependencies
- **Nethereum.Wallet.UI.Components.Trezor** - Trezor ViewModels and business logic
- **Nethereum.Wallet.UI.Components.Blazor** - Base Blazor components
- **Nethereum.Wallet.Trezor** - Trezor device communication
- **Nethereum.Signer.Trezor.Abstractions** - ITrezorPromptHandler interface

### See Also
- **Nethereum.Wallet.UI.Components** - Platform-agnostic MVVM components
- **Nethereum.Wallet** - Core wallet functionality

## Additional Resources

- [Nethereum Documentation](https://docs.nethereum.com)
- [Nethereum GitHub](https://github.com/Nethereum/Nethereum)
- [Trezor Documentation](https://trezor.io)
- [MudBlazor Documentation](https://mudblazor.com)
