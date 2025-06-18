namespace Nethereum.Wallet;

public class MnemonicInfo
{
    public string Label { get; set; } = string.Empty;
    public string Mnemonic { get; set; } = string.Empty;
    public string? Passphrase { get; set; }

    public MnemonicInfo() { }

    public MnemonicInfo(string label, string mnemonic, string? passphrase = null)
    {
        Label = label;
        Mnemonic = mnemonic;
        Passphrase = passphrase;
    }
}
