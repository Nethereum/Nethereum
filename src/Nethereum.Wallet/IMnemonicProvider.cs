#nullable enable

namespace Nethereum.Wallet
{
    public interface IMnemonicProvider
    {
        MnemonicInfo? GetMnemonic(string mnemonicId);
    }
}