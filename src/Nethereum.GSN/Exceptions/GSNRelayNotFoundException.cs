namespace Nethereum.GSN.Exceptions
{
    public class GSNRelayNotFoundException : GSNException
    {
        public GSNRelayNotFoundException(string message) : base(message ?? "Relay not found") { }
    }
}
