namespace Nethereum.Model
{
    public class Signature : ISignature
    {
        public Signature(byte[] r, byte[] s, byte[] v)
        {
            R = r;
            S = s;
            V = v;
        }

        public Signature()
        {

        }

        public byte[] R { get; set; }

        public byte[] S { get; set; }

        public byte[] V { get; set; }
    }
}