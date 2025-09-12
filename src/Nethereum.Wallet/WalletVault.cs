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
    }
}