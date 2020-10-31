namespace Nethereum.GSN.Exceptions
{
    public class GSNNoRegisteredRelaysException : GSNException
    {
        public GSNNoRegisteredRelaysException(string address)
            : base($"No relayers registered in the requested hub at {address}") { }
    }
}
