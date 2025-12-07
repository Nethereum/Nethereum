#nullable enable

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.RPC.Accounts;
using Nethereum.Web3.Accounts;
using Nethereum.Wallet.WalletAccounts;
using Nethereum.Signer.Trezor;

namespace Nethereum.Wallet.Trezor;

public class TrezorWalletAccount : WalletAccountBase
{
    public static readonly string TypeName = "trezor";
    public override string Type => TypeName;

    public uint Index { get; }
    public string DeviceId { get; }

    private readonly ITrezorSessionProvider _sessionProvider;
    private readonly ITrezorDeviceDiscoveryService _trezorDeviceDiscoveryService;

    public override string Name => Label ?? $"Trezor #{Index}";
    public override object? Settings => new { Index, DeviceId };
    public override string? GroupId => DeviceId;

    public TrezorWalletAccount(
        string address,
        string label,
        uint index,
        string deviceId,
        ITrezorSessionProvider sessionProvider,
        ITrezorDeviceDiscoveryService trezorDeviceDiscoveryService)
        : base(address, label)
    {
        Index = index;
        DeviceId = deviceId;
        _sessionProvider = sessionProvider;
        _trezorDeviceDiscoveryService = trezorDeviceDiscoveryService;
    }

    public override async Task<IAccount> GetAccountAsync()
    {
        var signer = await _sessionProvider.CreateSignerAsync(Index, DeviceId, Address, CancellationToken.None).ConfigureAwait(false);
        await signer.InitializeAsync().ConfigureAwait(false);

        var account = new ExternalAccount(signer);
        await account.InitialiseAsync().ConfigureAwait(false);
        return account;
    }

    public override async Task EnsureReadyAsync(CancellationToken cancellationToken = default)
    {
        await _trezorDeviceDiscoveryService.DiscoverAsync(DeviceId, Index, 1, cancellationToken);
        //var signer = await _sessionProvider.CreateSignerAsync(Index, DeviceId, Address, CancellationToken.None).ConfigureAwait(false);
        //await signer.InitializeAsync();
        //await signer.RefreshAddressFromDeviceAsync().ConfigureAwait(false);
    }

    public override JsonObject ToJson() => new()
    {
        ["type"] = Type,
        ["address"] = Address,
        ["label"] = Label,
        ["index"] = Index,
        ["deviceId"] = DeviceId,
        ["selected"] = IsSelected
    };

    public static TrezorWalletAccount FromJson(JsonElement json, ITrezorSessionProvider provider, ITrezorDeviceDiscoveryService trezorDeviceDiscoveryService)
    {
        var address = json.GetProperty("address").GetString()!;
        var label = json.GetProperty("label").GetString()!;
        var index = json.GetProperty("index").GetUInt32();
        var deviceId = json.GetProperty("deviceId").GetString()!;
        var account = new TrezorWalletAccount(address, label, index, deviceId, provider, trezorDeviceDiscoveryService)
        {
            IsSelected = json.TryGetProperty("selected", out var selected) && selected.GetBoolean()
        };
        return account;
    }

}
