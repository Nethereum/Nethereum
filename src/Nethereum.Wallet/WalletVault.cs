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

public class WalletVault : IMnemonicProvider
{
    public Dictionary<string, MnemonicInfo> Mnemonics { get; private set; } = new();
    public List<IWalletAccount> Accounts { get; private set; } = new();

    public List<IWalletAccountJsonFactory> Factories { get; private set; } = new();

    public WalletVault()
    {
        RegisterFactory(new PrivateKeyWalletAccountFactory());
        RegisterFactory(new MnemonicWalletAccountFactory(this)); // 'this' implements IMnemonicProvider
        RegisterFactory(new ViewOnlyWalletAccountFactory());
    }

    public void AddAccount(IWalletAccount account)
    {
        if (Accounts.Any(a => a.Address == account.Address))
            throw new InvalidOperationException("An account with the same address already exists in the vault.");

        Accounts.Add(account);
    }

    public string AddMnemonic(string label, string mnemonic, string? passphrase = null)
    {
        var id = $"mnemonic-{Mnemonics.Count}";
        Mnemonics[id] = new MnemonicInfo(label, mnemonic, passphrase);
        return id;
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

    public MnemonicInfo? GetMnemonic(string id)
    => Mnemonics.TryGetValue(id, out var info) ? info : null;

    public string Encrypt(string password)
    {
        var jsonObject = new JsonObject
        {
            ["mnemonics"] = new JsonObject(Mnemonics.Select(kvp =>
                    new KeyValuePair<string, JsonNode>(
                        kvp.Key,
                        new JsonObject
                        {
                            ["label"] = kvp.Value.Label,
                            ["mnemonic"] = kvp.Value.Mnemonic,
                            ["passphrase"] = kvp.Value.Passphrase
                        })
                )),
            ["accounts"] = new JsonArray(Accounts.Select(a =>
            {
                var factory = Factories.FirstOrDefault(f => f.Type == a.Type)
                    ?? throw new NotSupportedException($"No factory for account type {a.Type}");
                return factory.ToJson(a);
            }).ToArray())
        };

        var json = jsonObject.ToJsonString();

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.BlockSize = 128;
        aes.GenerateIV();
        aes.Key = new Rfc2898DeriveBytes(password, aes.IV, 10000).GetBytes(32);

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(json);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Array.Copy(aes.IV, 0, result, 0, aes.IV.Length);
        Array.Copy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);
        return Convert.ToBase64String(result);
    }

    public void Decrypt(string encrypted, string password)
    {
        var data = Convert.FromBase64String(encrypted);
        var iv = data.Take(16).ToArray();
        var cipher = data.Skip(16).ToArray();

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.BlockSize = 128;
        aes.IV = iv;
        aes.Key = new Rfc2898DeriveBytes(password, iv, 10000).GetBytes(32);

        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
        var json = Encoding.UTF8.GetString(plainBytes);

        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Mnemonics = root.GetProperty("mnemonics").EnumerateObject()
            .ToDictionary(e => e.Name, e =>
            {
                var obj = e.Value;
                return new MnemonicInfo(
                    obj.GetProperty("label").GetString()!,
                    obj.GetProperty("mnemonic").GetString()!,
                    obj.TryGetProperty("passphrase", out var p) ? p.GetString() : null
                );
            });

        var accountsArray = root.GetProperty("accounts").EnumerateArray();

        Accounts = accountsArray.Select(element =>
        {
            var type = element.GetProperty("type").GetString()
                ?? throw new InvalidOperationException("Account type missing.");
            var factory = Factories.FirstOrDefault(f => f.Type == type)
                ?? throw new NotSupportedException($"No factory for type: {type}");
            return factory.FromJson(element);
        }).ToList();
    }
}