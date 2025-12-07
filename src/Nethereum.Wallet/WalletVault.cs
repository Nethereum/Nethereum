#nullable enable

using System.Collections.Generic;
using System.Linq;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Nethereum.Wallet.Bip32;
using Nethereum.Wallet.WalletAccounts;
using System.Threading.Tasks;

namespace Nethereum.Wallet;

public class WalletVault
{
    public List<MnemonicInfo> Mnemonics { get; private set; } = new();
    public List<IWalletAccount> Accounts { get; private set; } = new();
    public List<HardwareWalletInfo> HardwareDevices { get; private set; } = new();

    public List<IWalletAccountJsonFactory> Factories { get; private set; } = new();

    private readonly IEncryptionStrategy _encryptionStrategy;

    public WalletVault(IEncryptionStrategy encryptionStrategy)
    {
        _encryptionStrategy = encryptionStrategy;
        RegisterFactory(new PrivateKeyWalletAccountFactory());
        RegisterFactory(new MnemonicWalletAccountFactory());
        RegisterFactory(new ViewOnlyWalletAccountFactory());
        RegisterFactory(new SmartContractWalletAccountFactory());
    }

    public void AddAccount(IWalletAccount account)
    {
        if (Accounts.Any(a => a.Address == account.Address))
            throw new InvalidOperationException("An account with the same address already exists in the vault.");

        Accounts.Add(account);
    }

    public void AddMnemonic(MnemonicInfo mnemonicInfo)
    {
        Mnemonics.Add(mnemonicInfo);
    }

    public MnemonicInfo? FindMnemonicById(string? mnemonicId)
    {
        return Mnemonics.FirstOrDefault(m => m.Id == mnemonicId);
    }

    public void AddAccount(IWalletAccount account, bool setAsSelected = false)
    {
        if (string.IsNullOrWhiteSpace(account.Label))
            account.Label = account.Address;

        if (Accounts.Any(a => a.Address == account.Address && a.Type == account.Type))
            throw new InvalidOperationException("Duplicate account.");

        if (setAsSelected)
        {
            foreach (var a in Accounts) a.IsSelected = false;
            account.IsSelected = true;
        }

        Accounts.Add(account);
    }

    public void RegisterFactory(IWalletAccountJsonFactory factory)
    {
        Factories.Add(factory);
    }

    public string Encrypt(string password)
    {
        var jsonObject = new JsonObject
        {
            ["mnemonics"] = new JsonArray(Mnemonics.Select(m =>
                    new JsonObject
                    {
                        ["label"] = m.Label,
                        ["mnemonic"] = m.Mnemonic,
                        ["passphrase"] = m.Passphrase != null ? JsonValue.Create(m.Passphrase) : null,
                        ["id"] = m.Id
                    }).ToArray()
                ),
            ["accounts"] = new JsonArray(Accounts.Select(a =>
            {
                var factory = Factories.FirstOrDefault(f => f.Type == a.Type)
                    ?? throw new NotSupportedException($"No factory for account type {a.Type}");
                return factory.ToJson(a);
            }).ToArray()),
            ["hardwareDevices"] = new JsonArray(HardwareDevices.Select(d =>
                new JsonObject
                {
                    ["id"] = d.Id,
                    ["label"] = d.Label,
                    ["type"] = d.Type
                }).ToArray())
        };

        var json = jsonObject.ToJsonString();
        var plainBytes = Encoding.UTF8.GetBytes(json);
        var encryptedBytes = _encryptionStrategy.Encrypt(plainBytes, password);

        return Convert.ToBase64String(encryptedBytes);
    }

    public void Decrypt(string encrypted, string password)
    {
        var data = Convert.FromBase64String(encrypted);
        var plainBytes = _encryptionStrategy.Decrypt(data, password);
        var json = Encoding.UTF8.GetString(plainBytes);

        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Mnemonics = root.GetProperty("mnemonics").EnumerateArray()
            .Select(element =>
            {
                return new MnemonicInfo(
                    element.GetProperty("label").GetString()!,
                    element.GetProperty("mnemonic").GetString()!,
                    element.TryGetProperty("passphrase", out var p) ? p.GetString() : null
                ) { Id = element.GetProperty("id").GetString()! };
            }).ToList();

        var accountsArray = root.GetProperty("accounts").EnumerateArray();

        Accounts = accountsArray.Select(element =>
        {
            var type = element.GetProperty("type").GetString()
                ?? throw new InvalidOperationException("Account type missing.");
            var factory = Factories.FirstOrDefault(f => f.Type == type)
                ?? throw new NotSupportedException($"No factory for type: {type}");
            return factory.FromJson(element, this);
        }).ToList();

        if (root.TryGetProperty("hardwareDevices", out var hardwareDevicesElement) &&
            hardwareDevicesElement.ValueKind == JsonValueKind.Array)
        {
            HardwareDevices = hardwareDevicesElement.EnumerateArray()
                .Select(element =>
                {
                    var id = element.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                    if (string.IsNullOrEmpty(id))
                    {
                        return null;
                    }

                    var label = element.TryGetProperty("label", out var labelProp) ? labelProp.GetString() ?? string.Empty : string.Empty;
                    var type = element.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? string.Empty : string.Empty;
                    return new HardwareWalletInfo
                    {
                        Id = id,
                        Label = label,
                        Type = type
                    };
                })
                .Where(info => info != null)
                .Cast<HardwareWalletInfo>()
                .ToList();
        }
        else
        {
            HardwareDevices = new List<HardwareWalletInfo>();
        }
    }

    public HardwareWalletInfo AddOrUpdateHardwareDevice(string deviceId, string type, string label)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new ArgumentException("Device id must be provided.", nameof(deviceId));
        }

        var existing = HardwareDevices.FirstOrDefault(d => string.Equals(d.Id, deviceId, StringComparison.OrdinalIgnoreCase));
        if (existing == null)
        {
            existing = new HardwareWalletInfo
            {
                Id = deviceId,
                Type = type,
                Label = label
            };
            HardwareDevices.Add(existing);
        }
        else if (!string.IsNullOrWhiteSpace(label))
        {
            existing.Label = label;
        }

        return existing;
    }

    public HardwareWalletInfo? FindHardwareDevice(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return null;
        }

        return HardwareDevices.FirstOrDefault(d => string.Equals(d.Id, deviceId, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<HardwareWalletInfo> GetHardwareDevicesByType(string type)
    {
        return HardwareDevices
            .Where(d => string.Equals(d.Type, type, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
