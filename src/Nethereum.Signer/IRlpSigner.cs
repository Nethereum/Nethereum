
namespace Nethereum.Signer
{
    internal interface IRlpSigner
    {
        byte[] Hash { get; }

        byte[] RawHash { get; }

        byte[][] Data { get; }

        EthECKey Key { get; }

        byte[] GetRLPEncoded();

        byte[] GetRLPEncodedRaw();
    }
}
