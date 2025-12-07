#nullable enable

using System.Text.Json;
using Nethereum.Wallet.WalletAccounts;

namespace Nethereum.Wallet.Trezor;

public class TrezorWalletAccountFactory : WalletAccountFactoryBase<TrezorWalletAccount>
{
    public override string Type => TrezorWalletAccount.TypeName;

    private readonly ITrezorSessionProvider _sessionProvider;
    private readonly ITrezorDeviceDiscoveryService _trezorDeviceDiscoveryService;

    public TrezorWalletAccountFactory(ITrezorSessionProvider sessionProvider, ITrezorDeviceDiscoveryService trezorDeviceDiscoveryService)
    {
        _sessionProvider = sessionProvider;
        _trezorDeviceDiscoveryService = trezorDeviceDiscoveryService;
    }

    public override IWalletAccount FromJson(JsonElement element, WalletVault vault)
    {
        return TrezorWalletAccount.FromJson(element, _sessionProvider, _trezorDeviceDiscoveryService);
    }
}
