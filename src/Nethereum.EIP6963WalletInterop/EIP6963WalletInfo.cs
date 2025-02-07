namespace Nethereum.EIP6963WalletInterop
{
    public class EIP6963WalletInfo
    {
        public string Uuid { get; set; }  // Unique identifier for the wallet
        public string Name { get; set; }      // Human-readable name
        public string Icon { get; set; }   // URL or base64 string for the wallet icon
        public string Rdns { get; set; }   // Reverse DNS for the wallet

    }
}