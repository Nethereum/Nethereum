namespace Nethereum.Model.Codecs
{
    public interface IReceiptCodec
    {
        byte[] Encode(Receipt receipt);

        Receipt Decode(byte[] rawBytes);
    }
}
