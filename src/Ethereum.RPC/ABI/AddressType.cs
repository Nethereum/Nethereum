namespace Ethereum.RPC.ABI
{
    public class AddressType : ABIType
    {
        public AddressType() : base("address")
        {
            //this will need to be only a string type one, converting to hex
            Decoder = new IntTypeDecoder();
            Encoder = new AddressTypeEncoder();
        }
    }
}