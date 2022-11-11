namespace Nethereum.GSN.Exceptions
{
    public class GSNRelayHubNotFoundException : GSNException
    {
        public GSNRelayHubNotFoundException(string address)
            : base($"Relay hub is not deployed at address {address}") { }
    }
}
