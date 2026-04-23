namespace Nethereum.Model
{
    /// <summary>
    /// Default mainnet-compatible layout: the full Account — including code
    /// hash and per-contract storage root — is RLP-encoded into a single
    /// blob per account. No side-channel required.
    /// </summary>
    public class RlpAccountLayout : IAccountLayoutStrategy
    {
        public static RlpAccountLayout Instance { get; } = new RlpAccountLayout();

        public bool HasExternalCodeHash => false;

        public byte[] EncodeAccount(Account account)
            => AccountEncoder.Current.Encode(account);

        public Account DecodeAccount(byte[] data)
        {
            if (data == null || data.Length == 0) return null;
            return AccountEncoder.Current.Decode(data);
        }
    }
}
