using System.Numerics;

namespace Nethereum.Contracts.QueryHandlers.MultiCall
{
    public interface IMulticallInputOutput
    {
        string Target { get; set; }
        byte[] GetCallData();
        void Decode(byte[] output);
        bool Success { get; set; }
        bool AllowFailure { get; set; }
        BigInteger Value { get; set; }
    }
}