using System.Numerics;

namespace Nethereum.Contracts
{
    public interface IMulticallInput
    {
       string Target { get; set; }
       byte[] GetCallData();
       BigInteger Value { get; set; }
    }


    public interface IMulticallInputOutput:IMulticallInput
    {
        void Decode(byte[] output);
        bool Success { get; set; }
        bool AllowFailure { get; set; }
    }
}