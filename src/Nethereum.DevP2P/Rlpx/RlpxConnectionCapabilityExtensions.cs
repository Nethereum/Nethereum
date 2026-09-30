namespace Nethereum.DevP2P.Rlpx
{
    public static class RlpxConnectionCapabilityExtensions
    {
        public static int NegotiatedSnapVersion(this RlpxConnection connection)
            => connection.SharedCapabilities.Find(c => c.Name == "snap")?.Version ?? 1;
    }
}
