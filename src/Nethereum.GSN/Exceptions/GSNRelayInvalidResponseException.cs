namespace Nethereum.GSN.Exceptions
{
    public class GSNRelayInvalidResponseException: GSNException
    {
        public GSNRelayInvalidResponseException() : base("Relay response is invalid") { }
    }
}
