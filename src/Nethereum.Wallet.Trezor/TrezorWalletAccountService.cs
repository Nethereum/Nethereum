#nullable enable

using System.Linq;
using System.Threading.Tasks;
using Nethereum.Wallet.WalletAccounts;

namespace Nethereum.Wallet.Trezor;

public class TrezorWalletAccountService
{
    private readonly WalletVault _vault;
    private readonly ITrezorSessionProvider _sessionProvider;
    private readonly ITrezorDeviceDiscoveryService _trezorDeviceDiscoveryService;

    public TrezorWalletAccountService(WalletVault vault, ITrezorSessionProvider sessionProvider, ITrezorDeviceDiscoveryService trezorDeviceDiscoveryService)
    {
        _vault = vault;
        _sessionProvider = sessionProvider;
        _trezorDeviceDiscoveryService = trezorDeviceDiscoveryService;
    }

    public async Task<TrezorWalletAccount> CreateAsync(uint index, string deviceId, string? label = null, bool setAsSelected = false, string? deviceLabel = null)
    {
        EnsureFactoryRegistered();
        EnsureHardwareDevice(deviceId, deviceLabel);

        var signer = await _sessionProvider.CreateSignerAsync(index, deviceId).ConfigureAwait(false);
        await signer.InitializeAsync().ConfigureAwait(false);
        var address = await signer.GetAddressAsync().ConfigureAwait(false);

        var account = BuildAccount(index, deviceId, address, label);
        _vault.AddAccount(account, setAsSelected);
        return account;
    }

    public TrezorWalletAccount CreateFromKnownAddress(uint index, string deviceId, string address, string? label = null, bool setAsSelected = false, bool addToVault = true, string? deviceLabel = null)
    {
        EnsureFactoryRegistered();
        EnsureHardwareDevice(deviceId, deviceLabel);

        var account = BuildAccount(index, deviceId, address, label);
        if (addToVault)
        {
            _vault.AddAccount(account, setAsSelected);
        }
        return account;
    }

    public TrezorWalletAccount BuildAccount(uint index, string deviceId, string address, string? label = null)
    {
        EnsureFactoryRegistered();
        return new TrezorWalletAccount(address, label ?? address, index, deviceId, _sessionProvider, _trezorDeviceDiscoveryService);
    }

    public void UpdateHardwareDeviceLabel(string deviceId, string label)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return;
        }

        var trimmed = string.IsNullOrWhiteSpace(label) ? GenerateDefaultDeviceLabel() : label.Trim();
        _vault.AddOrUpdateHardwareDevice(deviceId, TrezorWalletAccount.TypeName, trimmed);
    }

    private void EnsureFactoryRegistered()
    {
        var isRegistered = _vault.Factories.Any(f => string.Equals(f.Type, TrezorWalletAccount.TypeName, System.StringComparison.OrdinalIgnoreCase));
        if (!isRegistered)
        {
            _vault.RegisterFactory(new TrezorWalletAccountFactory(_sessionProvider, _trezorDeviceDiscoveryService));
        }
    }

    public string GetDefaultHardwareDeviceLabel() => GenerateDefaultDeviceLabel();

    private void EnsureHardwareDevice(string deviceId, string? label)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return;
        }

        var finalLabel = string.IsNullOrWhiteSpace(label) ? GenerateDefaultDeviceLabel() : label.Trim();
        _vault.AddOrUpdateHardwareDevice(deviceId, TrezorWalletAccount.TypeName, finalLabel);
    }

    private string GenerateDefaultDeviceLabel()
    {
        var count = _vault.GetHardwareDevicesByType(TrezorWalletAccount.TypeName).Count + 1;
        return $"Trezor Wallet {count}";
    }
}
