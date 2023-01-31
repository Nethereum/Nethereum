namespace Nethereum.Model
{
    public interface ISignature
    {
        byte[] R { get; }
        byte[] S { get; }
        byte[] V { get; set; }
    }
}