namespace Ethereum.RPC.ABI
{
    public class BytesType : ABIType
    {
        public BytesType() : base("bytes")
        {
            this.Decoder = new BytesTypeDecoder();
            this.Encoder = new BoolTypeEncoder();
        }

        public override int FixedSize => -1;
        
    }
}